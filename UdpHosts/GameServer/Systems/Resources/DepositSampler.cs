using System;
using System.Collections.Generic;
using System.Numerics;
using AeroMessages.GSS.V66;
using GameServer.StaticDB.Records.customdata;
using GameServer.StaticDB.Records.dbzonemetadata;

namespace GameServer.Systems.Resources;

/// <summary>One resource rolled out of a spot in a deposit.</summary>
public readonly record struct DepositYield(uint ItemId, ushort Quality, uint Quantity);

/// <summary>One resource's advertised share of a deposit, before anyone thumps it.</summary>
public readonly record struct DepositShare(uint ItemId, byte Percent);

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

    /// <summary>
    ///     What a deposit claims to hold before anyone samples it: each resource's share of the node
    ///     type's center-midpoint quantities. This is the advertisement the map draws, not a roll —
    ///     the same deposit always shows the same shares.
    /// </summary>
    public static List<DepositShare> AdvertisedShares(IReadOnlyList<ResourceNodeTypeResource> rows)
    {
        float total = 0;
        foreach (var row in rows)
        {
            total += (row.CenterLow + row.CenterHigh) / 2f;
        }

        if (total <= 0)
        {
            return [];
        }

        var shares = new List<DepositShare>(rows.Count);
        foreach (var row in rows)
        {
            var mid = (row.CenterLow + row.CenterHigh) / 2f;
            var percent = (byte)Math.Clamp(Math.Round(mid / total * 100f), 0, 100);
            if (percent == 0)
            {
                continue;
            }

            shares.Add(new DepositShare(row.ItemId, percent));
        }

        return shares;
    }

    /// <summary>
    ///     Every resource on offer inside a circle, most abundant first. This is what an outpost's map
    ///     radar advertises: the 1962 client's world map draws one disc per outpost and fills its
    ///     readout from the outpost's own <c>NearbyResourceItems</c>, so individual deposits are never
    ///     named to the client — only the resources they add up to within reach of the outpost.
    /// </summary>
    /// <param name="rowsFor">Resolves a node type to its yield rows; the caller owns the SDB lookup.</param>
    /// <param name="limit">The observer view carries sixteen slots and no more.</param>
    public static List<uint> ResourcesWithin(
        IEnumerable<ResourceDeposit> deposits,
        Vector3 center,
        float radius,
        Func<uint, IReadOnlyList<ResourceNodeTypeResource>> rowsFor,
        int limit = 16)
    {
        var abundance = new Dictionary<uint, int>();

        foreach (var deposit in deposits)
        {
            if (DistanceXY(deposit.Position, center) > radius + deposit.Radius)
            {
                continue;
            }

            foreach (var share in AdvertisedShares(rowsFor(deposit.NodeTypeId)))
            {
                abundance[share.ItemId] = abundance.GetValueOrDefault(share.ItemId) + share.Percent;
            }
        }

        var items = new List<uint>(abundance.Keys);
        items.Sort((a, b) => abundance[b] != abundance[a] ? abundance[b].CompareTo(abundance[a]) : a.CompareTo(b));

        return items.Count > limit ? items.GetRange(0, limit) : items;
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
