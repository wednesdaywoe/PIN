using System;
using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB;
using Serilog;
using LoadoutVisualType = AeroMessages.GSS.V66.Character.LoadoutConfig_Visual.LoadoutVisualType;

namespace GameServer.Data;

public class CharacterInventory
{
    private static readonly ILogger _logger = Log.ForContext<CharacterEntity>();

    private readonly Dictionary<ulong, Item> _items; // By guid
    private readonly Dictionary<uint, Resource> _resources; // By typeid
    private readonly Dictionary<int, Loadout> _loadouts; // By loadoutid

    private readonly IShard _shard;
    private readonly INetworkClient _player;
    private readonly CharacterEntity _character;

    public CharacterInventory(IShard shard, INetworkClient player, CharacterEntity character)
    {
        _shard = shard;
        _player = player;
        _character = character;
        _items = [];
        _resources = [];
        _loadouts = [];
    }

    public bool EnablePartialUpdates { get; set; }

    public void LoadHardcodedInventory()
    {
        foreach (var data in HardcodedCharacterData.TempHardcodedLoadouts)
        {
            HardcodedCharacterData.GenerateLoadoutAndItems(this, data);
        }

        foreach ((uint createId, uint chassisId) in HardcodedCharacterData.TempCharCreateLoadouts)
        {
            HardcodedCharacterData.GenerateCharCreateLoadoutAndItems(this, createId, chassisId);
        }
    }

    /// <summary>
    ///     Everything the server thinks the player is carrying, so a command can print it and settle
    ///     whether a missing item never got created or was created and never shown.
    /// </summary>
    public IEnumerable<Item> GetItems()
    {
        return _items.Values;
    }

    public IEnumerable<Resource> GetResources()
    {
        return _resources.Values;
    }

    /// <summary>
    ///     Writes how big this inventory is to the server log, per sub-inventory.
    /// </summary>
    /// <remarks>
    ///     <c>dbg_inventory</c> prints the same totals to the client console, which is the wrong place
    ///     for a queue that reads the server log, and it only fires when someone types it. This runs
    ///     at every full send, so the number is on record for every session whether or not anyone
    ///     thought to ask.
    /// </remarks>
    public void LogInventorySize(string reason)
    {
        var bySubInventory = string.Join(
            ", ",
            _items.Values
                .GroupBy(item => (InventoryType)item.SubInventory)
                .OrderByDescending(group => group.Count())
                .Select(group => $"{group.Key} {group.Count()}"));

        _logger.Information(
            "{Reason}: {ItemCount} item(s) [{BySubInventory}], {ResourceCount} resource(s), {LoadoutCount} loadout(s)",
            reason,
            _items.Count,
            bySubInventory,
            _resources.Count,
            _loadouts.Count);
    }

    public IEnumerable<uint> GetOwnedChassisIds()
    {
        return _loadouts.Values
            .Where(loadout => loadout.LoadoutType == "battleframe")
            .Select(loadout => loadout.ChassisID)
            .Distinct();
    }

    public int GetLoadoutIdForChassis(uint chassisId)
    {
        foreach (var (loadoutId, loadout) in _loadouts)
        {
            if (loadout.ChassisID == chassisId)
            {
                return loadoutId;
            }
        }

        return 0;
    }

    /// <summary>
    /// Get a loadout from the inventory by loadoutId
    /// </summary>
    /// <param name="loadoutId">The id of the loadout to get</param>
    /// <returns>The loadout data as LoadoutReferenceData or null if the loadoutId was invalid</returns>
    public LoadoutReferenceData GetLoadoutReferenceData(int loadoutId)
    {
        if (!_loadouts.TryGetValue(loadoutId, out var loadout))
        {
            return null;
        }

        var refData = new LoadoutReferenceData()
                      {
                          LoadoutId = loadoutId,
                          ChassisId = loadout.ChassisID
                      };

        var pveConfig = loadout.LoadoutConfigs[0];
        var pvpConfig = loadout.LoadoutConfigs[1];
        foreach (var itemRef in pveConfig.Items)
        {
            var item = _items[itemRef.ItemGUID];
            refData.SlottedItemsPvE.Add((LoadoutSlotType)itemRef.SlotIndex, item.SdbId);
        }

        foreach (var itemRef in pvpConfig.Items)
        {
            var item = _items[itemRef.ItemGUID];
            refData.SlottedItemsPvP.Add((LoadoutSlotType)itemRef.SlotIndex, item.SdbId);
        }

        return refData;
    }

