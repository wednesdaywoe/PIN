using GameServer.Packets;
using Xunit;

namespace GameServer.Tests.Transport;

/// <summary>
///     What stops a resent packet being processed twice. It has to remember what was actually taken
///     rather than the highest sequence acked, because the ack skips over a gap
///     (<see href="../../../Docs/gaps/network.md">NET-25</see>) and everything behind that gap would
///     otherwise read as a duplicate of something that never arrived.
/// </summary>
public class DeliveredSequencesTests
{
    [Fact]
    public void ASequenceIsRememberedOnceItIsTaken()
    {
        var delivered = new DeliveredSequences();

        Assert.False(delivered.Contains(7));
        delivered.Remember(7);
        Assert.True(delivered.Contains(7));
    }

    [Fact]
    public void RememberingTheSameSequenceTwiceCostsNothing()
    {
        var delivered = new DeliveredSequences(capacity: 4);

        delivered.Remember(7);
        delivered.Remember(7);

        Assert.Equal(1, delivered.Count);
    }

    [Fact]
    public void AGapDoesNotMakeWhatIsUnderItLookDelivered()
    {
        var delivered = new DeliveredSequences();

        delivered.Remember(9);
        delivered.Remember(10);

        Assert.False(delivered.Contains(8));
    }

    [Fact]
    public void TheOldestIsForgottenOnceItIsFull()
    {
        var delivered = new DeliveredSequences(capacity: 3);

        delivered.Remember(1);
        delivered.Remember(2);
        delivered.Remember(3);
        delivered.Remember(4);

        Assert.Equal(3, delivered.Count);
        Assert.False(delivered.Contains(1));
        Assert.True(delivered.Contains(2));
        Assert.True(delivered.Contains(4));
    }

    [Fact]
    public void AWrapIsJustAnotherSequence()
    {
        var delivered = new DeliveredSequences();

        delivered.Remember(65535);
        delivered.Remember(0);

        Assert.True(delivered.Contains(65535));
        Assert.True(delivered.Contains(0));
    }
}
