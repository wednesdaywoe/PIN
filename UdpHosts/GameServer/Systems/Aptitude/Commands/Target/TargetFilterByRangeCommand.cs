using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetFilterByRangeCommand : Command, ICommand
{
    private TargetFilterByRangeCommandDef Params;

    public TargetFilterByRangeCommand(TargetFilterByRangeCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (tfTargetFilterByRangeCommand): filters in place, Negate keeps the targets out of range
        var range = AbilitySystem.RegistryOp(context.Register, Params.Range, (Operand)Params.RangeRegop);
        var sourcePosition = context.Self.Position;
        context.Targets.SwapRemoveAll(target => (Vector3.Distance(sourcePosition, target.Position) > range) != (Params.Negate == 1));

        return Params.FailNoTargets == 0 || context.Targets.Count > 0;
    }
}