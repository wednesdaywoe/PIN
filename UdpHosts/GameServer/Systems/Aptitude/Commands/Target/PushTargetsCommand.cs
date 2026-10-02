using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class PushTargetsCommand : Command, ICommand
{
    private const int MaxStackSize = 100;
    private PushTargetsCommandDef Params;

    public PushTargetsCommand(PushTargetsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (apt::PushTargetsCommand): pushes copies onto the target stack and keeps working on the targets
        if (Params.Current == 1)
        {
            Push(context, context.Targets);
        }

        if (Params.Former == 1)
        {
            Push(context, context.FormerTargets);
        }

        return true;
    }

    private void Push(Context context, AptitudeTargets targets)
    {
        if (context.TargetStack.Count > MaxStackSize)
        {
            Logger.Warning("Target stack overflow from {Command} {CommandId} (ability {AbilityId})", nameof(PushTargetsCommand), Params.Id, context.AbilityId);
            return;
        }

        context.TargetStack.Push(new AptitudeTargets(targets));
    }
}