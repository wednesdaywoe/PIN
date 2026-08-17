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

    /// <summary>
    ///     Everything above is the pre-terrain answer, where height is borrowed from the destination
    ///     because there was nothing to measure. Everything below is what a ground probe changes, and the
    ///     case that matters is the one a tester found on 2026-08-17: a creature chasing a player uphill
    ///     climbed <i>alongside</i> the slope, in the air, because the height it was given belonged to
    ///     ground further up than the ground it was standing on.
    /// </summary>
    [Fact]
    public void WithAProbeItStandsOnTheGroundUnderItRatherThanOnTheWayToTheTarget()
    {
        // A hill rising at 45 degrees, and a target 40m along it and 40m up.
        Steering.GroundProbe hill = (Vector3 at, out float z) =>
        {
            z = at.X;
            return true;
        };

        var position = Vector3.Zero;
        for (var i = 0; i < 200; i++)
        {
            if (!Steering.TryStep(position, new Vector3(40f, 0f, 40f), stopWithin: 0f, Speed, Tick, out var next, hill))
            {
                break;
            }

            // Every step of the way, not only at the end: the height is the hill's, at the position
            // reached, rather than a fraction of the climb still to come.
            Assert.Equal(next.X, next.Z, 3);
            position = next;
        }

        Assert.True(position.X > 5f);
    }

    /// <summary>
    ///     Ground rising faster than <see cref="Steering.MaxSlope"/> across one step is a wall rather than
    ///     a hill. Per-step is the right measure here and a trap without a probe — see the remark on
    ///     <see cref="Steering.MaxSlope"/>.
    /// </summary>
    [Fact]
    public void WithAProbeAWallIsNotClimbed()
    {
        Steering.GroundProbe cliff = (Vector3 at, out float z) =>
        {
            z = at.X > 1f ? 50f : 0f;
            return true;
        };

        var position = new Vector3(0.9f, 0f, 0f);
        var moved = Steering.TryStep(position, new Vector3(20f, 0f, 50f), stopWithin: 0f, Speed, Tick, out var next, cliff);

        Assert.False(moved);
        Assert.Equal(position, next);
    }

    /// <summary>
    ///     A drop is taken in full and never refused. Refusing one leaves the creature hovering over the
    ///     edge it walked to, which is the failure this whole change exists to remove.
    /// </summary>
    [Fact]
    public void WithAProbeItWalksOffALedgeRatherThanHoveringOverIt()
    {
        Steering.GroundProbe ledge = (Vector3 at, out float z) =>
        {
            z = at.X > 1f ? -20f : 0f;
            return true;
        };

        var moved = Steering.TryStep(new Vector3(0.9f, 0f, 0f), new Vector3(20f, 0f, -20f), stopWithin: 0f, Speed, Tick, out var next, ledge);

        Assert.True(moved);
        Assert.Equal(-20f, next.Z, 4);
    }

    /// <summary>
    ///     A probe answers false over a hole in the collision and everywhere outside the loaded chunks,
    ///     and the old behaviour has to still be there underneath when it does.
    /// </summary>
    [Fact]
    public void AProbeThatKnowsNothingLeavesTheBorrowedHeightAlone()
    {
        Steering.GroundProbe nothing = (Vector3 at, out float z) =>
        {
            z = 0f;
            return false;
        };

        Steering.TryStep(Vector3.Zero, new Vector3(40f, 0f, 20f), stopWithin: 0f, Speed, Tick, out var probed, nothing);
        Steering.TryStep(Vector3.Zero, new Vector3(40f, 0f, 20f), stopWithin: 0f, Speed, Tick, out var unprobed);

        Assert.Equal(unprobed, probed);
        Assert.Equal(20f * (0.3f / 40f), probed.Z, 4);
    }

    /// <summary>
    ///     The horizontal half is untouched by the probe. Worth pinning because the flat step is what
    ///     every range and stopping distance in the AI is measured in.
    /// </summary>
    [Fact]
    public void AProbeDoesNotChangeHowFarAStepCovers()
    {
        Steering.GroundProbe undulating = (Vector3 at, out float z) =>
        {
            z = 3f;
            return true;
        };

        Steering.TryStep(Vector3.Zero, new Vector3(100f, 100f, 0f), stopWithin: 0f, Speed, Tick, out var next, undulating);

        Assert.Equal(Speed * Tick, new Vector2(next.X, next.Y).Length(), 4);
        Assert.Equal(3f, next.Z, 4);
    }
}
