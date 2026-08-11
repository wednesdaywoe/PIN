using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aero.Gen;
using Aero.Gen.Attributes;
using GameServer;
using GameServer.Packets;
using Shared.Udp;

namespace CaptureReplay;

public enum Direction
{
    ClientToServer,
    ServerToClient
}

/// <summary>
///     One GSS message recovered from the capture. <see cref="Definition" /> and
///     <see cref="Instance" /> are null when AeroMessages has no type for the pair, which is the
///     interesting case: it means the capture carries something PIN cannot yet describe.
/// </summary>
public sealed record DecodedMessage(
    ulong Timestamp,
    Direction Direction,
    ChannelType Channel,
    ushort SequenceNumber,
    byte ControllerId,
    ulong EntityId,
    byte MessageId,
    byte[] Body,
    Type Definition,
    IAero Instance)
{
    public string ControllerName => MessageRegistry.ControllerName(ControllerId);

    public string MessageName => Definition?.Name ?? $"msg{MessageId}";
}

public sealed record ReplayStats
{
    public int Datagrams { get; set; }

    public int Handshake { get; set; }

    public int FramingFailures { get; set; }

    public int SubPackets { get; set; }

    public int SplitFragments { get; set; }

    public int SplitsReassembled { get; set; }

    public int ResentPackets { get; set; }

    public int GssMessages { get; set; }

    public int Deserialized { get; set; }

    public int DeserializeErrors { get; set; }

    public int UnknownPairs { get; set; }

    public Dictionary<ChannelType, int> ByChannel { get; } = new();
}

/// <summary>
///     Walks a capture the way <see cref="NetworkClient" /> and <see cref="Channel" /> walk a live
///     socket: strip the socket id, iterate the framed sub-packets, then per channel undo the
///     resend XOR and reassemble split runs before handing the body to Aero.
/// </summary>
public sealed class SessionReplay
{
    private static readonly byte[] XorByte = [0xFF, 0xAA, 0xCC];
    private static readonly HashSet<string> HandshakeMagic = ["POKE", "HEHE", "KISS", "HUGG"];

    private readonly MessageRegistry _registry;
    private readonly bool _deserialize;

    // Split reassembly is per direction and per channel, mirroring the per-channel state a live
    // client keeps. Key is (direction, channel).
    private readonly Dictionary<(Direction, ChannelType), SortedList<ushort, byte[]>> _splits = new();
    private readonly HashSet<(Direction, ChannelType)> _inSplitMode = [];

    public SessionReplay(MessageRegistry registry, bool deserialize = true)
    {
        _registry = registry;
        _deserialize = deserialize;
    }

    public ReplayStats Stats { get; } = new();

    /// <summary>
    ///     Picks the busiest UDP flow and calls the endpoint that receives the POKE the server.
    ///     Falls back to whichever side sent fewer datagrams, since a game server talks far more
    ///     than the client it is serving.
    /// </summary>
    public static (string Client, string Server) IdentifyEndpoints(IReadOnlyList<Datagram> datagrams)
    {
        foreach (var d in datagrams)
        {
            if (d.Payload.Length >= 8
                && d.Payload[0] == 0 && d.Payload[1] == 0 && d.Payload[2] == 0 && d.Payload[3] == 0
                && Encoding.ASCII.GetString(d.Payload, 4, 4) == "POKE")
            {
                return (d.Source, d.Destination);
            }
        }

        var counts = new Dictionary<string, int>();
        foreach (var d in datagrams)
        {
            counts[d.Source] = counts.GetValueOrDefault(d.Source) + 1;
        }

        var ordered = counts.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();
        return ordered.Count >= 2 ? (ordered[0], ordered[^1]) : (ordered.FirstOrDefault() ?? "?", "?");
    }

    public IEnumerable<DecodedMessage> Replay(IEnumerable<Datagram> datagrams, string serverAddress)
    {
        foreach (var datagram in datagrams)
        {
            var payload = datagram.Payload;
            if (payload.Length < 4)
            {
                continue;
            }

            Stats.Datagrams++;
            var direction = datagram.Destination == serverAddress ? Direction.ClientToServer : Direction.ServerToClient;

            if (payload[0] == 0 && payload[1] == 0 && payload[2] == 0 && payload[3] == 0
                && payload.Length >= 8 && HandshakeMagic.Contains(Encoding.ASCII.GetString(payload, 4, 4)))
            {
                Stats.Handshake++;
                continue;
            }

            foreach (var message in WalkDatagram(datagram, payload, direction))
            {
                yield return message;
            }
        }
    }

