using GameServer.StaticDB;
using Xunit;
using WeaponSimClass = GameServer.Systems.WeaponSim.WeaponSim;
using WeaponSimState = GameServer.Systems.WeaponSim.WeaponSim.WeaponSimState;

namespace GameServer.Tests.Systems.WeaponSim;

public class SpreadTests
{
    [Theory]
    [InlineData(0u, 1f)]
    [InlineData(500u, 2f)]
    [InlineData(1000u, 3f)]
    public void Spread_RampsFromMinToMax(uint accumulatedSpreadTime, float expected)
    {
        var weapon = Weapon(minSpread: 1, maxSpread: 3, spreadPerBurst: 0.5f, spreadRampTime: 1000);

        Assert.Equal(expected, SpreadPct(weapon, accumulatedSpreadTime), 0.0001f);
    }

    [Theory]
    [InlineData(0u, 3f)]
    [InlineData(500u, 2f)]
    [InlineData(1000u, 1f)]
    public void InverseSpread_RampsFromMaxToMin(uint accumulatedSpreadTime, float expected)
    {
        // Negative SpreadPerBurst, e.g. HMG tightening up while firing
        var weapon = Weapon(minSpread: 1, maxSpread: 3, spreadPerBurst: -0.5f, spreadRampTime: 1000);

        Assert.Equal(expected, SpreadPct(weapon, accumulatedSpreadTime), 0.0001f);
    }

    [Fact]
    public void Spread_WithoutSpreadPerBurst_StaysAtMin()
    {
        var weapon = Weapon(minSpread: 1, maxSpread: 3, spreadPerBurst: 0, spreadRampTime: 1000);

        Assert.Equal(1f, SpreadPct(weapon, 1000), 0.0001f);
    }

    [Fact]
    public void Spread_MinAboveMax_ClampsToMax()
    {
        // BioCrossbow / AR ADS have minSpread > maxSpread
        var weapon = Weapon(minSpread: 5, maxSpread: 2, spreadPerBurst: 0, spreadRampTime: 0);

        Assert.Equal(2f, SpreadPct(weapon, 0), 0.0001f);
    }

    [Fact]
    public void Spread_AppliesFactorThenMovementBonus()
    {
        var weapon = Weapon(minSpread: 1, maxSpread: 3, spreadPerBurst: 0.5f, spreadRampTime: 1000);
        var state = new WeaponSimState { AccumulatedSpreadTime = 500, CurrentMovementSpreadBonus = 0.25f };

        float result = WeaponSimClass.GetCurrentSpreadPct(null, weapon, state, 2f, 0);

        Assert.Equal((2f * 2f) + 0.25f, result, 0.0001f);
    }

    private static float SpreadPct(WeaponTemplateResult weapon, uint accumulatedSpreadTime)
    {
        var state = new WeaponSimState { AccumulatedSpreadTime = accumulatedSpreadTime };
        return WeaponSimClass.GetCurrentSpreadPct(null, weapon, state, 1f, 0);
    }

    private static WeaponTemplateResult Weapon(float minSpread, float maxSpread, float spreadPerBurst, uint spreadRampTime)
    {
        return new WeaponTemplateResult
        {
            MinSpread = minSpread,
            MaxSpread = maxSpread,
            SpreadPerBurst = spreadPerBurst,
            SpreadRampTime = spreadRampTime,
        };
    }
}
