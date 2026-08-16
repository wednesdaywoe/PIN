using System;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Systems.Combat;
using Serilog;

namespace GameServer.Systems.AI;

/// <summary>
///     Turns the target <see cref="TargetSelection"/> picked into shots. Everything a player's client
///     would decide and send — where the character is looking, when a burst starts, how many rounds go
///     into it — is decided here instead, then pushed through the same
///     <c>WeaponSim.OnFireWeaponProjectile</c> a client fire message lands in, so NPC rounds hit,
///     fall off with range and crit on exactly the same code as a player's.
///
///     Abilities are the other way a monster could attack, and the one the milestone doc expected to
///     prefer, but <c>dbmonster::Monster</c> turns out to carry no ability column at all — only
///     <c>Weapon1Id</c>/<c>Weapon2Id</c> and the names of behaviour trees PIN doesn't load. So weapons
///     it is. Ammo, clips and reloading aren't modelled: an NPC fires forever.
/// </summary>
public static class NpcCombat
{
    /// <summary>
    ///     How closely the aim has to already match before a turn isn't worth putting on the wire.
    ///     Cosine of about a degree; below it, an NPC watching a motionless player would push a movement
    ///     update every tick that said nothing.
    /// </summary>
    private const float AimUnchangedCosine = 0.99985f;

    public static void Tick(IShard shard, CharacterEntity npc, AIState state, ulong now, ILogger logger)
    {
        if (!TryResolveTarget(shard, state, out var target) || !Sightline.TrySolve(npc, target, out var shot))
        {
            Hold(npc, state, now, HoldFire.NoTarget, logger);
            return;
        }

        // Turn to face the target whether or not the shot is on: an NPC that has noticed you should be
        // looking at you while it closes, and the muzzle position depends on which way it's turned.
        Face(npc, shot.Direction);

        if (!TryResolveWeapon(npc, state))
        {
            Hold(npc, state, now, HoldFire.NoWeapon, logger);
            return;
        }

        var window = state.CachedWindow;
        if (!window.Usable)
        {
            Hold(npc, state, now, HoldFire.WeaponUnusable, logger, state.CachedWeaponName, window.Range, shot.Separation);
            return;
        }

        if (!window.InRange(shot.Separation))
        {
            Hold(npc, state, now, HoldFire.OutOfRange, logger, state.CachedWeaponName, window.Range, shot.Separation);
            return;
        }

        if (!Sightline.IsClear(shard, npc, target, shot))
        {
            Hold(npc, state, now, HoldFire.NoLineOfSight, logger, state.CachedWeaponName, window.Range, shot.Separation);
            return;
        }

        state.LastHold = HoldFire.Firing;
        Fire(shard, npc, state, window, shot, now, logger);
    }

    /// <summary>
    ///     Drops everything an NPC was doing. Called for one that has died: the tick loop skips the dead,
    ///     so without this a monster killed mid-burst leaves a corpse the client still believes is firing.
    /// </summary>
    public static void Disengage(CharacterEntity npc, AIState state, ulong now)
    {
        StopFiring(npc, state, now);
        state.CurrentTargetId = null;
    }

