using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Movement;

public class OrientationLockCommand : Command, ICommand
{
    private OrientationLockCommandDef Params;

    public OrientationLockCommand(OrientationLockCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var target = context.Self;

        if (target is CharacterEntity)
        {
            context.Actives.Add(this, null);
        }

        return true;
    }

    // Known gap: this sends ForcedMovementCancelled for a movement the server never said had
    // started. The ForcedMovement event (GSS 5/113) only ever goes out for spawns and teleports, and
    // ForcedMovementCancelled.CommandId is an apt::BaseCommandDef id matching a uint in the same
    // position of ForcedMovementData, so a cancel looks like it's meant to name a movement the client
    // already knows about. Sending the start was tried and dropped (D5f in
    // Docs/In-Game-Tests/Charge-Camera.md): it fixed nothing, and every OrientationLock that matters
    // has an SDB duration of 0, so the start needs an end time nobody can derive. Guessing one risks
    // a real aim clamp lasting that long on some ability where it isn't masked by a camera. Fill
    // this in from a capture, not from a guess.
    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        var target = context.Self;
        if (target is CharacterEntity { IsPlayerControlled: true } character)
        {
            Logger.Information("{Command} Sending ForcedMovementCancelled {CommandId}", nameof(OrientationLockCommand), Params.Id);
            var player = character.Player;
            var message = new ForcedMovementCancelled
            {
                CommandId = Params.Id,
                ShortTime = context.Shard.CurrentShortTime,
            };
            player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
        }
    }
}
