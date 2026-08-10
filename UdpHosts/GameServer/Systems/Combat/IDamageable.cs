using GameServer.Entities;

namespace GameServer.Systems.Combat;

/// <summary>
///     Something a hit can land on. Implementing this is what makes an entity type reachable by weapon fire
///     and by the damage aptitude commands; both resolve their target to an <see cref="IDamageable"/> and
///     neither knows or cares what it actually hit.
///
///     Implementations own their own vitals and their own destruction, because health means something
///     different for each of them: characters have shields and a death state the client animates, vehicles
///     and deployables just stop existing. What they share is the funnel. Everything the attacker's side
///     worked out arrives as a <see cref="DamageInfo"/>, mitigation happens inside
///     <see cref="TakeDamage"/> and nowhere else, and the attacker is told what landed via
///     <see cref="DamageEvents"/>.
/// </summary>
public interface IDamageable : IEntity
{
    /// <summary>
    ///     Whether this can still be hurt. Damage paths check it before doing any work, so a corpse or a
    ///     wreck stops soaking shots the moment it dies rather than when it despawns.
    /// </summary>
    bool IsAlive { get; }

    void TakeDamage(DamageInfo damage);
}
