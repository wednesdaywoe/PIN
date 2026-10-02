using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GameServer.Packets;
using Serilog.Core;
using Shared.Udp;
using Xunit;

namespace GameServer.Tests.Network;

public class ChannelTests
{
    // Mirrors the private constants in Channel
    private const int MaxPacketSize = PacketServer.MTU - 84;
    private static readonly byte[] _xorByte = [0xFF, 0xAA, 0xCC];

    private readonly FakeNetworkClient _client = new();
    private readonly Dictionary<ChannelType, Channel> _channels;
    private readonly List<byte[]> _delivered = [];

    public ChannelTests()
    {
        _channels = Channel.GetChannels(_client, Logger.None);
        foreach (var channel in _channels.Values)
        {
            channel.PacketAvailable += p => _delivered.Add(p.Peek(p.BytesRemaining).ToArray());
        }
    }

    [Fact]
    public void Control_DeliversPayloadWithoutAck()
    {
        Receive(ChannelType.Control, Incoming(ChannelType.Control, null, [1, 2, 3]));

        Assert.Equal([[1, 2, 3]], _delivered);
        Assert.Empty(_client.Acks);
    }

    [Theory]
    [InlineData(ChannelType.Matrix)]
    [InlineData(ChannelType.ReliableGss)]
    public void Reliable_StripsSequenceNumberAndAcks(ChannelType type)
    {
        Receive(type, Incoming(type, 0x1234, [9, 8, 7]));

        Assert.Equal([[9, 8, 7]], _delivered);
        Assert.Equal([(type, (ushort)0x1234)], _client.Acks);
    }

    [Fact]
    public void Unreliable_StripsSequenceNumberWithoutAck()
    {
        Receive(ChannelType.UnreliableGss, Incoming(ChannelType.UnreliableGss, 5, [4, 5]));

        Assert.Equal([[4, 5]], _delivered);
        Assert.Empty(_client.Acks);
    }

