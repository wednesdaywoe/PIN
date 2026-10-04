using GameServer.Entities.Character;

namespace GameServer.Systems.Admin.Commands;

// For testing ultimates without fighting for two minutes first
[ServerCommand("Show or set your ultimate meter, 0 to 100", "supercharge [amount]", "supercharge", "ult")]
public class SuperChargeServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        float amount = 0;
        if (parameters.Length > 1 || (parameters.Length == 1 && !float.TryParse(parameters[0], out amount)))
        {
            SourceFeedback("Usage: supercharge [amount]", context);
            return;
        }

        if (context.SourcePlayer?.CharacterEntity is not CharacterEntity character)
        {
            SourceFeedback("Cannot set the ultimate meter without a valid player character", context);
            return;
        }

        if (parameters.Length == 1)
        {
            character.SetSuperCharge(amount);
        }

        SourceFeedback($"Ultimate meter {character.SuperCharge:0.#} of 100, charge speed {Combat.UltimateCharge.ChargeSpeed(character):0.##}", context);
    }
}