    public ulong CreateItem(uint sdbId)
    {
        ulong guid = _shard.GetNextGuid((byte)GuidService.AdditionalTypes.Item);
        Item item = new Item()
        {
            SdbId = sdbId,
            GUID = guid,
            SubInventory = GetInventoryTypeByItemTypeId(sdbId),
            Durability = 1000,

            // The one item the 2016 capture shows being added to a live inventory — 82337, arriving
            // on its own in a partial update — carries IsBound and nothing else. PIN sent no flags
            // at all, which is the only field where a fresh item differed from the real thing.
            DynamicFlags = (byte)ItemDynamicFlags.IsBound,
            TimestampEpoch = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Modules = [],
            Unk1 = 0,
            Unk3 = 0,
            Unk4 = 0,

            // Every item sampled out of the 2016 capture carries 1 here and nothing else — full
            // updates and partial ones, every item type, equipped and not. It was the only field
            // in the struct where PIN wrote a value the live server never sent, which makes it the
            // first suspect for an item the client accepts and then never lists.
            Unk5 = 1,
            Unk6 = [],
            Unk7 = 0,
        };

        _items.Add(guid, item);
        SendItemUpdate(guid);
        return guid;
    }

    /// <summary>
    ///     Item guids that a loadout slots, and so cannot be thrown away without leaving a battleframe
    ///     with a hole where a weapon used to be.
    /// </summary>
    public HashSet<ulong> GetLoadoutItemGuids()
    {
        var guids = new HashSet<ulong>();
        foreach (var loadout in _loadouts.Values)
        {
            foreach (var config in loadout.LoadoutConfigs)
            {
                foreach (var item in config.Items)
                {
                    guids.Add(item.ItemGUID);
                }
            }
        }

        return guids;
    }

    /// <summary>
    ///     Drops items and tells the client by resending the whole inventory. There is no "this item
    ///     is gone" message in the protocol PIN implements, and a partial update can only ever add or
    ///     restate an item, so a full update with <c>ClearExistingData</c> is what a removal looks
    ///     like on the wire.
    /// </summary>
    /// <returns>How many items were actually removed.</returns>
    public int RemoveItems(IEnumerable<ulong> guids)
    {
        var removed = 0;
        foreach (var guid in guids)
        {
            if (_items.Remove(guid))
            {
                removed++;
            }
        }

        if (removed > 0)
        {
            SendFullInventory();
        }

        return removed;
    }

    public void AddResource(uint sdbId, uint quantity)
    {
        if (!_resources.ContainsKey(sdbId))
        {
            Resource resource = new Resource()
            {
                Quantity = 0,
                SdbId = sdbId,
                SubInventory = GetInventoryTypeByItemTypeId(sdbId),
                TextKey = string.Empty,
                Unk2 = 0,
            };

            _resources.Add(sdbId, resource);
        }

        var res = _resources[sdbId];
        res.Quantity += quantity;
        _resources[sdbId] = res;
        SendResourceUpdate(sdbId);
    }

    public bool ConsumeResource(uint sdbId, uint cost)
    {
        if (!_resources.TryGetValue(sdbId, out var res))
        {
            return false;
        }

        if (res.Quantity < cost)
        {
            return false;
        }
        else
        {
            res.Quantity -= cost;

            if (res.Quantity > 0)
            {
                _resources[sdbId] = res;
            }
            else
            {
                _resources.Remove(sdbId);
            }

            SendResourceUpdate(sdbId);
            return true;
        }
    }

    public uint GetResourceQuantity(uint sdbId)
    {
        return _resources.TryGetValue(sdbId, out var value) ? value.Quantity : 0;
    }

    public void AddLoadout(Loadout loadout)
    {
        _loadouts.Add(loadout.FrameLoadoutId, loadout);
    }

