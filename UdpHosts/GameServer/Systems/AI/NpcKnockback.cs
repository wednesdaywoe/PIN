using System;
using System.Numerics;
using GameServer.Entities.Character;
using Serilog;

namespace GameServer.Systems.AI;

/// <summary>
///     Throws an NPC along an arc when an ability pushes it (ForcePush). A player's client moves its own character, so a
///     push on a player is a message to that client; nothing moves an NPC but the server, so its flight is stepped here,
///     and <see cref="NpcPose" /> sends the positions like any other movement. While in the air it neither walks nor shoots.
///
///     The flight itself is PIN's. The client's ForcePush only plays the knocked-back animation, and the original
///     server's physics never shipped.
/// </summary>
public static class NpcKnockback
{
    /// <summary>
    ///     Heavier than Earth's, chosen by feel: Healing Wave's push (12 m/s, lift 0.6) carries about 6 m and lasts
    ///     about two thirds of a second, rather than the 13 m and 1.3 s real gravity would give.
    /// </summary>
    public const float Gravity = 20f;

    /// <summary>Longest a flight can last, so one that never finds the ground still ends.</summary>
    public const float MaxFlightSeconds = 3f;

    /// <summary>
    ///     How far above the NPC the ground probe starts. Enough for a slope rising under a moving body; small enough
    ///     that beside a wall the probe starts below its top, which a 30 m probe did not (a Fiend thrown at a cliff
    ///     landed on the clifftop).
    /// </summary>
    public const float GroundHeadroom = 1f;

    /// <summary>Half a body's width, kept between the NPC's middle and a wall.</summary>
    public const float BodyRadius = 0.5f;

    private static readonly float[] WallProbeHeights = [0.5f, 1.2f];

    /// <summary>Extra ticks the landing pose is sent, a sixth of a second at the AI's 50 ms tick.</summary>
    private const int LandingPoseRepeats = 3;

    public static void Start(CharacterEntity npc, AIState state, Vector3 velocity)
    {
        state.KnockbackVelocity = velocity;
        state.KnockbackLaunchZ = npc.Position.Z;
        state.KnockbackSecondsLeft = MaxFlightSeconds;
    }

    /// <summary>
    ///     Moves the NPC one tick along its flight. False when it isn't knocked back, so the caller carries on as usual.
    /// </summary>
    public static bool Tick(IShard shard, CharacterEntity npc, AIState state, float elapsedSeconds)
    {
        if (!state.KnockedBack)
        {
            return false;
        }

        var physics = shard.Physics;
        var next = Step(
            npc.Position,
            state,
            elapsedSeconds,
            (Vector3 at, out float groundZ) => physics.TryGetGroundBelow(at, GroundHeadroom, out groundZ),
            physics.TryHitWorld);
        npc.SetPosition(next);
        shard.Physics.UpdateEntity(npc);

        if (!state.KnockedBack)
        {
            state.ForcedPoseTicks = LandingPoseRepeats;

            // Walking finds its ground from 30 m up and this from 1 m up. Where they differ (under an overhang),
            // the NPC will be lifted to the higher surface as soon as it walks.
            var walkGround = physics.TryGetGroundHeight(next, out var walkZ) ? walkZ.ToString("0.00") : "none";
            Log.Debug("NPC {Npc} landed at {Position}; walking would stand it at Z {WalkGround}", npc.EntityId, next, walkGround);
        }

        return true;
    }

    /// <summary>
    ///     Where the NPC is after one tick of flight. It stops short of a wall and drops from there, and lands on the
    ///     ground under its feet.
    /// </summary>
    internal static Vector3 Step(Vector3 position, AIState state, float elapsedSeconds, GroundProbe ground, WallProbe wall)
    {
        var velocity = state.KnockbackVelocity;
        var move = new Vector3(velocity.X, velocity.Y, 0f) * elapsedSeconds;
        var moveLength = move.Length();

        if (moveLength > 1e-5f)
        {
            // Rays at knee and chest height, reaching a body's width past the step, so it stops with its side
            // against the wall rather than its middle inside it
            var heading = move / moveLength;
            var allowed = moveLength;
            foreach (var height in WallProbeHeights)
            {
                if (wall(position + new Vector3(0f, 0f, height), heading, moveLength + BodyRadius, out var distance))
                {
                    allowed = MathF.Min(allowed, MathF.Max(0f, distance - BodyRadius));
                }
            }

            if (allowed < moveLength)
            {
                move = heading * allowed;
                velocity.X = 0f;
                velocity.Y = 0f;
            }
        }

        var next = position + move + new Vector3(0f, 0f, velocity.Z * elapsedSeconds);
        velocity.Z -= Gravity * elapsedSeconds;
        state.KnockbackVelocity = velocity;
        state.KnockbackSecondsLeft = MathF.Max(0f, state.KnockbackSecondsLeft - elapsedSeconds);

        // Probed from the NPC's own height, not the destination's: a fast drop would otherwise start below a ledge
        var probeFrom = new Vector3(next.X, next.Y, MathF.Max(next.Z, position.Z));
        // With no ground found (a hole in the zone's collision) it comes down at its launch height, but never lifted up to it
        var floor = ground(probeFrom, out var groundZ) ? groundZ : MathF.Min(state.KnockbackLaunchZ, position.Z);
        if (next.Z <= floor)
        {
            // Never below the surface, and the flight is over once it comes down onto it
            next.Z = floor;
            if (velocity.Z < 0f)
            {
                state.KnockbackSecondsLeft = 0f;
            }
        }

        return next;
    }

    public delegate bool GroundProbe(Vector3 at, out float groundZ);

    public delegate bool WallProbe(Vector3 origin, Vector3 direction, float maxDistance, out float distance);
}
