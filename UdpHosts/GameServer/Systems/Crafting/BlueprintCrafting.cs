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
///     Ingredients come from <c>dbitems::Blueprint_Items</c>, whose <c>item_type</c> resolves to a real
///     item on 25,823 of its 25,881 rows. <c>Blueprint_Resources</c> is checked but never satisfiable —
///     see <see cref="SDBInterface.GetBlueprintResources" /> — so a blueprint citing one is refused
///     rather than built for free.
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

        var plan = new CraftPlan
        {
            BlueprintId = blueprintId,
            BuildTimeSecs = blueprint.BuildTimeSecs,
            BlueprintType = blueprint.BlueprintType,
            Inputs = inputs,
            Outputs = outputs,
            Problem = FirstProblem(blueprintId, inputs, outputs, unresolved),
        };

        return plan;
    }

    /// <summary>
    ///     Checks the whole cost, then spends it, then pays out — in that order, so a shortfall on the
    ///     last ingredient cannot leave the first one already gone.
    /// </summary>
    /// <returns>True if the item was built. On false, <paramref name="error" /> says what was missing.</returns>
    public static bool TryCraft(CharacterInventory inventory, CraftPlan plan, out string error, out List<string> spent)
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

        if (claimed.Count > 0)
        {
            var removed = inventory.RemoveItems(claimed);
            spent.Add($"{removed} item(s) by guid");
        }

        foreach (var line in plan.Outputs)
        {
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

    private static string FirstProblem(uint blueprintId, List<CraftLine> inputs, List<CraftLine> outputs, List<uint> unresolved)
    {
        // Raw-material rows can never be met, so a blueprint carrying one would otherwise be built for
        // free — the exact failure the run sheet warns about.
        var resources = SDBInterface.GetBlueprintResources(blueprintId).Where(r => r.IsRequired != 0).ToList();
        if (resources.Count > 0)
        {
            var classes = string.Join(", ", resources.Select(r => r.ItemType).Distinct().Take(6));
            return $"costs {resources.Count} raw-material row(s) from Blueprint_Resources (classes {classes}); no 1962 item belongs to those classes, so the cost cannot be met";
        }

        if (unresolved.Count > 0)
        {
            return $"names {unresolved.Count} item id(s) that no longer exist: {string.Join(", ", unresolved.Distinct().Take(6))}";
        }

        if (inputs.Count == 0)
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
