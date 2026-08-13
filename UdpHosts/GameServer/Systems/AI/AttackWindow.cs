using System;
using GameServer.StaticDB;

namespace GameServer.Systems.AI;

/// <summary>
///     How far an NPC will shoot with a given weapon and how often, resolved once from the weapon
///     template so the tick loop only compares numbers.
///
///     A player's client decides all of this and the server only reacts to the fire messages it sends,
///     so there was no server-side model of a weapon's cadence before this. What's here is read off the
///     same template fields the client would be reading — <c>MsPerBurst</c> for the gap between bursts,
///     <c>MsBurstDuration</c> and <c>RoundsPerBurst</c> for the shots inside one — but whether the
///     client paces them exactly this way is unconfirmed. Being wrong makes NPCs fire at the wrong
///     rhythm, not fire wrongly.
/// </summary>
public readonly record struct AttackWindow
{
    /// <summary>
    ///     Slowest an NPC may cycle, so a template with no burst timing can't fire on every tick.
    /// </summary>
    private const ulong MinBurstIntervalMs = 250;

    /// <summary>
    ///     Whether this weapon can be fired by an NPC at all. False when the template describes no
    ///     reach, which would otherwise leave an NPC aiming forever at something it can never hit.
    /// </summary>
    public bool Usable { get; init; }

    /// <summary>Furthest separation, in metres, an NPC will open fire at.</summary>
    public float Range { get; init; }

    /// <summary>Milliseconds from the start of one burst to the start of the next.</summary>
    public ulong BurstIntervalMs { get; init; }

    /// <summary>
    ///     Calls into <c>WeaponSim.OnFireWeaponProjectile</c> that make up one burst. Usually 1:
    ///     WeaponSim expands a whole burst itself for weapons with no burst duration, exactly as it does
    ///     for the single message a client sends. Weapons that do have a duration get one call per round
    ///     instead, which is the shape the client sends them in.
    /// </summary>
    public int ShotsPerBurst { get; init; }

    /// <summary>Milliseconds between those calls. Zero when a burst is a single call.</summary>
    public ulong ShotIntervalMs { get; init; }

    /// <summary>
    ///     Builds the window for <paramref name="weapon"/>, where <paramref name="rateOfFireMult"/> is
    ///     the shooter's RateOfFire attribute — larger is faster, so it divides the intervals.
    /// </summary>
    public static AttackWindow Resolve(WeaponTemplateResult weapon, float rateOfFireMult)
    {
        if (weapon == null)
        {
            return default;
        }

        // Range is what the damage curve is anchored on, so prefer it and keep TargetingRange as the
        // fallback for templates that only fill one of the two in.
        var range = weapon.Range > 0f ? weapon.Range : weapon.TargetingRange;
        if (range <= 0f)
        {
            return default;
        }

        var scale = rateOfFireMult > 0f ? rateOfFireMult : 1f;
        var burstInterval = Math.Max(MinBurstIntervalMs, (ulong)(weapon.MsPerBurst / scale));

        var shotsPerBurst = 1;
        ulong shotInterval = 0;
        if (weapon.MsBurstDuration > 0 && weapon.RoundsPerBurst > 1)
        {
            shotsPerBurst = weapon.RoundsPerBurst;
            shotInterval = (ulong)(weapon.MsBurstDuration / scale) / (ulong)shotsPerBurst;
        }

        // A burst that outlasts its own cycle would have the next one starting before this one ends.
        if (shotInterval * (ulong)shotsPerBurst >= burstInterval)
        {
            burstInterval = (shotInterval * (ulong)shotsPerBurst) + MinBurstIntervalMs;
        }

        return new AttackWindow
        {
            Usable = true,
            Range = range,
            BurstIntervalMs = burstInterval,
            ShotsPerBurst = shotsPerBurst,
            ShotIntervalMs = shotInterval,
        };
    }

    /// <summary>
    ///     Whether a target <paramref name="separation"/> metres away is worth shooting at. Rounds do
    ///     still land past this, at the floor of the damage curve; this is where an NPC stops bothering.
    /// </summary>
    public bool InRange(float separation)
    {
        return Usable && separation <= Range;
    }
}
