using System.Collections.Generic;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.aptfs;

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

        var damaged = new HashSet<IAptitudeTarget>();

        foreach (IAptitudeTarget target in context.Targets)
        {
            if (damaged.Add(target))
            {
                ApplyDamage(target, attacker, context, damage, damageType);
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
                // Non-characters can not take damage yet, and splash never hits whoever set it off
                if (pair.Value is not CharacterEntity splashTarget || splashTarget == attacker || ReferenceEquals(pair.Value, context.Self))
                {
                    continue;
                }

                var distance = Vector3.Distance(origin, splashTarget.Position);
                if (distance > splashRange || !damaged.Add(splashTarget))
                {
                    continue;
                }

                float scale = 1f;
                if (Params.Falloff == 1 && distance > Params.Pointblankrange && splashRange > Params.Pointblankrange)
                {
                    scale = 1f - ((distance - Params.Pointblankrange) / (splashRange - Params.Pointblankrange));
                }

                ApplyDamage(splashTarget, attacker, context, damage * scale, damageType);
            }
        }

        return true;
    }

    private void ApplyDamage(IAptitudeTarget target, CharacterEntity attacker, Context context, float damage, byte damageType)
    {
        if (target is not CharacterEntity character)
        {
            Logger.Debug("{Command} {CommandId} can not damage non-character target {Target} yet", nameof(InflictDamageCommand), Params.Id, target);
            return;
        }

        // Prevent players from killing themselves with their own abilties
        if (character == attacker || ReferenceEquals(target, context.Self))
        {
            return;
        }

        // TODO: Use hostility rules once implemented; for now players can not damage other players
        if (attacker is { IsPlayerControlled: true } && character.IsPlayerControlled)
        {
            return;
        }

        var amount = (int)System.MathF.Round(damage);
        if (amount <= 0)
        {
            return;
        }

        character.TakeDamage(amount, attacker, damageType, 0);
    }
}
