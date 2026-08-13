using System;
using System.Numerics;

namespace GameServer.Systems.Spawning;

/// <summary>
///     Turns a group's anchor and radius into one standing position per member.
///     Pure, so a group's layout can be checked without a shard.
/// </summary>
public static class SpawnScatter
{
    /// <summary>
    ///     ~137.5°, the angle between successive seeds in a sunflower head. Any fixed angle that isn't a
    ///     neat fraction of a turn would do; this one is the standard choice because no two members ever
    ///     line up on the same bearing.
    /// </summary>
    private const float GoldenAngleRadians = 2.39996323f;

    /// <summary>
    ///     Where member <paramref name="slotIndex"/> of <paramref name="slotCount"/> stands.
    ///     Deterministic, so a group lays out the same way every run and a position in the log can be
    ///     matched against one on screen.
    /// </summary>
    /// <remarks>
    ///     Z comes straight off the anchor and is never adjusted. <c>LoadMapsCollision</c> is off and
    ///     <c>MapsPath</c> is empty, so there's no terrain to raycast down onto, and the anchor's height
    ///     is only trustworthy because it was copied off a real object in the zone. Moving a member in Z
    ///     would be inventing a height. Moving it in X and Y assumes the ground is flat over the radius,
    ///     which is why the radii in <c>spawn_group.json</c> are small.
    /// </remarks>
    public static Vector3 Placement(Vector3 anchor, float radius, int slotIndex, int slotCount)
    {
        if (slotCount <= 1 || radius <= 0f || slotIndex < 0)
        {
            return anchor;
        }

        // sqrt spreads members evenly over the disc instead of bunching them in the middle, which is
        // what a plain linear ramp does: the area of a ring grows with its radius, so the fraction of
        // members inside distance d has to grow with d squared.
        var distance = radius * MathF.Sqrt((slotIndex + 0.5f) / slotCount);
        var bearing = slotIndex * GoldenAngleRadians;

        return new Vector3(
            anchor.X + (distance * MathF.Cos(bearing)),
            anchor.Y + (distance * MathF.Sin(bearing)),
            anchor.Z);
    }
}
