using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.StaticDB;
using GameServer.Systems.Crafting;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     graded-crafting-1's probe: sends the client material stacks carrying a quality and five stats, to see whether
///     it draws them and whether it keeps two batches of one material apart.
/// </summary>
/// <remarks>
///     Wire only. Nothing is added to the server's inventory, so a craft can't spend these and the next
///     real update for the same material (or <c>dbg_inventory resend</c>) overwrites what the client shows.
///     <c>pair</c> sends two batches of one material in a single message; <c>tagged</c> also gives them
///     different values in <c>Resource.Unk2</c>, the one unnamed field that could be a batch id.
///     <c>give</c> and <c>spend</c> are real (graded-crafting-3): they add a batch to the server's inventory,
///     which is saved, and spend from the one batch of a given quality.
/// </remarks>
[ServerCommand(
    "Send a material stack with a quality and stats (graded-crafting-1 probe)",
    "batch <itemId> <quantity> <quality> [stat1..stat5] | batch pair <itemId> [tagged] | batch give <itemId> <quantity> <quality> [stat1..stat5] | batch spend <itemId> <quantity> <quality>",
    "batch")]
public class BatchProbeServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var inventory = context.SourcePlayer?.Inventory;
        if (inventory == null)
        {
            SourceFeedback("Need a player inventory", context);
            return;
        }

        if (parameters.Length >= 4 && parameters[0] is "give" or "spend")
        {
            GiveOrSpend(parameters, inventory, context);
            return;
        }

        var pair = parameters.Length >= 2 && parameters[0] == "pair";
        if (!pair && parameters.Length < 3)
        {
            SourceFeedback("batch <itemId> <quantity> <quality> [stat1..stat5] | batch pair <itemId> [tagged]", context);
            return;
        }

        var itemId = ParseUIntParameter(parameters[pair ? 1 : 0]);
        if (SDBInterface.GetRootItem(itemId) == null)
        {
            SourceFeedback($"No item {itemId}", context);
            return;
        }

        var stacks = new List<Resource>();
        if (pair)
        {
            var tagged = parameters.Length >= 3 && parameters[2] == "tagged";
            stacks.Add(Stack(itemId, 5, 300, [100, 150, 200, 250, 300], tagged ? 1u : 0u));
            stacks.Add(Stack(itemId, 7, 900, [700, 750, 800, 850, 900], tagged ? 2u : 0u));
        }
        else
        {
            var quantity = ParseUIntParameter(parameters[1]);
            var quality = (int)ParseUIntParameter(parameters[2]);
            var stats = parameters.Skip(3).Take(5).Select(p => (int)ParseUIntParameter(p)).ToArray();
            stacks.Add(Stack(itemId, quantity, quality, stats, 0));
        }

        inventory.SendResources(stacks);
        foreach (var stack in stacks)
        {
            Logger.Information("batch probe: item {ItemId} x{Quantity}, unk2 {Unk2}, text '{Text}'", stack.SdbId, stack.Quantity, stack.Unk2, stack.TextKey);
            SourceFeedback($"Sent {stack.SdbId} x{stack.Quantity} '{stack.TextKey}' unk2 {stack.Unk2}", context);
        }
    }

    private void GiveOrSpend(string[] parameters, Data.CharacterInventory inventory, ServerCommandContext context)
    {
        var itemId = ParseUIntParameter(parameters[1]);
        var quantity = ParseUIntParameter(parameters[2]);
        var quality = (int)ParseUIntParameter(parameters[3]);
        if (SDBInterface.GetRootItem(itemId) == null)
        {
            SourceFeedback($"No item {itemId}", context);
            return;
        }

        if (parameters[0] == "give")
        {
            var stats = parameters.Skip(4).Take(5).Select(p => (int)ParseUIntParameter(p)).ToArray();
            var batch = ResourceStats.Pack(itemId, quality, stats);
            inventory.AddResource(itemId, quantity, batch);
            Logger.Information("batch give: item {ItemId} x{Quantity}, batch '{Batch}', now {Total} across {Count} batch(es)", itemId, quantity, batch, inventory.GetResourceQuantity(itemId), inventory.GetResourceBatches(itemId).Count());
            SourceFeedback($"Gave {itemId} x{quantity} at quality {quality}; you hold {inventory.GetResourceQuantity(itemId)} in {inventory.GetResourceBatches(itemId).Count()} batch(es)", context);
            return;
        }

        var named = inventory.GetResourceBatches(itemId).Where(r => ResourceStats.Unpack(r.TextKey)?.Quality == quality).Select(r => r.TextKey).FirstOrDefault();
        var spent = named == null ? null : inventory.SpendResource(itemId, quantity, named);
        Logger.Information("batch spend: item {ItemId} x{Quantity} at quality {Quality}: {Result}", itemId, quantity, quality, spent == null ? "refused" : "spent");
        SourceFeedback(spent == null
            ? $"No batch of {itemId} at quality {quality} holds {quantity}"
            : $"Spent {quantity} of {itemId} at quality {quality}; you hold {inventory.GetResourceQuantity(itemId)} in {inventory.GetResourceBatches(itemId).Count()} batch(es)", context);
    }

    private static Resource Stack(uint itemId, uint quantity, int quality, int[] stats, uint unk2) => new()
    {
        SdbId = itemId,
        Quantity = quantity,
        SubInventory = Data.CharacterInventory.GetInventoryTypeByItemTypeId(itemId),
        TextKey = ResourceStats.Pack(itemId, quality, stats),
        Unk2 = unk2,
    };
}
