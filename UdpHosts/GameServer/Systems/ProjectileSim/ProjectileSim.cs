using System;
using System.Numerics;
using AeroMessages.GSS.V66;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;
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
        var hit = _shard.Physics.ProjectileRayCast(origin, direction, entity, trace);
        if (hit == null)
        {
            return;
        }

        if (!_shard.Entities.TryGetValue(hit.HitEntityId, out var hitEntity) || hitEntity is not CharacterEntity target || target == entity)
        {
            return;
        }

        if (!HostilityRules.CanDamage(entity, target))
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

        var damageFlags = (DamageResponseFlags)0;
        if (hit.Headshot || hit.Crit)
        {
            damageFlags |= DamageResponseFlags.Critical;
        }

        var damageType = entity.WeaponDamageTypeOverride ?? ammo.Damagetype;
        target.TakeDamage((int)MathF.Round(damage), entity, damageType, damageFlags);
    }

    /// <summary>
    ///    Fires a single hitscan projectile on for an ability (FireProjectile aptitude command).
    ///    Damage comes from the command instead of the equipped weapon, so there's no headshot multiplier
    ///    and no range decay. <see cref="DamageFalloff"/> is anchored on the weapon template's Range, and an
    ///    ability firing on its own has no equivalent.
    /// </summary>
    public void FireAbilityProjectile(CharacterEntity shooter, Vector3 origin, Vector3 direction, Ammo ammo, float damage)
    {
        var hit = _shard.Physics.ProjectileRayCast(origin, direction, shooter, 0);
        if (hit == null)
        {
            return;
        }

        if (!_shard.Entities.TryGetValue(hit.HitEntityId, out var hitEntity) || hitEntity is not CharacterEntity target || target == shooter)
        {
            return;
        }

        if (!HostilityRules.CanDamage(shooter, target))
        {
            return;
        }

        var damageFlags = (DamageResponseFlags)0;
        if (hit.Headshot || hit.Crit)
        {
            damageFlags |= DamageResponseFlags.Critical;
        }

        target.TakeDamage((int)MathF.Round(damage * hit.DamageMod), shooter, ammo.Damagetype, damageFlags);
    }

    /*
    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
    }
    */
}
