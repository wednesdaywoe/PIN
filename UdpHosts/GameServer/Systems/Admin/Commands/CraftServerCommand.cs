using System;
using System.Linq;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.Packets;
using GameServer.Systems.Crafting;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Builds a blueprint: spends its ingredients out of your inventory and hands you the output.
/// </summary>
/// <remarks>
///     A server command rather than something the client asks for, because the client cannot ask. v1.6
///     deleted the crafting panel and left no Lua binding that sends a fabrication command; 748 exported
///     <c>Game.*</c> names were read out of the 1962 binary and none of them is a sender. So the trigger
///     has to come from this side.
///     <para>
///     <c>check</c> prints the plan without spending anything, which is the only way to see whether a
///     blueprint's data survived 1962 before committing an inventory to it.
///     </para>
/// </remarks>
[ServerCommand(
    "Build a blueprint, spending its ingredients",
    "craft <blueprintId> | craft check <blueprintId> | craft find <outputItemId>",
    "craft",
    "build_blueprint")]
public class CraftServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var inventory = context.SourcePlayer?.Inventory;
        if (inventory == null)
        {
            SourceFeedback("Need a player inventory", context);
            return;
        }

        if (parameters.Length == 0)
        {
            SourceFeedback("craft <blueprintId> | craft check <blueprintId> | craft find <outputItemId>", context);
            return;
        }

        if (parameters[0].Equals("find", StringComparison.OrdinalIgnoreCase))
        {
            Find(parameters, context);
            return;
        }

        var checkOnly = parameters[0].Equals("check", StringComparison.OrdinalIgnoreCase);
        var idText = checkOnly ? parameters.ElementAtOrDefault(1) : parameters[0];

        if (idText == null || ParseUIntParameter(idText) == 0)
        {
            SourceFeedback($"'{idText ?? "(nothing)"}' is not a blueprint id", context);
            return;
        }

        var plan = BlueprintCrafting.Resolve(ParseUIntParameter(idText));

        SourceFeedback(
            $"Blueprint {plan.BlueprintId}: type {plan.BlueprintType}, {plan.BuildTimeSecs}s build",
            context);

        foreach (var line in plan.Inputs)
        {
            var held = line.IsResource
                ? inventory.GetResourceQuantity(line.ItemId)
                : (uint)inventory.GetItems().Count(i => i.SdbId == line.ItemId);
            SourceFeedback($"  costs x{line.Quantity} {BlueprintCrafting.Describe(line.ItemId)} — holding {held}", context);
        }

        foreach (var line in plan.ClassInputs)
        {
            if (line.Members.Count == 0)
            {
                SourceFeedback($"  costs x{line.Quantity} of material class {line.ClassId} - nothing belongs to it any more", context);
                continue;
            }

            // Class lines are met from every member at once, so the useful number is the total held
            // across them, with the breakdown after it.
            var held = line.Members.Sum(m => (long)inventory.GetResourceQuantity(m) + inventory.GetItems().Count(i => i.SdbId == m));
            var carried = line.Members.Where(m => inventory.GetResourceQuantity(m) > 0 || inventory.GetItems().Any(i => i.SdbId == m))
                                      .Select(m => $"{m} x{inventory.GetResourceQuantity(m) + (uint)inventory.GetItems().Count(i => i.SdbId == m)}")
                                      .ToList();
            var stat = line.StatRead == 0 ? string.Empty : $", reads stat {line.StatRead} ({StatName(line.StatRead)})";
            SourceFeedback(
                $"  costs x{line.Quantity} of material class {line.ClassId}{stat} - holding {held} across {line.Members.Count} member(s)",
                context);
            SourceFeedback(
                carried.Count > 0 ? $"      you carry: {string.Join(", ", carried)}" : $"      any of: {string.Join(", ", line.Members.Take(6))}",
                context);
        }

        foreach (var line in plan.Outputs)
        {
            SourceFeedback($"  pays  x{line.Quantity} {BlueprintCrafting.Describe(line.ItemId)}", context);
        }

        if (!plan.IsBuildable)
        {
            SourceFeedback($"Cannot build: {plan.Problem}", context);
            return;
        }

        if (checkOnly)
        {
            SourceFeedback("Data is intact. Run it without 'check' to build.", context);
            return;
        }

        // createitem announces what it hands over and craft did not, so a crafted item arrived with no
        // toast at all — the ingredients visibly left and nothing visibly replaced them.
        void Announce(CraftLine line) =>
            context.SourcePlayer.NetChannels[ChannelType.ReliableGss].SendMessage(
                new SimulateLootPickup
                {
                    Item = new() { SdbId = line.ItemId, Quantity = (ushort)line.Quantity },
                    RewardType = SimulateLootPickup.Type.General,
                },
                context.SourcePlayer.CharacterEntity.EntityId);

        if (!BlueprintCrafting.TryCraft(inventory, plan, Announce, out var error, out var spent))
        {
            SourceFeedback($"Did not build: {error}", context);
            Logger.Information("craft {BlueprintId}: refused — {Error}", plan.BlueprintId, error);
            return;
        }

        SourceFeedback($"Built. Spent {string.Join(", ", spent)}.", context);
        Logger.Information(
            "craft {BlueprintId}: spent [{Spent}], paid [{Paid}]",
            plan.BlueprintId,
            string.Join("; ", spent),
            string.Join("; ", plan.Outputs.Select(o => $"{o.ItemId} x{o.Quantity}")));
    }

    /// <summary>
    ///     The five names in <c>dbitems::ResourceStat</c>. The fifth's text never shipped: its
    ///     localisation key sits one past CPU's and has no row, so it was authored and then dropped.
    /// </summary>
    private static string StatName(byte stat) => stat switch
    {
        1 => "Purity",
        2 => "Power",
        3 => "Mass",
        4 => "CPU",
        5 => "unnamed",
        _ => "none",
    };

    private void Find(string[] parameters, ServerCommandContext context)
    {
        var itemId = parameters.Length > 1 ? ParseUIntParameter(parameters[1]) : 0;
        if (itemId == 0)
        {
            SourceFeedback("craft find <outputItemId>", context);
            return;
        }

        var found = SDBInterface.GetBlueprintIdsProducing(itemId);
        if (found.Count == 0)
        {
            SourceFeedback($"No blueprint lists {itemId} as its main output", context);
            return;
        }

        SourceFeedback($"{found.Count} blueprint(s) produce {BlueprintCrafting.Describe(itemId)}:", context);
        foreach (var id in found.Take(10))
        {
            var plan = BlueprintCrafting.Resolve(id);
            SourceFeedback($"  {id}: {plan.Inputs.Count} item ingredient(s), {plan.ClassInputs.Count} material class(es){(plan.IsBuildable ? string.Empty : " - " + plan.Problem)}", context);
        }
    }
}
