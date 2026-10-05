using System.Numerics;
using GameServer.Systems.AI;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     Whether world geometry stands between an area query's centre and a target. The client's
///     TargetPBAE and TargetConeAE pass IgnoreWalls to their spatial query as flag bit 2 (constructor
///     0xbb8520), so with it clear a target behind a wall isn't picked; 1,999 of 2,207 PBAE defs and
///     610 of 622 cone defs leave it clear.
/// </summary>
internal static class WallCheck
{
    // The server knows where a target stands, not its hitbox, so a few points up the body stand in for
    // it. Any one of them in sight is enough, as a wall that hides the legs still leaves the head.
    private static readonly float[] BodyHeights = [0.3f, 0.9f, 1.6f];

    /// <summary>
    ///     How far a query centre that sits on the ground, such as a projectile's landing point or a
    ///     character's feet, is lifted before casting, so the ray doesn't start inside the terrain.
    /// </summary>
    internal const float GroundLift = 0.5f;

    internal static bool InSight(Vector3 from, Vector3 targetFeet, NpcKnockback.WallProbe hitWorld)
    {
        foreach (var height in BodyHeights)
        {
            var to = targetFeet + new Vector3(0f, 0f, height);
            var delta = to - from;
            var distance = delta.Length();
            if (distance < 1e-3f)
            {
                return true;
            }

            if (!hitWorld(from, delta / distance, distance, out _))
            {
                return true;
            }
        }

        return false;
    }
}
