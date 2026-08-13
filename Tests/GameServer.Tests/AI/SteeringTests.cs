using System.Numerics;
using GameServer.Systems.AI;
using Xunit;

namespace GameServer.Tests.AI;

/// <summary>
///     <see cref="Steering"/> is the whole of an NPC's locomotion that can be checked without a client,
///     and the vertical half of it is the part worth pinning: the server has no terrain, so an NPC's
///     height comes from where its target is standing rather than from the ground under its own feet,
///     and the rules that keep that from turning into a flying monster are all in here.
/// </summary>
public class SteeringTests
{
    private const float Tick = 0.05f;
    private const float Speed = 6f;

    [Fact]
    public void ItWalksTowardsTheDestinationAtItsOwnSpeed()
    {
        var moved = Steering.TryStep(Vector3.Zero, new Vector3(100f, 0f, 0f), stopWithin: 0f, Speed, Tick, out var next);

        Assert.True(moved);
        Assert.Equal(0.3f, next.X, 4);
        Assert.Equal(0f, next.Y, 4);
    }

    [Fact]
    public void ItStandsStillOnceItIsCloseEnough()
    {
        var moved = Steering.TryStep(Vector3.Zero, new Vector3(10f, 0f, 0f), stopWithin: 12f, Speed, Tick, out var next);

        Assert.False(moved);
        Assert.Equal(Vector3.Zero, next);
    }

    /// <summary>
    ///     The step is capped at what's left, so it lands on the stopping distance rather than passing it
    ///     and being pulled back. Overshoot would be a movement update every tick, forever.
    /// </summary>
    [Fact]
    public void TheLastStepStopsExactlyAtTheStoppingDistance()
    {
        var moved = Steering.TryStep(Vector3.Zero, new Vector3(12.1f, 0f, 0f), stopWithin: 12f, Speed, Tick, out var next);

        Assert.True(moved);
        Assert.Equal(12.1f - 12f, next.X, 4);

        var again = Steering.TryStep(next, new Vector3(12.1f, 0f, 0f), stopWithin: 12f, Speed, Tick, out _);
        Assert.False(again);
    }

    [Fact]
    public void MovingDiagonallyStillCoversItsSpeedAlongTheGround()
    {
        var moved = Steering.TryStep(Vector3.Zero, new Vector3(100f, 100f, 0f), stopWithin: 0f, Speed, Tick, out var next);

        Assert.True(moved);
        Assert.Equal(Speed * Tick, new Vector2(next.X, next.Y).Length(), 4);
    }

    /// <summary>
    ///     Height is spread over the whole walk rather than taken as fast as the slope limit allows, so
    ///     the NPC is on the line between where it is and where it's going the entire way instead of
    ///     climbing early and crossing the rest at the destination's height.
    /// </summary>
    [Fact]
    public void ItClimbsInProportionToTheGroundItHasCovered()
    {
        var from = Vector3.Zero;
        var destination = new Vector3(40f, 0f, 20f);

        Steering.TryStep(from, destination, stopWithin: 0f, Speed, Tick, out var next);

        // 0.3m of 40m walked, so 0.3/40 of the 20m climbed.
        Assert.Equal(0.3f, next.X, 4);
        Assert.Equal(20f * (0.3f / 40f), next.Z, 4);
    }

    [Fact]
    public void WalkingUpASlopeArrivesAtTheDestinationHeight()
    {
        var position = Vector3.Zero;
        var destination = new Vector3(40f, 0f, 20f);

        for (var i = 0; i < 1000 && Steering.TryStep(position, destination, stopWithin: 0f, Speed, Tick, out var next); i++)
        {
            position = next;
        }

        Assert.Equal(40f, position.X, 2);
        Assert.Equal(20f, position.Z, 2);
    }

    /// <summary>
    ///     A target 10m up and 3m away is standing on something, not up a hill. Reaching it would need a
    ///     slope of 3.3, so the NPC closes flat and ends up underneath it.
    /// </summary>
    [Fact]
    public void ItRefusesAClimbTooSteepToBeGround()
    {
        var from = new Vector3(0f, 0f, 100f);
        var destination = new Vector3(3f, 0f, 110f);

        var moved = Steering.TryStep(from, destination, stopWithin: 0f, Speed, Tick, out var next);

        Assert.True(moved);
        Assert.True(next.X > from.X);
        Assert.Equal(100f, next.Z, 4);
    }

    [Fact]
    public void ADropTooSteepToWalkDownIsRefusedTheSameWay()
    {
        var from = new Vector3(0f, 0f, 100f);
        var destination = new Vector3(3f, 0f, 90f);

        Steering.TryStep(from, destination, stopWithin: 0f, Speed, Tick, out var next);

        Assert.Equal(100f, next.Z, 4);
    }

    /// <summary>
    ///     The slope is measured over the ground still to be covered, not the ground to the target, so a
    ///     destination that was walkable from far away doesn't stay walkable once the NPC is on top of it.
    /// </summary>
    [Fact]
    public void TheSlopeIsMeasuredAgainstWhatIsLeftAfterTheStoppingDistance()
    {
        var from = Vector3.Zero;
        var destination = new Vector3(14f, 0f, 5f);

        // 5m over 14m of ground is walkable; 5m over the 2m left after standing off 12m is not.
        Steering.TryStep(from, destination, stopWithin: 0f, Speed, Tick, out var far);
        Steering.TryStep(from, destination, stopWithin: 12f, Speed, Tick, out var near);

        Assert.True(far.Z > 0f);
        Assert.Equal(0f, near.Z, 4);
    }

    [Fact]
    public void NothingHappensWithoutSpeedOrTime()
    {
        Assert.False(Steering.TryStep(Vector3.Zero, new Vector3(50f, 0f, 0f), stopWithin: 0f, speed: 0f, Tick, out _));
        Assert.False(Steering.TryStep(Vector3.Zero, new Vector3(50f, 0f, 0f), stopWithin: 0f, Speed, elapsedSeconds: 0f, out _));
    }

    [Fact]
    public void DistanceIsFlatSoAHeightDifferenceDoesNotCountAsBeingFurtherAway()
    {
        Assert.Equal(3f, Steering.FlatDistance(Vector3.Zero, new Vector3(3f, 0f, 40f)), 4);
    }
}
