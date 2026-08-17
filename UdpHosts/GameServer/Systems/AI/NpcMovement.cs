using System;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Systems.Combat;
using Serilog;

namespace GameServer.Systems.AI;

/// <summary>
///     Walks an NPC toward the target <see cref="TargetSelection"/> picked, and home again when it hasn't
///     got one. This is the piece a player's client would be doing: movement is client-authoritative for
///     a player, so nothing in the server had ever decided where a character should be before this.
///
///     What it isn't: there is no pathing and no obstacle avoidance. It steers straight at the
///     destination and walks into whatever stands on that line, which the milestone accepted up front —
///     a monster that shoots back is worth more than a monster that walks around a rock, and pathing
///     wants a navmesh the server has no map to build one from.
/// </summary>
public static class NpcMovement
{
    /// <summary>
    ///     How close an NPC tries to get to what it is shooting at. Invented, like the perception
    ///     numbers next door in <see cref="TargetSelection"/> and tracked with them as DATA-10 — nothing
    ///     in <c>dbmonster</c> carries a preferred engagement distance, and the behaviour trees that
    ///     would have (<c>Behavior</c>, <c>BehaviorOffensive</c>) are names of assets PIN doesn't load.
    ///
    ///     Chosen as comfortably inside the perception radius, so an NPC that notices you at the edge of
    ///     it has an obvious distance to close and you can see it decide to. Still holds after that
    ///     radius narrowed from 40m to 25m — 12m leaves 13m of approach to watch.
    /// </summary>
    public const float PreferredStandoff = 12f;

    /// <summary>
    ///     How much further than <see cref="PreferredStandoff"/> a target has to get before the NPC
    ///     bothers to close again. Pure hysteresis: without it a target strafing on the spot has an NPC
    ///     stepping back and forth to hold an exact separation, and every one of those steps is a
    ///     movement update on the wire.
    /// </summary>
    public const float StandoffBand = 3f;

    /// <summary>
    ///     Fraction of a weapon's reach an NPC will stand at when that reach is shorter than
    ///     <see cref="PreferredStandoff"/> — a Melded Aranha's 5m claw has to be closed to or it never
    ///     lands. Kept short of the reach itself so a target drifting a metre doesn't drop the NPC
    ///     straight out of range.
    /// </summary>
    public const float ReachFraction = 0.8f;

    /// <summary>
    ///     How far from <see cref="AIState.Home"/> an NPC will chase before giving up and walking back.
    ///     Invented, and the number that stops a monster following a player across the zone.
    ///
    ///     Deliberately wider than the perception radius and narrower than a chase that could drag a
    ///     whole camp along behind one player. Left at 50m when perception narrowed to 25m: it bounds
    ///     how far a fight can wander from where it started, which is a question about the zone rather
    ///     than about eyesight. It bounds where an NPC can be, which
    ///     <see cref="TargetSelection.LeashRange"/> does not: that one is measured from the NPC, so it
    ///     travels with the NPC and never stops a chase.
    /// </summary>
    public const float ChaseLeash = 50f;

    /// <summary>How close to home counts as home.</summary>
    public const float HomeArrival = 1.5f;

    /// <summary>
    ///     Movement state for a character the client should draw running. The high nibble is
    ///     <see cref="Movestate.Running"/> and the low byte carries <see cref="MovementFlags.Movement"/>,
    ///     which is how <c>MovementStateContainer</c> reads the pair apart. Derived from those two enums
    ///     rather than seen on the wire — NET-12 has the packing itself down as unconfirmed — so an NPC
    ///     that slides along in an idle pose means this value is wrong, not that it isn't moving.
    /// </summary>
    private const short RunningState = 0x2004;

    /// <summary>What every character in the server has sat in until now, and still the resting state.</summary>
    private const short StandingState = 0x1000;

    public static void Tick(IShard shard, CharacterEntity npc, AIState state, float elapsedSeconds, ILogger logger)
    {
        if (!state.HasHome)
        {
            state.Home = npc.Position;
            state.HasHome = true;
        }

        var destination = ChooseDestination(shard, npc, state, logger, out var stopWithin, out var chasing);
        var distance = Steering.FlatDistance(npc.Position, destination);

        // Start closing only once the gap is worth crossing; keep closing until it's fully shut.
        if (state.Moving)
        {
            state.Moving = distance > stopWithin;
        }
        else
        {
            state.Moving = distance > stopWithin + StandoffBand;
        }

        if (!state.Moving)
        {
            if (state.Returning && !chasing)
            {
                state.Returning = false;
                logger.Debug("NPC {Npc} is home", npc.EntityId);
            }

            Halt(npc);
            return;
        }

        var speed = ResolveSpeed(npc, state).Run;
        if (!Steering.TryStep(npc.Position, destination, stopWithin, speed, elapsedSeconds, out var next, shard.Physics.TryGetGroundHeight))
        {
            Halt(npc);
            return;
        }

        if (npc.MovementState != RunningState)
        {
            npc.SetMovementState(RunningState);
            logger.Debug(
                "NPC {Npc} sets off {Where} at {Speed}m/s, {Distance}m away, stopping at {StopWithin}m",
                npc.EntityId,
                chasing ? $"after {state.CurrentTargetId}" : "home",
                speed,
                distance,
                stopWithin);
        }

        // An NPC with a target is turned to face it by NpcCombat, which runs after this and gets the
        // last word. One walking home has nothing pointing it anywhere, so it faces its own travel.
        if (!chasing)
        {
            FaceTravel(npc, next - npc.Position);
        }

        npc.SetPosition(next);

        // Without this the collider stays where the NPC used to be, and every raycast in the server
        // reads that stale body: shots at the NPC would hit thin air where it was, and its own line of
        // sight would be traced from a position it has left.
        shard.Physics.UpdateEntity(npc);
    }

