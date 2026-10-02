using System.Numerics;
using Xunit;
using PRNGFuncs = GameServer.Systems.PRNG.PRNG;

namespace GameServer.Tests.Systems.PRNG;

public class PRNGTests
{
    // Sample captured from the client, same values as PRNG.TestSpread
    private const uint SampleTime = 32404587;
    private const float SampleSpreadPct = 3.025f;
    private const byte SampleSlotIndex = 2;
    private static readonly Vector3 _sampleAim = new(-0.9987106323242188f, -0.03268186002969742f, 0.03884787857532501f);
    private static readonly Vector3 _sampleExpectedDirection = new(-0.997563f, -0.057552f, 0.039456f);

    [Fact]
    public void Spread_MatchesClientSample()
    {
        var (forward, right, up) = AimBasis(_sampleAim);

        PRNGFuncs.Spread(SampleTime, SampleSlotIndex, 0, forward, right, up, SampleSpreadPct, Vector3.Zero, SampleTime, out Vector3 result);
        result = Vector3.Normalize(result);

        Assert.Equal(_sampleExpectedDirection.X, result.X, 0.0001f);
        Assert.Equal(_sampleExpectedDirection.Y, result.Y, 0.0001f);
        Assert.Equal(_sampleExpectedDirection.Z, result.Z, 0.0001f);
    }

    [Fact]
    public void Spread_BelowThreshold_ReturnsAimForward()
    {
        var (forward, right, up) = AimBasis(_sampleAim);

        PRNGFuncs.Spread(SampleTime, SampleSlotIndex, 0, forward, right, up, 0.0005f, Vector3.Zero, SampleTime, out Vector3 result);

        Assert.Equal(forward, result);
    }

    [Fact]
    public void Spread_DiffersPerRound()
    {
        var (forward, right, up) = AimBasis(_sampleAim);

        PRNGFuncs.Spread(SampleTime, SampleSlotIndex, 0, forward, right, up, SampleSpreadPct, Vector3.Zero, SampleTime, out Vector3 round0);
        PRNGFuncs.Spread(SampleTime, SampleSlotIndex, 1, forward, right, up, SampleSpreadPct, Vector3.Zero, SampleTime, out Vector3 round1);

        Assert.NotEqual(round0, round1);
    }

    [Fact]
    public void Trace_IsDeterministic()
    {
        Assert.Equal(PRNGFuncs.Trace(SampleTime, 0), PRNGFuncs.Trace(SampleTime, 0));
        Assert.NotEqual(PRNGFuncs.Trace(SampleTime, 0), PRNGFuncs.Trace(SampleTime, 1));
    }

    // Same basis WeaponSim builds before calling Spread
    private static (Vector3 Forward, Vector3 Right, Vector3 Up) AimBasis(Vector3 aim)
    {
        Vector3 right = Vector3.Normalize(Vector3.Cross(aim, Vector3.UnitZ));
        Vector3 up = Vector3.Normalize(Vector3.Cross(right, aim));
        return (aim, right, up);
    }
}
