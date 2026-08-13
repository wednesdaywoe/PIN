using System.Linq;
using GameServer.Systems.AI;
using Xunit;

namespace GameServer.Tests.AI;

/// <summary>
///     <see cref="ThreatTable"/> is what gives NPC target selection its stickiness, so a target
///     survives a tick or two of broken line of sight instead of flickering to whoever is nearest.
///     These cover that decay/expiry behaviour without needing a live NPC or physics engine.
/// </summary>
public class ThreatTableTests
{
    /// <summary>A ceiling high enough to stay out of the way of cases that aren't about capping.</summary>
    private const float NoCap = 1000f;

    [Fact]
    public void ThreatAccumulatesAcrossMultipleAdds()
    {
        var table = new ThreatTable();

        table.AddThreat(entityId: 1, amount: 5f, maxThreat: NoCap);
        table.AddThreat(entityId: 1, amount: 3f, maxThreat: NoCap);

        var entry = table.EntriesByThreatDescending().Single();
        Assert.Equal(1ul, entry.EntityId);
        Assert.Equal(8f, entry.Score);
    }

    [Fact]
    public void DecayReducesEveryEntryByTheSameAmount()
    {
        var table = new ThreatTable();
        table.AddThreat(entityId: 1, amount: 10f, maxThreat: NoCap);
        table.AddThreat(entityId: 2, amount: 4f, maxThreat: NoCap);

        table.Decay(3f);

        var byId = table.EntriesByThreatDescending().ToDictionary(e => e.EntityId, e => e.Score);
        Assert.Equal(7f, byId[1]);
        Assert.Equal(1f, byId[2]);
    }

    [Fact]
    public void AnEntryThatDecaysToZeroOrBelowIsForgotten()
    {
        var table = new ThreatTable();
        table.AddThreat(entityId: 1, amount: 2f, maxThreat: NoCap);

        table.Decay(2f);

        Assert.Empty(table.EntriesByThreatDescending());
    }

    [Fact]
    public void EntriesComeBackHighestScoreFirst()
    {
        var table = new ThreatTable();
        table.AddThreat(entityId: 1, amount: 5f, maxThreat: NoCap);
        table.AddThreat(entityId: 2, amount: 20f, maxThreat: NoCap);
        table.AddThreat(entityId: 3, amount: 10f, maxThreat: NoCap);

        var ordered = table.EntriesByThreatDescending().Select(e => e.EntityId).ToArray();

        Assert.Equal(new ulong[] { 2, 3, 1 }, ordered);
    }

    [Fact]
    public void ZeroOrNegativeThreatIsNotAdded()
    {
        var table = new ThreatTable();

        table.AddThreat(entityId: 1, amount: 0f, maxThreat: NoCap);
        table.AddThreat(entityId: 1, amount: -5f, maxThreat: NoCap);

        Assert.Empty(table.EntriesByThreatDescending());
    }

    [Fact]
    public void DecayingAnEmptyTableIsANoOp()
    {
        var table = new ThreatTable();

        table.Decay(5f);

        Assert.Empty(table.EntriesByThreatDescending());
    }

    [Fact]
    public void ThreatStopsAtTheCap()
    {
        // The cap is what bounds how long an NPC stays interested. Uncapped, a target that stood in
        // front of one for a minute banked hundreds of points and took minutes of decay to fall back
        // under the engage threshold — N4 saw an NPC that would not let go.
        var table = new ThreatTable();

        for (var i = 0; i < 20; i++)
        {
            table.AddThreat(entityId: 1, amount: 10f, maxThreat: 60f);
        }

        Assert.Equal(60f, table.EntriesByThreatDescending().Single().Score);
    }

    [Fact]
    public void CappedThreatDecaysInBoundedTime()
    {
        var table = new ThreatTable();
        for (var i = 0; i < 20; i++)
        {
            table.AddThreat(entityId: 1, amount: 10f, maxThreat: 60f);
        }

        // Seven seconds of decay at the shipped 8/sec, against an engage threshold of 10. The exact
        // crossing is (60 - 10) / 8 = 6.25s; this asserts the bound, not the precise moment.
        table.Decay(56f);

        Assert.True(table.EntriesByThreatDescending().Single().Score < 10f);
    }

    [Fact]
    public void ForgettingDropsAnEntryOutright()
    {
        var table = new ThreatTable();
        table.AddThreat(entityId: 1, amount: 50f, maxThreat: NoCap);
        table.AddThreat(entityId: 2, amount: 50f, maxThreat: NoCap);

        table.Forget(1);

        Assert.Equal(2ul, table.EntriesByThreatDescending().Single().EntityId);
    }

    [Fact]
    public void ForgettingSomethingAbsentIsHarmless()
    {
        var table = new ThreatTable();

        table.Forget(99);

        Assert.Empty(table.EntriesByThreatDescending());
    }
}
