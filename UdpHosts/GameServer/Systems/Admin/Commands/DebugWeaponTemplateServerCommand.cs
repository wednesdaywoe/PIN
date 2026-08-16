using System.Text;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.Systems.Combat;
using GameServer.Systems.ProjectileSim;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand("Print server weapon info", "dbg_weapon", "dbg_weapon")]
public class DebugWeaponTemplateServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null || context.SourcePlayer.CharacterEntity == null)
        {
            SourceFeedback("Cannot without a valid player character", context);
            return;
        }

        var character = context.SourcePlayer.CharacterEntity;
        if (context.Target != null && context.Target is CharacterEntity commandTarget)
        {
            character = commandTarget;
        }

        var info = character.GetActiveWeaponDetails();

        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("GetActiveWeaponDetails");
        stringBuilder.AppendLine($"Weapon: {info.Weapon.DebugName}");
        stringBuilder.AppendLine($"WeaponId: {info.WeaponId}");
        stringBuilder.AppendLine($"Calculated Spread Factor: {info.Spread}");
        stringBuilder.AppendLine($"Attribute RateOfFire: {info.RateOfFire}");

        stringBuilder.AppendLine($"----- Components");
        stringBuilder.AppendLine($"ScopeId: {info.Weapon.ScopeId}");
        stringBuilder.AppendLine($"UnderbarrelId: {info.Weapon.UnderbarrelId}");
        stringBuilder.AppendLine($"AmmoId: {info.Weapon.AmmoId} (From Template)");

        stringBuilder.AppendLine($"----- Properties");
        stringBuilder.AppendLine($"WeaponFlags: {info.Weapon.WeaponFlags}");
        stringBuilder.AppendLine($"FireType: {info.Weapon.FireType}");
        stringBuilder.AppendLine($"SlotIndex: {info.Weapon.SlotIndex}");
        stringBuilder.AppendLine($"Range: {info.Weapon.Range}");
        stringBuilder.AppendLine($"EquipEnterMs: {info.Weapon.EquipEnterMs}");
        stringBuilder.AppendLine($"EquipExitMs: {info.Weapon.EquipExitMs}");

        /*
        stringBuilder.AppendLine($"----- Abilities");
        stringBuilder.AppendLine($"MeleeAbility: {info.Weapon.MeleeAbility}");
        stringBuilder.AppendLine($"AttackAbility: {info.Weapon.AttackAbility}");
        stringBuilder.AppendLine($"OverchargeAbility: {info.Weapon.OverchargeAbility}");
        stringBuilder.AppendLine($"BurstAbility: {info.Weapon.BurstAbility}");
        stringBuilder.AppendLine($"ReloadAbility: {info.Weapon.ReloadAbility}");
        stringBuilder.AppendLine($"EmptyAbility: {info.Weapon.EmptyAbility}");
        */

        /*
        stringBuilder.AppendLine($"----- Ammo, Clip, Reload");
        stringBuilder.AppendLine($"BaseClipSize: {info.Weapon.BaseClipSize}");
        stringBuilder.AppendLine($"MaxAmmo: {info.Weapon.MaxAmmo}");
        stringBuilder.AppendLine($"AmmoPerBurst: {info.Weapon.AmmoPerBurst}");
        stringBuilder.AppendLine($"MinAmmoPerBurst: {info.Weapon.MinAmmoPerBurst}");
        stringBuilder.AppendLine($"RoundsPerBurst: {info.Weapon.RoundsPerBurst}");
        stringBuilder.AppendLine($"MinRoundsPerBurst: {info.Weapon.MinRoundsPerBurst}");
        stringBuilder.AppendLine($"RoundReload: {info.Weapon.RoundReload}");
        stringBuilder.AppendLine($"ClipRegenMs: {info.Weapon.ClipRegenMs}");
        stringBuilder.AppendLine($"ReloadTime: {info.Weapon.ReloadTime}");
        stringBuilder.AppendLine($"ReloadPenalty: {info.Weapon.ReloadPenalty}");
        */

        /*
        stringBuilder.AppendLine($"----- Targets");
        stringBuilder.AppendLine($"MaxTargets: {info.Weapon.MaxTargets}");
        stringBuilder.AppendLine($"BurstBonusPerTarget: {info.Weapon.BurstBonusPerTarget}");
        stringBuilder.AppendLine($"TargetingRange: {info.Weapon.TargetingRange}");
        */

        stringBuilder.AppendLine($"----- Burst");
        stringBuilder.AppendLine($"MsPerBurst: {info.Weapon.MsPerBurst}");
        stringBuilder.AppendLine($"MsBurstDuration: {info.Weapon.MsBurstDuration}");

        stringBuilder.AppendLine($"----- Chargeup, Overcharge");
        stringBuilder.AppendLine($"MsChargeUp: {info.Weapon.MsChargeUp}");
        stringBuilder.AppendLine($"MsChargeUpMax: {info.Weapon.MsChargeUpMax}");
        stringBuilder.AppendLine($"MsChargeUpMin: {info.Weapon.MsChargeUpMin}");
        stringBuilder.AppendLine($"MsOverchargeDelay: {info.Weapon.MsOverchargeDelay}");

        stringBuilder.AppendLine($"----- Damage");
        stringBuilder.AppendLine($"MinDamage: {info.Weapon.MinDamage}");
        stringBuilder.AppendLine($"DamagePerRound: {info.Weapon.DamagePerRound}");
        stringBuilder.AppendLine($"HeadshotMult: {info.Weapon.HeadshotMult}");
        AppendDamageFalloff(stringBuilder, character, info.Weapon);
        AppendSplash(stringBuilder, character, info.Weapon);

        stringBuilder.AppendLine($"----- ?");
        stringBuilder.AppendLine($"MsReturn: {info.Weapon.MsReturn}");

        stringBuilder.AppendLine($"----- Spread");
        stringBuilder.AppendLine($"MinSpread: {info.Weapon.MinSpread}");
        stringBuilder.AppendLine($"MaxSpread: {info.Weapon.MaxSpread}");
        stringBuilder.AppendLine($"StartingSpread: {info.Weapon.StartingSpread}");
        stringBuilder.AppendLine($"SpreadPerBurst: {info.Weapon.SpreadPerBurst}");
        stringBuilder.AppendLine($"SpreadRampExponent: {info.Weapon.SpreadRampExponent}");
        stringBuilder.AppendLine($"SpreadRampTime: {info.Weapon.SpreadRampTime}");
        stringBuilder.AppendLine($"RunMinSpread: {info.Weapon.RunMinSpread}");
        stringBuilder.AppendLine($"JumpMinSpread: {info.Weapon.JumpMinSpread}");
        stringBuilder.AppendLine($"MsSpreadReturnDelay: {info.Weapon.MsSpreadReturnDelay}");
        stringBuilder.AppendLine($"MsSpreadReturn: {info.Weapon.MsSpreadReturn}");
        stringBuilder.AppendLine($"NoSpreadChance: {info.Weapon.NoSpreadChance}");

        /*
        stringBuilder.AppendLine($"----- Agility");
        stringBuilder.AppendLine($"Agility: {info.Weapon.Agility}");
        stringBuilder.AppendLine($"MsAgilityReturn: {info.Weapon.MsAgilityReturn}");
        stringBuilder.AppendLine($"MsAgilityReturnDelay: {info.Weapon.MsAgilityReturnDelay}");
        stringBuilder.AppendLine($"-----");
        */

        stringBuilder.AppendLine($"----- Attributes");
        var stats = character.GetActiveWeaponAttributes();
        for (int i = 0; i < stats.Length; i++)
        {
            var stat = stats[i];
            var attr = SDBInterface.GetAttributeDefinition(stat.Id);
            stringBuilder.AppendLine($"{stat.Id} {attr.Name} {stat.Value}");
        }

        stringBuilder.AppendLine($"-----");

        var message = stringBuilder.ToString();

        context.SourcePlayer.SendDebugLog(message);
        SourceFeedback($"Printing weapon info to console", context);
    }

    /// <summary>
    ///     Prints the range decay inputs, the curve resolved from them, and samples along it. The model is
    ///     still a guess, so this is the cheapest way to check it: compare the samples against real damage
    ///     numbers at those distances.
    /// </summary>
    private static void AppendDamageFalloff(StringBuilder stringBuilder, CharacterEntity character, WeaponTemplateResult weapon)
    {
        var ammo = SDBInterface.GetAmmo(weapon.AmmoId);
        var baseDamage = character.GetEffectiveWeaponDamage(weapon);

        stringBuilder.AppendLine($"Effective damage per round: {baseDamage}");

        if (ammo == null)
        {
            stringBuilder.AppendLine($"Ammo {weapon.AmmoId} not found, no range decay");
            return;
        }

        stringBuilder.AppendLine($"Ammo DamageDecay: {ammo.DamageDecay}, DamageDecayRangefrac: {ammo.DamageDecayRangefrac}, MinDamageFrac: {ammo.MinDamageFrac}");

        var falloff = DamageFalloff.Resolve(baseDamage, weapon, ammo);
        if (!falloff.Enabled)
        {
            stringBuilder.AppendLine("Range decay: disabled, full damage at every range");
            return;
        }

        stringBuilder.AppendLine($"Range decay: full to {falloff.FullDamageRange}m, down to {falloff.MinDamage} at {falloff.MaxRange}m");

        foreach (var fraction in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f, 1.25f })
        {
            var distance = falloff.MaxRange * fraction;
            stringBuilder.AppendLine($"  {distance:0.#}m: {falloff.DamageAt(distance):0.##}");
        }
    }

    /// <summary>
    ///     Says whether the equipped weapon explodes, and what the blast is worth at range. Worth printing
    ///     before firing anything, because most weapons do not: only 612 of 1264 ammo rows carry a radius,
    ///     so "nothing splashed" is the expected result for a rifle and a defect for a grenade launcher, and
    ///     this is the only way to tell those apart without reading the db.
    /// </summary>
    private static void AppendSplash(StringBuilder stringBuilder, CharacterEntity character, WeaponTemplateResult weapon)
    {
        var ammo = SDBInterface.GetAmmo(weapon.AmmoId);
        var splash = WeaponSplash.Resolve(ammo);

        if (!splash.Enabled)
        {
            stringBuilder.AppendLine($"Splash: none (ammo impact_radius {ammo?.ImpactRadius ?? 0f}), direct hits only");
            return;
        }

        var baseDamage = character.GetEffectiveWeaponDamage(weapon);

        stringBuilder.AppendLine($"Splash: {splash.Radius}m radius, full damage inside {splash.PointBlankRange}m");

        foreach (var fraction in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
        {
            var distance = splash.Radius * fraction;
            stringBuilder.AppendLine($"  {distance:0.#}m from impact: {baseDamage * splash.ScaleAt(distance):0.##}");
        }
    }
}