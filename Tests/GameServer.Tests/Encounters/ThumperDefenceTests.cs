using GameServer.Systems.Encounters.Encounters;
using Xunit;

namespace GameServer.Tests.Encounters;

/// <summary>
///     Pins the shape of the M7 defence multiplier: the factor a thumper's remaining health applies to
///     the sampled yield on a successful extraction. The curve itself is invented, half pay at zero
///     health up to full pay untouched, so what these tests protect is the contract the payout log
///     states, not a number recovered from retail.
/// </summary>
public class ThumperDefenceTests
{
    [Fact]
    public void AnUntouchedThumperPaysInFull()
    {
        Assert.Equal(1f, Thumper.DefenceMultiplier(4000, 4000));
    }

    [Fact]
    public void HalfHealthPaysThreeQuarters()
    {
        Assert.Equal(0.75f, Thumper.DefenceMultiplier(2000, 4000));
    }

    [Fact]
    public void TheFloorIsHalfPayEvenAtZeroHealth()
    {
        // Zero health with a success exit can't happen in game (zero health is the failure path), but
        // the floor is the documented bottom of the curve and division should behave there anyway.
        Assert.Equal(0.5f, Thumper.DefenceMultiplier(0, 4000));
    }

    [Fact]
    public void AnIndestructibleThumperIsNeverPenalised()
    {
        // MaxHealth 0 means the SDB gave the machine no pool, the same convention deployables use.
        Assert.Equal(1f, Thumper.DefenceMultiplier(0, 0));
    }

    [Fact]
    public void OverhealAndNegativesClampInsteadOfEscaping()
    {
        Assert.Equal(1f, Thumper.DefenceMultiplier(5000, 4000));
        Assert.Equal(0.5f, Thumper.DefenceMultiplier(-100, 4000));
    }
}
