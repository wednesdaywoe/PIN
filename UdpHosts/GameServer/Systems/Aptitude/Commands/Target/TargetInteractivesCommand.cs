using GameServer.Entities;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetInteractivesCommand : Command, ICommand
{
    private TargetInteractivesCommandDef Params;

    public TargetInteractivesCommand(TargetInteractivesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (tfTargetInteractivesCommand): filters in place
        context.Targets.RemoveAll(target => target is not BaseEntity { Interaction: not null });

        return Params.FailNoTargets == 0 || context.Targets.Count > 0;
    }
}