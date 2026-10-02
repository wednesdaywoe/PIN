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
        Assert.Equal(0, _context.Targets.Count);

        _context.Targets.Push(_c);
        new PopTargetsCommand(new PopTargetsCommandDef { Current = 1 }).Execute(_context);

        Assert.Equal([_a, _b], _context.Targets);
        Assert.Equal(0, _context.FormerTargets.Count);
    }

    [Fact]
    public void Swap_ExchangesCurrentAndFormer()
    {
        _context.Targets = new AptitudeTargets(_a);
        _context.FormerTargets = new AptitudeTargets(_b);

        new TargetSwapCommand(new TargetSwapCommandDef()).Execute(_context);

        Assert.Equal([_b], _context.Targets);
        Assert.Equal([_a], _context.FormerTargets);
    }

    [Fact]
    public void TargetSelf_PushesSelfAndKeepsPreviousAsFormer()
    {
        _context.Targets = new AptitudeTargets(_a);

        new TargetSelfCommand(new TargetSelfCommandDef()).Execute(_context);

        Assert.Equal([_a, _context.Self], _context.Targets);
        Assert.Equal([_a], _context.FormerTargets);
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

    [Fact]
    public void Chomp_InWhileLoop_RunsOnceThroughTheTargets()
    {
        // Pattern from 1543482: while (targets not empty) { act on target; chomp one }
        var factory = new TestFactory();
        var context = NewContext(factory);
        context.Targets = new AptitudeTargets(_a, _b, _c);
        var body = new FakeCommand(_ => true);
        factory.Add(1, new TargetStackEmptyCommand(new TargetStackEmptyCommandDef { NotEmpty = 1 }));
        factory.Add(2, body, new TargetTrimCommand(new TargetTrimCommandDef { Trimsize = 1, Chomp = 1, Current = 1, FromFront = 1 }));

        new WhileLoopCommand(new WhileLoopCommandDef { ConditionChain = 1, BodyChain = 2 }).Execute(context);

        Assert.Equal(3, body.Executions);
        Assert.Equal(0, context.Targets.Count);
    }

    private void Trim(uint trimSize, byte fromFront, byte chomp = 0)
    {
        new TargetTrimCommand(new TargetTrimCommandDef { Trimsize = trimSize, TrimsizeRegop = (byte)Operand.ASSIGN, Current = 1, FromFront = fromFront, Chomp = chomp }).Execute(_context);
    }
}
