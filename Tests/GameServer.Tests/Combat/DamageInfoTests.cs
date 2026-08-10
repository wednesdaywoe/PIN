using AeroMessages.GSS.V66;
using GameServer.Systems.Combat;
using Xunit;

namespace GameServer.Tests.Combat;

/// <summary>
///     Damage travels as a float and lands as whole points. Every <see cref="IDamageable"/> rounds through
///     <see cref="DamageInfo.Points"/> so a hit worth 0.4 is ignored by all of them or by none of them.
/// </summary>
public class DamageInfoTests
{
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.4f, 0)]
    [InlineData(0.5f, 0)]  // MathF.Round is banker's rounding: 0.5 goes to the even 0
    [InlineData(0.6f, 1)]
    [InlineData(1.5f, 2)]
    [InlineData(2.5f, 2)]
    [InlineData(99.9f, 100)]
    public void PointsRoundsTheAmount(float amount, int expected)
    {
        Assert.Equal(expected, new DamageInfo { Amount = amount }.Points);
    }

    /// <summary>
    ///     Falloff and mitigation can drive an amount below half a point. Everything that takes damage treats
    ///     that as a miss rather than as a free hit.
    /// </summary>
    [Fact]
    public void AnAmountThatRoundsAwayIsNothing()
    {
        Assert.Equal(0, new DamageInfo { Amount = 0.001f }.Points);
    }

    [Fact]
    public void TheRestOfTheHitIsCarriedAsGiven()
    {
        var damage = new DamageInfo
        {
            Amount = 12f,
            DamageType = 7,
            Flags = DamageResponseFlags.Critical,
        };

        Assert.Equal(12, damage.Points);
        Assert.Equal(7, damage.DamageType);
        Assert.Equal(DamageResponseFlags.Critical, damage.Flags);
        Assert.Null(damage.Attacker);
    }
}
