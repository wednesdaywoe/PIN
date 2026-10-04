using System.Numerics;
using AeroMessages.GSS.V66;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Movement;

/// <summary>
///     Moves the user to where the ability happened (InitPosition): where its projectile landed. A server-only command
///     whose defs shipped as bare ids, so this reading comes from where it is used: Assassinate fires an invisible
///     "Assassinate Teleport Ammo" (ability 41964 on landing is this command alone), and the client plays the blink
///     but leaves the moving to the server. Stops <see cref="StopShort" /> m before the landing point so a projectile
///     that struck a monster doesn't put the user inside it. Players only, sent as forced movement type 1 like the
///     admin <c>tp</c>.
/// </summary>
public class TeleportCommand : Command, ICommand
{
    public const float StopShort = 1.5f;

    private TeleportCommandDef Params;

    public TeleportCommand(TeleportCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Initiator is not CharacterEntity { IsPlayerControlled: true, IsAlive: true } character)
        {
            Logger.Debug("{Command} {CommandId}: {Initiator} is not a living player, not teleported", nameof(TeleportCommand), Params.Id, context.Initiator);
            return true;
        }

        var destination = Destination(character.Position, context.InitPosition);
        if (destination == character.Position)
        {
            return true;
        }

        Logger.Debug("{Command} {CommandId}: teleporting {Character} from {From} to {To}", nameof(TeleportCommand), Params.Id, character, character.Position, destination);
        character.SetPosition(destination);
        character.MarkPlacedAt(destination);
        var forcedMove = new ForcedMovement
        {
            Data = new ForcedMovementData
            {
                Type = 1,
                Params1 = new ForcedMovementType1Params { Position = destination, Direction = character.AimDirection, Velocity = Vector3.Zero, Time = context.Shard.CurrentTime + 1 },
            },
            ShortTime = context.Shard.CurrentShortTime,
        };
        character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(forcedMove, character.EntityId);
        return true;
    }

    /// <summary><paramref name="target" />, moved <see cref="StopShort" /> m back toward <paramref name="from" /> across the ground.</summary>
    internal static Vector3 Destination(Vector3 from, Vector3 target)
    {
        var back = from - target;
        back.Z = 0f;
        var length = back.Length();
        if (length <= StopShort)
        {
            return from;
        }

        return target + (back / length * StopShort);
    }
}
