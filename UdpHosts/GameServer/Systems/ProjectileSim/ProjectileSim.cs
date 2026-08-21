using System.Numerics;
using AeroMessages.GSS.V66;
using GameServer.Entities.Character;
using GameServer.Physics;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Combat;
using GameServer.Systems.Hostility;
using Serilog;

namespace GameServer.Systems.ProjectileSim;

public class ProjectileSim
{
    private readonly Shard _shard;
    private readonly ILogger _logger;

    public ProjectileSim(Shard shard)
    {
        _shard = shard;
        _logger = shard.Logger.ForContext(typeof(ProjectileSim));
    }

    public void FireProjectile(CharacterEntity entity, uint trace, Vector3 origin, Vector3 direction, Ammo ammo, WeaponTemplateResult weapon)
    {
        // A shot that struck the world still resolves, because it may still explode. `target` is null in
        // that case and everything below is written to expect it.
        if (!TryResolveHit(entity, origin, direction, trace, out var hit, out var target))
        {
            return;
        }

        // Range decay applies to the weapon's damage per round. Hit location and headshot scale what's left
        var distance = Vector3.Distance(origin, hit.Position);
        var falloff = DamageFalloff.Resolve(entity.GetEffectiveWeaponDamage(weapon), weapon, ammo);
        var decayed = falloff.DamageAt(distance);

        if (falloff.Enabled && distance > falloff.FullDamageRange)
        {
            _logger.Debug(
                "Damage falloff for {Weapon} at {Distance}m: {Base} -> {Decayed} (full to {FullDamageRange}m, {MinDamage} at {MaxRange}m)",
                weapon.DebugName,
                distance,
                falloff.BaseDamage,
                decayed,
                falloff.FullDamageRange,
                falloff.MinDamage,
                falloff.MaxRange);
        }

        var damageType = entity.WeaponDamageTypeOverride ?? ammo.Damagetype;

        if (target != null)
        {
            float damage = decayed * hit.DamageMod;
            if (hit.Headshot && weapon.HeadshotMult > 0)
            {
                damage *= weapon.HeadshotMult;
            }

            target.TakeDamage(new DamageInfo
            {
                Amount = damage,
                Attacker = entity,
                DamageType = damageType,
                Flags = ResolveFlags(hit),
                WeaponId = weapon.WeaponSdbId,
                WeaponName = weapon.DebugName,
            });
        }

        // Splash is deliberately based on `decayed` rather than on what the direct target took: a headshot
        // multiplier and a hit-location modifier belong to the body they were read off, not to everyone
        // standing near it.
        ApplySplash(entity, hit.Position, ammo, decayed, damageType, target, weapon);
    }

    /// <summary>
    ///    Fires a single hitscan projectile on for an ability (FireProjectile aptitude command).
    ///    Damage comes from the command instead of the equipped weapon, so there's no headshot multiplier
    ///    and no range decay. <see cref="DamageFalloff"/> is anchored on the weapon template's Range, and an
    ///    ability firing on its own has no equivalent.
    /// </summary>
    public void FireAbilityProjectile(CharacterEntity shooter, Vector3 origin, Vector3 direction, Ammo ammo, float damage)
    {
        // Deliberately still nothing on a world hit, unlike the weapon path above. An ability's area damage
        // is InflictDamageCommand's job and it has its own radius off the command def, so reading the ammo
        // radius here as well would apply two blasts to any chain that fires a projectile and then inflicts
        // damage. DATA-21 is about weapons.
        if (!TryResolveHit(shooter, origin, direction, 0, out var hit, out var target) || target == null)
        {
            return;
        }

        target.TakeDamage(new DamageInfo
        {
            Amount = damage * hit.DamageMod,
            Attacker = shooter,
            DamageType = ammo.Damagetype,
            Flags = ResolveFlags(hit),
        });
    }

    /*
    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
    }
    */

    private static DamageResponseFlags ResolveFlags(ProjectileHitResult hit)
    {
        return hit.Headshot || hit.Crit ? DamageResponseFlags.Critical : 0;
    }

