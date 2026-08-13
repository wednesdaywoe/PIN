namespace GameServer.Systems.AI;

/// <summary>
///     Why an NPC with a target isn't shooting at it. Logged when it changes, because an NPC that
///     stands there doing nothing is otherwise indistinguishable from an NPC that isn't running.
/// </summary>
public enum HoldFire
{
    Firing,
    NoTarget,
    NoWeapon,
    WeaponUnusable,
    OutOfRange,
    NoLineOfSight,
}

public sealed class AIState
{
    public ThreatTable Threat { get; } = new();

    public ulong? CurrentTargetId { get; set; }

    /// <summary>Rounds still owed to the burst in progress. Zero between bursts.</summary>
    public int RoundsLeftInBurst { get; set; }

    /// <summary>
    ///     Whether the client has been told this NPC started shooting and not yet that it stopped.
    ///     Stays set through the gaps between bursts; see <c>NpcCombat</c> for why.
    /// </summary>
    public bool WeaponHot { get; set; }

    /// <summary>Shard time the burst in progress began, which is what the next one is scheduled off.</summary>
    public ulong BurstStartedAt { get; set; }

    /// <summary>Earliest shard time the next burst may start.</summary>
    public ulong NextBurstTime { get; set; }

    /// <summary>Earliest shard time the next round within the current burst may go out.</summary>
    public ulong NextShotTime { get; set; }

    /// <summary>Last reason this NPC didn't shoot, so the log gets one line per change and not per tick.</summary>
    public HoldFire LastHold { get; set; }

    /// <summary>
    ///     The weapon <see cref="CachedWindow"/> was resolved from. Resolving one means walking several
    ///     SDB tables, and doing that on every tick of every NPC also reproduced the two "Failed to get
    ///     WeaponSpread/RateOfFire Attribute" warnings 20 times a second — monster weapons carry no item
    ///     attribute ranges, so those fire for every NPC that exists.
    /// </summary>
    public uint CachedWeaponId { get; set; }

    public AttackWindow CachedWindow { get; set; }

    public string CachedWeaponName { get; set; }
}
