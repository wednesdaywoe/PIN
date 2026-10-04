using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

public class RequireSuperChargeCommand : Command, ICommand
{
    private RequireSuperChargeCommandDef Params;

    public RequireSuperChargeCommand(RequireSuperChargeCommandDef par)
        : base(par)
    {
        Params = par;
    }

    /// <summary>Passes when the ultimate meter holds at least Percent points (100 in almost every def).</summary>
    public bool Execute(Context context)
    {
        if ((context.Self ?? context.Initiator) is CharacterEntity character)
        {
            var percent = AbilitySystem.RegistryOp(context.Register, Params.Percent, (Operand)Params.PercentRegop);
            var enough = character.SuperCharge >= percent - 0.001f;
            if (!enough)
            {
                Logger.Debug("{Command} {CommandId}: ultimate meter {Value} is short of {Percent} on {Character}", nameof(RequireSuperChargeCommand), Params.Id, character.SuperCharge, percent, character);
            }

            var passed = enough != (Params.Negate == 1);

            // Everything this ultimate goes on to do is its damage, which shouldn't pay back the meter it just spent
            if (passed && Params.Negate != 1)
            {
                context.FromUltimate = true;
            }

            return passed;
        }

        Logger.Warning("{Command} {CommandId} fails because target is not a Character. If this is happening, we should investigate why.", nameof(RequireSuperChargeCommand), Params.Id);

        return false;
    }
}