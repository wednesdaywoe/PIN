using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Target;

public class TargetByHealthCommand : Command, ICommand
{
    private TargetByHealthCommandDef Params;

    public TargetByHealthCommand(TargetByHealthCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (tfTargetByHealthCommand): filters in place, Negate keeps the targets below the threshold
        var healthPct = AbilitySystem.RegistryOp(context.Register, Params.HealthPct, (Operand)Params.HealthRegop);
        context.Targets.SwapRemoveAll(target => AtLeast(target, healthPct) == (Params.Negate == 1));

        return Params.FailNoTargets == 0 || context.Targets.Count > 0;
    }

    private static bool AtLeast(IAptitudeTarget target, float healthPct)
    {
        if (target is not CharacterEntity character || character.MaxHealth.Value <= 0)
        {
            return false;
        }

        return character.CurrentHealth * 100f / character.MaxHealth.Value >= healthPct;
    }
}