using System.Collections.Generic;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Combat;
using GameServer.Systems.Hostility;

namespace GameServer.Systems.Aptitude.Commands.Damage;

public class InflictDamageCommand : Command, ICommand
{
    private InflictDamageCommandDef Params;

    public InflictDamageCommand(InflictDamageCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var attacker = context.Initiator as CharacterEntity ?? context.Initiator?.Owner;

        float damage = AbilitySystem.RegistryOp(context.Register, Params.Damagepoints, (Operand)Params.DamagepointsRegop);
        byte damageType = Params.DamageType;
        float splashRange = AbilitySystem.RegistryOp(context.Register, Params.Splashrange, (Operand)Params.SplashrangeRegop);

        if (Params.Weapondamage == 1 || Params.Weapondamagetype == 1 || Params.UseWeaponRadius == 1)
        {
            var weaponDetails = attacker?.GetActiveWeaponDetails();
            if (weaponDetails != null)
            {
                var ammo = SDBInterface.GetAmmo(weaponDetails.Weapon.AmmoId);
                if (Params.Weapondamage == 1)
                {
                    damage = attacker.GetEffectiveWeaponDamage(weaponDetails.Weapon);
                }

                if (Params.Weapondamagetype == 1 && ammo != null)
                {
                    damageType = attacker.WeaponDamageTypeOverride ?? ammo.Damagetype;
                }

                if (Params.UseWeaponRadius == 1 && ammo != null)
                {
                    splashRange = ammo.ImpactRadius;
                }
            }
            else
            {
                Logger.Debug("{Command} {CommandId} requests weapon data but initiator has no active weapon", nameof(InflictDamageCommand), Params.Id);
            }
        }

        if (Params.Usedmgdealt == 1)
        {
            // Would reuse the damage dealt by a previous hit in this execution; dealt damage is not tracked on the context yet
            Logger.Debug("{Command} {CommandId} specifies Usedmgdealt which is not implemented", nameof(InflictDamageCommand), Params.Id);
        }

        if (damage <= 0)
        {
            return true;
        }

        // Keyed by entity id because a direct target and a splash target arrive as different interfaces
        var damaged = new HashSet<ulong>();

        foreach (IAptitudeTarget target in context.Targets)
        {
            if (!damaged.Add(target.EntityId))
            {
                continue;
            }

            if (target is IDamageable damageable)
            {
                ApplyDamage(damageable, attacker, context, damage, damageType);
            }
            else
            {
                Logger.Debug("{Command} {CommandId} can not damage {Target}, which has no health", nameof(InflictDamageCommand), Params.Id, target);
            }
        }

        if (splashRange > 0)
        {
            Vector3 origin;
            if (Params.Frominitiatorpos == 1)
            {
                origin = context.InitPosition;
            }
            else if (context.Targets.TryPeek(out var splashCenter))
            {
                origin = splashCenter.Position;
            }
            else
            {
                origin = context.Self.Position;
            }

            foreach (var pair in context.Shard.Entities)
            {
                // Splash never hits whoever set it off
                if (pair.Value is not IDamageable splashTarget || !splashTarget.IsAlive || ReferenceEquals(splashTarget, attacker))
                {
                    continue;
                }

                var distance = Vector3.Distance(origin, splashTarget.Position);
                if (distance > splashRange || !damaged.Add(splashTarget.EntityId))
                {
                    continue;
                }

                var scale = Params.Falloff == 1 ? SplashFalloff.Scale(distance, splashRange, Params.Pointblankrange) : 1f;

                ApplyDamage(splashTarget, attacker, context, damage * scale, damageType);
            }
        }

        return true;
    }

    private void ApplyDamage(IDamageable target, CharacterEntity attacker, Context context, float damage, byte damageType)
    {
        // Prevent players from killing themselves with their own abilties
        if (ReferenceEquals(target, attacker) || target.EntityId == context.Self?.EntityId)
        {
            return;
        }

        if (!target.IsAlive || !HostilityRules.CanDamage(attacker, target))
        {
            return;
        }

        target.TakeDamage(new DamageInfo
        {
            Amount = damage,
            Attacker = attacker,
            DamageType = damageType,
        });
    }
}
