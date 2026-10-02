using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetSelfCommand : Command, ICommand
{
    private TargetSelfCommandDef Params;

    public TargetSelfCommand(TargetSelfCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (apt::TargetSelfCommand): adds self if it is not a target yet
        if (!context.Targets.Contains(context.Self))
        {
            context.Targets.Push(context.Self);
        }

        return true;
    }
}