    [Fact]
    public void Reliable_AcksAcrossSequenceWraparound()
    {
        Receive(ChannelType.Matrix, Incoming(ChannelType.Matrix, 0xFFFE, [1]), Incoming(ChannelType.Matrix, 0x0002, [2]));

        Assert.Equal([0xFFFE, 0x0002], _client.Acks.Select(a => a.SequenceNumber));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Resent_PayloadIsUnXored(byte resendCount)
    {
        Receive(ChannelType.Matrix, Incoming(ChannelType.Matrix, 10, [0x00, 0x12, 0xFF], resendCount: resendCount));

        Assert.Equal([[0x00, 0x12, 0xFF]], _delivered);
    }

    [Fact]
    public void Split_ReassemblesFragmentsInOrder()
    {
        Receive(ChannelType.ReliableGss,
                Incoming(ChannelType.ReliableGss, 20, [1, 2], isSplit: true),
                Incoming(ChannelType.ReliableGss, 21, [3, 4], isSplit: true),
                Incoming(ChannelType.ReliableGss, 22, [5]));

        Assert.Equal([[1, 2, 3, 4, 5]], _delivered);
    }

    [Fact]
    public void Split_ReassemblesAcrossSequenceWraparound()
    {
        Receive(ChannelType.ReliableGss,
                Incoming(ChannelType.ReliableGss, 0xFFFF, [1, 2], isSplit: true),
                Incoming(ChannelType.ReliableGss, 0x0000, [3, 4], isSplit: true),
                Incoming(ChannelType.ReliableGss, 0x0001, [5]));

        Assert.Equal([[1, 2, 3, 4, 5]], _delivered);
    }

    [Fact]
    public void Split_ResentFragmentIsIgnored()
    {
        Receive(ChannelType.ReliableGss,
                Incoming(ChannelType.ReliableGss, 30, [1, 2], isSplit: true),
                Incoming(ChannelType.ReliableGss, 30, [1, 2], isSplit: true, resendCount: 1),
                Incoming(ChannelType.ReliableGss, 31, [3]));

        Assert.Equal([[1, 2, 3]], _delivered);
    }

    [Fact]
    public void Reliable_ResentPacketIsDeliveredOnceAndReAcked()
    {
        // The client resends when our ack got lost, so the duplicate needs another ack but must not be handled twice
        Receive(ChannelType.Matrix,
                Incoming(ChannelType.Matrix, 40, [7]),
                Incoming(ChannelType.Matrix, 40, [7], resendCount: 1));

        Assert.Equal([[7]], _delivered);
        Assert.Equal([40, 40], _client.Acks.Select(a => a.SequenceNumber));
    }

    [Fact]
    public void Reliable_ResentFirstFragmentAfterSplitCompletedIsIgnored()
    {
        Receive(ChannelType.ReliableGss,
                Incoming(ChannelType.ReliableGss, 50, [1], isSplit: true),
                Incoming(ChannelType.ReliableGss, 51, [2]),
                Incoming(ChannelType.ReliableGss, 50, [1], isSplit: true, resendCount: 1),
                Incoming(ChannelType.ReliableGss, 52, [3]));

        Assert.Equal([[1, 2], [3]], _delivered);
    }

    [Fact]
    public void Send_SmallMessageIsOnePacketWithHeaderAndSequence()
    {
        var channel = _channels[ChannelType.Matrix];
        channel.Send(new byte[] { 1, 2, 3 });
        channel.Send(new byte[] { 4 });
        channel.Process(CancellationToken.None);

        Assert.Equal(2, _client.Sent.Count);
        var (header, seq, payload) = ParseOutgoing(_client.Sent[0]);
        Assert.Equal(ChannelType.Matrix, header.Channel);
        Assert.False(header.IsSplit);
        Assert.Equal(0, header.ResendCount);
        Assert.Equal(_client.Sent[0].Length, header.Length);
        Assert.Equal(1, seq);
        Assert.Equal([1, 2, 3], payload);
        Assert.Equal(2, ParseOutgoing(_client.Sent[1]).Seq);
    }

    [Fact]
    public void Send_GssGoesToSequencedQueue()
    {
        _channels[ChannelType.ReliableGss].Send(new byte[] { 1 });

        Assert.Single(_client.SequencedMessages);
        Assert.Empty(_client.Sent);
    }

    [Theory]
    [InlineData(MaxPacketSize - 4)]
    [InlineData(MaxPacketSize - 3)]
    [InlineData(5000)]
    public void Send_LargeMessageIsSplitAndReassembles(int size)
    {
        var data = Enumerable.Range(0, size).Select(i => (byte)i).ToArray();

        _channels[ChannelType.ReliableGss].Send(data);
        var fragments = _client.SequencedMessages.ToArray();

        Assert.Equal((int)Math.Ceiling(size / (double)(MaxPacketSize - 4)), fragments.Length);
        Assert.All(fragments, f => Assert.True(f.Length <= MaxPacketSize));
        Assert.All(fragments[..^1], f => Assert.True(ParseOutgoing(f).Header.IsSplit));
        Assert.False(ParseOutgoing(fragments[^1]).Header.IsSplit);

        // Feed what we sent into a fresh receiving channel
        var receiver = Channel.GetChannels(new FakeNetworkClient(), Logger.None)[ChannelType.ReliableGss];
        var received = new List<byte[]>();
        receiver.PacketAvailable += p => received.Add(p.Peek(p.BytesRemaining).ToArray());
        foreach (var fragment in fragments)
        {
            var header = ParseOutgoing(fragment).Header;
            receiver.HandlePacket(new GamePacket(header, fragment[2..]));
        }

        receiver.Process(CancellationToken.None);

        Assert.Equal([data], received);
    }

    /// <summary>
    ///     Builds a packet the way NetworkClient.HandlePacket hands it to a channel: header parsed, data after the header
    /// </summary>
    private static GamePacket Incoming(ChannelType type, ushort? seq, byte[] payload, bool isSplit = false, byte resendCount = 0)
    {
        var body = new List<byte>();
        if (seq.HasValue)
        {
            body.Add((byte)(seq.Value >> 8));
            body.Add((byte)seq.Value);
        }

        body.AddRange(resendCount > 0 ? payload.Select(b => (byte)(b ^ _xorByte[resendCount - 1])) : payload);

        var header = new GamePacketHeader(type, resendCount, isSplit, (ushort)(body.Count + 2));
        return new GamePacket(header, body.ToArray());
    }

    private static (GamePacketHeader Header, ushort Seq, byte[] Payload) ParseOutgoing(Memory<byte> packet)
    {
        var bytes = packet.ToArray();
        byte[] headerBytes = [bytes[1], bytes[0]];
        var header = Deserializer.ReadStruct<GamePacketHeader>(headerBytes.AsMemory());
        var seq = (ushort)((bytes[2] << 8) | bytes[3]);
        return (header, seq, bytes[4..]);
    }

    private void Receive(ChannelType type, params GamePacket[] packets)
    {
        var channel = _channels[type];
        foreach (var packet in packets)
        {
            channel.HandlePacket(packet);
        }

        channel.Process(CancellationToken.None);
    }
}
