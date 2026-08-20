using GameServer.Systems.Combat;
using Xunit;

namespace GameServer.Tests.Combat;

/// <summary>
///     Pins the splash curve InflictDamageCommand applies. Same caveat as the range decay curve: this says
///     the code does what we meant, not that the client agrees (D3 in Docs/In-Game-Tests/Damage-Loop.html).
/// </summary>
public class SplashFalloffTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    [InlineData(2f)]
    public void EverythingInsidePointBlankTakesTheWholeHit(float distance)
    {
        Assert.Equal(1f, SplashFalloff.Scale(distance, radius: 10f, pointBlankRange: 2f));
    }

    [Fact]
    public void DamageFallsLinearlyFromPointBlankToTheEdge()
    {
        // 2m of point blank then 8m to fall over, so 6m is a quarter of the way out
        Assert.Equal(0.75f, SplashFalloff.Scale(4f, radius: 10f, pointBlankRange: 2f), 3);
        Assert.Equal(0.5f, SplashFalloff.Scale(6f, radius: 10f, pointBlankRange: 2f), 3);
        Assert.Equal(0.25f, SplashFalloff.Scale(8f, radius: 10f, pointBlankRange: 2f), 3);
    }

    [Fact]
    public void NothingIsLeftAtTheEdge()
    {
        Assert.Equal(0f, SplashFalloff.Scale(10f, radius: 10f, pointBlankRange: 2f));
        Assert.Equal(0f, SplashFalloff.Scale(50f, radius: 10f, pointBlankRange: 2f));
    }

    [Fact]
    public void NoPointBlankRangeMeansTheFallStartsAtTheCentre()
    {
        Assert.Equal(1f, SplashFalloff.Scale(0f, radius: 10f, pointBlankRange: 0f));
        Assert.Equal(0.5f, SplashFalloff.Scale(5f, radius: 10f, pointBlankRange: 0f), 3);
    }

    /// <summary>
    ///     A point blank range that swallows the radius leaves no distance to interpolate over. Full damage
    ///     everywhere is the answer that doesn't divide by zero or go negative.
    /// </summary>
    [Theory]
    [InlineData(10f)]
    [InlineData(25f)]
    public void APointBlankRangeAtOrPastTheRadiusMeansFullDamage(float pointBlankRange)
    {
        Assert.Equal(1f, SplashFalloff.Scale(5f, radius: 10f, pointBlankRange: pointBlankRange));
        Assert.Equal(1f, SplashFalloff.Scale(9.9f, radius: 10f, pointBlankRange: pointBlankRange));
    }
}
