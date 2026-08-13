using System.Numerics;

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

    /// <summary>Where this NPC was first seen standing. What it is leashed to and what it walks back to.</summary>
    public Vector3 Home { get; set; }

    public bool HasHome { get; set; }

    /// <summary>
    ///     Whether it is currently walking somewhere. Held across ticks so the stopping distance has
    ///     hysteresis: it closes all the way in, then stands until the target has opened a real gap,
    ///     instead of shuffling a few centimetres every tick to hold an exact separation.
    /// </summary>
    public bool Moving { get; set; }

    /// <summary>
    ///     Whether it has given up on a chase and is walking home. Held until it arrives, so an NPC that
    ///     leashes out with its target still in perception doesn't turn round and start chasing again on
    ///     the next tick, and the one after that, forever.
    /// </summary>
    public bool Returning { get; set; }

    /// <summary>
    ///     The last place the current target was seen standing on the ground. Destinations are taken from
    ///     here rather than from where the target is right now, because the only thing that makes a
    ///     target's Z a ground height is the target being on the ground — see <see cref="NpcMovement"/>.
    /// </summary>
    public Vector3 TargetFooting { get; set; }

    public bool HasTargetFooting { get; set; }

    /// <summary>The monster type <see cref="CachedSpeed"/> was resolved from; 0 before the first resolve.</summary>
    public uint CachedSpeedTypeId { get; set; }

    public MoveSpeed CachedSpeed { get; set; }

    /// <summary>
    ///     The pose <see cref="NpcPose"/> last put on the wire. Held so an NPC that hasn't moved or
    ///     turned since the last tick sends nothing at all, rather than repeating itself to every
    ///     scoped client twenty times a second for as long as it stands there.
    /// </summary>
    public Vector3 LastSentPosition { get; set; }

    public Quaternion LastSentOrientation { get; set; }

    public Vector3 LastSentAim { get; set; }

    public short LastSentMovementState { get; set; }

    /// <summary>
    ///     False until the first pose goes out, so that a spawn is always announced. Without it an NPC
    ///     spawned exactly where the defaults sit would compare equal and never send one.
    /// </summary>
    public bool HasSentPose { get; set; }
}
