using System.Numerics;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Impact;

/// <summary>
///     Throws each target away from where the ability struck. The client's tfForcePushCommand (0xebb280) only queues
///     the knocked-back animation, so the throw is PIN's: Strength is the launch speed in metres per second (10 to 30 in
///     most of the 632 defs), and Loft tips it upward, 0 flat along the ground and 1 at 45 degrees.
/// </summary>
public class ForcePushCommand : Command, ICommand
{
    private ForcePushCommandDef Params;

    public ForcePushCommand(ForcePushCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Unread: Falloff (0 in 364 defs, 5 to 60 in the rest; whether it is a distance or a rate is unknown) and
        // DoAnimation (the client plays that itself).
        var strength = AbilitySystem.RegistryOp(context.Register, Params.Strength, (Operand)Params.StrengthRegop);
        if (strength <= 0f)
        {
            // Usually a module without the knockback stat the chain reads (Crater 86369 has no Crater Knockback Potency)
            Logger.Debug("{Command} {CommandId} pushes no one: strength {Strength}", nameof(ForcePushCommand), Params.Id, strength);
            return true;
        }

        // ImpactPosition: away from where the ability started or landed; otherwise away from whoever used it
        var source = Params.ImpactPosition == 1 || context.Initiator == null ? context.InitPosition : context.Initiator.Position;

        foreach (IAptitudeTarget target in context.Targets)
        {
            if (target is not CharacterEntity character || !character.IsAlive)
            {
                continue;
            }

            if (character.ImmunePhysics)
            {
                Logger.Debug("{Command} {CommandId} doesn't move {Target}, which an effect makes immune to physics", nameof(ForcePushCommand), Params.Id, character);
                continue;
            }

            var velocity = Launch(source, character.Position, strength, Params.Loft);

            if (character.IsPlayerControlled)
            {
                PushPlayer(context, character, velocity);
            }
            else
            {
                context.Shard.AI.KnockBack(character, velocity);
            }
        }

        return true;
    }

    internal static Vector3 Launch(Vector3 source, Vector3 target, float strength, float loft)
    {
        var away = target - source;
        away.Z = 0f;
        away = away.LengthSquared() > 1e-4f ? Vector3.Normalize(away) : Vector3.Zero;

        var direction = away + new Vector3(0f, 0f, loft);
        return direction.LengthSquared() > 1e-6f ? Vector3.Normalize(direction) * strength : Vector3.Zero;
    }

    /// <summary>
    ///     A player's own client moves them, so the push is a message to it. Untested: no player has been pushed by an
    ///     NPC since this was written. It used to send a fixed 45 m/s straight up.
    /// </summary>
    private static void PushPlayer(Context context, CharacterEntity character, Vector3 push)
    {
        var velocity = new Vector3(character.Velocity[0], character.Velocity[1], character.Velocity[2]) + push;

        var message = new ForcedMovement
        {
            Data = new AeroMessages.GSS.V66.ForcedMovementData
            {
                Type = 5,
                Params5 = new AeroMessages.GSS.V66.ForcedMovementType5Params
                {
                    Velocity = velocity,
                    Time1 = context.Shard.CurrentTime + 19,
                    Time2 = context.Shard.CurrentTime + 20,
                    Unk2 = 0
                }
            },

            ShortTime = context.Shard.CurrentShortTime,
        };
        character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
    }
}
