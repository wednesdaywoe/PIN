using System.Numerics;
using AeroMessages.GSS.V66;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities.Character;

namespace GameServer.Systems.AI;

/// <summary>
///     Streams an NPC's pose to the clients that can see it.
///
///     A player's movement reaches other clients as a <c>CurrentPoseUpdate</c>, forwarded by
///     <c>MovementRelay</c> for every input its own client sends. An NPC had no equivalent.
///     <c>EntityManager.FlushChanges</c> deliberately skips <c>Character_MovementView</c> — "those
///     changes are basically handled entirely by CurrentPoseUpdate" — which was true for as long as
///     nothing except a client ever moved a character, and stopped being true the moment the server
///     started walking monsters around.
///
///     So the walk happened only on the server. A client held whatever position came in its scope-in
///     keyframe, and the next time it heard anything was the checksum reconciliation in
///     <c>EntityManager</c> noticing a mismatch and sending a fresh keyframe — at which point the
///     monster crossed the whole distance in one frame. Server-side steering was smooth throughout;
///     nothing in the log could have shown this, because the log only ever saw the server's own copy.
///
///     Sending only on change keeps an idle monster silent rather than repeating itself twenty times
///     a second, and makes a standing NPC cost nothing.
/// </summary>
public static class NpcPose
{
    /// <summary>
    ///     One centimetre, squared. Below this is arithmetic noise in the steering rather than travel:
    ///     a running NPC covers about 30cm per tick, so this cannot suppress a real step.
    /// </summary>
    private const float MovedEnough = 0.01f * 0.01f;

    public static void Publish(IShard shard, CharacterEntity npc, AIState state)
    {
        if (!Changed(npc, state))
        {
            return;
        }

        state.LastSentPosition = npc.Position;
        state.LastSentOrientation = npc.Orientation;
        state.LastSentAim = npc.AimDirection;
        state.LastSentMovementState = npc.MovementState;
        state.HasSentPose = true;

        shard.EntityMan.SendToScoped(npc, new CurrentPoseUpdate
        {
            Data = new CurrentPoseUpdateData
            {
                // Zero means every field is written in full. The alternates the flags select are
                // narrower deltas against the previous pose, which suit the client's own uplink at
                // its much higher rate; at 20 a second an NPC is better off self-describing.
                Flags = 0x00,

                // The client interpolates between poses rather than snapping to each one, and this is
                // what tells it how far apart they were. A stale or repeated value here is the
                // difference between walking and stuttering.
                ShortTime = shard.CurrentShortTime,
                UnkAlwaysPresent = 0x79,
                MovementState = (ushort)npc.MovementState,
                Position = npc.Position,
                Rotation = npc.Orientation,
                Aim = npc.AimDirection,
            }
        });
    }

    private static bool Changed(CharacterEntity npc, AIState state)
    {
        if (!state.HasSentPose)
        {
            return true;
        }

        return Vector3.DistanceSquared(npc.Position, state.LastSentPosition) > MovedEnough
               || npc.Orientation != state.LastSentOrientation
               || npc.AimDirection != state.LastSentAim
               || npc.MovementState != state.LastSentMovementState;
    }
}
