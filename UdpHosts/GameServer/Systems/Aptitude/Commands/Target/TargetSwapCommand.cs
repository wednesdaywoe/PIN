using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetSwapCommand : Command, ICommand
{
    private TargetSwapCommandDef Params;

    public TargetSwapCommand(TargetSwapCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (apt::TargetSwapCommand): swaps first, then clears
        (context.Targets, context.FormerTargets) = (context.FormerTargets, context.Targets);

        if (Params.ClearCurrent == 1)
        {
            context.Targets.Clear();
        }

        if (Params.ClearFormer == 1)
        {
            context.FormerTargets.Clear();
        }

        return true;
    }
}