using System.Numerics;

namespace GameServer.Physics;

/// <summary>
///    The result of a projectile trace that hit an entity body, including the hit location modifiers resolved from the pose data.
/// </summary>
public class ProjectileHitResult
{
    public ulong HitEntityId { get; init; }

    public Vector3 Position { get; init; }

    public bool Headshot { get; init; }

    public bool Crit { get; init; }

    public float DamageMod { get; init; } = 1f;
}