    /// <summary>
    ///     Sends everything the character is carrying, as one message with the items split across
    ///     three arrays because each one is length-prefixed by a byte.
    /// </summary>
    /// <remarks>
    ///     The size is logged, broken down by sub-inventory, because nothing else measures it and it
    ///     is a standing suspect for <see href="../../../Docs/In-Game-Tests/Inventory.md">I1</see>.
    ///     PIN gives a character every frame in the game at login — 20 chassis and their default
    ///     modules in two configurations each — which is not what a retail character carried, and a
    ///     client that caps a bag would have nowhere to put the next item. Resources are counted
    ///     separately here for the same reason: they arrive in their own array and
    ///     <see href="../../../Docs/In-Game-Tests/Resource-Payout.md">G1</see> proved they arrive
    ///     fine, so any ceiling that exists is on the item side.
    /// </remarks>
    public void SendFullInventory()
    {
        LogInventorySize("SendFullInventory");

        if (_items.Count > (255 * 3) - 1)
        {
            throw new NotImplementedException("Too many items in inventory, CharacterInventory.SendFullInventory has to be updated");
        }

        if (_resources.Count > 254)
        {
            throw new NotImplementedException("Too many resources in inventory, CharacterInventory.SendFullInventory has to be updated");
        }

        if (_loadouts.Count > 254)
        {
            throw new NotImplementedException("Too many loadouts in inventory, CharacterInventory.SendFullInventory has to be updated");
        }

        var update = new InventoryUpdate()
        {
            ClearExistingData = 1,
            ItemsPart1Length = 0,
            ItemsPart1 = [],
            ItemsPart2Length = 0,
            ItemsPart2 = [],
            ItemsPart3Length = 0,
            ItemsPart3 = [],
            Resources = [.. _resources.Values],
            Loadouts = [.. _loadouts.Values],
            Unk = 1,
            SecondItems = [],
            SecondResources = []
        };

        if (_items.Count >= 255)
        {
            var tmp = _items.Values.ToArray();
            update.ItemsPart1Length = 255;
            update.ItemsPart1Full = tmp[..255];
            if (_items.Count >= 510)
            {
                update.ItemsPart2Length = 255;
                update.ItemsPart2Full = tmp[255..510];

                update.ItemsPart3Length = (byte)tmp[510..^0].Length;
                update.ItemsPart3 = tmp[510..^0];
            }
            else
            {
                update.ItemsPart2Length = (byte)tmp[255..^0].Length;
                update.ItemsPart2 = tmp[255..^0];
            }
        }
        else
        {
            update.ItemsPart1Length = (byte)_items.Count;
            update.ItemsPart1 = [.. _items.Values];
        }

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(update, _character.EntityId);
    }

    public void SendItemUpdate(ulong guid)
    {
        if (!EnablePartialUpdates)
        {
            return;
        }

        var item = _items[guid];
        var update = new InventoryUpdate()
        {
            ClearExistingData = 0,
            ItemsPart1Length = 1,
            ItemsPart1 =
            [
                item
            ],
            ItemsPart2Length = 0,
            ItemsPart2 = [],
            ItemsPart3Length = 0,
            ItemsPart3 = [],
            Resources = [],
            Loadouts = [],

            // 0 on a partial update and 1 on a full one, which is the split every InventoryUpdate
            // in the 2016 capture follows. PIN was sending 1 on both.
            Unk = 0,
            SecondItems = [],
            SecondResources = []
        };

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(update, _character.EntityId);
    }

    public void SendResourceUpdate(uint sdbId)
    {
        if (!EnablePartialUpdates)
        {
            return;
        }

        var resource = _resources[sdbId];
        var update = new InventoryUpdate()
        {
            ClearExistingData = 0,
            ItemsPart1Length = 0,
            ItemsPart1 = [],
            ItemsPart2Length = 0,
            ItemsPart2 = [],
            ItemsPart3Length = 0,
            ItemsPart3 = [],
            Resources =
            [
                resource
            ],
            Loadouts = [],
            Unk = 0, // Partial update, see SendItemUpdate
            SecondItems = [],
            SecondResources = []
        };

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(update, _character.EntityId);
    }

    public void SendEquipmentChanges(ulong oldItemGuid, ulong newItemGuid)
    {
        if (!EnablePartialUpdates)
        {
            return;
        }

        var itemChanges = new Item[]
        {
        };

        if (oldItemGuid != 0)
        {
            var oldItem = _items[oldItemGuid];
            itemChanges = [.. itemChanges, oldItem];
        }

        if (newItemGuid != 0)
        {
            var newItem = _items[newItemGuid];
            itemChanges = [.. itemChanges, newItem];
        }

        var update = new InventoryUpdate()
        {
            ClearExistingData = 0,
            ItemsPart1Length = (byte)itemChanges.Length,
            ItemsPart1 = itemChanges,
            ItemsPart2Length = 0,
            ItemsPart2 = [],
            ItemsPart3Length = 0,
            ItemsPart3 = [],
            Resources = [],
            Loadouts = [.. _loadouts.Values],
            Unk = 0, // Partial update, see SendItemUpdate
            SecondItems = [],
            SecondResources = []
        };

        _player.NetChannels[ChannelType.ReliableGss].SendMessage(update, _character.EntityId);
    }

