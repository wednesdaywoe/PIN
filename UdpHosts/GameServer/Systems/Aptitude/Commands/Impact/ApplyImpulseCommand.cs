using System;
using System.Numerics;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Impact;

/// <summary>
///     Launches each target: Meteor Strike's jump and the shove on landing, Crater's slam, Evasive Maneuver's dash.
///     The direction is the client's own (tfApplyImpulseCommand 0xebace0, direction at 0xebafe0): a base direction is
///     pitched up by Loftangle degrees and turned by Yawangle degrees, scaled to Speed (negative reverses it), and with
///     Alongvelocity the target's current velocity is added on. The client only runs this itself when AllowPrediction is
///     set, and then only on its own player; everything else is the server's to send.
/// </summary>
public class ApplyImpulseCommand : Command, ICommand
{
    private const float MovingSpeedSquared = 0.01f;

    private ApplyImpulseCommandDef Params;

    public ApplyImpulseCommand(ApplyImpulseCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var speed = AbilitySystem.RegistryOp(context.Register, Params.Speed, (Operand)Params.SpeedRegop);
        if (speed == 0f)
        {
            return true;
        }

        foreach (IAptitudeTarget target in context.Targets)
        {
            if (target is not CharacterEntity character || !character.IsAlive)
            {
                continue;
            }

            if (character.ImmunePhysics)
            {
                Logger.Debug("{Command} {CommandId} doesn't move {Target}, which an effect makes immune to physics", nameof(ApplyImpulseCommand), Params.Id, character);
                continue;
            }

            var isUser = ReferenceEquals(character, context.Initiator);
            var velocity = Impulse(context, character, isUser, speed);

            if (character.IsPlayerControlled)
            {
                if (Params.AllowPrediction == 1 && isUser)
                {
                    // The client has already launched its own player
                    continue;
                }

                SendToPlayer(context, character, velocity);
            }
            else
            {
                context.Shard.AI.KnockBack(character, velocity);
            }

            Logger.Debug("{Command} {CommandId} launches {Target} at {Velocity}", nameof(ApplyImpulseCommand), Params.Id, character, velocity);
        }

        return true;
    }

    private Vector3 Impulse(Context context, CharacterEntity character, bool isUser, float speed)
    {
        var current = character.Velocity;
        var facing = Flat(character.AimDirection);

        // Base direction. With Alongvelocity, the way the target is moving, or facing when it stands still, as the client
        // does. Without, the client passes a direction this code can't see. Taken here as the user's aim, pitch included,
        // because Meteor Strike's dive (45 m/s, no loft) only makes sense along it; and for anyone else, away from where
        // the ability struck, as ForcePush does. Both are guesses.
        Vector3 direction;
        if (Params.Alongvelocity == 1)
        {
            direction = (current.X * current.X) + (current.Y * current.Y) > MovingSpeedSquared ? new Vector3(current.X, current.Y, 0f) : facing;
        }
        else if (isUser)
        {
            direction = character.AimDirection.LengthSquared() > 1e-6f ? Vector3.Normalize(character.AimDirection) : facing;
        }
        else
        {
            var source = context.InitPosition != Vector3.Zero ? context.InitPosition : context.Initiator?.Position ?? character.Position;
            direction = Flat(character.Position - source);
            if (direction == Vector3.Zero)
            {
                direction = facing;
            }
        }

        var loft = Params.Loftangle * MathF.PI / 180f;
        var yaw = Params.Yawangle * MathF.PI / 180f;

        var horizontal = MathF.Sqrt((direction.X * direction.X) + (direction.Y * direction.Y));
        if (horizontal > 1e-4f)
        {
            var scale = ((MathF.Cos(loft) * horizontal) - (direction.Z * MathF.Sin(loft))) / horizontal;
            direction = new Vector3(direction.X * scale, direction.Y * scale, (direction.Z * MathF.Cos(loft)) + (MathF.Sin(loft) * horizontal));
        }

        direction = new Vector3(
            (direction.X * MathF.Cos(yaw)) - (direction.Y * MathF.Sin(yaw)),
            (direction.Y * MathF.Cos(yaw)) + (direction.X * MathF.Sin(yaw)),
            direction.Z);

        var length = direction.Length();
        var velocity = length > 1e-6f ? direction * (speed / length) : Vector3.Zero;
        return Params.Alongvelocity == 1 ? velocity + current : velocity;
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.Z = 0f;
        return v.LengthSquared() > 1e-6f ? Vector3.Normalize(v) : Vector3.Zero;
    }

    /// <summary>
    ///     Forced movement type 5 is what the client's own impulse sets (0xd30ed0): a velocity and a start and end time,
    ///     which must differ. The end is Duration after the start, as the client computes it.
    /// </summary>
    private void SendToPlayer(Context context, CharacterEntity character, Vector3 velocity)
    {
        var start = context.Shard.CurrentTime + 1;
        var message = new ForcedMovement
        {
            Data = new AeroMessages.GSS.V66.ForcedMovementData
            {
                Type = 5,
                Params5 = new AeroMessages.GSS.V66.ForcedMovementType5Params
                {
                    Velocity = velocity,
                    Time1 = start,
                    Time2 = start + Math.Max(Params.Duration, 1u),
                    Unk2 = 0
                }
            },

            ShortTime = context.Shard.CurrentShortTime,
        };
        character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
    }
}
