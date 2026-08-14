using System;
using System.Collections.Generic;
using System.Numerics;
using AeroMessages.GSS.V66;
using GameServer.StaticDB.Records.customdata;
using GameServer.StaticDB.Records.dbzonemetadata;

namespace GameServer.Systems.Resources;

/// <summary>One resource rolled out of a spot in a deposit.</summary>
public readonly record struct DepositYield(uint ItemId, ushort Quality, uint Quantity);

/// <summary>
///     The arithmetic of a deposit: which one covers a spot, how far from its heart the spot is, and
///     what the shipped gradient pays there.
/// </summary>
/// <remarks>
///     Distance is measured in X and Y only. A thumper stands on the player's own footing and the
///     scan overlay projects onto the map, so height never decides anything here — which is also what
///     makes deposits safe to author without server-side terrain.
///
///     The gradient itself is shipped data, not invention: <c>dbzonemetadata::ResourceNodeTypeResource</c>
///     gives every resource in a node type a quantity range at the center and another at the rim, and
///     this samples a linear blend of the two at the spot's distance fraction. Only the deposit
///     positions are PIN's own content.
/// </remarks>
public static class DepositSampler
{
    /// <summary>
    ///     The deposit covering a spot, or null for barren ground. Overlapping deposits go to the
    ///     nearest center, so authoring two discs over one valley stays predictable.
    /// </summary>
    public static ResourceDeposit FindDeposit(IEnumerable<ResourceDeposit> deposits, Vector3 position)
    {
        ResourceDeposit best = null;
        var bestDistance = float.MaxValue;

        foreach (var deposit in deposits)
        {
            var distance = DistanceXY(deposit.Position, position);
            if (distance <= deposit.Radius && distance < bestDistance)
            {
                best = deposit;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>0 at the deposit's center, 1 at its rim and beyond.</summary>
    public static float DistanceFraction(ResourceDeposit deposit, Vector3 position)
    {
        if (deposit.Radius <= 0f)
        {
            return 0f;
        }

        return Math.Clamp(DistanceXY(deposit.Position, position) / deposit.Radius, 0f, 1f);
    }

    /// <summary>
    ///     Rolls every resource of a node type at a distance fraction. Rows that roll zero are left
    ///     out, so a rim sample of a center-heavy vein can legitimately come back empty.
    /// </summary>
    public static List<DepositYield> Sample(IReadOnlyList<ResourceNodeTypeResource> rows, float distanceFraction, Random rng)
    {
        var yields = new List<DepositYield>();

        foreach (var row in rows)
        {
            var low = Lerp(row.CenterLow, row.EdgeLow, distanceFraction);
            var high = Lerp(row.CenterHigh, row.EdgeHigh, distanceFraction);
            if (high < low)
            {
                (low, high) = (high, low);
            }

            var quantity = (uint)rng.Next((int)Math.Round(low), (int)Math.Round(high) + 1);
            if (quantity == 0)
            {
                continue;
            }

            var qualityLow = Math.Min(row.ItemQualityLow, row.ItemQualityHigh);
            var qualityHigh = Math.Max(row.ItemQualityLow, row.ItemQualityHigh);
            var quality = (ushort)rng.Next((int)qualityLow, (int)qualityHigh + 1);

            yields.Add(new DepositYield(row.ItemId, quality, quantity));
        }

        return yields;
    }

    /// <summary>
    ///     The same yields as percentages of the whole, which is the shape both
    ///     <c>GeographicalReportResponse</c> and <c>ResourceNodeCompletedEvent</c> carry.
    /// </summary>
    public static ResourceCompositionData[] ToComposition(IReadOnlyList<DepositYield> yields)
    {
        float total = 0;
        foreach (var y in yields)
        {
            total += y.Quantity;
        }

        if (total == 0)
        {
            return [];
        }

        var composition = new ResourceCompositionData[yields.Count];
        for (var i = 0; i < yields.Count; i++)
        {
            composition[i] = new ResourceCompositionData
            {
                ItemTypeId = yields[i].ItemId,
                ResourceQuality = yields[i].Quality,
                Percent = yields[i].Quantity / total * 100f,
            };
        }

        return composition;
    }

    /// <summary>Flat distance, the only kind a deposit measures. Public because the scan's range check must agree with it — zone 448's basin floor is 91m below the station shelf, and a 3D reading would push deposits a short walk away out of range.</summary>
    public static float DistanceXY(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static float Lerp(float center, float edge, float t) => center + ((edge - center) * t);
}
