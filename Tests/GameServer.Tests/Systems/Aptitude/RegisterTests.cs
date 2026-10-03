using GameServer.Enums;
using GameServer.StaticDB.Records.apt;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Register;
using Xunit;
using static GameServer.Tests.Systems.Aptitude.AptitudeTestHelpers;

namespace GameServer.Tests.Systems.Aptitude;

public class RegisterTests
{
    // Register as first and the param as second, so subtract and divide compute param - register and param / register.
    // Matches the client's FUN_00bc1130 in FirefallClient.exe. Which side is the exponent has not been confirmed.
    [Theory]
    [InlineData(Operand.ASSIGN, 8f)]
    [InlineData(Operand.ADD, 10f)]
    [InlineData(Operand.ADD_ALT, 10f)]
    [InlineData(Operand.MULTIPLY, 16f)]
    [InlineData(Operand.MULTIPLY_ALT, 16f)]
    [InlineData(Operand.EXPONENTIATE, 64f)]
    [InlineData(Operand.SUBTRACT, 6f)]
    [InlineData(Operand.DIVIDE, 4f)]
    [InlineData(Operand.MINIMUM, 2f)]
    [InlineData(Operand.MAXIMUM, 8f)]
    public void RegistryOp(Operand op, float expected)
    {
        Assert.Equal(expected, AbilitySystem.RegistryOp(2f, 8f, op));
    }

    [Fact]
    public void RegistryOp_NaNRegister_TakesSecond()
    {
        Assert.Equal(8f, AbilitySystem.RegistryOp(float.NaN, 8f, Operand.ADD));
    }

    [Fact]
    public void SetRegister_AppliesOperand()
    {
        var context = NewContext();
        context.Register = 3;

        new SetRegisterCommand(new SetRegisterCommandDef { RegisterVal = 4, Regop = (byte)Operand.ADD }).Execute(context);

        Assert.Equal(7f, context.Register);
    }

    // A stack, as the client's PushRegister/PeekRegister/PopRegister (FUN_00bb3ae0, 00bb37c0, 00bb3940) keep it
    [Fact]
    public void PushThenPop_IsLastInFirstOut()
    {
        var context = NewContext();
        var push = new PushRegisterCommand(new PushRegisterCommandDef());
        var pop = new PopRegisterCommand(new PopRegisterCommandDef { Regop = (byte)Operand.ASSIGN });

        context.Register = 5;
        push.Execute(context);
        context.Register = 7;
        push.Execute(context);
        Assert.Equal(7f, context.Register);

        context.Register = 0;
        Assert.True(pop.Execute(context));
        Assert.Equal(7f, context.Register);
        Assert.True(pop.Execute(context));
        Assert.Equal(5f, context.Register);
        Assert.False(pop.Execute(context));
    }

    [Fact]
    public void PeekRegister_CombinesTopAndKeepsIt()
    {
        var context = NewContext();
        context.Register = 4;
        new PushRegisterCommand(new PushRegisterCommandDef()).Execute(context);

        context.Register = 0.5f;
        var peek = new PeekRegisterCommand(new PeekRegisterCommandDef { Regop = (byte)Operand.MULTIPLY });
        Assert.True(peek.Execute(context));
        Assert.Equal(2f, context.Register);
        Assert.Single(context.RegisterStack);
    }

    [Fact]
    public void PeekRegister_EmptyStack_Fails()
    {
        Assert.False(new PeekRegisterCommand(new PeekRegisterCommandDef()).Execute(NewContext()));
    }

    // Creeping Death's cloud (effect 13042): its apply chain pushes 4, and each update tick peeks it into the radius
    // (0.5 x top) and replaces it with top + 1, so the cloud grows half a metre a tick.
    [Fact]
    public void CreepingDeathSequence_GrowsByOneEachTick()
    {
        var context = NewContext();
        var push = new PushRegisterCommand(new PushRegisterCommandDef());
        var peek = new PeekRegisterCommand(new PeekRegisterCommandDef { Regop = (byte)Operand.MULTIPLY });
        var pop = new PopRegisterCommand(new PopRegisterCommandDef { Regop = (byte)Operand.ADD });

        context.Register = 4;
        push.Execute(context);

        for (var tick = 0; tick < 3; tick++)
        {
            context.Register = 0.5f;
            peek.Execute(context);
            Assert.Equal(2f + tick * 0.5f, context.Register);

            context.Register = 1;
            pop.Execute(context);
            push.Execute(context);
        }

        Assert.Equal(7f, context.RegisterStack.Peek());
    }

    [Fact]
    public void CopyContext_CopiesRegisterStackInOrder()
    {
        var context = NewContext();
        context.RegisterStack.Push(1);
        context.RegisterStack.Push(2);

        var copy = Context.CopyContext(context);
        copy.RegisterStack.Pop();

        Assert.Equal(2f, context.RegisterStack.Peek());
        Assert.Equal(1f, copy.RegisterStack.Peek());
    }

    [Theory]
    [InlineData(10f, true)]
    [InlineData(10.5f, true)]
    [InlineData(9.5f, true)]
    [InlineData(10.6f, false)]
    public void RegisterComparison_EqualToWithinTolerance(float register, bool expected)
    {
        Assert.Equal(expected, Compare(register, new RegisterComparisonCommandDef { CompareVal = 10, EqualTol = 0.5f, EqualTo = 1 }));
    }

    [Theory]
    [InlineData(9f, true, false)]
    [InlineData(10f, false, false)]
    [InlineData(11f, false, true)]
    public void RegisterComparison_LessAndGreater(float register, bool less, bool greater)
    {
        Assert.Equal(less, Compare(register, new RegisterComparisonCommandDef { CompareVal = 10, LessThan = 1 }));
        Assert.Equal(greater, Compare(register, new RegisterComparisonCommandDef { CompareVal = 10, GreaterThan = 1 }));
    }

    [Fact]
    public void RegisterComparison_NoConditionSet_IsFalse()
    {
        Assert.False(Compare(10f, new RegisterComparisonCommandDef { CompareVal = 10 }));
    }

    [Fact]
    public void RegisterRandom_StaysInRange()
    {
        var context = NewContext();
        var command = new RegisterRandomCommand(new RegisterRandomCommandDef { MinValue = 2, MaxValue = 5, Regop = (byte)Operand.ASSIGN });

        for (var i = 0; i < 1000; i++)
        {
            command.Execute(context);
            Assert.InRange(context.Register, 2f, 5f);
        }
    }

    private static bool Compare(float register, RegisterComparisonCommandDef def)
    {
        var context = NewContext();
        context.Register = register;
        return new RegisterComparisonCommand(def).Execute(context);
    }
}
