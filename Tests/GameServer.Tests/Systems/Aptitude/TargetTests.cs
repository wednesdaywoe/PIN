using System.Linq;
using GameServer.Enums;
using GameServer.StaticDB.Records.apt;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Logic;
using GameServer.Systems.Aptitude.Commands.Target;
using Xunit;
using static GameServer.Tests.Systems.Aptitude.AptitudeTestHelpers;

namespace GameServer.Tests.Systems.Aptitude;

public class TargetTests
{
    private readonly Context _context = NewContext();
    private readonly FakeTarget _a = new("a");
    private readonly FakeTarget _b = new("b");
    private readonly FakeTarget _c = new("c");

    [Fact]
    public void PushThenPop_RestoresTargets()
    {
        _context.Targets = new AptitudeTargets(_a, _b);

        new PushTargetsCommand(new PushTargetsCommandDef { Current = 1 }).Execute(_context);
        Assert.Equal([_a, _b], _context.Targets);

        _context.Targets.Push(_c);
        Assert.True(new PopTargetsCommand(new PopTargetsCommandDef { Current = 1 }).Execute(_context));

        Assert.Equal([_a, _b], _context.Targets);
        Assert.Empty(_context.TargetStack);
    }

    [Fact]
    public void PushPop_IsAStack()
    {
        var push = new PushTargetsCommand(new PushTargetsCommandDef { Current = 1 });
        var pop = new PopTargetsCommand(new PopTargetsCommandDef { Current = 1 });
        _context.Targets = new AptitudeTargets(_a);
        push.Execute(_context);
        _context.Targets = new AptitudeTargets(_b);
        push.Execute(_context);

        pop.Execute(_context);
        Assert.Equal([_b], _context.Targets);
        pop.Execute(_context);
        Assert.Equal([_a], _context.Targets);
        Assert.False(pop.Execute(_context));
    }

    [Fact]
    public void PushPop_CurrentAndFormer()
    {
        _context.Targets = new AptitudeTargets(_a);
        _context.FormerTargets = new AptitudeTargets(_b);
        new PushTargetsCommand(new PushTargetsCommandDef { Current = 1, Former = 1 }).Execute(_context);
        _context.Targets = new AptitudeTargets();
        _context.FormerTargets = new AptitudeTargets();

        new PopTargetsCommand(new PopTargetsCommandDef { Current = 1, Former = 1 }).Execute(_context);

        Assert.Equal([_a], _context.Targets);
        Assert.Equal([_b], _context.FormerTargets);
    }

    [Fact]
    public void Peek_CopiesTopWithoutPopping()
    {
        var peek = new PeekTargetsCommand(new PeekTargetsCommandDef { Current = 1 });
        Assert.False(peek.Execute(_context));

        _context.Targets = new AptitudeTargets(_a);
        new PushTargetsCommand(new PushTargetsCommandDef { Current = 1 }).Execute(_context);
        _context.Targets = new AptitudeTargets();

        Assert.True(peek.Execute(_context));
        Assert.Equal([_a], _context.Targets);
        Assert.Single(_context.TargetStack);
    }

    [Theory]
    [InlineData(0, 0, new[] { "b" }, new[] { "a" })]
    [InlineData(1, 0, new string[0], new[] { "a" })]
    [InlineData(0, 1, new[] { "b" }, new string[0])]
    public void Swap_SwapsThenClears(byte clearCurrent, byte clearFormer, string[] current, string[] former)
    {
        _context.Targets = new AptitudeTargets(_a);
        _context.FormerTargets = new AptitudeTargets(_b);

        new TargetSwapCommand(new TargetSwapCommandDef { ClearCurrent = clearCurrent, ClearFormer = clearFormer }).Execute(_context);

        Assert.Equal(current, _context.Targets.Select(t => t.ToString()));
        Assert.Equal(former, _context.FormerTargets.Select(t => t.ToString()));
    }

    [Fact]
    public void TargetSelf_AddsSelfOnce()
    {
        _context.Targets = new AptitudeTargets(_a);
        _context.FormerTargets = new AptitudeTargets(_b);
        var command = new TargetSelfCommand(new TargetSelfCommandDef());

        command.Execute(_context);
        command.Execute(_context);

        Assert.Equal([_a, _context.Self], _context.Targets);
        Assert.Equal([_b], _context.FormerTargets);
    }

