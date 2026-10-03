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
            position = NpcKnockback.Step(position, state, 0.05f, FlatGround);
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
            position = NpcKnockback.Step(position, state, 0.05f, NoGround);
        }

        Assert.Equal(40f, position.Z);
        Assert.True(position.X > 2f);
    }
}
