using GameServer.StaticDB.Records.apt;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Logic;
using Xunit;
using static GameServer.Tests.Systems.Aptitude.AptitudeTestHelpers;

namespace GameServer.Tests.Systems.Aptitude;

public class LogicTests
{
    private readonly TestFactory _factory = new();
    private readonly Context _context;

    public LogicTests()
    {
        _context = NewContext(_factory);
    }

    [Fact]
    public void AndChain_StopsAtFirstFailure()
    {
        var last = Returns(true);

        Assert.False(NewChain(Returns(true), Returns(false), last).Execute(_context));
        Assert.Equal(0, last.Executions);
    }

    [Fact]
    public void OrChain_StopsAtFirstSuccess()
    {
        var last = Returns(true);

        Assert.True(NewChain(Returns(false), Returns(true), last).Execute(_context, Chain.ExecutionMethod.OrChain));
        Assert.Equal(0, last.Executions);
    }

    [Fact]
    public void EmptyChains()
    {
        Assert.True(NewChain().Execute(_context));
        Assert.False(NewChain().Execute(_context, Chain.ExecutionMethod.OrChain));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Negate(bool chainResult, bool expected)
    {
        _factory.Add(1, Returns(chainResult));

        Assert.Equal(expected, new LogicNegateCommand(new LogicNegateCommandDef { NegateChain = 1 }).Execute(_context));
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void LogicOr_RunsSecondChainOnlyIfFirstFails(bool first, int secondExecutions)
    {
        var second = Returns(true);
        _factory.Add(1, Returns(first));
        _factory.Add(2, second);

        Assert.True(new LogicOrCommand(new LogicOrCommandDef { AChain = 1, BChain = 2 }).Execute(_context));
        Assert.Equal(secondExecutions, second.Executions);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void LogicAndChain_AlwaysSuccess(byte alwaysSuccess, bool expected)
    {
        _factory.Add(1, Returns(false));

        Assert.Equal(expected, new LogicAndChainCommand(new LogicAndChainCommandDef { AndChain = 1, AlwaysSuccess = alwaysSuccess }).Execute(_context));
    }

    [Theory]
    [InlineData(true, 1, 0)]
    [InlineData(false, 0, 1)]
    public void ConditionalBranch_RunsThenOrElse(bool condition, int thenExecutions, int elseExecutions)
    {
        var then = Returns(true);
        var otherwise = Returns(true);
        _factory.Add(1, Returns(condition));
        _factory.Add(2, then);
        _factory.Add(3, otherwise);

        new ConditionalBranchCommand(new ConditionalBranchCommandDef { IfChain = 1, ThenChain = 2, ElseChain = 3 }).Execute(_context);

        Assert.Equal(thenExecutions, then.Executions);
        Assert.Equal(elseExecutions, otherwise.Executions);
    }

    [Fact]
    public void Logic_RestoresExecutionHint()
    {
        ExecutionHint seen = default;
        _factory.Add(1, new FakeCommand(c =>
        {
            seen = c.ExecutionHint;
            return true;
        }));
        _context.ExecutionHint = ExecutionHint.ApplyEffect;

        new LogicNegateCommand(new LogicNegateCommandDef { NegateChain = 1 }).Execute(_context);

        Assert.Equal(ExecutionHint.Logic, seen);
        Assert.Equal(ExecutionHint.ApplyEffect, _context.ExecutionHint);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void While_CountsToCondition(byte doWhile)
    {
        // while (register < 3) register++
        AddCounterLoop(limit: 3);

        new WhileLoopCommand(new WhileLoopCommandDef { ConditionChain = 1, BodyChain = 2, DoWhile = doWhile }).Execute(_context);

        Assert.Equal(3f, _context.Register);
    }

    [Theory]
    [InlineData(0, 0f)]
    [InlineData(1, 1f)]
    public void While_FalseCondition_DoWhileStillRunsBodyOnce(byte doWhile, float expected)
    {
        AddCounterLoop(limit: 0);

        new WhileLoopCommand(new WhileLoopCommandDef { ConditionChain = 1, BodyChain = 2, DoWhile = doWhile }).Execute(_context);

        Assert.Equal(expected, _context.Register);
    }

    [Fact]
    public void While_StopsAtMaximumLaps()
    {
        AddCounterLoop(limit: 1000);

        new WhileLoopCommand(new WhileLoopCommandDef { ConditionChain = 1, BodyChain = 2 }).Execute(_context);

        Assert.Equal(100f, _context.Register);
    }

    private void AddCounterLoop(int limit)
    {
        _factory.Add(1, new FakeCommand(c => c.Register < limit));
        _factory.Add(2, new FakeCommand(c =>
        {
            c.Register++;
            return true;
        }));
    }
}
