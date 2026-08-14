using System;
using System.Linq;
using GameServer;
using GameServer.Packets;
using Shared.Udp;
using Xunit;

namespace GameServer.Tests.Transport;

/// <summary>
///     The outbound half of reliability. Every constant these tests assert against was measured off
///     the 2016 capture with <c>CaptureReplay --transport</c>, so a test that fails here either
///     found a bug or is being told the protocol changed.
/// </summary>
public class RetransmitQueueTests
{
    private static readonly DateTime Start = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void APacketIsNotDueBeforeTheTimeout()
    {
        var queue = new RetransmitQueue();
        queue.Track(1, Packet(1, [0xAA, 0xBB]), Start);

        Assert.Empty(queue.Due(Start.AddMilliseconds(RetransmitQueue.TimeoutMs - 1)));
        Assert.Equal(1, queue.Pending);
    }

    [Fact]
    public void APacketIsDueOnceTheTimeoutPasses()
    {
        var queue = new RetransmitQueue();
        queue.Track(7, Packet(7, [0xAA, 0xBB]), Start);

        var due = queue.Due(Start.AddMilliseconds(RetransmitQueue.TimeoutMs));

        var resend = Assert.Single(due);
        Assert.Equal(7, resend.SequenceNumber);
        Assert.Equal(1, resend.Attempt);
        Assert.False(resend.GaveUp);
        Assert.Equal(1, queue.Resent);
    }

    [Fact]
    public void AnAckRetiresThePacketSoItIsNeverResent()
    {
        var queue = new RetransmitQueue();
        queue.Track(7, Packet(7, [0xAA]), Start);

        Assert.Equal(1, queue.Acknowledge(7));
        Assert.Equal(0, queue.Pending);
        Assert.Empty(queue.Due(Start.AddMilliseconds(RetransmitQueue.TimeoutMs * 10)));
        Assert.Equal(0, queue.Resent);
    }

    [Fact]
    public void AnAckIsCumulativeAndRetiresTheWholeRunBehindIt()
    {
        // The capture's client acked only 60% of the server's reliable packets and still needed 24
        // resends in the whole session, which is only possible if one ack covers the run behind it.
        var queue = new RetransmitQueue();
        foreach (var sequenceNumber in Enumerable.Range(1, 5))
        {
            queue.Track((ushort)sequenceNumber, Packet((ushort)sequenceNumber, [0xAA]), Start);
        }

        Assert.Equal(4, queue.Acknowledge(4));
        Assert.Equal(1, queue.Pending);

        var resend = Assert.Single(queue.Due(Start.AddMilliseconds(RetransmitQueue.TimeoutMs)));
        Assert.Equal(5, resend.SequenceNumber);
    }

    [Fact]
    public void AnAckCoversTheOtherSideOfAWrap()
    {
        var queue = new RetransmitQueue();
        queue.Track(65534, Packet(65534, [0xAA]), Start);
        queue.Track(65535, Packet(65535, [0xAA]), Start);
        queue.Track(0, Packet(0, [0xAA]), Start);
        queue.Track(1, Packet(1, [0xAA]), Start);

        Assert.Equal(3, queue.Acknowledge(0));
        Assert.Equal(1, queue.Pending);
    }

    [Fact]
    public void AnAckForSomethingOlderRetiresNothing()
    {
        var queue = new RetransmitQueue();
        queue.Track(100, Packet(100, [0xAA]), Start);

        Assert.Equal(0, queue.Acknowledge(99));
        Assert.Equal(1, queue.Pending);
    }

    [Fact]
    public void AResendRearmsTheTimeoutRatherThanFiringEveryTick()
    {
        var queue = new RetransmitQueue();
        queue.Track(7, Packet(7, [0xAA]), Start);

        var firstDue = Start.AddMilliseconds(RetransmitQueue.TimeoutMs);
        Assert.Single(queue.Due(firstDue));
        Assert.Empty(queue.Due(firstDue.AddMilliseconds(1)));

        var resend = Assert.Single(queue.Due(firstDue.AddMilliseconds(RetransmitQueue.TimeoutMs)));
        Assert.Equal(2, resend.Attempt);
    }

