using System.Linq;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetByEffectCommand : Command, ICommand
{
    private TargetByEffectCommandDef Params;

    public TargetByEffectCommand(TargetByEffectCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (tfTargetByEffectCommand): with FilterList the targets are filtered in place,
        // without it this only checks whether any target matches
        if (Params.FilterList == 1)
        {
            context.Targets.RemoveAll(target => !Matches(context, target));

            return Params.FailNoTargets == 0 || context.Targets.Count > 0;
        }

        return Params.FailNoTargets == 0 || context.Targets.Any(target => Matches(context, target));
    }

    private bool Matches(Context context, IAptitudeTarget target)
    {
        foreach (EffectState active in target.GetActiveEffects())
        {
            if (active == null)
            {
                continue;
            }

            var condition = Params.EffectId == active.Effect.Id && active.Stacks >= Params.StackCount;
            if (Params.Negate == 1)
            {
                condition = !condition;
            }

            if (condition)
            {
                if (Params.SameInitiator == 1)
                {
                    return Params.Negate == 1
                               ? context.Initiator == active.Context.Initiator
                               : context.Initiator != active.Context.Initiator;
                }

                return true;
            }
        }

        return false;
    }
}