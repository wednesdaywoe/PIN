using System.Numerics;
using GameServer.Entities.Character;

namespace GameServer.Systems.AI;

/// <summary>
///     A resolved line from a shooter's muzzle to a target's chest. <paramref name="Range"/> is the
///     length of that line and is what a raycast should be capped at; <paramref name="Separation"/> is
///     the plain distance between the two characters and is what range checks should read, so a weapon's
///     reach doesn't quietly change with the height of the muzzle.
/// </summary>
public readonly record struct Shot(Vector3 Origin, Vector3 Direction, float Range, float Separation);

/// <summary>
///     Where an NPC has to aim to hit a target, and whether anything is standing in the way.
///     Target selection and the attack pass both need this and have to agree on it: selection
///     deliberately keeps hold of a target through a moment of broken line of sight, so the shot
///     is the one that has to look again before it fires.
/// </summary>
public static class Sightline
{
    /// <summary>
    ///     How far above a character's origin to aim. Characters are positioned at their feet, so
    ///     firing at <see cref="CharacterEntity.Position"/> puts every round into the ground. Chest
    ///     height on a human-sized frame; <c>dbmonster</c> carries BodyHeight but nothing reads it yet.
    /// </summary>
    public const float AimHeight = 1.5f;

    /// <summary>
    ///     Works out the shot from <paramref name="shooter"/> to <paramref name="target"/>. False when
    ///     the two are close enough to be degenerate, which is the one case where there's no direction
    ///     to normalise.
    /// </summary>
    public static bool TrySolve(CharacterEntity shooter, CharacterEntity target, out Shot shot)
    {
        shot = default;

        // The muzzle sits off to one side, so the origin depends on which way the shooter is looking,
        // which depends on where the target is. Aim roughly first, then solve from the real muzzle.
        var coarse = target.Position - shooter.Position;
        var separation = coarse.Length();
        if (separation <= 0f)
        {
            return false;
        }

        var origin = shooter.GetProjectileOrigin(coarse / separation);
        var toAimPoint = target.Position + new Vector3(0f, 0f, AimHeight) - origin;
        var range = toAimPoint.Length();
        if (range <= 0f)
        {
            return false;
        }

        shot = new Shot(origin, toAimPoint / range, range, separation);
        return true;
    }

    /// <summary>
    ///     Whether <paramref name="shot"/> reaches <paramref name="target"/> without hitting something
    ///     else first. The shooter's own body is excluded by <c>TargetRayCast</c>.
    /// </summary>
    public static bool IsClear(IShard shard, CharacterEntity shooter, CharacterEntity target, in Shot shot)
    {
        var (hit, _, hitEntityId) = shard.Physics.TargetRayCast(shot.Origin, shot.Direction, shooter, shot.Range);
        return !hit || hitEntityId == target.EntityId;
    }
}
