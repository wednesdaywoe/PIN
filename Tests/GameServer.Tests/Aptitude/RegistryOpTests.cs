using GameServer.Enums;
using GameServer.Systems.Aptitude;
using Xunit;

namespace GameServer.Tests.Aptitude;

/// <summary>
///     <see cref="AbilitySystem.RegistryOp"/> combines a command's literal parameter with whatever the chain
///     left in the register. Nearly every damage, duration and range parameter goes through it, and the
///     argument order is easy to get backwards, so the asymmetric operations are worth pinning by hand.
/// </summary>
public class RegistryOpTests
{
    private const float Register = 10f;
    private const float Param = 3f;

    [Fact]
    public void AssignIgnoresTheRegister()
    {
        Assert.Equal(Param, AbilitySystem.RegistryOp(Register, Param, Operand.ASSIGN));
    }

    [Theory]
    [InlineData(Operand.ADD)]
    [InlineData(Operand.ADD_ALT)]
    public void AddSums(Operand op)
    {
        Assert.Equal(13f, AbilitySystem.RegistryOp(Register, Param, op));
    }

    [Theory]
    [InlineData(Operand.MULTIPLY)]
    [InlineData(Operand.MULTIPLY_ALT)]
    public void MultiplyProducts(Operand op)
    {
        Assert.Equal(30f, AbilitySystem.RegistryOp(Register, Param, op));
    }

    /// <summary>
    ///     Subtract and divide take the register off the parameter rather than the other way round. Nothing
    ///     else here can tell the two orders apart.
    /// </summary>
    [Fact]
    public void SubtractAndDivideTakeTheRegisterFromTheParameter()
    {
        Assert.Equal(-7f, AbilitySystem.RegistryOp(Register, Param, Operand.SUBTRACT));
        Assert.Equal(0.3f, AbilitySystem.RegistryOp(Register, Param, Operand.DIVIDE), 5);
    }

    [Fact]
    public void ExponentiateRaisesTheParameterToTheRegister()
    {
        Assert.Equal(59049f, AbilitySystem.RegistryOp(Register, Param, Operand.EXPONENTIATE), 1);
    }

    [Fact]
    public void MinimumAndMaximumPickASide()
    {
        Assert.Equal(Param, AbilitySystem.RegistryOp(Register, Param, Operand.MINIMUM));
        Assert.Equal(Register, AbilitySystem.RegistryOp(Register, Param, Operand.MAXIMUM));

        Assert.Equal(Param, AbilitySystem.RegistryOp(Param, Register, Operand.MINIMUM));
        Assert.Equal(Register, AbilitySystem.RegistryOp(Param, Register, Operand.MAXIMUM));
    }

    /// <summary>
    ///     An empty register is NaN, which means "the chain never put anything here" rather than a number to
    ///     compute with. Every operation has to fall through to the command's own parameter, otherwise the
    ///     NaN spreads into damage and silently zeroes the hit.
    /// </summary>
    [Theory]
    [InlineData(Operand.ASSIGN)]
    [InlineData(Operand.ADD)]
    [InlineData(Operand.MULTIPLY)]
    [InlineData(Operand.SUBTRACT)]
    [InlineData(Operand.DIVIDE)]
    [InlineData(Operand.MINIMUM)]
    [InlineData(Operand.MAXIMUM)]
    [InlineData(Operand.EXPONENTIATE)]
    public void AnEmptyRegisterLeavesTheParameterAlone(Operand op)
    {
        Assert.Equal(Param, AbilitySystem.RegistryOp(float.NaN, Param, op));
    }

    [Fact]
    public void AnUnknownOperandLeavesTheParameterAlone()
    {
        Assert.Equal(Param, AbilitySystem.RegistryOp(Register, Param, (Operand)200));
    }
}
