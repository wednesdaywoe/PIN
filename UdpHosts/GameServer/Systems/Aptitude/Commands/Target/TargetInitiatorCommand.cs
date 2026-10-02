using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetInitiatorCommand : Command, ICommand
{
    private TargetInitiatorCommandDef Params;

    public TargetInitiatorCommand(TargetInitiatorCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (apt::TargetInitiatorCommand): adds initiator if it is not a target yet
        if (!context.Targets.Contains(context.Initiator))
        {
            context.Targets.Push(context.Initiator);
        }

        return true;
    }
}