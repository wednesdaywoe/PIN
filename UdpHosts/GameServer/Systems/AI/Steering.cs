using System;
using System.Numerics;

namespace GameServer.Systems.AI;

/// <summary>
///     One step of direct steering: where a character standing at one point ends up after moving toward
///     another for a slice of time. No pathing, no avoidance — it walks the straight line and will walk
///     into whatever is on it.
///
///     The vertical half is the awkward one, and it has two answers now. Given a
///     <see cref="GroundProbe"/> — which the server can supply since terrain loaded on 2026-08-16 — the
///     step lands on the ground measured under where it lands, which is the whole point of having
///     terrain at all. Without one, the old answer stands: the server knows where a player is standing,
///     and a player standing on the ground is a ground height measured at that spot, so the destination
///     carries a plausible Z and the walk climbs toward it.
///
///     The fallback is not dead code. A probe returns false over a hole in the collision and everywhere
///     outside the loaded chunks, and every offline test runs without one.
/// </summary>
public static class Steering
{
    /// <summary>
    ///     Steepest ground an NPC will follow, as a rise over run — 1.0 is 45°. What it is measured over
    ///     depends on which answer the vertical half is using, and the difference matters.
    ///
    ///     <b>Without a ground probe it is measured over the whole remaining walk</b>, and that is what
    ///     stops an NPC from levitating: a target on a roof 10m up and 3m away needs a slope of 3.3 to
    ///     reach, so the NPC keeps its own height and walks to the wall below. A target 20m up a hill 40m
    ///     off needs 0.5, so that one it follows. Measuring it per tick instead is the trap, and the first
    ///     cut of this fell in it: a 45° climb allowed every tick lets an NPC gain the 10m over the first
    ///     10m of a 40m approach and cross the remaining 30m at roof height, which is the exact behaviour
    ///     the limit exists to prevent.
    ///
    ///     <b>With a probe it is measured over the step</b>, and per-step is now correct rather than a
    ///     trap, because the height being tested is the real ground at the real place rather than a guess
    ///     at the far end of the walk. Ground that rises faster than this over one step is a wall, and the
    ///     NPC holds its position instead of climbing it.
    /// </summary>
    public const float MaxSlope = 1f;

    /// <summary>
    ///     A millimetre of slack on the slope limit, and it is not cosmetic. Ground at exactly
    ///     <see cref="MaxSlope"/> is walkable by definition, but a step of 0.3m up a 45° hill computes a
    ///     rise of 0.30000004 against a limit of 0.3, so without this the limiting case is decided by
    ///     float error and a creature refuses the hill it is supposed to be able to climb.
    /// </summary>
    private const float SlopeTolerance = 1e-3f;

    /// <summary>
    ///     Answers "how high is the world here", or false where nothing is. Supplied by the shard as
    ///     <c>PhysicsEngine.TryGetGroundHeight</c>; null everywhere the answer is unavailable, including
    ///     every offline test.
    /// </summary>
    public delegate bool GroundProbe(Vector3 at, out float groundZ);

    /// <summary>
    ///     Advances <paramref name="from"/> toward <paramref name="destination"/> and reports whether it
    ///     moved at all. Every distance in here is flat, including <paramref name="stopWithin"/>: an NPC
    ///     on a slope should cover ground at its stated speed rather than be slowed by the climb. That
    ///     makes it the one place in the AI measured differently from <see cref="Shot.Separation"/>,
    ///     which the range checks use and which is a plain 3D distance. The two only disagree when a
    ///     target is well above or below, and <see cref="MaxSlope"/> is what keeps that from getting far.
    ///
    ///     <paramref name="ground"/> is the difference between standing on the hill and walking up beside
    ///     it. Pass one wherever the shard is in reach; pass null and the pre-terrain behaviour is
    ///     unchanged.
    /// </summary>
    public static bool TryStep(Vector3 from, Vector3 destination, float stopWithin, float speed, float elapsedSeconds, out Vector3 next, GroundProbe ground = null)
    {
        next = from;

        if (speed <= 0f || elapsedSeconds <= 0f)
        {
            return false;
        }

        var toGo = destination - from;
        var flat = new Vector2(toGo.X, toGo.Y);
        var distance = flat.Length();
        if (distance <= stopWithin)
        {
            return false;
        }

        // Ground left to cover. The step is capped at it, so arriving is exact rather than an overshoot
        // corrected back the next tick — an NPC oscillating a few centimetres around its stopping
        // distance would push a movement update every tick forever.
        var run = distance - stopWithin;
        var step = MathF.Min(speed * elapsedSeconds, run);
        var direction = flat / distance;

        var landing = new Vector3(
            from.X + (direction.X * step),
            from.Y + (direction.Y * step),
            from.Z);

        if (ground != null && ground(landing, out var groundZ))
        {
            // The rise is measured between two pieces of ground, never against the height the creature
            // happens to be carrying. That distinction is the whole correctness of this branch: a wave
            // member spawns inside a hillside and a chase from before terrain leaves one in the air, and
            // measuring from its own Z would read both as a wall and freeze it there permanently. Falling
            // back to its own Z is only for the case where the ground under it is unknown.
            var standing = ground(from, out var fromGroundZ) ? fromGroundZ : from.Z;

            // Only the climb is capped, and only over the step. Ground rising faster than MaxSlope across
            // one step is a wall, and walking into a wall means holding position rather than mounting it.
            // A drop is never refused — a creature that walks off a ledge belongs at the bottom of it, and
            // refusing would leave it hovering over the edge, which is the bug this branch exists to end.
            if (groundZ - standing > (step * MaxSlope) + SlopeTolerance)
            {
                return false;
            }

            next = landing with { Z = groundZ };
            return true;
        }

        // No ground to stand on, so back to borrowing the destination's height. Spread the whole climb
        // evenly over the whole walk, so the NPC arrives at the destination's height exactly as it
        // arrives at its distance and is never higher than the line between the two. A rise that would
        // need steeper ground than MaxSlope isn't walked at all: it keeps the height it has and closes
        // flat.
        var climb = MathF.Abs(toGo.Z) <= run * MaxSlope ? toGo.Z * (step / run) : 0f;

        next = landing with { Z = from.Z + climb };
        return true;
    }

    /// <summary>Flat distance between two points, which is what every range in the AI is measured in.</summary>
    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        return new Vector2(a.X - b.X, a.Y - b.Y).Length();
    }
}
