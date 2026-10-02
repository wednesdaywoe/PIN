using System.Linq;
using GameServer.Enums;
using GameServer.StaticDB.Records.apt;
using GameServer.Systems.Aptitude;
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

    private void Trim(uint trimSize, byte fromFront)
    {
        new TargetTrimCommand(new TargetTrimCommandDef { Trimsize = trimSize, TrimsizeRegop = (byte)Operand.ASSIGN, Current = 1, FromFront = fromFront }).Execute(_context);
    }
}