    [Fact]
    public void TargetClear_LeavesFormerAlone()
    {
        _context.Targets = new AptitudeTargets(_a);
        _context.FormerTargets = new AptitudeTargets(_b);

        new TargetClearCommand(new TargetClearCommandDef { Current = 1 }).Execute(_context);

        Assert.Equal(0, _context.Targets.Count);
        Assert.Equal([_b], _context.FormerTargets);
    }

    [Theory]
    [InlineData(0, new[] { "b" })]
    [InlineData(1, new string[0])]
    public void TargetPrevious_AddsFormerToCurrent(byte clearFormer, string[] former)
    {
        _context.Targets = new AptitudeTargets(_a);
        _context.FormerTargets = new AptitudeTargets(_b);

        new TargetPreviousCommand(new TargetPreviousCommandDef { Clearformer = clearFormer }).Execute(_context);

        Assert.Equal([_a, _b], _context.Targets);
        Assert.Equal(former, _context.FormerTargets.Select(t => t.ToString()));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void StackEmpty(byte notEmpty, bool expectedWhenEmpty)
    {
        var command = new TargetStackEmptyCommand(new TargetStackEmptyCommandDef { NotEmpty = notEmpty });

        Assert.Equal(expectedWhenEmpty, command.Execute(_context));
        _context.Targets.Push(_a);
        Assert.Equal(!expectedWhenEmpty, command.Execute(_context));
    }

    [Theory]
    [InlineData(0, new[] { "a", "b" })]
    [InlineData(1, new[] { "b", "c" })]
    public void Trim_KeepsTrimSizeTargets(byte fromFront, string[] expected)
    {
        _context.Targets = new AptitudeTargets(_a, _b, _c);

        Trim(trimSize: 2, fromFront);

        Assert.Equal(expected, _context.Targets.Select(t => t.ToString()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Trim_FewerTargetsThanTrimSize_KeepsAll(byte fromFront)
    {
        // e.g. "protect the 2 closest allies" with only one ally around
        _context.Targets = new AptitudeTargets(_a);

        Trim(trimSize: 2, fromFront);

        Assert.Equal([_a], _context.Targets);
    }

    [Fact]
    public void Trim_SizeFromRegister()
    {
        _context.Targets = new AptitudeTargets(_a, _b, _c);
        _context.Register = 1;

        new TargetTrimCommand(new TargetTrimCommandDef { Trimsize = 0, TrimsizeRegop = (byte)Operand.ADD, Current = 1 }).Execute(_context);

        Assert.Equal([_a], _context.Targets);
    }

    [Theory]
    [InlineData(0, 1u, new[] { "a", "b" })]
    [InlineData(1, 1u, new[] { "b", "c" })]
    [InlineData(1, 5u, new string[0])]
    public void Chomp_RemovesTrimSizeTargets(byte fromFront, uint trimSize, string[] expected)
    {
        _context.Targets = new AptitudeTargets(_a, _b, _c);

        Trim(trimSize, fromFront, chomp: 1);

        Assert.Equal(expected, _context.Targets.Select(t => t.ToString()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Chomp_InWhileLoop_EmptiesTheTargets(byte fromFront)
    {
        // Pattern from 501195 and 1543482: while (targets not empty) { act; chomp one }
        var factory = new TestFactory();
        var context = NewContext(factory);
        context.Targets = new AptitudeTargets(_a, _b, _c);
        var body = new FakeCommand(_ => true);
        factory.Add(1, new TargetStackEmptyCommand(new TargetStackEmptyCommandDef { NotEmpty = 1 }));
        factory.Add(2, body, new TargetTrimCommand(new TargetTrimCommandDef { Trimsize = 1, Chomp = 1, Current = 1, FromFront = fromFront }));

        new WhileLoopCommand(new WhileLoopCommandDef { ConditionChain = 1, BodyChain = 2 }).Execute(context);

        Assert.Equal(3, body.Executions);
        Assert.Equal(0, context.Targets.Count);
    }

    private void Trim(uint trimSize, byte fromFront, byte chomp = 0)
    {
        new TargetTrimCommand(new TargetTrimCommandDef { Trimsize = trimSize, TrimsizeRegop = (byte)Operand.ASSIGN, Current = 1, FromFront = fromFront, Chomp = chomp }).Execute(_context);
    }
}
