using System.Collections.Generic;
using System.Linq;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetByEffectTagCommand : Command, ICommand
{
    private TargetByEffectTagCommandDef Params;

    public TargetByEffectTagCommand(TargetByEffectTagCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (tfTargetByEffectTagCommand): counts the target's effects with the tag against StackCount,
        // filters in place, and Negate keeps the targets below the count
        var effectTagEffectIds = SDBInterface.GetStatusEffectTag(Params.TagId) ?? [];
        context.Targets.SwapRemoveAll(target => (TaggedEffects(target, effectTagEffectIds) >= Params.StackCount) == (Params.Negate == 1));

        return Params.FailNoTargets == 0 || context.Targets.Count > 0;
    }

    private static int TaggedEffects(IAptitudeTarget target, HashSet<uint> effectIds)
    {
        return target.GetActiveEffects().Count(active => active != null && effectIds.Contains(active.Effect.Id));
    }
}