using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.StaticDB;
using GameServer.Systems.Crafting;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     GRADE1's probe: sends the client material stacks carrying a quality and five stats, to see whether
///     it draws them and whether it keeps two batches of one material apart.
/// </summary>
/// <remarks>
///     Wire only. Nothing is added to the server's inventory, so a craft can't spend these and the next
///     real update for the same material (or <c>dbg_inventory resend</c>) overwrites what the client shows.
///     <c>pair</c> sends two batches of one material in a single message; <c>tagged</c> also gives them
///     different values in <c>Resource.Unk2</c>, the one unnamed field that could be a batch id.
/// </remarks>
[ServerCommand(
    "Send a material stack with a quality and stats (GRADE1 probe)",
    "batch <itemId> <quantity> <quality> [stat1..stat5] | batch pair <itemId> [tagged]",
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

    private static Resource Stack(uint itemId, uint quantity, int quality, int[] stats, uint unk2) => new()
    {
        SdbId = itemId,
        Quantity = quantity,
        SubInventory = Data.CharacterInventory.GetInventoryTypeByItemTypeId(itemId),
        TextKey = ResourceStats.Pack(itemId, quality, stats),
        Unk2 = unk2,
    };
}
