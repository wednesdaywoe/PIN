using System.Linq;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     graded-crafting-2's module probe: puts module ids into every held copy of an item's <c>Item.Modules</c>,
///     to see whether the client counts them as slotted and folds their effect into the item's numbers.
/// </summary>
/// <remarks>Wire and memory only, not saved. <c>clear</c> sends an empty list again.</remarks>
[ServerCommand(
    "Put module ids on a held item (graded-crafting-2 module probe)",
    "itemmod <itemId> <moduleId> [<moduleId>...] | itemmod <itemId> clear",
    "itemmod")]
public class ItemModuleProbeServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var inventory = context.SourcePlayer?.Inventory;
        if (inventory == null || parameters.Length < 2)
        {
            SourceFeedback("itemmod <itemId> <moduleId> [...] | itemmod <itemId> clear", context);
            return;
        }

        var itemId = ParseUIntParameter(parameters[0]);
        var held = inventory.GetItems().Where(item => item.SdbId == itemId).ToList();
        if (held.Count == 0)
        {
            SourceFeedback($"You hold no item {itemId}", context);
            return;
        }

        var modules = parameters[1] == "clear" ? [] : parameters.Skip(1).Select(ParseUIntParameter).ToArray();
        foreach (var item in held)
        {
            inventory.SetItemModules(item.GUID, modules);
            Logger.Information("item module probe: item {ItemId} guid {Guid:X}, modules [{Modules}]", itemId, item.GUID, string.Join(", ", modules));
        }

        SourceFeedback($"Sent {held.Count} cop{(held.Count == 1 ? "y" : "ies")} of {itemId} with modules [{string.Join(", ", modules)}]", context);
    }
}
