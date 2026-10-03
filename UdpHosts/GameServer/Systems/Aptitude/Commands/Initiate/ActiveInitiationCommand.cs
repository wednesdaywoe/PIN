using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Initiate;

public class ActiveInitiationCommand : Command, ICommand
{
    public ActiveInitiationCommand(ActiveInitiationCommandDef par)
    : base(par)
    {
    }

    public bool Execute(Context context)
    {
        // As the client (apt::ActiveInitiationCommand, FUN_00bc0ec0): the first pass marks the context initiated, sets the
        // activation time to the init time and snapshots the initiator; later passes succeed without touching it.
        // Not done: the client first asks the context's tfInputState and, while it is unset, returns its third result
        // (neither success nor failure), which holds a staged or calldown activation until the input arrives. The server
        // only runs a chain after the client has sent ActivateAbility, by which point that wait is over.
        // Also not done: the client snapshots aim and velocity alongside the position; nothing here reads them yet.
        if (context.Initiated)
        {
            return true;
        }

        context.Initiated = true;
        context.ActivationTime = context.InitTime;
        context.InitPosition = context.Initiator.Position;
        return true;
    }
}
