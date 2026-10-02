using GameServer.Enums;
using GameServer.StaticDB.Records.apt;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Aptitude.Commands.Register;
using Xunit;
using static GameServer.Tests.Systems.Aptitude.AptitudeTestHelpers;

namespace GameServer.Tests.Systems.Aptitude;

public class RegisterTests
{
    // Current behaviour with the register as first and the param as second, so subtract and divide compute param - register
    // and param / register. The SDB backs this up for divide: chain 1147243 loads a charge rate into the register, divides
    // 400000 by it and uses the result as a duration. Subtract and exponentiate have not been confirmed against the client.
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

    [Fact]
    public void PushThenPop_RestoresRegister()
    {
        var context = NewContext();
        context.Register = 5;

        new PushRegisterCommand(new PushRegisterCommandDef()).Execute(context);
        Assert.Equal(0f, context.Register);
        Assert.Equal(5f, context.FormerRegister);

        context.Register = 9;
        new PopRegisterCommand(new PopRegisterCommandDef()).Execute(context);
        Assert.Equal(5f, context.Register);
        Assert.Equal(0f, context.FormerRegister);
    }

    [Theory]
    [InlineData(2f, 3f, true)]
    [InlineData(0f, 0f, false)]
    public void PeekRegister_CombinesWithFormerAndReturnsNonZero(float register, float former, bool expected)
    {
        var context = NewContext();
        context.Register = register;
        context.FormerRegister = former;

        var result = new PeekRegisterCommand(new PeekRegisterCommandDef { Regop = (byte)Operand.ADD }).Execute(context);

        Assert.Equal(register + former, context.Register);
        Assert.Equal(expected, result);
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