    [Fact]
    public void APacketThatIsNeverAckedIsAbandonedRatherThanResentForever()
    {
        var queue = new RetransmitQueue();
        queue.Track(7, Packet(7, [0xAA]), Start);

        var now = Start;
        for (var attempt = 1; attempt <= RetransmitQueue.MaxAttempts; attempt++)
        {
            now = now.AddMilliseconds(RetransmitQueue.TimeoutMs);
            Assert.False(Assert.Single(queue.Due(now)).GaveUp);
        }

        now = now.AddMilliseconds(RetransmitQueue.TimeoutMs);
        var gaveUp = Assert.Single(queue.Due(now));

        Assert.True(gaveUp.GaveUp);
        Assert.Equal(0, queue.Pending);
        Assert.Equal(1, queue.Abandoned);
        Assert.Equal(RetransmitQueue.MaxAttempts, queue.Resent);
    }

    [Fact]
    public void AResendCarriesTheSameBytesUnderTheResendCountRetailStamped()
    {
        // 15 of the capture's 24 resends have their original in the capture too, and all 15 are
        // byte-identical to it once the XOR is undone.
        var body = new byte[] { 0x01, 0x02, 0x03, 0xFF, 0x00 };
        var original = Packet(4242, body);

        var resent = RetransmitQueue.EncodeResend(original, RetransmitQueue.ResendCountStamp);

        var header = ReadHeader(resent);
        Assert.Equal(RetransmitQueue.ResendCountStamp, header.ResendCount);
        Assert.Equal(ChannelType.ReliableGss, header.Channel);
        Assert.Equal(original.Length, resent.Length);

        // The sequence number is read before the XOR starts, so it must survive untouched
        Assert.Equal(original.Span[2], resent.Span[2]);
        Assert.Equal(original.Span[3], resent.Span[3]);

        Assert.Equal(body, Decode(resent));
    }

    [Fact]
    public void EveryEncodableResendCountRoundTrips()
    {
        var body = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        foreach (var count in new byte[] { 1, 2, 3 })
        {
            var resent = RetransmitQueue.EncodeResend(Packet(9, body), count);
            Assert.Equal(count, ReadHeader(resent).ResendCount);
            Assert.Equal(body, Decode(resent));
        }
    }

    [Fact]
    public void AFourthResendCountCannotBeEncoded()
    {
        // Two bits in the header and three entries in the XOR table, which is why MaxAttempts is 3
        Assert.Throws<ArgumentOutOfRangeException>(() => RetransmitQueue.EncodeResend(Packet(1, [0xAA]), 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => RetransmitQueue.EncodeResend(Packet(1, [0xAA]), 0));
    }

    [Fact]
    public void AnEmptyQueueHasNothingToDo()
    {
        var queue = new RetransmitQueue();

        Assert.Empty(queue.Due(Start.AddHours(1)));
        Assert.Equal(0, queue.Acknowledge(500));
    }

    /// <summary>
    ///     A packet shaped the way <see cref="Channel" /> shapes one: two header bytes with the
    ///     11-bit length counting itself, then the sequence number, then the body.
    /// </summary>
    private static Memory<byte> Packet(ushort sequenceNumber, byte[] body)
    {
        var bytes = new byte[4 + body.Length];
        var header = new GamePacketHeader(ChannelType.ReliableGss, 0, false, (ushort)bytes.Length);

        Serializer.WritePrimitive(Utils.SimpleFixEndianness(header.PacketHeader)).CopyTo(bytes.AsMemory());
        Serializer.WritePrimitive(Utils.SimpleFixEndianness(sequenceNumber)).CopyTo(bytes.AsMemory(2, 2));
        body.CopyTo(bytes, 4);

        return bytes;
    }

    private static GamePacketHeader ReadHeader(Memory<byte> packet)
    {
        var headerBytes = packet[..2].ToArray();
        Array.Reverse(headerBytes);
        return Deserializer.ReadStruct<GamePacketHeader>(headerBytes.AsMemory());
    }

    /// <summary>
    ///     Undoes a resend the way the receiving half of <see cref="Channel" /> does, which is what
    ///     makes this a round trip rather than a restatement of the encoder.
    /// </summary>
    private static byte[] Decode(Memory<byte> packet)
    {
        byte[] xorByte = [0xFF, 0xAA, 0xCC];
        var mask = xorByte[ReadHeader(packet).ResendCount - 1];

        return packet[4..].ToArray().Select(b => (byte)(b ^ mask)).ToArray();
    }
}
