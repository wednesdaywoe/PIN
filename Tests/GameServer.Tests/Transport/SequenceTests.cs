using GameServer.Packets;
using Xunit;

namespace GameServer.Tests.Transport;

/// <summary>
///     Sequence numbers wrap every 65536 packets, which on the unreliable channel the 2016 session
///     did five and a half times. The comparison that decides whether to ack has to survive that,
///     because getting it wrong stops acking for the rest of the session rather than for one packet.
/// </summary>
public class SequenceTests
{
    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(1, 1, false)]
    [InlineData(0, 65535, true)]
    [InlineData(65535, 0, false)]
    [InlineData(3, 65533, true)]
    [InlineData(65533, 3, false)]
    public void LaterIsLaterEvenAcrossTheWrap(ushort sequenceNumber, ushort than, bool expected)
    {
        Assert.Equal(expected, Sequence.IsAfter(sequenceNumber, than));
    }

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(2, 2, true)]
    [InlineData(3, 2, false)]
    [InlineData(65535, 0, true)]
    [InlineData(0, 65535, false)]
    public void CoveredByACumulativeAckIsTheOppositeOfLater(ushort sequenceNumber, ushort than, bool expected)
    {
        Assert.Equal(expected, Sequence.IsAtOrBefore(sequenceNumber, than));
    }

    [Fact]
    public void HalfTheNumberSpaceAwayIsTheEdgeOfWhatCanBeCompared()
    {
        // 32768 apart is ambiguous by construction: it reads as before in one direction and after
        // in the other. Nothing gets near it, since the whole queue empties inside two seconds.
        Assert.True(Sequence.IsAfter(32767, 0));
        Assert.False(Sequence.IsAfter(32768, 0));
    }
}
