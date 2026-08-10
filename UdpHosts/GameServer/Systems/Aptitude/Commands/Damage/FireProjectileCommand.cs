using System;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Damage;

public class FireProjectileCommand : Command, ICommand
{
    private FireProjectileCommandDef Params;

    public FireProjectileCommand(FireProjectileCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var shooter = context.Initiator as CharacterEntity ?? context.Initiator?.Owner ?? context.Self as CharacterEntity;
        if (shooter == null)
        {
            Logger.Debug("{Command} {CommandId} could not resolve a character to fire from ({Initiator})", nameof(FireProjectileCommand), Params.Id, context.Initiator);
            return true;
        }

        var ammo = SDBInterface.GetAmmo(Params.Ammotype);
        if (ammo == null)
        {
            Logger.Warning("{Command} {CommandId} references unknown ammo type {AmmoType}", nameof(FireProjectileCommand), Params.Id, Params.Ammotype);
            return true;
        }

        float damage = AbilitySystem.RegistryOp(context.Register, Params.Damage, (Operand)Params.DamageRegop);
        if (Params.UseWeaponDamage == 1)
        {
            var weaponDetails = shooter.GetActiveWeaponDetails();
            if (weaponDetails != null)
            {
                damage = shooter.GetEffectiveWeaponDamage(weaponDetails.Weapon);
            }
        }

        Vector3 direction = shooter.AimDirection;
        if (Params.AimAtTarget == 1 && context.Targets.TryPeek(out var aimTarget))
        {
            var delta = aimTarget.Position - shooter.Position;
            if (delta.LengthSquared() > 0.0001f)
            {
                direction = Vector3.Normalize(delta);
            }
        }

        var origin = shooter.GetProjectileOrigin(direction);
        var bursts = Math.Max((byte)1, Params.Burstcount);
        for (var i = 0; i < bursts; i++)
        {
            context.Shard.ProjectileSim.FireAbilityProjectile(shooter, origin, direction, ammo, damage);
        }

        return true;
    }
}
