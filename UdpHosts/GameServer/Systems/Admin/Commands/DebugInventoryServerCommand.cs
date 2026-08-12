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

        if (parameters.Length > 0 && parameters[0].Equals("resend", StringComparison.OrdinalIgnoreCase))
        {
            inventory.SendFullInventory();
            SourceFeedback("Inventory printed to console and resent in full", context);
            return;
        }

        SourceFeedback("Inventory printed to console", context);
    }
}
