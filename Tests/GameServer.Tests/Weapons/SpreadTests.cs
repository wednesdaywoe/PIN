using System.Numerics;
using GameServer.Systems.PRNG;
using Xunit;

namespace GameServer.Tests.Weapons;

/// <summary>
///     <see cref="PRNG.Spread"/> is a reimplementation of the client's deterministic spread RNG. Because both
///     sides run it from the same seed, the server can resolve hits against the same scattered direction the
///     player saw, which only works while our version reproduces theirs exactly.
/// </summary>
public class SpreadTests
{
    /// <summary>
    ///     A single shot captured from the client: these inputs produced this direction in game. The values
    ///     were carried in a throwaway PRNG.TestSpread method that printed them and threw; this is the same
    ///     comparison, made to fail on its own.
    /// </summary>
    [Fact]
    public void ReproducesACapturedClientShot()
    {
        const uint Time = 32404587;
        const float SpreadPct = 3.025f;
        const byte SlotIndex = 2;
        const byte Round = 0;

        var aim = new Vector3(-0.9987106323242188f, -0.03268186002969742f, 0.03884787857532501f);
        var expected = new Vector3(-0.997563f, -0.057552f, 0.039456f);

        var result = Scatter(aim, Time, SlotIndex, Round, SpreadPct);

        AssertDirection(expected, result, tolerance: 1e-5f);
    }

    [Fact]
    public void NoSpreadFiresStraightDownTheAim()
    {
        var aim = Vector3.Normalize(new Vector3(1f, 0.2f, 0f));

        PRNG.Spread(1000, 0, 0, aim, Vector3.UnitY, Vector3.UnitZ, 0f, Vector3.Zero, 1000, out var result);

        Assert.Equal(aim, result);
    }

    /// <summary>
    ///     Both sides seed from the client's fire time, weapon slot and round index, so the same shot has to
    ///     scatter the same way every time it's resolved.
    /// </summary>
    [Fact]
    public void TheSameShotAlwaysScattersTheSameWay()
    {
        var aim = Vector3.Normalize(new Vector3(1f, 0f, 0f));

        var first = Scatter(aim, 12345, 1, 3, 5f);
        var second = Scatter(aim, 12345, 1, 3, 5f);

        Assert.Equal(first, second);
    }

    [Fact]
    public void EachRoundOfABurstScattersDifferently()
    {
        var aim = Vector3.Normalize(new Vector3(1f, 0f, 0f));

        var first = Scatter(aim, 12345, 1, 0, 5f);
        var second = Scatter(aim, 12345, 1, 1, 5f);
        var third = Scatter(aim, 12345, 1, 2, 5f);

        Assert.NotEqual(first, second);
        Assert.NotEqual(second, third);
        Assert.NotEqual(first, third);
    }

    /// <summary>
    ///     Spread is a percentage, so a wider one has to scatter further off the aim than a tighter one.
    /// </summary>
    [Fact]
    public void AWiderSpreadLandsFurtherFromTheAim()
    {
        var aim = Vector3.Normalize(new Vector3(1f, 0f, 0f));

        var tight = Vector3.Distance(aim, Scatter(aim, 999, 0, 0, 1f));
        var wide = Vector3.Distance(aim, Scatter(aim, 999, 0, 0, 20f));

        Assert.True(wide > tight, $"a 20% spread scattered {wide} from the aim, a 1% spread {tight}");
    }

    /// <summary>
    ///     Scatters <paramref name="aim"/> the way WeaponSim does, building the same basis and treating the
    ///     shot as the first of a burst.
    /// </summary>
    private static Vector3 Scatter(Vector3 aim, uint time, byte slotIndex, byte round, float spreadPct)
    {
        var right = Vector3.Normalize(Vector3.Cross(aim, Vector3.UnitZ));
        var up = Vector3.Normalize(Vector3.Cross(right, aim));

        PRNG.Spread(time, slotIndex, round, aim, right, up, spreadPct, Vector3.Zero, time, out var result);

        return Vector3.Normalize(result);
    }

    private static void AssertDirection(Vector3 expected, Vector3 actual, float tolerance)
    {
        Assert.True(
            Vector3.Distance(expected, actual) <= tolerance,
            $"expected ({expected.X}, {expected.Y}, {expected.Z}) but got ({actual.X}, {actual.Y}, {actual.Z})");
    }
}
