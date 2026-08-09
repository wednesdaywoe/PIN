using System;
using System.Numerics;
using AeroMessages.GSS.V66;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.ProjectileSim;

public class ProjectileSim
{
    private readonly Shard _shard;

    public ProjectileSim(Shard shard)
    {
        _shard = shard;
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

        // TODO: Use hostility rules once implemented; for now players can not damage each other
        if (entity.IsPlayerControlled && target.IsPlayerControlled)
        {
            return;
        }

        // TODO: Range based damage decay (weapon.MinDamage, ammo.DamageDecayRangefrac, ammo.MinDamageFrac)
        float damage = weapon.DamagePerRound * hit.DamageMod;
        if (hit.Headshot && weapon.HeadshotMult > 0)
        {
            damage *= weapon.HeadshotMult;
        }

        var damageFlags = (DamageResponseFlags)0;
        if (hit.Headshot || hit.Crit)
        {
            damageFlags |= DamageResponseFlags.Critical;
        }

        target.TakeDamage((int)MathF.Round(damage), entity, ammo.Damagetype, damageFlags);
    }

    /*
    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
    }
    */
}
