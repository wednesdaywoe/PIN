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

        // A placed object firing (Fungal Bloom's fungus throwing spores) fires from itself, not from its owner, which
        // the shooter above resolves to so the damage is credited to them. Only here are AimOffset and Spread read:
        // the fungus's "0 0 100" aims straight up and Spread 100 scatters the spores all around it. Player abilities
        // keep firing along the player's aim as before.
        var firer = context.Initiator is CharacterEntity ? null : context.Initiator;
        if (firer != null)
        {
            // From above the object, so the shot doesn't strike the object's own body first (the fungus is ~3 m tall)
            origin = firer.Position + new Vector3(0f, 0f, 3.5f);
            var offset = ParseOffset(Params.AimOffset);
            if (Params.AimAtTarget == 1 && context.Targets.TryPeek(out var objectTarget) && (objectTarget.Position - origin).LengthSquared() > 0.0001f)
            {
                direction = Vector3.Normalize(objectTarget.Position - origin);
            }
            else if (offset.LengthSquared() > 0.0001f)
            {
                direction = Vector3.Normalize(offset);
            }
            else if (firer is Entities.Deployable.DeployableEntity { AimDirection: var aim } && aim.LengthSquared() > 0.0001f)
            {
                direction = Vector3.Normalize(aim);
            }
        }

        var bursts = Math.Max((byte)1, Params.Burstcount);
        for (var i = 0; i < bursts; i++)
        {
            var shot = firer != null && Params.Spread > 0 ? Scatter(direction, Params.Spread) : direction;
            context.Shard.ProjectileSim.FireAbilityProjectile(shooter, origin, shot, ammo, damage, context.ExecutionHint == ExecutionHint.ApplyEffect ? context : null, context.FromUltimate);
        }

        return true;
    }

    private static Vector3 ParseOffset(string text)
    {
        var parts = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 3
               && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
               && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y)
               && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var z)
                   ? new Vector3(x, y, z)
                   : Vector3.Zero;
    }

    /// <summary>
    ///     A random direction within a cone around <paramref name="direction" />. Spread is read as the cone's full
    ///     width in degrees (a guess), capped so a shot never points below level.
    /// </summary>
    internal static Vector3 Scatter(Vector3 direction, float spreadDegrees)
    {
        var halfAngle = MathF.Min(spreadDegrees / 2f, 80f) * MathF.PI / 180f;
        var side = MathF.Abs(direction.Z) < 0.99f ? Vector3.Normalize(Vector3.Cross(direction, Vector3.UnitZ)) : Vector3.UnitX;
        var up = Vector3.Cross(side, direction);
        var tilt = MathF.Acos(1f - (Random.Shared.NextSingle() * (1f - MathF.Cos(halfAngle))));
        var turn = Random.Shared.NextSingle() * 2f * MathF.PI;
        return Vector3.Normalize((direction * MathF.Cos(tilt)) + (((side * MathF.Cos(turn)) + (up * MathF.Sin(turn))) * MathF.Sin(tilt)));
    }
}
