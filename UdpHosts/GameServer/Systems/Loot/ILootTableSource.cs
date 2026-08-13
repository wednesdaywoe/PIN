using System.Collections.Generic;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;

namespace GameServer.Systems.Loot;

/// <summary>
///     The three <c>dbitems</c> loot tables, behind a lookup <see cref="LootRoller"/> can be tested
///     against without a <c>clientdb.sd2</c>.
/// </summary>
public interface ILootTableSource
{
    LootTable GetTable(uint id);

    IReadOnlyList<LootTableItemDist> GetItems(uint lootTableId);

    IReadOnlyList<LootTableSubTableDist> GetSubtables(uint lootTableId);
}

/// <summary>The shipped tables, as the server loads them.</summary>
public sealed class SdbLootTableSource : ILootTableSource
{
    public static readonly SdbLootTableSource Instance = new();

    public LootTable GetTable(uint id) => SDBInterface.GetLootTable(id);

    public IReadOnlyList<LootTableItemDist> GetItems(uint lootTableId) => SDBInterface.GetLootTableItemDist(lootTableId) ?? [];

    public IReadOnlyList<LootTableSubTableDist> GetSubtables(uint lootTableId) => SDBInterface.GetLootTableSubTableDist(lootTableId) ?? [];
}
