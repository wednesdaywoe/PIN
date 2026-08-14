using System.Collections.Generic;
using System.Linq;
using GameServer.StaticDB;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Throws items away, which nothing else could do — <c>createitem</c> only ever added, so a
///     session that made a mistake carried it until the next login.
/// </summary>
/// <remarks>
///     Items a loadout slots are skipped unless <c>force</c> is passed. The starting inventory is
///     almost entirely those: every hardcoded loadout builds its own chassis and its own copy of each
///     slotted item, so a fresh character carries a couple of hundred of them and clearing the lot
///     would strip every battleframe. <c>loose</c> exists for the opposite reason — it removes exactly
///     the items no loadout is holding, which is the clutter a testing session actually creates.
/// </remarks>
[ServerCommand("Remove items from your inventory", "removeitem <typeId|guid|loose> [force]", "removeitem", "remove_item", "delitem")]
public class RemoveItemServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer?.Inventory == null)
        {
            SourceFeedback("Need a player inventory", context);
            return;
        }

        if (parameters.Length == 0)
        {
            SourceFeedback("removeitem <typeId|guid|loose> [force]", context);
            return;
        }

        var inventory = context.SourcePlayer.Inventory;
        var slotted = inventory.GetLoadoutItemGuids();
        var force = parameters.Length > 1 && parameters[1].Equals("force", System.StringComparison.OrdinalIgnoreCase);
        var items = inventory.GetItems().ToArray();

        List<ulong> targets;
        string what;

        if (parameters[0].Equals("loose", System.StringComparison.OrdinalIgnoreCase))
        {
            targets = items.Where(i => !slotted.Contains(i.GUID)).Select(i => i.GUID).ToList();
            what = "loose item(s)";
        }
        else
        {
            var id = ParseULongParameter(parameters[0]);
            if (id == 0)
            {
                SourceFeedback($"'{parameters[0]}' is not a type id, a guid, or 'loose'", context);
                return;
            }

            // A type id names every copy; a guid names exactly one. Type ids are small and guids are
            // not, so which was meant is never ambiguous in practice — but match guids first anyway.
            targets = items.Where(i => i.GUID == id).Select(i => i.GUID).ToList();
            what = $"item(s) with guid {id}";

            if (targets.Count == 0)
            {
                targets = items.Where(i => i.SdbId == id).Select(i => i.GUID).ToList();
                var itemInfo = SDBInterface.GetRootItem((uint)id);
                what = itemInfo == null ? $"item(s) of type {id}" : $"item(s) of type {id} ({(Enums.ItemType)itemInfo.Type})";
            }
        }

        var skipped = 0;
        if (!force)
        {
            skipped = targets.Count(slotted.Contains);
            targets = targets.Where(guid => !slotted.Contains(guid)).ToList();
        }

        var removed = inventory.RemoveItems(targets);

        var message = $"Removed {removed} {what}";
        if (skipped > 0)
        {
            message += $"; kept {skipped} a loadout is using — pass 'force' to take those too";
        }

        SourceFeedback(message, context);
        Logger.Information("removeitem: removed {Removed}, skipped {Skipped} slotted", removed, skipped);
    }
}
