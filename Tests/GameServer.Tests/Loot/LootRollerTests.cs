using System;
using System.Collections.Generic;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Loot;
using Xunit;

namespace GameServer.Tests.Loot;

/// <summary>
///     The loot chain a zone 448 monster actually walks, rebuilt out of the real rows so the roller can
///     be checked without a <c>clientdb.sd2</c>.
///
///     What these pin is the reading of <c>roll_mode</c>, which is a guess. The column shipped with six
///     values and no documentation, and the two behaviours here were inferred from the arithmetic of the
///     tables themselves: 80/10/5/5 summing to exactly 100 is a distribution over one result, while
///     100/100/8 on the same table can only be three independent chances. If that reading is wrong these
///     tests still pass and the drop rates in game are wrong, which is what
///     Docs/In-Game-Tests/Kill-Rewards.md is for.
/// </summary>
public class LootRollerTests
{
    /// <summary>Crystite, the only resource on this path. Every other id here is an item.</summary>
    private const uint Crystite = 10;

    /// <summary>"CORE Gaia Creature Kill Loot 2 (Small)" — what monster type 528 points at.</summary>
    private const uint GaiaSmallKill = 5703;

    /// <summary>"CORE NPC Kill Loot 2 (Small) - Crystite/Powerups Subtable".</summary>
    private const uint NpcKillLoot2 = 2;

    /// <summary>"Small Crystite Drops (2-20)".</summary>
    private const uint SmallCrystite = 25;

    [Fact]
    public void AWeightedTableSummingToOneHundredAlwaysProducesExactlyOneResult()
    {
        var roller = new LootRoller(Zone448Tables(), new Random(1));

        for (var i = 0; i < 200; i++)
        {
            var drops = roller.Roll(SmallCrystite);

            Assert.Single(drops);
            Assert.True(drops[Crystite] is >= 2 and <= 20);
        }
    }

    [Fact]
    public void ModeTwoRollsEveryEntryIndependentlySoACertaintyAlwaysFires()
    {
        var roller = new LootRoller(Zone448Tables(), new Random(2));

        // 5703's two 100% subtables both fire every time; only the 5% resource-tier one is a gamble.
        // Reaching crystite still needs 2's own 25% roll, so the assertion is on the branch below it
        // being taken at all rather than on crystite arriving.
        var reached = 0;
        for (var i = 0; i < 400; i++)
        {
            if (roller.Roll(GaiaSmallKill).ContainsKey(Crystite))
            {
                reached++;
            }
        }

        // 25% of 400 is 100. A seeded run lands near it; the band is wide enough that only a broken
        // reading of the mode — every time, or never — trips it.
        Assert.InRange(reached, 60, 145);
    }

    /// <summary>
    ///     "Melded Loot Common" (5463), verbatim — monster 528's second loot table reaches it. Its rows are
    ///     5000/1000/100, which cannot be percentages, and reading them as such made a table with a rarity
    ///     gradient drop something on every kill. K1's first run is what surfaced it.
    /// </summary>
    [Fact]
    public void ATableWithRowsAboveOneHundredIsReadOutOfTenThousand()
    {
        var tables = new FakeTables();
        tables.AddTable(5463, rollMode: 0);
        tables.AddItem(5463, itemId: 77343, min: 0, max: 0, probability: 5000);
        tables.AddItem(5463, itemId: 77344, min: 0, max: 0, probability: 1000);
        tables.AddItem(5463, itemId: 77345, min: 0, max: 0, probability: 100);

        var roller = new LootRoller(tables, new Random(7));

        var dropped = 0;
        for (var i = 0; i < 1000; i++)
        {
            if (roller.Roll(5463).Count > 0)
            {
                dropped++;
            }
        }

        // 6100 out of 10000. Read as percent this is 1000 of 1000, which is the bug.
        Assert.InRange(dropped, 560, 660);
    }

    [Fact]
    public void AMissingTableDropsNothingRatherThanThrowing()
    {
        var roller = new LootRoller(Zone448Tables(), new Random(3));

        Assert.Empty(roller.Roll(999999));
        Assert.Empty(roller.Roll(0));
    }

    [Fact]
    public void ASubtableCycleTerminates()
    {
        var tables = new FakeTables();
        tables.AddTable(1, rollMode: 2);
        tables.AddTable(2, rollMode: 2);
        tables.AddSubtable(1, subtableId: 2, probability: 100);
        tables.AddSubtable(2, subtableId: 1, probability: 100);
        tables.AddItem(2, itemId: Crystite, min: 1, max: 1, probability: 100);

        // Two tables that name each other, both certain. Without the depth guard this recurses until
        // the stack runs out; with it, the award happens a bounded number of times and the call returns.
        var drops = new LootRoller(tables, new Random(4)).Roll(1);

        Assert.InRange(drops[Crystite], 1u, 8u);
    }

