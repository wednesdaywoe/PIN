using System;
using System.Numerics;

namespace GameServer.Systems.AI;

/// <summary>
///     One step of direct steering: where a character standing at one point ends up after moving toward
///     another for a slice of time. No pathing, no avoidance — it walks the straight line and will walk
///     into whatever is on it.
///
///     The vertical half is the awkward one. The server has no terrain at all: <c>LoadMapsCollision</c>
///     is off and <c>MapsPath</c> is empty, so a downward raycast — the obvious way to sit an NPC on the
///     ground, and what the milestone doc assumed — hits nothing. What the server does know is where a
///     player is standing, because the client sends it, and a player standing on the ground is a ground
///     height measured at that spot. So a destination carries a plausible Z and the walk climbs toward
///     it, limited to <see cref="MaxSlope"/> so the NPC can only follow ground a creature could walk up.
/// </summary>
public static class Steering
{
    /// <summary>
    ///     Steepest ground an NPC will follow, as a rise over run — 1.0 is 45°, measured over the whole
    ///     remaining walk rather than one tick of it. This is what stops an NPC from levitating: a target
    ///     on a roof 10m up and 3m away needs a slope of 3.3 to reach, so the NPC keeps its own height
    ///     and walks to the wall below. A target 20m up a hill 40m off needs 0.5, so that one it follows.
    ///
    ///     Measuring it per tick instead is the trap, and the first cut of this fell in it: a 45° climb
    ///     allowed every tick lets an NPC gain the 10m over the first 10m of a 40m approach and cross the
    ///     remaining 30m at roof height, which is the exact behaviour the limit exists to prevent.
    /// </summary>
    public const float MaxSlope = 1f;

    /// <summary>
    ///     Advances <paramref name="from"/> toward <paramref name="destination"/> and reports whether it
    ///     moved at all. Every distance in here is flat, including <paramref name="stopWithin"/>: an NPC
    ///     on a slope should cover ground at its stated speed rather than be slowed by the climb. That
    ///     makes it the one place in the AI measured differently from <see cref="Shot.Separation"/>,
    ///     which the range checks use and which is a plain 3D distance. The two only disagree when a
    ///     target is well above or below, and <see cref="MaxSlope"/> is what keeps that from getting far.
    /// </summary>
    public static bool TryStep(Vector3 from, Vector3 destination, float stopWithin, float speed, float elapsedSeconds, out Vector3 next)
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

        // Spread the whole climb evenly over the whole walk, so the NPC arrives at the destination's
        // height exactly as it arrives at its distance and is never higher than the line between the
        // two. A rise that would need steeper ground than MaxSlope isn't walked at all: it keeps the
        // height it has and closes flat.
        var climb = MathF.Abs(toGo.Z) <= run * MaxSlope ? toGo.Z * (step / run) : 0f;

        next = new Vector3(
            from.X + (direction.X * step),
            from.Y + (direction.Y * step),
            from.Z + climb);
        return true;
    }

    /// <summary>Flat distance between two points, which is what every range in the AI is measured in.</summary>
    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        return new Vector2(a.X - b.X, a.Y - b.Y).Length();
    }
}
