using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class PeekTargetsCommand : Command, ICommand
{
    private PeekTargetsCommandDef Params;

    public PeekTargetsCommand(PeekTargetsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (apt::PeekTargetsCommand): copies the top of the target stack without popping it,
        // failing when the stack is empty
        if (Params.Former == 1)
        {
            if (!context.TargetStack.TryPeek(out var former))
            {
                return false;
            }

            context.FormerTargets = new AptitudeTargets(former);
        }

        if (Params.Current == 1)
        {
            if (!context.TargetStack.TryPeek(out var current))
            {
                return false;
            }

            context.Targets = new AptitudeTargets(current);
        }

        return true;
    }
}