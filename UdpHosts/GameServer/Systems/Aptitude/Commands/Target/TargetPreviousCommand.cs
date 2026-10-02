using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetPreviousCommand : Command, ICommand
{
    private TargetPreviousCommandDef Params;

    public TargetPreviousCommand(TargetPreviousCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (apt::TargetPreviousCommand): adds the former targets to the current ones
        context.Targets.AddRange(context.FormerTargets);

        if (Params.Clearformer == 1)
        {
            context.FormerTargets.Clear();
        }

        return true;
    }
}