using GameServer.Entities.Character;

namespace GameServer.Systems.Admin.Commands;

// For testing heals: there is no other way to wound a friendly character, which can't be shot
[ServerCommand("Show or set the current target's health, or your own with no target", "sethealth [amount]", "sethealth", "hp")]
public class SetHealthServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        int amount = 0;
        if (parameters.Length > 1 || (parameters.Length == 1 && !int.TryParse(parameters[0], out amount)))
        {
            SourceFeedback("Usage: sethealth [amount]", context);
            return;
        }

        if (context.SourcePlayer?.CharacterEntity == null)
        {
            SourceFeedback("Cannot set health without a valid player character", context);
            return;
        }

        if ((context.Target ?? context.SourcePlayer.CharacterEntity) is not CharacterEntity character)
        {
            SourceFeedback("The target is not a character", context);
            return;
        }

        if (parameters.Length == 1)
        {
            character.SetCurrentHealth(amount);
        }

        SourceFeedback($"{character.AeroEntityId} health {character.CurrentHealth} of {character.MaxHealth.Value}", context);
    }
}