    [Fact]
    public void AQuantityRangeStaysInsideItsBounds()
    {
        var tables = new FakeTables();
        tables.AddTable(1, rollMode: 0);
        tables.AddItem(1, itemId: Crystite, min: 70, max: 150, probability: 100);

        var roller = new LootRoller(tables, new Random(5));

        for (var i = 0; i < 200; i++)
        {
            Assert.True(roller.Roll(1)[Crystite] is >= 70 and <= 150);
        }
    }

    [Fact]
    public void ARowWithNoQuantityAwardsOne()
    {
        var tables = new FakeTables();
        tables.AddTable(1, rollMode: 0);
        tables.AddItem(1, itemId: 33816, min: 0, max: 0, probability: 100);

        Assert.Equal(1u, new LootRoller(tables, new Random(6)).Roll(1)[33816]);
    }

    /// <summary>
    ///     Monster 528's chain, verbatim from the client db. 5675 and 6080 are stubbed empty — they are
    ///     equipment and resource-item tiers, neither of which this milestone pays.
    /// </summary>
    private static FakeTables Zone448Tables()
    {
        var tables = new FakeTables();

        tables.AddTable(GaiaSmallKill, rollMode: 2);
        tables.AddSubtable(GaiaSmallKill, subtableId: NpcKillLoot2, probability: 100);
        tables.AddSubtable(GaiaSmallKill, subtableId: 5675, probability: 100);
        tables.AddSubtable(GaiaSmallKill, subtableId: 6080, probability: 5);

        tables.AddTable(NpcKillLoot2, rollMode: 2);
        tables.AddSubtable(NpcKillLoot2, subtableId: SmallCrystite, probability: 25);
        tables.AddItem(NpcKillLoot2, itemId: 33816, min: 1, max: 1, probability: 20);
        tables.AddItem(NpcKillLoot2, itemId: 33815, min: 1, max: 1, probability: 15);

        tables.AddTable(SmallCrystite, rollMode: 0);
        tables.AddItem(SmallCrystite, itemId: Crystite, min: 2, max: 4, probability: 80);
        tables.AddItem(SmallCrystite, itemId: Crystite, min: 5, max: 10, probability: 10);
        tables.AddItem(SmallCrystite, itemId: Crystite, min: 11, max: 15, probability: 5);
        tables.AddItem(SmallCrystite, itemId: Crystite, min: 16, max: 20, probability: 5);

        tables.AddTable(5675, rollMode: 3);
        tables.AddTable(6080, rollMode: 3);

        return tables;
    }

    private sealed class FakeTables : ILootTableSource
    {
        private readonly Dictionary<uint, LootTable> _tables = new();
        private readonly Dictionary<uint, List<LootTableItemDist>> _items = new();
        private readonly Dictionary<uint, List<LootTableSubTableDist>> _subtables = new();

        public LootTable GetTable(uint id) => _tables.GetValueOrDefault(id);

        public IReadOnlyList<LootTableItemDist> GetItems(uint lootTableId) => _items.GetValueOrDefault(lootTableId) ?? [];

        public IReadOnlyList<LootTableSubTableDist> GetSubtables(uint lootTableId) => _subtables.GetValueOrDefault(lootTableId) ?? [];

        public void AddTable(uint id, byte rollMode)
        {
            _tables[id] = new LootTable { Id = id, RollMode = rollMode, Name = $"table {id}" };
        }

        public void AddItem(uint lootTableId, uint itemId, ushort min, ushort max, ushort probability)
        {
            if (!_items.TryGetValue(lootTableId, out var rows))
            {
                rows = [];
                _items[lootTableId] = rows;
            }

            rows.Add(new LootTableItemDist
            {
                LootTableId = (ushort)lootTableId,
                ItemdropId = itemId,
                MinQuantity = min,
                MaxQuantity = max,
                Probability = probability,
            });
        }

        public void AddSubtable(uint lootTableId, uint subtableId, ushort probability)
        {
            if (!_subtables.TryGetValue(lootTableId, out var rows))
            {
                rows = [];
                _subtables[lootTableId] = rows;
            }

            rows.Add(new LootTableSubTableDist
            {
                LootTableId = (ushort)lootTableId,
                SubtableId = (ushort)subtableId,
                Probability = probability,
            });
        }
    }
}
