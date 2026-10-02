using System.Linq;
using System.Numerics;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Target;
using Xunit;
using static GameServer.Tests.Systems.Aptitude.AptitudeTestHelpers;

namespace GameServer.Tests.Systems.Aptitude;

public class TargetFilterTests
{
    private readonly Context _context = NewContext();
    private readonly FakeTarget _a = new("a") { Position = new Vector3(1, 0, 0) };
    private readonly FakeTarget _b = new("b") { Position = new Vector3(10, 0, 0) };
    private readonly FakeTarget _c = new("c") { Position = new Vector3(2, 0, 0) };
    private readonly FakeTarget _d = new("d") { Position = new Vector3(20, 0, 0) };

    public TargetFilterTests()
    {
        _context.Targets = new AptitudeTargets(_a, _b, _c, _d);
        _context.FormerTargets = new AptitudeTargets(_d);
    }

    [Fact]
    public void SwapRemoveAll_MovesTheLastTargetIntoTheGap()
    {
        _context.Targets.SwapRemoveAll(t => t == _b);

        Assert.Equal([_a, _d, _c], _context.Targets);
    }

    [Theory]
    [InlineData(0, new[] { "a", "c" })]
    [InlineData(1, new[] { "d", "b" })]
    public void FilterByRange_KeepsInRangeOrNegated(byte negate, string[] expected)
    {
        // Self is at the origin
        new TargetFilterByRangeCommand(new TargetFilterByRangeCommandDef { Range = 5, Negate = negate }).Execute(_context);

        Assert.Equal(expected, _context.Targets.Select(t => t.ToString()));
        Assert.Equal([_d], _context.FormerTargets);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void FilterByRange_FailNoTargets(byte failNoTargets, bool expected)
    {
        Assert.Equal(expected, new TargetFilterByRangeCommand(new TargetFilterByRangeCommandDef { Range = 0.5f, FailNoTargets = failNoTargets }).Execute(_context));
    }

    [Fact]
    public void ByCharacterState_KeepsTargetsThatAreNotCharacters()
    {
        new TargetByCharacterStateCommand(new TargetByCharacterStateCommandDef { Living = 1 }).Execute(_context);

        Assert.Equal(4, _context.Targets.Count);
    }

    [Fact]
    public void ByObjectType_RemovesUnselectedTypes()
    {
        Assert.False(new TargetByObjectTypeCommand(new TargetByObjectTypeCommandDef { Character = 1, FailNoTargets = 1 }).Execute(_context));

        Assert.Equal(0, _context.Targets.Count);
        Assert.Equal([_d], _context.FormerTargets);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 4)]
    public void ByEffect_OnlyFiltersWithFilterList(byte filterList, int remaining)
    {
        new TargetByEffectCommand(new TargetByEffectCommandDef { EffectId = 1, FilterList = filterList }).Execute(_context);

        Assert.Equal(remaining, _context.Targets.Count);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void ByEffect_CheckOnly_FailsWhenNoTargetMatches(byte failNoTargets, bool expected)
    {
        Assert.Equal(expected, new TargetByEffectCommand(new TargetByEffectCommandDef { EffectId = 1, FailNoTargets = failNoTargets }).Execute(_context));
    }

    [Fact]
    public void TargetOwner_WithoutOwner_FailsAndLeavesTargetsAlone()
    {
        Assert.False(new TargetOwnerCommand(new TargetOwnerCommandDef { FailNone = 1 }).Execute(_context));

        Assert.Equal(4, _context.Targets.Count);
        Assert.Equal([_d], _context.FormerTargets);
    }
}
