using System;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.ProjectileSim;

/// <summary>
///     The resolved range to damage curve for one weapon and ammo pair. Damage is full out to
///     <see cref="FullDamageRange"/>, falls linearly to <see cref="MinDamage"/> at <see cref="MaxRange"/>,
///     and stays there beyond it.
///
///     The shape is a guess. Ammo carries DamageDecay, DamageDecayRangefrac and MinDamageFrac, weapon
///     templates carry MinDamage and Range, but how the client combines them hasn't been confirmed against
///     a real clientdb. All of the guess lives in <see cref="Resolve"/>, which returns a disabled curve
///     whenever the numbers don't describe a sensible falloff, so being wrong leaves damage as it was
///     before decay existed instead of quietly weakening every weapon. /dbg_weapon prints and samples the
///     resolved curve, which is how to check it against the client.
/// </summary>
public readonly record struct DamageFalloff
{
    /// <summary>
    ///     Whether this pair decays at all. When false, <see cref="DamageAt"/> returns
    ///     <see cref="BaseDamage"/> at every range and the other values are unset.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    ///     Damage for a round that lands inside <see cref="FullDamageRange"/>, after weapon damage
    ///     overrides and multipliers but before hit location and headshot modifiers.
    /// </summary>
    public float BaseDamage { get; init; }

    public float MinDamage { get; init; }

    public float FullDamageRange { get; init; }

    public float MaxRange { get; init; }

    /// <summary>
    ///     Builds the curve for <paramref name="weapon"/> firing <paramref name="ammo"/>, where
    ///     <paramref name="baseDamage"/> is the shooter's effective damage per round.
    /// </summary>
    public static DamageFalloff Resolve(float baseDamage, WeaponTemplateResult weapon, Ammo ammo)
    {
        var none = new DamageFalloff { BaseDamage = baseDamage };

        if (weapon == null || ammo == null || ammo.DamageDecay == 0 || weapon.Range <= 0f || baseDamage <= 0f)
        {
            return none;
        }

        // Both minimums are read as a fraction of a full round rather than absolute points, so a weapon buff
        // carries the floor up with it instead of decaying down to the unbuffed minimum. The higher wins.
        var templateFrac = weapon.DamagePerRound > 0 ? (float)weapon.MinDamage / weapon.DamagePerRound : 0f;
        var minDamageFrac = Math.Clamp(MathF.Max(templateFrac, ammo.MinDamageFrac), 0f, 1f);
        var fullDamageRange = Math.Clamp(ammo.DamageDecayRangefrac, 0f, 1f) * weapon.Range;

        // Nothing to decay towards, or no distance to decay over
        if (minDamageFrac >= 1f || fullDamageRange >= weapon.Range)
        {
            return none;
        }

        return new DamageFalloff
        {
            Enabled = true,
            BaseDamage = baseDamage,
            MinDamage = minDamageFrac * baseDamage,
            FullDamageRange = fullDamageRange,
            MaxRange = weapon.Range,
        };
    }

    /// <summary>
    ///     Damage for a round that travelled <paramref name="distance"/> metres before it hit.
    /// </summary>
    public float DamageAt(float distance)
    {
        if (!Enabled || distance <= FullDamageRange)
        {
            return BaseDamage;
        }

        if (distance >= MaxRange)
        {
            return MinDamage;
        }

        var decayed = (distance - FullDamageRange) / (MaxRange - FullDamageRange);
        return BaseDamage + ((MinDamage - BaseDamage) * decayed);
    }
}
