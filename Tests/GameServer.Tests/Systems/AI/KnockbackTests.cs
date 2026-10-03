using System.Numerics;
using GameServer.Systems.AI;
using GameServer.Systems.Aptitude.Commands.Impact;
using Xunit;

namespace GameServer.Tests.Systems.AI;

public class KnockbackTests
{
    private static bool FlatGround(Vector3 at, out float groundZ)
    {
        groundZ = 0f;
        return true;
    }

    private static bool NoGround(Vector3 at, out float groundZ)
    {
        groundZ = at.Z;
        return false;
    }

    private static bool NoWall(Vector3 origin, Vector3 direction, float maxDistance, out float distance)
    {
        distance = maxDistance;
        return false;
    }

    // A wall across the X axis at X = 3, rising 10 m out of flat ground at Z = 0; its top is a ledge at Z = 10
    private static bool WallAtThree(Vector3 origin, Vector3 direction, float maxDistance, out float distance)
    {
        distance = maxDistance;
        if (direction.X <= 0f || origin.X >= 3f || origin.Z > 10f)
        {
            return false;
        }

        distance = (3f - origin.X) / direction.X;
        return distance < maxDistance;
    }

    private static bool GroundBesideWall(Vector3 at, out float groundZ)
    {
        // Ground below the probe's start only: what a short probe beside the wall sees
        groundZ = at.X >= 3f && at.Z + NpcKnockback.GroundHeadroom >= 10f ? 10f : 0f;
        return true;
    }

    private static Vector3 Fly(Vector3 start, Vector3 velocity, NpcKnockback.GroundProbe ground, NpcKnockback.WallProbe wall)
    {
        var state = new AIState { KnockbackVelocity = velocity, KnockbackLaunchZ = start.Z, KnockbackSecondsLeft = NpcKnockback.MaxFlightSeconds };
        var position = start;
        while (state.KnockedBack)
        {
            position = NpcKnockback.Step(position, state, 0.05f, ground, wall);
        }

        return position;
    }

    [Fact]
    public void ThrownAtAWall_StopsAgainstItAndDropsToTheGroundBelow()
    {
        var landed = Fly(Vector3.Zero, new Vector3(12f, 0f, 7f), GroundBesideWall, WallAtThree);

        Assert.InRange(landed.X, 2f, 2.5f + 0.01f);
        Assert.Equal(0f, landed.Z);
    }

    [Fact]
    public void ThrownHighEnough_ClearsTheWallAndLandsOnTop()
    {
        // The probes are at knee and chest height, so a throw that rises above the ledge before it gets there goes over
        var landed = Fly(new Vector3(2f, 0f, 0f), new Vector3(1f, 0f, 30f), GroundBesideWall, WallAtThree);

        Assert.True(landed.X > 3f);
        Assert.Equal(10f, landed.Z);
    }

    [Fact]
    public void WithoutGround_NeverLiftedToLaunchHeight()
    {
        var state = new AIState { KnockbackVelocity = new Vector3(0f, 0f, -1f), KnockbackLaunchZ = 40f, KnockbackSecondsLeft = 1f };

        var next = NpcKnockback.Step(new Vector3(0f, 0f, 30f), state, 0.05f, NoGround, NoWall);

        Assert.True(next.Z <= 30f);
    }

    [Fact]
    public void Launch_PointsAwayAndTipsUpByLoft()
    {
        var velocity = ForcePushCommand.Launch(Vector3.Zero, new Vector3(0f, 5f, 0f), 12f, 1f);

        Assert.Equal(12f, velocity.Length(), 0.001f);
        Assert.Equal(0f, velocity.X, 0.001f);
        Assert.Equal(velocity.Y, velocity.Z, 0.001f);
    }

    [Fact]
    public void Launch_IgnoresHeightDifference()
    {
        // A target on a slope below is still pushed level, not driven into the ground
        var velocity = ForcePushCommand.Launch(new Vector3(0f, 0f, 3f), new Vector3(5f, 0f, 0f), 10f, 0f);

        Assert.Equal(new Vector3(10f, 0f, 0f), velocity);
    }

    [Fact]
    public void Launch_StraightUpWhenStandingOnTheSource()
    {
        var velocity = ForcePushCommand.Launch(Vector3.Zero, Vector3.Zero, 10f, 0.5f);

        Assert.Equal(new Vector3(0f, 0f, 10f), velocity);
    }

    [Fact]
    public void HealingWavePush_LandsAboutSixMetresAway()
    {
        var state = new AIState { KnockbackVelocity = ForcePushCommand.Launch(Vector3.Zero, Vector3.UnitY, 12f, 0.6f), KnockbackSecondsLeft = NpcKnockback.MaxFlightSeconds };
        var position = Vector3.Zero;
        var ticks = 0;

        while (state.KnockedBack && ticks < 100)
        {
            position = NpcKnockback.Step(position, state, 0.05f, FlatGround, NoWall);
            ticks++;
        }

        Assert.False(state.KnockedBack);
        Assert.Equal(0f, position.Z);
        Assert.InRange(position.Y, 5f, 7.5f);
        Assert.InRange(ticks, 10, 16);
    }

    [Fact]
    public void WithoutGround_LandsAtLaunchHeight()
    {
        var state = new AIState { KnockbackVelocity = new Vector3(5f, 0f, 5f), KnockbackLaunchZ = 40f, KnockbackSecondsLeft = NpcKnockback.MaxFlightSeconds };
        var position = new Vector3(0f, 0f, 40f);

        while (state.KnockedBack)
        {
            position = NpcKnockback.Step(position, state, 0.05f, NoGround, NoWall);
        }

        Assert.Equal(40f, position.Z);
        Assert.True(position.X > 2f);
    }
}
