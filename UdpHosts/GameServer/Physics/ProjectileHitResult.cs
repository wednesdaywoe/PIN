using System.Numerics;

namespace GameServer.Physics;

/// <summary>
///    Where a projectile trace landed, and on what. <see cref="HitEntityId"/> is 0 when the shot struck
///    the world rather than an entity — terrain, a static, or a body with nothing behind it — in which
///    case <see cref="Position"/> is still the real impact point and the hit location modifiers are all
///    at their defaults. A trace that reached its maximum range without touching anything produces no
///    result at all rather than one of these.
/// </summary>
public class ProjectileHitResult
{
    /// <summary>The entity struck, or 0 for the world. See the note on the class.</summary>
    public ulong HitEntityId { get; init; }

    public Vector3 Position { get; init; }

    public bool Headshot { get; init; }

    public bool Crit { get; init; }

    public float DamageMod { get; init; } = 1f;
}
