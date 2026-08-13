using GameServer.Entities.Character;
using GameServer.Systems.Hazards;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Prints what the world thinks it is doing to you: how deep in water the client says you are, and
///     which side of the nearest melding wall you are standing on.
///
///     The reason this exists is that both readings are otherwise only observable by dying to them. The
///     melded side of a perimeter is inferred from the winding of its control points rather than read out
///     of the data, so being able to walk up to a wall with <c>invuln</c> on and watch the answer flip is
///     the only way to confirm it without a very short session.
/// </summary>
[ServerCommand("Show the environmental hazards acting on you or the current target", "hazard", "hazard", "env")]
public class HazardServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer == null || context.SourcePlayer.CharacterEntity == null)
        {
            SourceFeedback("Cannot check hazards without a valid player character", context);
            return;
        }

        var character = context.SourcePlayer.CharacterEntity;
        if (context.Target is CharacterEntity commandTarget)
        {
            character = commandTarget;
        }

        var (water, melding, bearing, submersion) = context.Shard.Hazards.Describe(character);

        SourceFeedback($"{character} at {character.Position}, invulnerable: {character.Invulnerable}", context);
        SourceFeedback(
            $"Water level {submersion.Level}/{Submersion.MaxLevel} (depth {submersion.Depth:0.00}, description {submersion.DescIndex}), hazard {water}",
            context);

        if (melding == null)
        {
            SourceFeedback("No melding perimeter in this zone", context);
            return;
        }

        SourceFeedback(
            $"Nearest melding {melding.PerimiterSetName} at {bearing.Distance:0}m, melded: {bearing.Melded}",
            context);
    }
}
