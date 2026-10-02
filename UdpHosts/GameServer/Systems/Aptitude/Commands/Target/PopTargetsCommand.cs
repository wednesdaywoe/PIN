using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class PopTargetsCommand : Command, ICommand
{
    private PopTargetsCommandDef Params;

    public PopTargetsCommand(PopTargetsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (apt::PopTargetsCommand): restores from the target stack in the reverse order of PushTargets,
        // failing when the stack is empty
        if (Params.Former == 1)
        {
            if (!context.TargetStack.TryPop(out var former))
            {
                return false;
            }

            context.FormerTargets = former;
        }

        if (Params.Current == 1)
        {
            if (!context.TargetStack.TryPop(out var current))
            {
                return false;
            }

            context.Targets = current;
        }

        return true;
    }
}