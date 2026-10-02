using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetFriendliesCommand : Command, ICommand
{
    private TargetFriendliesCommandDef Params;

    public TargetFriendliesCommand(TargetFriendliesCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var factions = context.Abilities.Factions;
        return TargetStanceFilter.Apply(context, Params.IncludeSelf, Params.IncludeInitiator, Params.IncludeOwner, Params.FailNoTargets, target => factions.IsFriendly(context.Self, target));
    }
}
