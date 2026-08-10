using GameServer.Systems.Combat;
using Xunit;

namespace GameServer.Tests.Combat;

/// <summary>
///     <see cref="ShieldSim"/> ticks every 100ms, so any recharge rate under 10 a second is worth less than
///     a whole point per tick. These cover the carry that stops those rates truncating away to nothing.
/// </summary>
public class ShieldRechargeTests
{
    private const float TickSeconds = 0.1f;

    [Fact]
    public void ARateBelowOnePointPerTickStillRecharges()
    {
        var recharge = new ShieldRecharge();
        var total = 0;

        // 5/sec over ten 100ms ticks is half a point each time
        for (var tick = 0; tick < 10; tick++)
        {
            total += recharge.Accumulate(5, TickSeconds);
        }

        Assert.Equal(5, total);
    }

    [Fact]
    public void TheFirstTicksOfASlowRateReturnNothingUntilTheFractionAddsUp()
    {
        var recharge = new ShieldRecharge();

        Assert.Equal(0, recharge.Accumulate(5, TickSeconds));
        Assert.Equal(1, recharge.Accumulate(5, TickSeconds));
        Assert.Equal(0, recharge.Accumulate(5, TickSeconds));
        Assert.Equal(1, recharge.Accumulate(5, TickSeconds));
    }

    [Fact]
    public void AWholeSecondAtAnyRateIsWorthThatRate()
    {
        var recharge = new ShieldRecharge();

        Assert.Equal(42, recharge.Accumulate(42, 1f));
    }

    [Fact]
    public void ResetDropsTheCarriedFraction()
    {
        var recharge = new ShieldRecharge();

        recharge.Accumulate(5, TickSeconds);
        recharge.Reset();

        // Without the reset this tick would have completed the point started before it
        Assert.Equal(0, recharge.Accumulate(5, TickSeconds));
    }

    [Fact]
    public void AZeroRateNeverProducesAPoint()
    {
        var recharge = new ShieldRecharge();

        for (var tick = 0; tick < 100; tick++)
        {
            Assert.Equal(0, recharge.Accumulate(0, TickSeconds));
        }
    }
}
