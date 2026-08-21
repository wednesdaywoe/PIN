using System;
using System.Collections.Generic;
using System.Linq;
using GameServer.Data;
using GameServer.Enums;
using GameServer.StaticDB;

namespace GameServer.Systems.Crafting;

/// <summary>
///     Deterministic crafting off <c>dbitems::Blueprints</c>: resolve a blueprint's ingredients against a
///     character's inventory, spend them, hand back the output.
/// </summary>
/// <remarks>
///     <para>
///     This is not the fabrication system the client's <c>Fabrication_*</c> messages talk to.
///     <c>dbfabrication::Recipe</c> carries no ingredient rows and no output item — its 285 rows are a
///     price (<c>build_cost</c>), a level, and a <c>result_loot_table_id</c>, so fabrication in 1962 is a
///     paid roll on a loot table. Blueprints are the half with real ingredient lists, and they are what
///     "spend the ingredients, place the output" describes.
///     </para>
///     <para>
///     A cost has two halves and they are priced differently. <c>dbitems::Blueprint_Items</c> names
///     specific items — its <c>item_type</c> resolves to a real one on 25,823 of its 25,881 rows.
///     <c>dbitems::Blueprint_Resources</c> names a material <em>class</em>: its <c>item_type</c> is a
///     <c>RootItem.item_subtype</c>, and any item of that class settles the line. See
///     <see cref="SDBInterface.GetItemsOfSubtype" />.
///     </para>
///     <para>
///     A class the 1.6 cull emptied cannot be met by anything, and a blueprint costed in one is refused
///     rather than built for free. Those are mostly intermediate components — "Grenade Payload I" and
///     the like — whose items went away while the recipes citing them stayed.
///     </para>
/// </remarks>
public static class BlueprintCrafting
{
    /// <summary>
    ///     Reads a blueprint into a plan. Never throws and never returns null: a blueprint that cannot be
    ///     built comes back with <see cref="CraftPlan.Problem" /> set, so the caller has something to say.
    /// </summary>
    public static CraftPlan Resolve(uint blueprintId)
    {
        var blueprint = SDBInterface.GetBlueprint(blueprintId);
        if (blueprint == null)
        {
            return new CraftPlan { BlueprintId = blueprintId, Problem = $"{blueprintId} is not a blueprint id" };
        }

        var rows = SDBInterface.GetBlueprintItems(blueprintId);
        if (rows == null || rows.Count == 0)
        {
            return new CraftPlan
            {
                BlueprintId = blueprintId,
                BuildTimeSecs = blueprint.BuildTimeSecs,
                BlueprintType = blueprint.BlueprintType,
                Problem = "no Blueprint_Items rows — this blueprint has no ingredient list",
            };
        }

        var inputs = new List<CraftLine>();
        var outputs = new List<CraftLine>();
        var unresolved = new List<uint>();

        foreach (var row in rows)
        {
            var item = SDBInterface.GetRootItem(row.ItemType);
            if (item == null)
            {
                unresolved.Add(row.ItemType);
                continue;
            }

            // A quantity of zero shows up on six rows and means the tuner left it blank, not "free".
            var line = new CraftLine(row.ItemType, row.RsrcQuantity == 0 ? 1 : row.RsrcQuantity, IsResource(row.ItemType));
            (row.IsOutput != 0 ? outputs : inputs).Add(line);
        }

        // main_output_item_id is the blueprint's headline product. Usually it is also one of the
        // is_output rows; when it isn't, the item rows alone would pay nothing.
        if (blueprint.MainOutputItemId != 0 && outputs.All(o => o.ItemId != blueprint.MainOutputItemId))
        {
            if (SDBInterface.GetRootItem(blueprint.MainOutputItemId) == null)
            {
                unresolved.Add(blueprint.MainOutputItemId);
            }
            else
            {
                outputs.Add(new CraftLine(blueprint.MainOutputItemId, 1, IsResource(blueprint.MainOutputItemId)));
            }
        }

        var classInputs = new List<CraftClassLine>();
        foreach (var raw in SDBInterface.GetBlueprintResources(blueprintId))
        {
            if (raw.IsRequired == 0)
            {
                continue;
            }

            classInputs.Add(new CraftClassLine(
                raw.ItemType,
                raw.RsrcQuantity == 0 ? 1 : raw.RsrcQuantity,
                raw.ResourceStat,
                raw.ItemAttribute,
                SDBInterface.GetItemsOfSubtype(raw.ItemType)));
        }

        var plan = new CraftPlan
        {
            BlueprintId = blueprintId,
            BuildTimeSecs = blueprint.BuildTimeSecs,
            BlueprintType = blueprint.BlueprintType,
            Inputs = inputs,
            Outputs = outputs,
            ClassInputs = classInputs,
            Problem = FirstProblem(inputs, outputs, classInputs, unresolved),
        };

        return plan;
    }

