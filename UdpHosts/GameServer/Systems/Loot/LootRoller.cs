using System;
using System.Collections.Generic;
using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.Loot;

/// <summary>
///     Walks a <c>dbitems::LootTable</c> down through its subtables and returns what fell out.
/// </summary>
/// <remarks>
///     <para>
///     The tables shipped intact and tuned: 2472 of them, 34159 item rows, and the first six are named
///     "CORE NPC Kill Loot 1..6 (Tiny/Small/Medium/Large/Giant/Boss)". Nothing in PIN read any of it
///     until this class.
///     </para>
///     <para>
///     What did not ship is what <c>roll_mode</c> means. The column carries six values and the client
///     is the only thing that ever interpreted them, so the two handled here are read off the data's
///     own arithmetic rather than off documentation:
///     </para>
///     <list type="bullet">
///     <item>
///     Mode 2 rolls every entry independently. "CORE NPC Kill Loot 2 (Small)" carries two subtables at
///     100 and one at 8, which only makes sense if each is its own chance.
///     </item>
///     <item>
///     Everything else picks at most one entry, weighted. "Small Crystite Drops (2-20)" is mode 0 and
///     its four rows are 80/10/5/5, summing to exactly 100 — a distribution over one result, not four
///     chances. Where weights sum below 100 the remainder is nothing, which is how mode 3's
///     "Creature Kill - Small Equipment Drop" (5/4/3/2/1) stays rare.
///     </item>
///     </list>
///     <para>
///     Modes 1, 4 and 5 exist in the file and no table reached from a zone 448 monster uses one, so
///     they fall into the weighted branch untested. Probability reads as a percentage: the tables on
///     the kill path all use small percentages, though the column tops out at 10000 elsewhere, so
///     some table somewhere is using basis points and will roll wrong here.
///     </para>
/// </remarks>
public sealed class LootRoller
{
    /// <summary>Guards against a subtable cycle in data nobody has validated.</summary>
    private const int MaxDepth = 8;

    private readonly ILootTableSource _tables;
    private readonly Random _rng;

    public LootRoller(ILootTableSource tables, Random rng)
    {
        _tables = tables;
        _rng = rng;
    }

    /// <summary>
    ///     Rolls <paramref name="lootTableId"/> and everything under it. Returns one entry per item that
    ///     dropped, already collapsed so an item reached twice down different branches appears once.
    /// </summary>
    public Dictionary<uint, uint> Roll(uint lootTableId)
    {
        var results = new Dictionary<uint, uint>();
        RollInto(lootTableId, results, 0);
        return results;
    }

    private void RollInto(uint lootTableId, Dictionary<uint, uint> results, int depth)
    {
        if (lootTableId == 0 || depth >= MaxDepth)
        {
            return;
        }

        var table = _tables.GetTable(lootTableId);
        if (table == null)
        {
            return;
        }

        var items = _tables.GetItems(lootTableId);
        var subtables = _tables.GetSubtables(lootTableId);

        if (table.RollMode == 2)
        {
            foreach (var item in items)
            {
                if (Hits(item.Probability))
                {
                    Award(results, item);
                }
            }

            foreach (var sub in subtables)
            {
                if (Hits(sub.Probability))
                {
                    RollInto(sub.SubtableId, results, depth + 1);
                }
            }

            return;
        }

        // One roll across items and subtables together, because a table that mixes them is offering a
        // choice between them rather than running two draws.
        var total = 0;
        foreach (var item in items)
        {
            total += item.Probability;
        }

        foreach (var sub in subtables)
        {
            total += sub.Probability;
        }

        if (total <= 0)
        {
            return;
        }

        // Weights that sum below 100 leave the remainder as "nothing dropped"; weights that sum above
        // it are a distribution and always produce something.
        var roll = _rng.Next(Math.Max(total, 100));

        foreach (var item in items)
        {
            roll -= item.Probability;
            if (roll < 0)
            {
                Award(results, item);
                return;
            }
        }

        foreach (var sub in subtables)
        {
            roll -= sub.Probability;
            if (roll < 0)
            {
                RollInto(sub.SubtableId, results, depth + 1);
                return;
            }
        }
    }

    private bool Hits(ushort probability)
    {
        return probability > 0 && _rng.Next(100) < probability;
    }

    private void Award(Dictionary<uint, uint> results, LootTableItemDist item)
    {
        var low = item.MinQuantity;
        var high = Math.Max(item.MaxQuantity, low);

        // Most item rows carry no quantity at all, which reads as one of the thing rather than none of
        // it — a drop that awards zero would not have been written down.
        var quantity = (uint)Math.Max(1, low == high ? low : _rng.Next(low, high + 1));

        results[item.ItemdropId] = results.GetValueOrDefault(item.ItemdropId) + quantity;
    }
}