    /// <summary>
    ///     Stops an NPC that has died or despawned pretending to run. The tick loop skips the dead, so
    ///     like <c>NpcCombat.Disengage</c> this has to be called for them from outside it.
    /// </summary>
    public static void Halt(CharacterEntity npc)
    {
        if (npc.MovementState != StandingState)
        {
            npc.SetMovementState(StandingState);
        }
    }

    private static void FaceTravel(CharacterEntity npc, Vector3 travelled)
    {
        var flat = new Vector3(travelled.X, travelled.Y, 0f);
        if (flat.LengthSquared() <= 0f)
        {
            return;
        }

        var direction = Vector3.Normalize(flat);
        npc.SetAim(direction, Facing.Towards(direction, npc.Orientation));
    }

    /// <summary>
    ///     Where to walk and how close to get. A target it may still chase, otherwise home.
    /// </summary>
    private static Vector3 ChooseDestination(IShard shard, CharacterEntity npc, AIState state, ILogger logger, out float stopWithin, out bool chasing)
    {
        var target = ResolveTarget(shard, state);

        if (target == null)
        {
            state.HasTargetFooting = false;
        }
        else
        {
            // A target's Z is only a ground height while the target is standing on the ground, so take
            // it when it is and keep the last one when it isn't. A player jetpacking overhead is still
            // chased across the ground; what stops is the NPC rising to meet them. Anything that isn't
            // a character (a thumper) is planted, and its position is its footing.
            if (target is not CharacterEntity character || !character.IsAirborne)
            {
                state.TargetFooting = target.Position;
                state.HasTargetFooting = true;
            }
            else if (state.HasTargetFooting)
            {
                state.TargetFooting = new Vector3(target.Position.X, target.Position.Y, state.TargetFooting.Z);
            }
            else
            {
                state.TargetFooting = new Vector3(target.Position.X, target.Position.Y, npc.Position.Z);
                state.HasTargetFooting = true;
            }
        }

        // Once leashed out it walks all the way home before it will chase anything again, or it turns
        // round at the leash and comes straight back out.
        if (state.Returning || Steering.FlatDistance(npc.Position, state.Home) > ChaseLeash)
        {
            if (!state.Returning)
            {
                state.Returning = true;
                logger.Debug(
                    "NPC {Npc} has chased {Target} {Distance}m from home and is going back",
                    npc.EntityId,
                    state.CurrentTargetId,
                    Steering.FlatDistance(npc.Position, state.Home));
            }

            stopWithin = HomeArrival;
            chasing = false;
            return state.Home;
        }

        if (target == null)
        {
            stopWithin = HomeArrival;
            chasing = false;
            return state.Home;
        }

        stopWithin = StandoffFor(state);
        chasing = true;
        return state.TargetFooting;
    }

    /// <summary>
    ///     How close this NPC wants to be, which is <see cref="PreferredStandoff"/> unless its weapon
    ///     can't reach that far. The window is whatever <c>NpcCombat</c> last resolved; before it has
    ///     resolved anything the preferred distance stands, which is the right guess for a weapon nobody
    ///     has looked at yet.
    /// </summary>
    private static float StandoffFor(AIState state)
    {
        var window = state.CachedWindow;
        if (!window.Usable || window.Range <= 0f)
        {
            return PreferredStandoff;
        }

        return MathF.Min(PreferredStandoff, window.Range * ReachFraction);
    }

    private static MoveSpeed ResolveSpeed(CharacterEntity npc, AIState state)
    {
        var typeId = npc.StaticInfo.CharacterTypeId;
        if (state.CachedSpeedTypeId == typeId && typeId != 0)
        {
            return state.CachedSpeed;
        }

        state.CachedSpeedTypeId = typeId;
        state.CachedSpeed = MoveSpeed.Resolve(typeId);
        return state.CachedSpeed;
    }

    private static IDamageable ResolveTarget(IShard shard, AIState state)
    {
        if (state.CurrentTargetId is not ulong targetId
            || !shard.Entities.TryGetValue(targetId, out var entity)
            || entity is not IDamageable target
            || !target.IsAlive)
        {
            return null;
        }

        return target;
    }
}
