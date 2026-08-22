using System;
using GameServer.StaticDB;
using GameServer.Systems.Loot;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Puts a drop on the ground at your feet, so the pickup loop can be tested without farming kills.
/// </summary>
/// <remarks>
///     Same doctrine as <c>deposit add</c> and <c>spawngroup</c>: the server holds no terrain, so a
///     player standing somewhere is the only proof a coordinate is ground a drop can rest on.
/// </remarks>
[ServerCommand(
    "Drop an item on the ground where you are standing",
    "droploot <itemId> [quantity] [mine|free]",
    "droploot",
    "drop_loot")]
public class DropLootServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var character = context.SourcePlayer?.CharacterEntity;
        if (character == null)
        {
            SourceFeedback("Need a character in the world", context);
            return;
        }

        if (parameters.Length == 0)
        {
            SourceFeedback("droploot <itemId> [quantity] [mine|free]", context);
            return;
        }

        var itemId = ParseUIntParameter(parameters[0]);
        if (SDBInterface.GetRootItem(itemId) == null)
        {
            SourceFeedback($"No item data for {itemId}", context);
            return;
        }

        var quantity = parameters.Length > 1 ? Math.Max(1, ParseUIntParameter(parameters[1])) : 1;

        // A powerup belongs to nobody by default, which is how the capture writes one. "mine" and "free"
        // force the other reading, because ownership is the half of a drop a single tester cannot see.
        var owned = parameters.Length > 2
            ? parameters[2].Equals("mine", StringComparison.OrdinalIgnoreCase)
            : !Powerups.IsPowerup(itemId);

        var dropped = context.Shard.Loot.Drop(
            character.Position,
            itemId,
            quantity,
            owned ? character : null,
            $"droploot by {character.EntityId}");

        SourceFeedback(
            dropped
                ? $"Dropped {itemId} x{quantity} at your feet, {(owned ? "yours" : "free to anyone")}"
                : "Nothing dropped — the loot board is full or the item has no data. See the log.",
            context);
    }
}
