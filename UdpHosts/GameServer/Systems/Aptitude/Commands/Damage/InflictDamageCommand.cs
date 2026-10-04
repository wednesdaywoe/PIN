using System.Collections.Generic;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Entities.TinyObject;
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

        // Only set when this command reads the equipped weapon. An ability carrying its own damage points
        // isn't the weapon's doing, so the combat log shouldn't credit one.
        uint weaponId = 0;
        string weaponName = null;

        // A tiny object (a poison cloud) has no weapon. Read as the owner's gun, Creeping Death's cloud would hit
        // everyone inside for the gun's damage twice a second on top of its own; its real damage is the effect it
        // applies to each target.
        if (Params.Weapondamage == 1 && context.Self is TinyObjectEntity)
        {
            Logger.Debug("{Command} {CommandId} wants weapon damage from tiny object {Self}, which has no weapon: no damage", nameof(InflictDamageCommand), Params.Id, context.Self);
            return true;
        }

        if (Params.Weapondamage == 1 || Params.Weapondamagetype == 1 || Params.UseWeaponRadius == 1)
        {
            var weaponDetails = attacker?.GetActiveWeaponDetails();
            if (weaponDetails != null)
            {
                var ammo = SDBInterface.GetAmmo(weaponDetails.Weapon.AmmoId);
                if (Params.Weapondamage == 1)
                {
                    damage = attacker.GetEffectiveWeaponDamage(weaponDetails.Weapon);
                    weaponId = weaponDetails.Weapon.WeaponSdbId;
                    weaponName = weaponDetails.Weapon.DebugName;
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
                ApplyDamage(damageable, attacker, context, damage, damageType, weaponId, weaponName);
            }
            else
            {
                Logger.Debug("{Command} {CommandId} can not damage {Target}, which has no health", nameof(InflictDamageCommand), Params.Id, target);
            }
        }

        // An effect's tick (a poison's damage over time) hurts the one carrying it and no one else. Poison Ball's tick
        // carries Splashrange 5, and with splash every poisoned creature also hit each poisoned neighbour, so three
        // stacked Fiends each took three ticks a second. Only the original server knew what that splash was for; its
        // description promises damage over time to the poisoned, nothing more.
        // The same goes for damage an effect aims at whoever carries it (TargetSelf, then InflictDamage), which is how
        // Creeping Death's cloud deals its damage: a 1 m splash there let bunched-up targets hit each other.
        var effectOnHolder = context.ExecutionHint == ExecutionHint.ApplyEffect && context.Targets.Count == 1 && context.Targets.Contains(context.Self);
        if (splashRange > 0 && (context.ExecutionHint == ExecutionHint.UpdateEffect || effectOnHolder))
        {
            Logger.Debug("{Command} {CommandId} is an effect tick, ignoring its {Splash}m splash", nameof(InflictDamageCommand), Params.Id, splashRange);
        }
        else if (splashRange > 0)
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

                ApplyDamage(splashTarget, attacker, context, damage * scale, damageType, weaponId, weaponName);
            }
        }

        return true;
    }

    private void ApplyDamage(IDamageable target, CharacterEntity attacker, Context context, float damage, byte damageType, uint weaponId, string weaponName)
    {
        // Prevent players from killing themselves with their own abilities. This used to skip context.Self as well, which
        // is the same character in an ability but the victim inside an effect, so a poison's damage over time spared
        // the one thing it was poisoning.
        if (ReferenceEquals(target, attacker))
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
            WeaponId = weaponId,
            WeaponName = weaponName,
            FromUltimate = context.FromUltimate,
        });
    }
}
