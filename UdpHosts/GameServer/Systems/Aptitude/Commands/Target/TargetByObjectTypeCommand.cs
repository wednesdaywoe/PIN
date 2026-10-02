using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Entities.Vehicle;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetByObjectTypeCommand : Command, ICommand
{
    private TargetByObjectTypeCommandDef Params;

    public TargetByObjectTypeCommand(TargetByObjectTypeCommandDef par)
: base(par)
    {
        Params = par;
    }

    // TODO: Handle Params.Projectile
    // TODO: Handle Params.Tinyobject
    public bool Execute(Context context)
    {
        // As the client (tfTargetByObjectTypeCommand): filters in place
        context.Targets.RemoveAll(target => !Matches(target));

        return Params.FailNoTargets == 0 || context.Targets.Count > 0;
    }

    private bool Matches(IAptitudeTarget target)
    {
        return (Params.Character == 1 && target is CharacterEntity)
               || (Params.Deployable == 1 && target is DeployableEntity)
               || (Params.Vehicle == 1 && target is VehicleEntity);
    }
}