using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetClearCommand : Command, ICommand
{
    private TargetClearCommandDef Params;

    public TargetClearCommand(TargetClearCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.Current == 1)
        {
            context.Targets.Clear();
        }

        if (Params.Former == 1)
        {
            context.FormerTargets.Clear();
        }

        return true;
    }
}