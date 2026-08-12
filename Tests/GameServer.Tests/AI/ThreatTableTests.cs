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
    [Fact]
    public void ThreatAccumulatesAcrossMultipleAdds()
    {
        var table = new ThreatTable();

        table.AddThreat(entityId: 1, amount: 5f);
        table.AddThreat(entityId: 1, amount: 3f);

        var entry = table.EntriesByThreatDescending().Single();
        Assert.Equal(1ul, entry.EntityId);
        Assert.Equal(8f, entry.Score);
    }

    [Fact]
    public void DecayReducesEveryEntryByTheSameAmount()
    {
        var table = new ThreatTable();
        table.AddThreat(entityId: 1, amount: 10f);
        table.AddThreat(entityId: 2, amount: 4f);

        table.Decay(3f);

        var byId = table.EntriesByThreatDescending().ToDictionary(e => e.EntityId, e => e.Score);
        Assert.Equal(7f, byId[1]);
        Assert.Equal(1f, byId[2]);
    }

    [Fact]
    public void AnEntryThatDecaysToZeroOrBelowIsForgotten()
    {
        var table = new ThreatTable();
        table.AddThreat(entityId: 1, amount: 2f);

        table.Decay(2f);

        Assert.Empty(table.EntriesByThreatDescending());
    }

    [Fact]
    public void EntriesComeBackHighestScoreFirst()
    {
        var table = new ThreatTable();
        table.AddThreat(entityId: 1, amount: 5f);
        table.AddThreat(entityId: 2, amount: 20f);
        table.AddThreat(entityId: 3, amount: 10f);

        var ordered = table.EntriesByThreatDescending().Select(e => e.EntityId).ToArray();

        Assert.Equal(new ulong[] { 2, 3, 1 }, ordered);
    }

    [Fact]
    public void ZeroOrNegativeThreatIsNotAdded()
    {
        var table = new ThreatTable();

        table.AddThreat(entityId: 1, amount: 0f);
        table.AddThreat(entityId: 1, amount: -5f);

        Assert.Empty(table.EntriesByThreatDescending());
    }

    [Fact]
    public void DecayingAnEmptyTableIsANoOp()
    {
        var table = new ThreatTable();

        table.Decay(5f);

        Assert.Empty(table.EntriesByThreatDescending());
    }
}
