using System.Collections.Generic;
using GameServer.Data;

namespace GameServer.Systems.Crafting;

/// <summary>One line of a blueprint's cost or its payout.</summary>
/// <param name="ItemId">An id in <c>dbitems::RootItem</c>.</param>
/// <param name="Quantity">How many, from the blueprint row.</param>
/// <param name="IsResource">
///     Resources stack as a counter and are spent with <see cref="CharacterInventory.ConsumeResource" />;
///     everything else is a separate object per copy, held by guid.
/// </param>
public readonly record struct CraftLine(uint ItemId, uint Quantity, bool IsResource);

/// <summary>
///     One raw-material line of a blueprint's cost: so many units of a material class, any member of
///     which will do.
/// </summary>
/// <param name="ClassId">
///     A <c>RootItem.item_subtype</c>. Iron Bars, Tungsten Bars, Titanium Bars and Uranium Rods are all
///     class 128, so a recipe asking for Metals takes whichever of them you happen to be carrying.
/// </param>
/// <param name="Quantity">Total units needed, across however many members you spend.</param>
/// <param name="StatRead">
///     Which of the five stats in <c>dbitems::ResourceStat</c> the recipe reads from what you supply —
///     1 Purity, 2 Power, 3 Mass, 4 CPU, 5 unnamed. Zero means the line is a flat quantity.
///     <para>
///     Recorded and reported, but it changes nothing yet: a material's stat values are per-batch and
///     ride on the item rather than living in static data, and PIN does not write them.
///     </para>
/// </param>
/// <param name="Attribute">
///     An <c>AttributeDefinition</c> id the line feeds directly, on the 1,316 rows that carry one. Never
///     set on a line that also reads a stat — the two are exclusive.
/// </param>
/// <param name="Members">Every item of the class. Empty means the class was emptied by the 1.6 cull.</param>
public sealed record CraftClassLine(uint ClassId, uint Quantity, byte StatRead, uint Attribute, IReadOnlyList<uint> Members);

/// <summary>
///     A blueprint read out of static data and checked over: what it costs, what it pays, and whether
///     the data behind it is intact enough to build.
/// </summary>
public sealed class CraftPlan
{
    public uint BlueprintId { get; init; }

    public uint BuildTimeSecs { get; init; }

    public byte BlueprintType { get; init; }

    public List<CraftLine> Inputs { get; init; } = [];

    public List<CraftLine> Outputs { get; init; } = [];

    /// <summary>The raw-material half of the cost, priced in classes rather than items.</summary>
    public List<CraftClassLine> ClassInputs { get; init; } = [];

    /// <summary>Why this blueprint cannot be built at all, or null if it can.</summary>
    public string Problem { get; init; }

    public bool IsBuildable => Problem == null;
}
