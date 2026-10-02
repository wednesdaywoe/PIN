using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetHostilesCommand : Command, ICommand
{
    private TargetHostilesCommandDef Params;

    public TargetHostilesCommand(TargetHostilesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var factions = context.Abilities.Factions;
        return TargetStanceFilter.Apply(context, Params.IncludeSelf, Params.IncludeInitiator, Params.IncludeOwner, Params.FailNoTargets, target => factions.IsHostile(context.Self, target));
    }
}
