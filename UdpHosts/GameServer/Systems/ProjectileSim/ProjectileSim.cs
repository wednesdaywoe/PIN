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

        float damage = decayed * hit.DamageMod;
        if (hit.Headshot && weapon.HeadshotMult > 0)
        {
            damage *= weapon.HeadshotMult;
        }

        target.TakeDamage(new DamageInfo
        {
            Amount = damage,
            Attacker = entity,
            DamageType = entity.WeaponDamageTypeOverride ?? ammo.Damagetype,
            Flags = ResolveFlags(hit),
        });
    }

    /// <summary>
    ///    Fires a single hitscan projectile on for an ability (FireProjectile aptitude command).
    ///    Damage comes from the command instead of the equipped weapon, so there's no headshot multiplier
    ///    and no range decay. <see cref="DamageFalloff"/> is anchored on the weapon template's Range, and an
    ///    ability firing on its own has no equivalent.
    /// </summary>
    public void FireAbilityProjectile(CharacterEntity shooter, Vector3 origin, Vector3 direction, Ammo ammo, float damage)
    {
        if (!TryResolveHit(shooter, origin, direction, 0, out var hit, out var target))
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
    ///     Traces a shot and reports back the character it landed on, if it landed on one the shooter is
    ///     allowed to hurt. Everything past this point differs between a weapon and an ability.
    /// </summary>
    private bool TryResolveHit(CharacterEntity shooter, Vector3 origin, Vector3 direction, uint trace, out ProjectileHitResult hit, out CharacterEntity target)
    {
        target = null;
        hit = _shard.Physics.ProjectileRayCast(origin, direction, shooter, trace);

        if (hit == null)
        {
            return false;
        }

        if (!_shard.Entities.TryGetValue(hit.HitEntityId, out var hitEntity) || hitEntity is not CharacterEntity character || character == shooter)
        {
            return false;
        }

        if (!HostilityRules.CanDamage(shooter, character))
        {
            return false;
        }

        target = character;
        return true;
    }
}
