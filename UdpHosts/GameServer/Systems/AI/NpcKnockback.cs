using System;
using System.Numerics;
using GameServer.Entities.Character;

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

        var next = Step(npc.Position, state, elapsedSeconds, shard.Physics.TryGetGroundHeight);
        npc.SetPosition(next);
        shard.Physics.UpdateEntity(npc);
        return true;
    }

    /// <summary>
    ///     Where the NPC is after one tick of flight. Ends the flight on landing: on the ground when it is known, and
    ///     otherwise back at the height it was launched from.
    /// </summary>
    internal static Vector3 Step(Vector3 position, AIState state, float elapsedSeconds, GroundProbe ground)
    {
        var velocity = state.KnockbackVelocity;
        var next = position + (velocity * elapsedSeconds);
        velocity.Z -= Gravity * elapsedSeconds;
        state.KnockbackVelocity = velocity;
        state.KnockbackSecondsLeft = MathF.Max(0f, state.KnockbackSecondsLeft - elapsedSeconds);

        var floor = ground(next, out var groundZ) ? groundZ : state.KnockbackLaunchZ;
        if (velocity.Z < 0f && next.Z <= floor)
        {
            next.Z = floor;
            state.KnockbackSecondsLeft = 0f;
        }

        return next;
    }

    public delegate bool GroundProbe(Vector3 at, out float groundZ);
}
