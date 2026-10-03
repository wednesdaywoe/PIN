using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Duration;
using NSubstitute;
using Xunit;
using static GameServer.Tests.Systems.Aptitude.AptitudeTestHelpers;

namespace GameServer.Tests.Systems.Aptitude;

public class ReplenishableDurationTests
{
    private readonly ReplenishableDurationCommand _command = new(new ReplenishableDurationCommandDef { Id = 1 });
    private readonly Context _context = NewContext();

    public ReplenishableDurationTests()
    {
        _context.InitTime = 100_000;
    }

    [Fact]
    public void Lasts_TheRegisterInSeconds()
    {
        _context.Register = 8;

        Assert.True(RunAt(107_999));
        Assert.False(RunAt(108_000));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    public void UnusableRegister_FallsBackToTenSeconds(float register)
    {
        _context.Register = register;

        Assert.True(RunAt(109_999));
        Assert.False(RunAt(110_000));
    }

    [Fact]
    public void LongRegister_IsCappedAtTwoMinutes()
    {
        _context.Register = 100_000;

        Assert.False(RunAt(220_000));
    }

    private bool RunAt(uint now)
    {
        _context.Shard.CurrentTime.Returns(now);
        return _command.Execute(_context);
    }
}

public class CopyContextTests
{
    [Fact]
    public void Copies_DoNotShareTargetLists()
    {
        var original = NewContext();
        original.Targets.Push(new FakeTarget("a"));

        var first = Context.CopyContext(original);
        var second = Context.CopyContext(original);
        first.Targets.Clear();
        first.Targets.Push(new FakeTarget("b"));

        Assert.Equal("a", Assert.Single(original.Targets).ToString());
        Assert.Equal("a", Assert.Single(second.Targets).ToString());
        Assert.Equal("b", Assert.Single(first.Targets).ToString());
    }
}
