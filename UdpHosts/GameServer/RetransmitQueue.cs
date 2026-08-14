#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using GameServer.Packets;
using Shared.Udp;

namespace GameServer;

/// <summary>
///     Holds every reliable packet a channel has sent until the client says it arrived, and hands
///     back the ones that have gone unanswered too long. Without this "reliable" only meant the
///     server acked what the client sent, never that the server's own messages survived a drop
///     (NET-1).
/// </summary>
/// <remarks>
///     <para>
///         Everything here is decided by what the 2016 capture recorded rather than by a guess.
///         Run <c>CaptureReplay --transport</c> on it to reproduce any of these numbers.
///     </para>
///     <para>
///         Not thread safe, and doesn't need to be: a channel only ever sends, acks and resends
///         from the shard thread, the same invariant the character store runs on.
///     </para>
/// </remarks>
public sealed class RetransmitQueue
{
    /// <summary>
    ///     How long to wait for an ack before sending it again. Retail resent 24 packets across
    ///     the 2016 session and the 15 whose original is also in the capture were resent between
    ///     322ms and 665ms later, median 452ms, on a link whose round trip measured about 165ms.
    /// </summary>
    public const int TimeoutMs = 450;

    /// <summary>
    ///     Attempts after the original before a packet is abandoned. Retail's own limit can't be
    ///     measured from the capture, which holds no sequence resent twice, so this takes the only
    ///     number the protocol itself suggests: the header's resend field is two bits and its XOR
    ///     table has three entries, so three is where the encoding stops counting. That buys about
    ///     1.4 seconds of cover, and the alternative to a limit is holding a packet for a client
    ///     that is never going to answer.
    /// </summary>
    public const int MaxAttempts = 3;

    /// <summary>
    ///     What retail stamped on a resend. All 24 in the capture carry 3, in both directions and
    ///     on both reliable channels, and none carries 1 or 2 despite every one of them being a
    ///     first resend. So the field reads as a marker at its top value rather than as an attempt
    ///     counter, and PIN sends what the client was built to receive.
    /// </summary>
    public const byte ResendCountStamp = 3;

    // Channel.cs applies the same table on the way in. Index is the resend count minus one.
    private static readonly byte[] XorByte = [0xFF, 0xAA, 0xCC];

    private readonly Dictionary<ushort, Entry> _pending = [];

    public int Pending => _pending.Count;

    public int Resent { get; private set; }

    public int Abandoned { get; private set; }

    /// <summary>
    ///     Rewrites a packet as a resend of itself: the same bytes with the header's resend count
    ///     stamped and everything past the sequence number XORed by that count's constant. Proven
    ///     against the capture, where all 15 resends with a surviving original are byte-identical
    ///     to it once the XOR is undone.
    /// </summary>
    public static Memory<byte> EncodeResend(ReadOnlyMemory<byte> packet, byte resendCount)
    {
        if (resendCount is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(resendCount), resendCount, "The header carries the resend count in two bits, so only 1 to 3 can be encoded");
        }

        // Header, then the sequence number, then the body the XOR covers. A packet without room
        // for both was never tracked, since only sequenced channels are tracked at all.
        const int HeaderAndSequenceSize = 4;

        var bytes = packet.ToArray();
        var headerBytes = bytes[..2];
        Array.Reverse(headerBytes);
        var header = Deserializer.ReadStruct<GamePacketHeader>(headerBytes.AsMemory());

        var resendHeader = new GamePacketHeader(header.Channel, resendCount, header.IsSplit, header.Length);
        Serializer.WritePrimitive(Utils.SimpleFixEndianness(resendHeader.PacketHeader)).CopyTo(bytes.AsMemory());

        var mask = XorByte[resendCount - 1];
        for (var i = HeaderAndSequenceSize; i < bytes.Length; i++)
        {
            bytes[i] ^= mask;
        }

        return bytes;
    }

    /// <summary>
    ///     Remember a packet until it is acked. Re-tracking a sequence replaces the entry, which is
    ///     what a wrap does after 65536 sends.
    /// </summary>
    public void Track(ushort sequenceNumber, Memory<byte> packet, DateTime sentAt)
    {
        _pending[sequenceNumber] = new Entry(packet, sentAt, 0);
    }

    /// <summary>
    ///     Retire everything the client has confirmed. An ack is cumulative: the capture's acks put
    ///     <c>NextSeqNum</c> at <c>AckForNum + 1</c> on 36753 of 36759 of them, and the client acked
    ///     only 60% of the server's reliable packets while the whole session needed 24 resends, so
    ///     an ack names the highest sequence received in an unbroken run rather than one packet.
    /// </summary>
    /// <returns>How many pending packets the ack retired.</returns>
    public int Acknowledge(ushort ackForNum)
    {
        var retired = _pending.Keys.Where(seq => Sequence.IsAtOrBefore(seq, ackForNum)).ToList();

        foreach (var sequenceNumber in retired)
        {
            _pending.Remove(sequenceNumber);
        }

        return retired.Count;
    }

    /// <summary>
    ///     Every packet whose ack is overdue, encoded as a resend and re-armed. A packet that has
    ///     used up its attempts is dropped from the queue and reported instead, because at some point
    ///     a client that has not answered four times is not going to.
    /// </summary>
    public IReadOnlyList<Resend> Due(DateTime now)
    {
        var due = new List<Resend>();
        var deadline = now.AddMilliseconds(-TimeoutMs);

        // Oldest first, so a run of drops goes back out in the order it was sent
        foreach (var (sequenceNumber, entry) in _pending.Where(p => p.Value.SentAt <= deadline).OrderBy(p => p.Value.SentAt).ToList())
        {
            if (entry.Attempts >= MaxAttempts)
            {
                _pending.Remove(sequenceNumber);
                Abandoned++;
                due.Add(new Resend(sequenceNumber, default, entry.Attempts, GaveUp: true));
                continue;
            }

            _pending[sequenceNumber] = entry with { SentAt = now, Attempts = entry.Attempts + 1 };
            Resent++;
            due.Add(new Resend(sequenceNumber, EncodeResend(entry.Packet, ResendCountStamp), entry.Attempts + 1, GaveUp: false));
        }

        return due;
    }

    /// <summary>
    ///     One packet that needs sending again, or, with <see cref="GaveUp" /> set, one that has run
    ///     out of attempts and is being reported rather than sent.
    /// </summary>
    public readonly record struct Resend(ushort SequenceNumber, Memory<byte> Packet, int Attempt, bool GaveUp);

    private readonly record struct Entry(Memory<byte> Packet, DateTime SentAt, int Attempts);
}
