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

    /// <summary>Why this blueprint cannot be built at all, or null if it can.</summary>
    public string Problem { get; init; }

    public bool IsBuildable => Problem == null;
}