    /// <summary>
    ///     Checks the whole cost, then spends it, then pays out — in that order, so a shortfall on the
    ///     last ingredient cannot leave the first one already gone.
    /// </summary>
    /// <returns>True if the item was built. On false, <paramref name="error" /> says what was missing.</returns>
    /// <param name="announce">
    ///     Called for each output line just before it is added, so a caller can tell the player what is
    ///     coming. The live server announced a pickup ahead of the inventory that produced it, and the
    ///     order matters to the client, so this runs first rather than after the fact.
    /// </param>
    public static bool TryCraft(CharacterInventory inventory, CraftPlan plan, Action<CraftLine> announce, out string error, out List<string> spent)
    {
        spent = [];
        error = null;

        if (!plan.IsBuildable)
        {
            error = plan.Problem;
            return false;
        }

        var items = inventory.GetItems().ToList();
        var slotted = inventory.GetLoadoutItemGuids();

        // Which guids each item-shaped ingredient would take. Held aside so the spend below cannot pick
        // the same copy twice for two ingredient lines that name the same item.
        var claimed = new List<ulong>();
        var shortfalls = new List<string>();

        foreach (var line in plan.Inputs)
        {
            if (line.IsResource)
            {
                var held = inventory.GetResourceQuantity(line.ItemId);
                if (held < line.Quantity)
                {
                    shortfalls.Add($"{Describe(line.ItemId)} x{line.Quantity} (holding {held})");
                }

                continue;
            }

            // Loadout-slotted copies are off limits: spending one would strip a battleframe to build a
            // module for it. Same rule removeitem uses.
            var free = items.Where(i => i.SdbId == line.ItemId && !slotted.Contains(i.GUID) && !claimed.Contains(i.GUID))
                            .Take((int)line.Quantity)
                            .Select(i => i.GUID)
                            .ToList();

            if (free.Count < line.Quantity)
            {
                shortfalls.Add($"{Describe(line.ItemId)} x{line.Quantity} (holding {free.Count} spare)");
                continue;
            }

            claimed.AddRange(free);
        }

        // A class line draws from every member at once, so what matters is the total held across them,
        // not whether any single material covers it on its own.
        var classDraws = new List<(CraftClassLine Line, List<(uint ItemId, uint Take)> Resources, List<ulong> Guids)>();
        foreach (var line in plan.ClassInputs)
        {
            var remaining = line.Quantity;
            var fromResources = new List<(uint ItemId, uint Take)>();
            var fromItems = new List<ulong>();

            foreach (var member in line.Members)
            {
                if (remaining == 0)
                {
                    break;
                }

                var held = inventory.GetResourceQuantity(member);
                if (held > 0)
                {
                    var take = Math.Min(held, remaining);
                    fromResources.Add((member, take));
                    remaining -= take;
                    continue;
                }

                foreach (var item in items.Where(i => i.SdbId == member && !slotted.Contains(i.GUID) && !claimed.Contains(i.GUID)))
                {
                    if (remaining == 0)
                    {
                        break;
                    }

                    fromItems.Add(item.GUID);
                    claimed.Add(item.GUID);
                    remaining--;
                }
            }

            if (remaining > 0)
            {
                shortfalls.Add($"{line.Quantity} units of material class {line.ClassId} (holding {line.Quantity - remaining}, any of {string.Join("/", line.Members.Take(4))})");
                continue;
            }

            classDraws.Add((line, fromResources, fromItems));
        }

        if (shortfalls.Count > 0)
        {
            error = "missing " + string.Join(", ", shortfalls);
            return false;
        }

        foreach (var line in plan.Inputs.Where(l => l.IsResource))
        {
            // Checked above, but ConsumeResource is the only thing that can say no for certain.
            if (!inventory.ConsumeResource(line.ItemId, line.Quantity))
            {
                error = $"{Describe(line.ItemId)} went missing between the check and the spend";
                return false;
            }

            spent.Add($"{Describe(line.ItemId)} x{line.Quantity}");
        }

        foreach (var draw in classDraws)
        {
            foreach (var (itemId, take) in draw.Resources)
            {
                if (!inventory.ConsumeResource(itemId, take))
                {
                    error = $"{Describe(itemId)} went missing between the check and the spend";
                    return false;
                }

                spent.Add($"{Describe(itemId)} x{take} toward class {draw.Line.ClassId}");
            }

            if (draw.Guids.Count > 0)
            {
                spent.Add($"{draw.Guids.Count} item(s) toward class {draw.Line.ClassId}");
            }
        }

        if (claimed.Count > 0)
        {
            var removed = inventory.RemoveItems(claimed);
            spent.Add($"{removed} item(s) by guid");
        }

        foreach (var line in plan.Outputs)
        {
            announce?.Invoke(line);

            if (line.IsResource)
            {
                inventory.AddResource(line.ItemId, line.Quantity);
                continue;
            }

            for (var i = 0; i < line.Quantity; i++)
            {
                inventory.CreateItem(line.ItemId);
            }
        }

        return true;
    }

    public static string Describe(uint itemId)
    {
        var item = SDBInterface.GetRootItem(itemId);
        return item == null ? $"item {itemId} (unknown)" : $"item {itemId} ({(ItemType)item.Type})";
    }

    private static bool IsResource(uint itemId)
    {
        var item = SDBInterface.GetRootItem(itemId);
        return item != null && ((ItemFlags)item.Flags).HasFlag(ItemFlags.Resource);
    }

    private static string FirstProblem(List<CraftLine> inputs, List<CraftLine> outputs, List<CraftClassLine> classInputs, List<uint> unresolved)
    {
        // A class with no members left cannot be met by anything, so a blueprint costed in one would
        // otherwise be built for free — the exact failure the run sheet warns about.
        var empty = classInputs.Where(c => c.Members.Count == 0).Select(c => c.ClassId).Distinct().ToList();
        if (empty.Count > 0)
        {
            return $"costs material class(es) {string.Join(", ", empty.Take(6))}, which no surviving item belongs to";
        }

        if (unresolved.Count > 0)
        {
            return $"names {unresolved.Count} item id(s) that no longer exist: {string.Join(", ", unresolved.Distinct().Take(6))}";
        }

        if (inputs.Count == 0 && classInputs.Count == 0)
        {
            return "has no ingredients — building it would cost nothing";
        }

        if (outputs.Count == 0)
        {
            return "has no output item";
        }

        return null;
    }
}
