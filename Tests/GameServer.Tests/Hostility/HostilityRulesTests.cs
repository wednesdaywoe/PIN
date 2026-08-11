using GameServer.Enums;
using GameServer.Systems.Hostility;
using Xunit;

namespace GameServer.Tests.Hostility;

/// <summary>
///     The rules that decide who may hurt whom, minus the faction relations table. Anything involving two
///     different factions resolves through SDBUtils.GetFactionStance and so needs a real clientdb; that half
///     is H1-H7 in Docs/In-Game-Tests/Hostility.md. What's left here is every path that answers
///     before the table is consulted, which includes the two that protect players from each other.
/// </summary>
public class HostilityRulesTests
{
    [Fact]
    public void SharingATeamIsFriendlyEvenAcrossFactions()
    {
        var source = FakeEntity.InFactionAndTeam(factionId: 1, teamId: 5);
        var other = FakeEntity.InFactionAndTeam(factionId: 2, teamId: 5);

        Assert.Equal(HostilityStance.Friendly, HostilityRules.GetStance(source, other));
        Assert.True(HostilityRules.AreFriendly(source, other));
        Assert.False(HostilityRules.CanDamage(source, other));
    }

    /// <summary>
    ///     Every player character is faction 1, so this is what stops players shooting each other now that
    ///     the explicit IsPlayerControlled check is gone.
    /// </summary>
    [Fact]
    public void SharingAFactionIsFriendly()
    {
        var source = FakeEntity.InFaction(1);
        var other = FakeEntity.InFaction(1);

        Assert.Equal(HostilityStance.Friendly, HostilityRules.GetStance(source, other));
        Assert.False(HostilityRules.CanDamage(source, other));
    }

    [Fact]
    public void DifferentTeamsFallThroughToTheFactionCheck()
    {
        var source = FakeEntity.InFactionAndTeam(factionId: 1, teamId: 5);
        var other = FakeEntity.InFactionAndTeam(factionId: 1, teamId: 6);

        Assert.Equal(HostilityStance.Friendly, HostilityRules.GetStance(source, other));
    }

    [Fact]
    public void AnEntityWithNoFactionIsNeutralToEveryone()
    {
        var factioned = FakeEntity.InFaction(1);
        var unknown = FakeEntity.WithNoHostilityInfo();

        Assert.Equal(HostilityStance.Neutral, HostilityRules.GetStance(factioned, unknown));
        Assert.Equal(HostilityStance.Neutral, HostilityRules.GetStance(unknown, factioned));
        Assert.Equal(HostilityStance.Neutral, HostilityRules.GetStance(unknown, unknown));
    }

    /// <summary>
    ///     Neutral is not protected. Only friendlies are, matching the client's own friendly/hostile/neither
    ///     split, so an entity nobody has given a faction to can still be shot.
    /// </summary>
    [Fact]
    public void NeutralsCanBeDamaged()
    {
        var source = FakeEntity.InFaction(1);
        var unknown = FakeEntity.WithNoHostilityInfo();

        Assert.True(HostilityRules.CanDamage(source, unknown));
        Assert.False(HostilityRules.AreHostile(source, unknown));
        Assert.False(HostilityRules.AreFriendly(source, unknown));
    }

    [Fact]
    public void ANullAttackerIsEnvironmentalDamageAndAlwaysAllowed()
    {
        Assert.True(HostilityRules.CanDamage(null, FakeEntity.InFaction(1)));
    }

    [Fact]
    public void NothingCanDamageANullTarget()
    {
        Assert.False(HostilityRules.CanDamage(FakeEntity.InFaction(1), null));
        Assert.False(HostilityRules.CanDamage(null, null));
    }

    [Fact]
    public void ANullSideIsNeutralRatherThanAnException()
    {
        Assert.Equal(HostilityStance.Neutral, HostilityRules.GetStance(null, FakeEntity.InFaction(1)));
        Assert.Equal(HostilityStance.Neutral, HostilityRules.GetStance(FakeEntity.InFaction(1), null));
    }
}
