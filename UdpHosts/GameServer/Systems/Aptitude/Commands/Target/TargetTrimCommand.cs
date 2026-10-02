using System;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetTrimCommand : Command, ICommand
{
    private TargetTrimCommandDef Params;

    public TargetTrimCommand(TargetTrimCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Keeps <trimSize> targets on the stack, from ability 30187. Guardian Angel - II ; Protect 2 closest allies
        // With Chomp it removes <trimSize> targets instead. While loops use that to work through the stack one target
        // per lap until it is empty (e.g. 501195, 1543482).
        // As the client (apt::TargetTrimCommand): FromFront removes from the start of the list (the oldest targets),
        // otherwise from the end, whether keeping or chomping.
        var trimSize = (int)AbilitySystem.RegistryOp(context.Register, Params.Trimsize, (Enums.Operand)Params.TrimsizeRegop);

        if (Params.Former == 1)
        {
            Trim(context.FormerTargets, trimSize);
        }

        if (Params.Current == 1)
        {
            Trim(context.Targets, trimSize);
        }

        return true;
    }

    private void Trim(AptitudeTargets targets, int trimSize)
    {
        int targetsToRemove;
        if (Params.Chomp == 1)
        {
            targetsToRemove = trimSize;
        }
        else
        {
            targetsToRemove = targets.Count - trimSize;
            if (targetsToRemove < 0)
            {
                // 39360 Heavy Turret
                Logger.Debug("{Command} {CommandId} Not enough targets for TargetTrimCommand, investigate if this is expected", nameof(TargetTrimCommand), Params.Id);
            }
        }

        targetsToRemove = Math.Clamp(targetsToRemove, 0, targets.Count);

        if (Params.FromFront == 1)
        {
            targets.RemoveBottomN(targetsToRemove);
        }
        else
        {
            targets.PopN(targetsToRemove);
        }
    }
}
