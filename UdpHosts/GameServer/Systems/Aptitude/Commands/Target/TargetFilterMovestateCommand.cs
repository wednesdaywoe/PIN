using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetFilterMovestateCommand : Command, ICommand
{
    private TargetFilterMovestateCommandDef Params;

    public TargetFilterMovestateCommand(TargetFilterMovestateCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (tfTargetFilterMovestateCommand): filters in place, Negate keeps the targets that do not match
        context.Targets.SwapRemoveAll(target => Matches(target) == (Params.Negate == 1));

        return Params.FailNoTargets == 0 || context.Targets.Count > 0;
    }

    private bool Matches(IAptitudeTarget target)
    {
        if (target is not CharacterEntity character)
        {
            return false;
        }

        var movestate = character.MovementStateContainer.Movestate;
        return (Params.Standing == 1 && movestate == Movestate.Standing)
                   || (Params.Running == 1 && movestate == Movestate.Running)
                   || (Params.Falling == 1 && movestate == Movestate.Falling)
                   || (Params.Sliding == 1 && movestate == Movestate.Sliding)
                   || (Params.Walking == 1 && movestate == Movestate.Walking)
                   || (Params.Jetpack == 1 && movestate == Movestate.Jetpack)
                   || (Params.Gliding == 1 && movestate == Movestate.Glider)
                   || (Params.Thruster == 1 && movestate == Movestate.GliderThrusters)
                   || (Params.Stall == 1 && movestate == Movestate.GliderStalling)
                   || (Params.KnockdownOnground == 1 && movestate == Movestate.Knockdown)
                   || (Params.KnockdownFalling == 1 && movestate == Movestate.KnockdownFalling)
                   || (Params.JetpackSprint == 1 && movestate == Movestate.JetpackSprint);
    }
}