using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.Combat;

/// <summary>
///     Whether a round explodes when it lands, and over what. Resolved from the ammo record alone, the same
///     way <see cref="ProjectileSim.DamageFalloff"/> resolves range decay, so the reading of the shipped
///     columns can be pinned offline and the firing code stays a loop.
/// </summary>
/// <remarks>
///     <para>
///     <c>dbitems::Ammo.impact_radius</c> shipped with a real radius on 612 of 1264 rows and PIN loaded the
///     record without ever reading the field, so no weapon in the game splashed — a round landing on the
///     ground between two enemies was a miss, because the ground has no health. DATA-21.
///     </para>
///     <para>
///     Two of the three readings here could plausibly have gone the other way, which is why they are a
///     class with tests rather than an <c>if</c> at the call site. <b>-1 is a sentinel, not a distance</b>:
///     3 rows carry it, all "Desecrated" ammo, so the test is greater-than-zero and not non-zero.
///     <b>Ordinary bullets genuinely do not splash</b> — "Normal Bullet (assault rifle)" reads 0 — so this
///     changes explosives and leaves rifles exactly as they were. And <b><c>max_hits</c> is not a cap on
///     any of it</b>, however much the name suggests one: it reads 1 on 584 of the 612 rows that do carry a
///     radius, including every thrown grenade and every mortar shell in the game, so it counts what a
///     projectile touches on the way to its impact rather than what the explosion reaches.
///     </para>
/// </remarks>
public readonly record struct WeaponSplash
{
    /// <summary>
    ///     Whether this ammo explodes at all. False for most of the table, and when false the other two
    ///     values are unset and no second damage pass should run.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>Metres from the impact point that the blast reaches, past which it does nothing.</summary>
    public float Radius { get; init; }

    /// <summary>
    ///     Inner zone taking the whole hit, from <c>min_impact_radius</c>, which is exactly what
    ///     <see cref="SplashFalloff"/> already means by a point blank range. Zero on 497 of the 612
    ///     exploding rows, so most blasts fall off from the centre outwards and only the largest have a
    ///     core that does not.
    /// </summary>
    public float PointBlankRange { get; init; }

    public static WeaponSplash Resolve(Ammo ammo)
    {
        if (ammo == null || ammo.ImpactRadius <= 0f)
        {
            return default;
        }

        // A core at or past the edge would mean full damage everywhere, which SplashFalloff already handles
        // by returning 1 — but clamping here keeps the resolved values honest for anything that reads them
        // rather than passing them straight on, /dbg_weapon included.
        var pointBlank = ammo.MinImpactRadius > 0f && ammo.MinImpactRadius < ammo.ImpactRadius
            ? ammo.MinImpactRadius
            : 0f;

        return new WeaponSplash
        {
            Enabled = true,
            Radius = ammo.ImpactRadius,
            PointBlankRange = pointBlank,
        };
    }

    /// <summary>
    ///     The fraction of a full round something <paramref name="distance"/> metres from the impact takes.
    ///     Zero when this ammo does not explode, so a caller can multiply unconditionally.
    /// </summary>
    public float ScaleAt(float distance)
    {
        return !Enabled || distance > Radius ? 0f : SplashFalloff.Scale(distance, Radius, PointBlankRange);
    }
}