    public void EquipItemByGUID(int loadoutId, LoadoutSlotType slot, ulong guid)
    {
        ulong changedOldItemGUID = 0;
        ulong changedNewItemGUID = guid;

        // Unequip old Item (if any)
        if (_loadouts[loadoutId].LoadoutConfigs[0].Items.Any((e) => e.SlotIndex == (byte)slot))
        {
            // Set Item to unequipped
            var oldItemGUID = _loadouts[loadoutId].LoadoutConfigs[0].Items.First((e) => e.SlotIndex == (byte)slot).ItemGUID;
            changedOldItemGUID = oldItemGUID;
            var oldItem = _items[oldItemGUID];
            oldItem.DynamicFlags = (byte)(oldItem.DynamicFlags ^ (byte)ItemDynamicFlags.IsEquipped);
            _items[oldItemGUID] = oldItem;

            // Update CurrentLoadout
            _character.CurrentLoadout.SlottedItems[slot] = 0;

            // Update LoadoutConfigs
            _loadouts[loadoutId].LoadoutConfigs[0].Items = [.. _loadouts[loadoutId].LoadoutConfigs[0].Items.Where(e => e.SlotIndex != (byte)slot)];
        }

        // Equip new item (if any)
        if (guid != 0)
        {
            // Update Item to Equipped
            var item = _items[guid];
            item.DynamicFlags = (byte)(item.DynamicFlags | (byte)ItemDynamicFlags.IsEquipped);
            _items[guid] = item;

            // Update CurrentLoadout
            _character.CurrentLoadout.SlottedItems[slot] = item.SdbId;

            // Update LoadoutConfig
            _loadouts[loadoutId].LoadoutConfigs[0].Items = [.. _loadouts[loadoutId].LoadoutConfigs[0].Items, new LoadoutConfig_Item() { ItemGUID = guid, SlotIndex = (byte)slot }];
        }

        // Update StaticInfo when visuals are changed
        var equippedSdbId = (guid != 0) ? _items[guid].SdbId : 0;
        switch (slot)
        {
            case LoadoutSlotType.Glider:
                _character.SetStaticInfo(_character.StaticInfo with { LoadoutGlider = equippedSdbId });
                break;
            case LoadoutSlotType.Vehicle:
                _character.SetStaticInfo(_character.StaticInfo with { LoadoutVehicle = equippedSdbId });
                break;
        }

        SendEquipmentChanges(changedOldItemGUID, changedNewItemGUID);
    }

    public void EquipVisualBySdbId(int loadoutId, LoadoutVisualType visual, LoadoutSlotType slot, uint sdb_id)
    {
        // Unequip old item (if any)
        if (_loadouts[loadoutId].LoadoutConfigs[0].Visuals.Any(i => i.VisualType == visual))
        {
            // Update Visuals
            _loadouts[loadoutId].LoadoutConfigs[0].Visuals = [.. _loadouts[loadoutId].LoadoutConfigs[0].Visuals.Where(e => e.VisualType != visual)];
        }

        // Equip new item (if any)
        if (sdb_id != 0)
        {
            // Update Visuals
            _loadouts[loadoutId].LoadoutConfigs[0].Visuals =
            [
                .. _loadouts[loadoutId].LoadoutConfigs[0].Visuals,
                new LoadoutConfig_Visual() { ItemSdbId = sdb_id, VisualType = visual, Data1 = 0, Data2 = 0, Transform = [] },
            ];
            _ = _items.First(e => e.Value.SdbId == sdb_id).Value;
        }

        var equippedGUID = (sdb_id != 0) ? _items.First(e => e.Value.SdbId == sdb_id).Value.GUID : 0;
        EquipItemByGUID(loadoutId, slot, equippedGUID);
    }

    private byte GetInventoryTypeByItemTypeId(uint sdbId)
    {
        var itemInfo = SDBInterface.GetRootItem(sdbId);
        if (itemInfo != null)
        {
            return GetInventoryTypeByItemType(itemInfo.Type);
        }
        else
        {
            return (byte)InventoryType.Bag;
        }
    }

    private byte GetInventoryTypeByItemType(byte itemType)
    {
        var result = InventoryType.Bag;
        switch ((ItemType)itemType)
        {
            case ItemType.TinkerTools:
                result = InventoryType.Bag;
                break;
            case ItemType.ItemModule:
                result = InventoryType.Bag;
                break;
            case ItemType.PaletteModule:
                result = InventoryType.Bag;
                break;
            case ItemType.CraftingStation:
                result = InventoryType.Bag;
                break;
            case ItemType.ResourceItem:
                result = InventoryType.Bag;
                break;
            case ItemType.LockBoxKey:
                result = InventoryType.Bag;
                break;
            case ItemType.Basic: // NOTE: There are some basic type items and resources that go into cache
                result = InventoryType.Bag;
                break;
            case ItemType.Consumable:
                result = InventoryType.Cache;
                break;
            case ItemType.AbilityModule:
                result = InventoryType.Gear;
                break;
            case ItemType.FrameModule:
                result = InventoryType.Gear;
                break;
            case ItemType.Weapon:
                result = InventoryType.Gear;
                break;
            case ItemType.Chassis:
                result = InventoryType.Gear;
                break;
            default:
                _logger.Warning("Unknown InventoryType for ItemType {ItemType}, defaulting to {Result}", (ItemType)itemType, result);
                break;
        }

        return (byte)result;
    }
}