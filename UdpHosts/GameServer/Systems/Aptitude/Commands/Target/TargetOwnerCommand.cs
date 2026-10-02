using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetOwnerCommand : Command, ICommand
{
    private TargetOwnerCommandDef Params;

    public TargetOwnerCommand(TargetOwnerCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (tfTargetOwnerCommand): adds the owner, leaving the former targets alone
        if (context.Self.Owner != null)
        {
            context.Targets.Push(context.Self.Owner);
        }
        else if (Params.FailNone == 1)
        {
            return false;
        }

        return true;
    }
}