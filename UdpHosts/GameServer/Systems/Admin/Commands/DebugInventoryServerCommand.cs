using System;
using System.Linq;
using System.Text;
using GameServer.Enums;
using GameServer.StaticDB;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Prints what the server believes the player is carrying, and can push the whole lot again.
///     <para>
///     An item that createitem made but the client never showed leaves no trace otherwise: the
///     pickup toast comes from SimulateLootPickup, which is sent whether or not anything was added,
///     and the InventoryUpdate that carries the item is fire and forget. Listing the contents
///     separates "never created" from "created and not displayed", and <c>resend</c> answers the
///     next question by replacing the partial update with the same full one sent at spawn.
///     </para>
/// </summary>
[ServerCommand("Log the server's view of your inventory, 'resend' to send it again", "dbg_inventory [resend]", "dbg_inventory", "dbg_inv")]
public class DebugInventoryServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer?.Inventory == null)
        {
            SourceFeedback("Cannot without a player inventory", context);
            return;
        }

        var inventory = context.SourcePlayer.Inventory;
        var items = inventory.GetItems().ToArray();
        var resources = inventory.GetResources().ToArray();

        // A type id asks about one item, which is the question worth asking: the full listing is a
        // couple of hundred lines of loadout gear on the wrong machine, and "is my scan hammer here"
        // drowns in it.
        if (parameters.Length > 0 && uint.TryParse(parameters[0], out var wanted))
        {
            var matches = items.Where(i => i.SdbId == wanted).ToArray();
            var slotted = inventory.GetLoadoutItemGuids();

            SourceFeedback($"Item {wanted}: {matches.Length} copy/copies of {items.Length} items", context);
            foreach (var match in matches)
            {
                Logger.Information(
                    "dbg_inventory {TypeId}: guid {Guid:X} in {SubInventory} flags {Flags}{Slotted}",
                    wanted,
                    match.GUID,
                    (InventoryType)match.SubInventory,
                    (ItemDynamicFlags)match.DynamicFlags,
                    slotted.Contains(match.GUID) ? ", slotted in a loadout" : string.Empty);
            }

            return;
        }

        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"Inventory: {items.Length} items, {resources.Length} resources, partial updates {(inventory.EnablePartialUpdates ? "on" : "off")}");

        stringBuilder.AppendLine("----- Items");
        foreach (var item in items)
        {
            var itemInfo = SDBInterface.GetRootItem(item.SdbId);
            var itemType = itemInfo != null ? ((ItemType)itemInfo.Type).ToString() : "no RootItem";
            stringBuilder.AppendLine($"{item.SdbId} guid {item.GUID:X} {itemType} in {(InventoryType)item.SubInventory} flags {(ItemDynamicFlags)item.DynamicFlags}");
        }

        stringBuilder.AppendLine("----- Resources");
        foreach (var resource in resources)
        {
            stringBuilder.AppendLine($"{resource.SdbId} x{resource.Quantity} in {(InventoryType)resource.SubInventory}");
        }

        stringBuilder.AppendLine("-----");

        context.SourcePlayer.SendDebugLog(stringBuilder.ToString());

        // The listing goes to the client console, which is the wrong machine for a queue that reads
        // the server log — the totals at least belong in both.
        inventory.LogInventorySize("dbg_inventory");

        // Resources go to the server log too. The client's debug window has a size cap and 70 items
        // overrun it, so the resource section is the half that gets cut, which is exactly the half
        // UI1 compares against /invprobe. Items stay client-side; there are too many to be worth it.
        foreach (var resource in resources)
        {
            Logger.Information(
                "dbg_inventory resource: {SdbId} x{Quantity} in {SubInventory}",
                resource.SdbId,
                resource.Quantity,
                (InventoryType)resource.SubInventory);
        }

        if (parameters.Length > 0 && parameters[0].Equals("resend", StringComparison.OrdinalIgnoreCase))
        {
            inventory.SendFullInventory();
            SourceFeedback("Inventory printed to console and resent in full", context);
            return;
        }

        SourceFeedback("Inventory printed to console", context);
    }
}
