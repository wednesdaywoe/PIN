using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class PushTargetsCommand : Command, ICommand
{
    private PushTargetsCommandDef Params;

    public PushTargetsCommand(PushTargetsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (Params.Former == 1 && context.FormerTargets.Count > 0)
        {
            // assuming push == saving for later, this shouldnt occur and it doesnt in 1962
            Logger.Debug("[PushTargets] Former = 1, FormerTargets count {count}", context.FormerTargets.Count);
        }

        // Saves a copy for PopTargets and keeps working on the current targets. The SDB relies on that, e.g.
        // PushTargets > TargetTrim > ImpactApplyEffect > PopTargets in 570262, and PushTargets > PeekTargets.
        if (Params.Current == 1)
        {
            context.FormerTargets = new AptitudeTargets(context.Targets);
        }

        return true;
    }
}