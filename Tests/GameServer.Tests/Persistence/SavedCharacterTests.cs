using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Data.Persistence;
using GameServer.Enums;
using Xunit;

namespace GameServer.Tests.Persistence;

/// <summary>
///     What a character carries out of a session, mapped from the live inventory structs to the ones
///     that go to disk. Pure over its inputs, so it's the half of persistence that can be checked
///     without a shard.
/// </summary>
public class SavedCharacterTests
{
    private static readonly DateTimeOffset SavedAt = DateTimeOffset.FromUnixTimeSeconds(1755000000);

    [Fact]
    public void CarriesResourcesAndItemsThrough()
    {
        var saved = Snapshot(
            resources: [Resource(10, 89), Resource(86668, 32)],
            items: [Item(56826), Item(78711)]);

        Assert.Equal([(10u, 89u), (86668u, 32u)], saved.Resources.Select(r => (r.SdbId, r.Quantity)));
        Assert.Equal([56826u, 78711u], saved.Items.Select(i => i.SdbId));
        Assert.Equal(448u, saved.LastZoneId);
        Assert.Equal(17u, saved.LastOutpostId);
        Assert.Equal(SavedCharacter.CurrentVersion, saved.Version);
    }

    /// <summary>
    ///     A resource can be spent down to nothing, and <c>ConsumeResource</c> drops the row when it is.
    ///     Anything that reaches here at zero is noise, and writing it would resurrect an empty stack on
    ///     the next login.
    /// </summary>
    [Fact]
    public void DropsResourcesTheCharacterNoLongerHas()
    {
        var saved = Snapshot(resources: [Resource(10, 0), Resource(86668, 4)], items: []);

        Assert.Equal([86668u], saved.Resources.Select(r => r.SdbId));
    }

    /// <summary>
    ///     Nothing restores the loadout, so an item that came back still flagged equipped would claim a
    ///     slot no loadout is holding it in.
    /// </summary>
    [Fact]
    public void ClearsTheEquippedFlagButKeepsTheRest()
    {
        var equipped = Item(56826);
        equipped.DynamicFlags = (byte)(ItemDynamicFlags.IsBound | ItemDynamicFlags.IsEquipped);

        var saved = Snapshot(resources: [], items: [equipped]);

        Assert.Equal((byte)ItemDynamicFlags.IsBound, saved.Items[0].DynamicFlags);
    }

    [Fact]
    public void KeepsDurabilityModulesAndTheOriginalTimestamp()
    {
        var item = Item(56826);
        item.Durability = 640;
        item.Modules = [101, 202];
        item.TimestampEpoch = 1479150435;

        var saved = Snapshot(resources: [], items: [item]);

        Assert.Equal(640, saved.Items[0].Durability);
        Assert.Equal([101u, 202u], saved.Items[0].Modules);
        Assert.Equal(1479150435u, saved.Items[0].TimestampEpoch);
    }

    /// <summary>
    ///     The autosave skips writing when the serialized snapshot matches the last one, so a snapshot
    ///     has to be stable against the order a dictionary happens to enumerate in. Otherwise an idle
    ///     player rewrites their save every minute forever.
    /// </summary>
    [Fact]
    public void SerializesIdenticallyWhateverOrderTheInventoryEnumeratesIn()
    {
        var forwards = Snapshot(
            resources: [Resource(10, 89), Resource(86668, 32)],
            items: [Item(56826), Item(78711)]);

        var backwards = Snapshot(
            resources: [Resource(86668, 32), Resource(10, 89)],
            items: [Item(78711), Item(56826)]);

        Assert.Equal(JsonSerializer.Serialize(forwards), JsonSerializer.Serialize(backwards));
    }

    private static SavedCharacter Snapshot(IEnumerable<Resource> resources, IEnumerable<Item> items)
    {
        return SavedCharacter.Snapshot(0x99aabbccddee01c0, 448, 17, 3600, resources, items, SavedAt);
    }

    private static Resource Resource(uint sdbId, uint quantity)
    {
        return new Resource { SdbId = sdbId, Quantity = quantity, SubInventory = 1, TextKey = string.Empty, Unk2 = 0 };
    }

    private static Item Item(uint sdbId)
    {
        return new Item
        {
            SdbId = sdbId,
            GUID = 0x1f00000000000000ul + sdbId,
            SubInventory = 2,
            Durability = 1000,
            DynamicFlags = (byte)ItemDynamicFlags.IsBound,
            TimestampEpoch = 1755000000,
            Modules = []
        };
    }
}
