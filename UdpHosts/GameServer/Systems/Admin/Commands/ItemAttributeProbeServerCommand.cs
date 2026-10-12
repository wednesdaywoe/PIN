using System;
using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.StaticDB;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     graded-crafting-2's probe: writes per-item attribute values onto every held copy of an item, to see whether the
///     client's tooltip draws them in place of the item type's.
/// </summary>
/// <remarks>
///     Sends the type's whole attribute set, as retail did, with the named attributes changed. The values
///     ride in <c>Item.Unk6</c> as (attribute id, float bits); the 2016 capture decodes cleanly that way.
///     Wire and memory only: not saved, and damage is still computed from the SDB.
///     <c>clear</c> sends the item with an empty list again.
/// </remarks>
[ServerCommand(
    "Write per-item attribute values onto a held item (graded-crafting-2 probe)",
    "itemattr <itemId> <attributeId> <value> [<attributeId> <value>...] | itemattr <itemId> clear",
    "itemattr")]
public class ItemAttributeProbeServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var inventory = context.SourcePlayer?.Inventory;
        if (inventory == null || parameters.Length < 2)
        {
            SourceFeedback("itemattr <itemId> <attributeId> <value> [...] | itemattr <itemId> clear", context);
            return;
        }

        var itemId = ParseUIntParameter(parameters[0]);
        var held = inventory.GetItems().Where(item => item.SdbId == itemId).ToList();
        if (held.Count == 0)
        {
            SourceFeedback($"You hold no item {itemId}", context);
            return;
        }

        var values = new SortedDictionary<uint, float>();
        if (parameters[1] != "clear")
        {
            foreach (var (attributeId, range) in SDBInterface.GetItemAttributeRange(itemId))
            {
                values[attributeId] = range.Base;
            }

            for (var i = 1; i + 1 < parameters.Length; i += 2)
            {
                values[ParseUIntParameter(parameters[i])] = float.Parse(parameters[i + 1], System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        var pairs = values
            .Select(pair => new ItemUnkData { Unk1 = pair.Key, Unk2 = BitConverter.SingleToUInt32Bits(pair.Value) })
            .ToArray();
        // Every held copy: run 1 had two rifles and no way to tell which one the tooltip showed.
        var shown = string.Join(", ", values.Select(pair => $"{pair.Key}={pair.Value}"));
        foreach (var item in held)
        {
            inventory.SetItemAttributes(item.GUID, pairs);
            Logger.Information("item attribute probe: item {ItemId} guid {Guid:X}, {Count} pair(s): {Pairs}", itemId, item.GUID, pairs.Length, shown);
        }

        SourceFeedback($"Sent {held.Count} cop{(held.Count == 1 ? "y" : "ies")} of {itemId} with {pairs.Length} attribute(s): {shown}", context);
    }
}