    private IEnumerable<DecodedMessage> WalkDatagram(Datagram datagram, byte[] payload, Direction direction)
    {
        var index = 4; // past the socket id -- GameServer.cs:98

        while (index + 2 <= payload.Length)
        {
            // NetworkClient.cs:76 -- the two header bytes are big endian on the wire
            var slice = payload[index..(index + 2)];
            Array.Reverse(slice);
            var header = Deserializer.ReadStruct<GamePacketHeader>(slice.AsMemory());

            // The 11-bit length counts its own header bytes
            if (header.Length < 2 || index + header.Length > payload.Length)
            {
                Stats.FramingFailures++;
                yield break;
            }

            var body = payload[(index + 2)..(index + header.Length)];
            index += header.Length;
            Stats.SubPackets++;
            Stats.ByChannel[header.Channel] = Stats.ByChannel.GetValueOrDefault(header.Channel) + 1;

            var message = HandleSubPacket(datagram, direction, header, body);
            if (message != null)
            {
                yield return message;
            }
        }
    }

    private DecodedMessage HandleSubPacket(Datagram datagram, Direction direction, GamePacketHeader header, byte[] body)
    {
        // Control is the only unsequenced channel -- Channel.cs:64
        var sequenced = header.Channel != ChannelType.Control;
        ushort sequenceNumber = 0;

        if (sequenced)
        {
            if (body.Length < 2)
            {
                return null;
            }

            sequenceNumber = Utils.SimpleFixEndianness(BitConverter.ToUInt16(body, 0));
            body = body[2..];
        }

        // Channel.cs:95 -- a resend is the same bytes XORed with a per-attempt constant
        if (header.ResendCount > 0)
        {
            Stats.ResentPackets++;
            var mask = XorByte[header.ResendCount - 1];
            for (var i = 0; i < body.Length; i++)
            {
                body[i] ^= mask;
            }
        }

        var key = (direction, header.Channel);

        if (_inSplitMode.Contains(key))
        {
            _splits[key][sequenceNumber] = body;
            Stats.SplitFragments++;

            if (header.IsSplit)
            {
                return null;
            }

            // Channel.cs:110 -- the first non-split packet ends the run and concatenates it
            _inSplitMode.Remove(key);
            body = _splits[key].SelectMany(pair => pair.Value).ToArray();
            _splits[key].Clear();
            Stats.SplitsReassembled++;
        }
        else if (header.IsSplit)
        {
            _inSplitMode.Add(key);
            _splits[key] = new SortedList<ushort, byte[]> { { sequenceNumber, body } };
            Stats.SplitFragments++;
            return null;
        }

        return header.Channel is ChannelType.ReliableGss or ChannelType.UnreliableGss
                   ? DecodeGss(datagram, direction, header.Channel, sequenceNumber, body)
                   : null;
    }

    private DecodedMessage DecodeGss(Datagram datagram, Direction direction, ChannelType channel, ushort sequenceNumber, byte[] body)
    {
        // NetworkClient.cs:195 -- controller, then a 7-byte entity id, then the message id
        if (body.Length < 9)
        {
            return null;
        }

        var controllerId = body[0];
        Span<byte> entity = stackalloc byte[8];
        body.AsSpan(1, 7).CopyTo(entity);
        var entityId = BitConverter.ToUInt64(entity) << 8;
        var messageId = body[8];
        var payload = body[9..];

        Stats.GssMessages++;

        // A capture holds both halves of the conversation, so direction picks the id space
        var src = direction == Direction.ClientToServer
                      ? AeroMessageIdAttribute.MsgSrc.Command
                      : AeroMessageIdAttribute.MsgSrc.Message;

        var definition = _registry.LookupGss(src, controllerId, messageId);
        if (definition == null)
        {
            Stats.UnknownPairs++;
            return new DecodedMessage(datagram.Timestamp, direction, channel, sequenceNumber, controllerId, entityId, messageId, payload, null, null);
        }

        IAero instance = null;
        if (_deserialize)
        {
            instance = Deserialize(definition, payload);
            if (instance != null)
            {
                Stats.Deserialized++;
            }
            else
            {
                Stats.DeserializeErrors++;
            }
        }

        return new DecodedMessage(datagram.Timestamp, direction, channel, sequenceNumber, controllerId, entityId, messageId, payload, definition, instance);
    }

    /// <summary>
    ///     Controller and view types are delta-encoded: a keyframe packs every field, an update
    ///     packs only the ones that changed behind a leading bitmask. Aero generates a separate
    ///     <c>UnpackChanges</c> for the latter, so a body that will not fit the full layout is
    ///     retried as a delta before it is called a failure.
    /// </summary>
    private static IAero Deserialize(Type definition, byte[] payload)
    {
        // Not throwing is not the same as being right: fed a body from a different client
        // version, Aero will happily read plausible garbage and stop early. Only a read that
        // consumes the body exactly is treated as a decode.
        var full = (IAero)Activator.CreateInstance(definition);
        if (TryRead(() => full.Unpack(payload), payload.Length))
        {
            return full;
        }

        if (!typeof(IAeroViewInterface).IsAssignableFrom(definition))
        {
            return null;
        }

        var delta = (IAeroViewInterface)Activator.CreateInstance(definition);
        return TryRead(() => delta.UnpackChanges(payload), payload.Length) ? (IAero)delta : null;
    }

    private static bool TryRead(Func<int> read, int expected)
    {
        try
        {
            return read() == expected;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