    /// <summary>
    ///     Damages everything the blast reaches, having already dealt with whatever the round struck
    ///     directly. Does nothing at all for a weapon whose ammo carries no radius, which is most of them.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Which ammo explodes and how the blast falls off is <see cref="WeaponSplash"/>'s reading of the
    ///     shipped columns, and the notes on why those columns read the way they do live there. Ability
    ///     splash is a separate path that was never affected — <c>InflictDamageCommand</c> has always had
    ///     its own, off <c>Splashrange</c> — and this deliberately does not touch it. DATA-21.
    ///     </para>
    ///     <para>
    ///     Walking every entity in the shard per shot is the same thing <c>InflictDamageCommand</c> does and
    ///     is affordable for the same reason: the loop is not entered at all unless the ammo explodes, so
    ///     ordinary rifle fire costs one comparison.
    ///     </para>
    /// </remarks>
    private void ApplySplash(
        CharacterEntity shooter,
        Vector3 center,
        Ammo ammo,
        float baseDamage,
        byte damageType,
        IDamageable directTarget,
        WeaponTemplateResult weapon)
    {
        var splash = WeaponSplash.Resolve(ammo);
        if (!splash.Enabled || baseDamage <= 0f)
        {
            return;
        }

        var hits = 0;

        foreach (var pair in _shard.Entities)
        {
            // The direct target already took its own round, at its own hit location. Damaging it again here
            // would charge one shot twice, which is the mistake V7 is written to catch.
            if (pair.Value is not IDamageable splashTarget
                || !splashTarget.IsAlive
                || ReferenceEquals(splashTarget, shooter)
                || ReferenceEquals(splashTarget, directTarget))
            {
                continue;
            }

            var scale = splash.ScaleAt(Vector3.Distance(center, splashTarget.Position));
            if (scale <= 0f || !HostilityRules.CanDamage(shooter, splashTarget))
            {
                continue;
            }

            splashTarget.TakeDamage(new DamageInfo
            {
                Amount = baseDamage * scale,
                Attacker = shooter,
                DamageType = damageType,
                WeaponId = weapon.WeaponSdbId,
                WeaponName = weapon.DebugName,
            });

            hits++;
        }

        if (hits > 0)
        {
            _logger.Debug(
                "Splash from {Weapon} ({Ammo}) at {Radius}m caught {Hits} entities beyond the direct hit",
                weapon?.DebugName,
                ammo.Name,
                splash.Radius,
                hits);
        }
    }

    /// <summary>
    ///     Traces a shot and reports back where it landed, and separately what it landed on if that is
    ///     something the shooter is allowed to hurt. Everything past this point differs between a weapon
    ///     and an ability.
    ///     <para>
    ///     The two answers are separate on purpose. False means the round touched nothing at all and there
    ///     is no impact anywhere to reason about. True with a null <paramref name="target"/> means it
    ///     landed somewhere real that cannot bleed — terrain, a wall, a corpse — which is a miss for a
    ///     rifle and an explosion for a grenade. Reporting those two as the same thing is what made every
    ///     shot into the ground disappear.
    ///     </para>
    /// </summary>
    private bool TryResolveHit(CharacterEntity shooter, Vector3 origin, Vector3 direction, uint trace, out ProjectileHitResult hit, out IDamageable target)
    {
        target = null;
        hit = _shard.Physics.ProjectileRayCast(origin, direction, shooter, trace);

        if (hit == null)
        {
            return false;
        }

        // Plenty of things have collision without being able to bleed. A shot into one of those still
        // happened somewhere, so the impact stands and only the target is left unset.
        if (!_shard.Entities.TryGetValue(hit.HitEntityId, out var hitEntity) || hitEntity is not IDamageable damageable || ReferenceEquals(damageable, shooter))
        {
            return true;
        }

        if (!damageable.IsAlive || !HostilityRules.CanDamage(shooter, damageable))
        {
            return true;
        }

        target = damageable;
        return true;
    }
}
