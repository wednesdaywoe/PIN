using GameServer.Systems.Combat;
using Xunit;

namespace GameServer.Tests.Combat;

/// <summary>
///     Characters and deployables both replicate health as a percentage byte, and both go through
///     <see cref="Vitals.HealthPercent"/> so their health bars behave the same way.
/// </summary>
public class VitalsTests
{
    [Theory]
    [InlineData(100, 100, 100)]
    [InlineData(50, 100, 50)]
    [InlineData(1, 100, 1)]
    [InlineData(0, 100, 0)]
    [InlineData(333, 1000, 33)]
    public void ReportsThePercentageOfTheMaximum(int current, int max, byte expected)
    {
        Assert.Equal(expected, Vitals.HealthPercent(current, max));
    }

    /// <summary>
    ///     It truncates, so a target clinging on below 1% reads as 0 while still being alive. Health itself is
    ///     what decides alive or dead; this is only what the bar shows.
    /// </summary>
    [Fact]
    public void SomethingBarelyAliveReadsAsZero()
    {
        Assert.Equal(0, Vitals.HealthPercent(1, 1000));
    }

    /// <summary>
    ///     A pool with no maximum is the deployable that got no health from the SDB. It has to report zero
    ///     rather than divide by zero.
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, 0)]
    [InlineData(0, -10)]
    public void APoolWithNoMaximumReadsAsZero(int current, int max)
    {
        Assert.Equal(0, Vitals.HealthPercent(current, max));
    }

    [Fact]
    public void NegativeHealthReadsAsZeroRatherThanWrappingTheByte()
    {
        Assert.Equal(0, Vitals.HealthPercent(-50, 100));
    }
}