    /// <summary>
    ///     Refreshes the cached attack window if the NPC's weapon changed, and reports whether it has one
    ///     at all. Resolving costs several SDB lookups and, for a monster, two warnings about missing item
    ///     attribute ranges — at 20Hz per NPC that was 40 log lines a second saying nothing.
    ///
    ///     Public because movement needs the answer too, and needs it first: how close an NPC wants to
    ///     stand is a fraction of what its weapon can reach, so a movement pass that runs before this has
    ///     nothing to read and falls back to the default. That is what sent a melee Aranha to the 12m a
    ///     rifle wants before it closed to the 4m its claw needs — <see cref="AIEngine"/> now resolves the
    ///     weapon once, up front, so both passes see the same window on the same tick.
    /// </summary>
    public static bool TryResolveWeapon(CharacterEntity npc, AIState state)
    {
        var weaponId = npc.GetActiveWeaponId();
        if (weaponId == 0)
        {
            state.CachedWeaponId = 0;
            return false;
        }

        if (weaponId == state.CachedWeaponId)
        {
            return true;
        }

        var details = npc.GetActiveWeaponDetails();
        if (details?.Weapon == null)
        {
            state.CachedWeaponId = 0;
            return false;
        }

        state.CachedWeaponId = weaponId;
        state.CachedWeaponName = details.Weapon.DebugName;

        // details.RateOfFire is 1 for every creature in the game and always has been: it is read off the
        // weapon's item attributes and monster weapons carry none, so the fallback is what arrives. The
        // tier scalar is the only thing that has ever moved it, and it is applied here rather than inside
        // GetActiveWeaponDetails because that method answers for players too, where a tier means nothing.
        state.CachedWindow = AttackWindow.Resolve(details.Weapon, details.RateOfFire * npc.ScalingRateOfFireMultiplier);
        return true;
    }

    private static void Hold(CharacterEntity npc, AIState state, ulong now, HoldFire reason, ILogger logger)
    {
        Hold(npc, state, now, reason, logger, null, 0f, 0f);
    }

    /// <summary>
    ///     Stops the NPC shooting and says why, once, when the reason changes. Per-tick would be 20 lines
    ///     a second; saying nothing at all is worse, because then an NPC standing over a target doing
    ///     nothing looks exactly like an AI that never ran.
    /// </summary>
    private static void Hold(CharacterEntity npc, AIState state, ulong now, HoldFire reason, ILogger logger, string weapon, float weaponRange, float separation)
    {
        StopFiring(npc, state, now);

        if (state.LastHold == reason)
        {
            return;
        }

        state.LastHold = reason;

        // Having no target is the resting state of every NPC in the zone, not news.
        if (reason == HoldFire.NoTarget)
        {
            return;
        }

        logger.Debug(
            "NPC {Npc} holding fire on {Target}: {Reason} (weapon {Weapon}, reach {Reach}m, target at {Separation}m)",
            npc.EntityId,
            state.CurrentTargetId,
            reason,
            weapon ?? "none",
            weaponRange,
            separation);
    }

    private static void Face(CharacterEntity npc, Vector3 direction)
    {
        if (Vector3.Dot(npc.AimDirection, direction) >= AimUnchangedCosine)
        {
            return;
        }

        npc.SetAim(direction, Facing.Towards(direction, npc.Orientation));
    }

    private static bool TryResolveTarget(IShard shard, AIState state, out IDamageable target)
    {
        target = null;
        return state.CurrentTargetId is ulong targetId
               && shard.Entities.TryGetValue(targetId, out var entity)
               && (target = entity as IDamageable) != null
               && target.IsAlive;
    }

    private static void Fire(IShard shard, CharacterEntity npc, AIState state, in AttackWindow window, in Shot shot, ulong now, ILogger logger)
    {
        if (state.RoundsLeftInBurst == 0)
        {
            if (now < state.NextBurstTime)
            {
                return;
            }

            state.RoundsLeftInBurst = window.ShotsPerBurst;
            state.BurstStartedAt = now;
            state.NextShotTime = now;
            state.WeaponHot = true;
            npc.SetFireBurst((uint)now);

            // The bearing is the yaw the server is asserting the NPC stands at, in world degrees. It is
            // true by construction — Face has already pointed it at the target this tick — so it proves
            // nothing on its own. What it is for is comparing two monsters: spawn a Chosen and an Aranha
            // in the same place, and if both bearings agree while only one renders facing the player,
            // the difference is in the model rather than in anything the server decided.
            logger.Debug(
                "NPC {Npc} opens fire on {Target} at {Separation}m, bearing {Bearing:0.#}°: {Shots} shot(s) every {ShotInterval}ms, next burst in {BurstInterval}ms",
                npc.EntityId,
                state.CurrentTargetId,
                shot.Separation,
                MathF.Atan2(shot.Direction.Y, shot.Direction.X) * 180f / MathF.PI,
                window.ShotsPerBurst,
                window.ShotIntervalMs,
                window.BurstIntervalMs);
        }

        if (now < state.NextShotTime)
        {
            return;
        }

        // One round per tick at most. Catching up on a burst that fell behind would fire several rounds
        // stamped with the same time, and the spread PRNG is seeded off that time, so they'd all fly
        // down the identical line. A slow tick should slow an NPC down, not stack its shots.
        shard.WeaponSim.OnFireWeaponProjectile(npc, (uint)now, shot.Direction);
        state.RoundsLeftInBurst--;
        state.NextShotTime = now + window.ShotIntervalMs;

        if (state.RoundsLeftInBurst == 0)
        {
            state.NextBurstTime = state.BurstStartedAt + window.BurstIntervalMs;
        }
    }

