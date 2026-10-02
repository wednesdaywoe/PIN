using GameServer.StaticDB.Records.aptfs;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Target;
using Xunit;
using static GameServer.Tests.Systems.Aptitude.AptitudeTestHelpers;

namespace GameServer.Tests.Systems.Aptitude;

public class HostilityTests
{
    // Faction ids, priorities, default stances and relations taken from the SDB
    private const byte Accord = 1;
    private const byte Chosen = 2;
    private const byte Friendly = 4;
    private const byte Monster = 5;
    private const byte Bandit = 8;
    private const byte Neutral = 9;
    private const byte Hellhounds = 19;
    private const byte Copa = 20;

    private static readonly FactionStances _stances = new(
        [
            Faction(Accord, 10, 0),
            Faction(Chosen, 50, -1),
            Faction(Friendly, 0, 1),
            Faction(Monster, 50, -1),
            Faction(Bandit, 100, -1),
            Faction(Neutral, 0, 0),
            Faction(Hellhounds, 100, 0),
            Faction(Copa, 100, 0),
        ],
        [
            Relation(Accord, Chosen, -2, bidirectional: true),
            Relation(Accord, Copa, 1),
            Relation(Accord, Monster, -2),
            Relation(Monster, Accord, -2),
            Relation(Monster, Monster, 0, bidirectional: true),
            Relation(Hellhounds, Chosen, -2, bidirectional: true),
        ]);

    private readonly FakeTarget _self = new("self", Accord);
    private readonly FakeTarget _chosen = new("chosen", Chosen);
    private readonly FakeTarget _monster = new("monster", Monster);
    private readonly FakeTarget _copa = new("copa", Copa);
    private readonly FakeTarget _otherAccord = new("accord", Accord);
    private readonly FakeTarget _neutral = new("neutral", Neutral);

    [Theory]
    [InlineData(Accord, Copa, 1)] // relation row
    [InlineData(Chosen, Accord, -2)] // reverse of a bidirectional row
    [InlineData(Chosen, Hellhounds, -2)] // reverse of a bidirectional row
    [InlineData(Copa, Accord, 0)] // directional row does not apply in reverse, falls back to the defaults
    [InlineData(Accord, Accord, 1)] // same faction without a row
    [InlineData(Monster, Monster, 0)] // row overrides the same faction default
    [InlineData(Accord, Bandit, -1)] // no row: bandit default wins on priority
    [InlineData(Neutral, Friendly, 0)] // no row, same priority: the less friendly default
    public void GetStance(byte from, byte to, sbyte expected)
    {
        Assert.Equal(expected, _stances.GetStance(from, to));
    }

    [Fact]
    public void TargetHostiles_KeepsHostileTargets()
    {
        var context = WithTargets(_chosen, _monster, _copa, _otherAccord, _neutral);

        Assert.True(new TargetHostilesCommand(new TargetHostilesCommandDef()).Execute(context));

        Assert.Equal([_chosen, _monster], context.Targets);
        Assert.Equal(0, context.FormerTargets.Count);
    }

    [Fact]
    public void TargetFriendlies_KeepsFriendlyTargets()
    {
        var context = WithTargets(_chosen, _monster, _copa, _otherAccord, _neutral);

        new TargetFriendliesCommand(new TargetFriendliesCommandDef()).Execute(context);

        Assert.Equal([_copa, _otherAccord], context.Targets);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void IncludeSelf_ForcesSelfIn(byte includeSelf, bool expected)
    {
        var context = WithTargets(_self);

        new TargetFriendliesCommand(new TargetFriendliesCommandDef { IncludeSelf = includeSelf }).Execute(context);

        Assert.Equal(expected, context.Targets.Count == 1);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void IncludeInitiator_ForcesInitiatorIn(byte includeInitiator, bool expected)
    {
        // A hostile initiator, e.g. the source of an effect on the target
        var context = WithTargets(_chosen);
        context.Initiator = _chosen;

        new TargetHostilesCommand(new TargetHostilesCommandDef { IncludeInitiator = includeInitiator }).Execute(context);

        Assert.Equal(expected, context.Targets.Count == 1);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(0, true)]
    public void FailNoTargets(byte failNoTargets, bool expected)
    {
        var context = WithTargets(_copa);

        Assert.Equal(expected, new TargetHostilesCommand(new TargetHostilesCommandDef { FailNoTargets = failNoTargets }).Execute(context));
    }

    private static Faction Faction(uint id, byte priority, sbyte stance) => new() { Id = id, DefaultStancePriority = priority, DefaultStance = stance };

    private static FactionRelations Relation(uint a, uint b, sbyte stance, bool bidirectional = false) => new() { FactionA = a, FactionB = b, HostilityStance = stance, HostilityBidirectional = (byte)(bidirectional ? 1 : 0) };

    private Context WithTargets(params IAptitudeTarget[] targets)
    {
        var context = NewContext(factions: _stances, self: _self);
        context.Targets = new AptitudeTargets(targets);
        return context;
    }
}