    /// <summary>
    ///     Tells the client the shooting is over, once, for an NPC that has stopped having a reason to
    ///     shoot — target dead, out of range, behind cover, or dead itself.
    ///
    ///     Deliberately not sent at the end of every burst. A one-round burst starts and ends inside the
    ///     same tick, so the pair would carry the same timestamp and describe a burst of zero length;
    ///     leaving it open until the NPC actually stops is the same shape as a player holding a trigger
    ///     down. Repeated <c>WeaponBurstFired</c> updates are what mark each burst. Whether the client
    ///     draws that the way it draws a player doing it is an N5 question.
    ///
    ///     The end is sent rather than a cancel because <c>WeaponBurstCancelled</c> has never gone out
    ///     from here and what it does to a remote character hasn't been checked.
    /// </summary>
    private static void StopFiring(CharacterEntity npc, AIState state, ulong now)
    {
        if (!state.WeaponHot)
        {
            return;
        }

        state.RoundsLeftInBurst = 0;
        state.WeaponHot = false;
        npc.SetFireEnd((uint)now);
    }
}

/// <summary>
///     Body orientation from an aim direction.
/// </summary>
public static class Facing
{
    /// <summary>
    ///     A quarter turn. The client's characters carry their local forward along +Y, so facing a
    ///     bearing means yawing to a quarter turn short of it. Measured, not chosen: the 2016 capture's
    ///     pose stream puts every rig retail ever oriented — 40+ monster types and the clients' own
    ///     remote players alike — at exactly this offset from both travel and aim under a +X reading
    ///     (`CaptureReplay --facing`, run 2026-08-15, spreads under 20° on everything that walks).
    /// </summary>
    private const float ForwardAxisOffset = MathF.PI / 2f;

    /// <summary>
    ///     The orientation to store on a character so it stands looking along <paramref name="direction"/>,
    ///     or <paramref name="current"/> when the direction is straight up or down and says nothing about
    ///     which way to stand. Yaw only — characters don't lean back to shoot upwards.
    ///
    ///     The stored orientation is the inverse of the world rotation — never in doubt, since
    ///     <c>GetProjectileOrigin</c> and the physics engine both invert it on the way out. Local forward
    ///     is +Y, per <see cref="ForwardAxisOffset"/>; it was +X until 2026-08-15, a guess N1 seemed to
    ///     confirm because a humanoid's rendered body follows the aim vector closely enough to mask a
    ///     wrong quaternion. A creature's doesn't, which was the whole of CLIENT-3: the sideways Aranha
    ///     was the one rig honest enough to show the convention error every character had. Player
    ///     orientations never pass through here — clients author them in their own convention, which is
    ///     exactly the one this now matches. This is the only place in the server that derives an
    ///     orientation from a direction, so it's the thing to reuse rather than re-derive.
    /// </summary>
    public static Quaternion Towards(Vector3 direction, Quaternion current)
    {
        if (direction.X == 0f && direction.Y == 0f)
        {
            return current;
        }

        var yaw = MathF.Atan2(direction.Y, direction.X) - ForwardAxisOffset;
        return Quaternion.Inverse(Quaternion.CreateFromAxisAngle(Vector3.UnitZ, yaw));
    }
}
