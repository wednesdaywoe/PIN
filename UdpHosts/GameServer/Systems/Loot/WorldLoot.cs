using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using AeroMessages.Common;
using AeroMessages.GSS.V66.AreaVisualData.View;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities;
using GameServer.Entities.AreaVisualData;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB;
using Serilog;

namespace GameServer.Systems.Loot;

/// <summary>
///     Loot lying on the ground: the boards that hold it, the drop, and the pickup.
/// </summary>
/// <remarks>
///     <para>
///     Read out of the 2016 capture rather than guessed. It carries 62 <c>LootObjectView</c> messages and
///     41 <c>CollectLoot</c> commands, which is the whole loop live: the server publishes a 24-slot table
///     of drops on an <c>AreaVisualData</c> entity, and the client answers with the entity, the slot index
///     and the item id it believes it is taking.
///     </para>
///     <para>
///     Two shapes appear in that capture and the split is exact. A powerup (33815 Health, 33816 Ammo) has
///     <c>HaveEntity = 0</c> and carries <c>Unk3 = {1, 0}</c>; everything else — crystite and equipment —
///     names an owning character and carries no <c>Unk3</c>. So <c>Unk3.Unk1 = 1</c> reads as "anyone may
///     take this", and PIN writes it the same way. Retail dropped crystite on the ground too, which PIN
///     does not yet: resources still pay straight into the ledger, because that path is verified and this
///     one is new.
///     </para>
///     <para>
///     A board is per-place — not one for the world, and not one per drop. The capture settles it: its 62
///     view messages name about forty entities, one pickup run took slots 1, 2, 3, 7, 16, 18 and 23 off a
///     single one, and none of those entities ever carried an <c>ObserverView</c>. So a board is a table
///     that fills with whatever falls near it. PIN reuses one within <see cref="BoardRadius" /> metres
///     that still has a free slot, opens a new one otherwise, and closes a board that has stood empty for
///     a minute. Each drop carries its own world position, which is what the client draws.
///     </para>
/// </remarks>
public sealed class WorldLoot
{
    /// <summary>The view's own size. 24 <c>LootObjects_N</c> slots, no more.</summary>
    public const uint SlotCount = 24;

    /// <summary>
    ///     How long a drop lies there. PIN's number: nothing shipped says what retail used, and the
    ///     capture cannot answer it either — a drop nobody collected is a drop nobody recorded the end of.
    ///     Two minutes outlasts the fight that produced it.
    /// </summary>
    private const uint LifetimeMs = 120_000;

    /// <summary>How close you have to be. The client decides when to offer a pickup; this only refuses one that came from nowhere.</summary>
    private const float CollectRange = 15f;

    /// <summary>How far a drop can fall from a board before it wants a board of its own.</summary>
    private const float BoardRadius = 50f;

    private const float BoardScopeRange = 200f;
    private const uint BoardIdleMs = 60_000;
    private const uint SweepIntervalMs = 1000;

    private readonly IShard _shard;
    private readonly ILogger _logger;
    private readonly List<Board> _boards = [];

    private ulong _lastSweep;

    public WorldLoot(IShard shard)
    {
        _shard = shard;
        _logger = shard.Logger.ForContext<WorldLoot>();
    }

    /// <summary>
    ///     Puts one stack on the ground.
    /// </summary>
    /// <param name="owner">Who may take it, or null for anyone. A powerup is always anyone's.</param>
    /// <returns>False if nothing was dropped, which the log always explains.</returns>
    public bool Drop(Vector3 position, uint itemId, uint quantity, CharacterEntity owner, string reason)
    {
        var root = SDBInterface.GetRootItem(itemId);
        if (root == null)
        {
            _logger.Warning("{Reason} tried to drop item {ItemId}, which has no RootItem row", reason, itemId);
            return false;
        }

        var board = BoardNear(position);
        if (!board.TryTakeSlot(out var index))
        {
            // Only reachable if a fresh board is somehow full, which cannot happen today. Kept because
            // "nothing dropped" has to say why.
            _logger.Warning("{Reason}: board {EntityId:x} has no free slot for item {ItemId}", reason, board.Entity.EntityId, itemId);
            return false;
        }

        // Quantity is a byte on the wire, so a stack larger than 255 cannot be described. The largest in
        // the capture is 58 crystite.
        var onWire = (byte)Math.Clamp(quantity, 1, byte.MaxValue);

        var data = new LootObjectData
        {
            Time = _shard.CurrentTime,
            HaveEntity = (byte)(owner == null ? 0 : 1),
            Entity = owner?.AeroEntityId ?? new EntityId { Backing = 0 },
            HaveUnk3 = (byte)(owner == null ? 1 : 0),
            Unk3 = new LootObjectUnkOptionalData { Unk1 = 1, Unk2 = 0 },

            // Three halves whose meaning never came out of the capture — the values run about -3.5 to 3,
            // both signs, and nothing in the loop reads them back. Left at zero rather than guessed at.
            Unk4 = default,

            Position = position,
            LootSdbId = itemId,
            Quantity = onWire,
            Unk5 = 0,
            Unk6 = 0,
            Unk7 = [0, 0],
        };

        board.Set(index, data);
        board.Drops[index] = new HeldDrop
        {
            ItemId = itemId,
            Quantity = onWire,
            Position = position,
            OwnerId = owner?.EntityId ?? 0,
            ExpireAt = _shard.CurrentTimeLong + LifetimeMs,
        };

        _logger.Information(
            "{Reason} dropped item {ItemId} ({ItemType}) x{Quantity} at {Position} — board {EntityId:x} slot {Slot}, {Ownership}",
            reason,
            itemId,
            (ItemType)root.Type,
            onWire,
            position,
            board.Entity.EntityId,
            index,
            owner == null ? "free to anyone" : $"owned by {owner.EntityId}");

        return true;
    }

    /// <summary>
    ///     Answers the client's <c>CollectLoot</c>. Everything it names is checked before anything is
    ///     given: the board has to exist, the slot has to hold the item the client thinks it does, and the
    ///     character has to be near enough to reach it.
    /// </summary>
    public void Collect(INetworkPlayer collector, ulong boardEntityId, uint index, uint itemId)
    {
        var character = collector?.CharacterEntity;
        if (character == null)
        {
            return;
        }

        var board = _boards.Find(b => b.Entity.EntityId == boardEntityId);
        if (board == null)
        {
            _logger.Debug("CollectLoot from {Character} named entity {EntityId:x}, which is not a loot board", character.EntityId, boardEntityId);
            return;
        }

        if (!board.Drops.TryGetValue(index, out var drop))
        {
            // Two players reaching for the same drop is the ordinary way to get here, so this is not a
            // warning.
            _logger.Debug("CollectLoot from {Character} named empty slot {Slot}", character.EntityId, index);
            return;
        }

        if (drop.ItemId != itemId)
        {
            _logger.Warning(
                "CollectLoot from {Character} named item {ClaimedId} in slot {Slot}, which holds {ActualId}",
                character.EntityId,
                itemId,
                index,
                drop.ItemId);
            return;
        }

        if (drop.OwnerId != 0 && drop.OwnerId != character.EntityId)
        {
            _logger.Information("CollectLoot: slot {Slot} belongs to {Owner}, not {Character}", index, drop.OwnerId, character.EntityId);
            return;
        }

        var distance = Vector3.Distance(character.Position, drop.Position);
        if (distance > CollectRange)
        {
            _logger.Warning(
                "CollectLoot from {Character} for slot {Slot} at {Distance:0.0}m, beyond {Range}m — not paid",
                character.EntityId,
                index,
                distance,
                CollectRange);
            return;
        }

        board.Clear(index);

        // The pickup is announced before whatever it turns into arrives — the order the live server used,
        // and the one CreateItemServerCommand already follows.
        collector.NetChannels[ChannelType.ReliableGss].SendMessage(
            new SimulateLootPickup
            {
                Item = new RewardInfoData { SdbId = drop.ItemId, Quantity = (ushort)drop.Quantity },
                RewardType = SimulateLootPickup.Type.General,
            },
            character.EntityId);

        Deliver(collector, character, drop);
    }

    /// <summary>Ages drops out of their slots, and boards out of the world once they have been empty a while.</summary>
    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (currentTime < _lastSweep + SweepIntervalMs || _boards.Count == 0)
        {
            return;
        }

        _lastSweep = currentTime;

        for (var i = _boards.Count - 1; i >= 0; i--)
        {
            var board = _boards[i];

            List<uint> expired = null;
            foreach (var (index, drop) in board.Drops)
            {
                if (currentTime > drop.ExpireAt)
                {
                    (expired ??= []).Add(index);
                }
            }

            if (expired != null)
            {
                foreach (var index in expired)
                {
                    var drop = board.Drops[index];
                    board.Clear(index);
                    _logger.Information(
                        "Loot slot {Slot} on board {EntityId:x} expired holding item {ItemId} x{Quantity}",
                        index,
                        board.Entity.EntityId,
                        drop.ItemId,
                        drop.Quantity);
                }
            }

            if (board.Drops.Count > 0)
            {
                board.EmptySince = 0;
                continue;
            }

            if (board.EmptySince == 0)
            {
                board.EmptySince = currentTime;
            }
            else if (currentTime > board.EmptySince + BoardIdleMs)
            {
                _shard.EntityMan.Remove(board.Entity.EntityId);
                _boards.RemoveAt(i);
                _logger.Information("Loot board {EntityId:x} closed after standing empty", board.Entity.EntityId);
            }
        }
    }

    private void Deliver(INetworkPlayer collector, CharacterEntity character, HeldDrop drop)
    {
        if (ItemRules.IsResource(drop.ItemId))
        {
            collector.Inventory.AddResource(drop.ItemId, drop.Quantity);
            _logger.Information("{Character} collected {Quantity} of resource {ItemId}", character.EntityId, drop.Quantity, drop.ItemId);
            return;
        }

        if (Powerups.Apply(character, drop.ItemId, _logger))
        {
            return;
        }

        ItemPayout.Pay(collector.Inventory, drop.ItemId, drop.Quantity, _logger, $"{character.EntityId} collecting a drop");
    }

    private Board BoardNear(Vector3 position)
    {
        foreach (var board in _boards)
        {
            if (board.Drops.Count < SlotCount
                && _shard.Entities.ContainsKey(board.Entity.EntityId)
                && Vector3.Distance(board.Entity.Position, position) <= BoardRadius)
            {
                return board;
            }
        }

        var entity = _shard.EntityMan.SpawnAreaVisualData(position, new ScopingComponent { Range = BoardScopeRange });
        entity.AreaVisualData_LootObjectView = new LootObjectView();

        // Retail's loot entities carry this view and nothing else — no ObserverView keyframe appears on
        // any of the capture's forty-odd boards, so PIN drops the one its constructor builds rather than
        // announcing a board as an object in its own right.
        entity.AreaVisualData_ObserverView = null;

        var created = new Board { Entity = entity };
        _boards.Add(created);

        _logger.Information("Loot board {EntityId:x} opened at {Position}", entity.EntityId, position);
        return created;
    }

    private struct HeldDrop
    {
        public uint ItemId;
        public uint Quantity;
        public Vector3 Position;
        public ulong OwnerId;
        public ulong ExpireAt;
    }

    private sealed class Board
    {
        public AreaVisualDataEntity Entity { get; init; }

        public Dictionary<uint, HeldDrop> Drops { get; } = [];

        /// <summary>When the last drop left, or 0 while the board is holding something.</summary>
        public ulong EmptySince { get; set; }

        public bool TryTakeSlot(out uint index)
        {
            for (uint i = 0; i < SlotCount; i++)
            {
                if (!Drops.ContainsKey(i))
                {
                    index = i;
                    return true;
                }
            }

            index = 0;
            return false;
        }

        public void Clear(uint index)
        {
            Drops.Remove(index);
            Set(index, null);
        }

        public void Set(uint index, LootObjectData? data)
        {
            var view = Entity.AreaVisualData_LootObjectView;

            switch (index)
            {
                case 0: view.LootObjects_0Prop = data; break;
                case 1: view.LootObjects_1Prop = data; break;
                case 2: view.LootObjects_2Prop = data; break;
                case 3: view.LootObjects_3Prop = data; break;
                case 4: view.LootObjects_4Prop = data; break;
                case 5: view.LootObjects_5Prop = data; break;
                case 6: view.LootObjects_6Prop = data; break;
                case 7: view.LootObjects_7Prop = data; break;
                case 8: view.LootObjects_8Prop = data; break;
                case 9: view.LootObjects_9Prop = data; break;
                case 10: view.LootObjects_10Prop = data; break;
                case 11: view.LootObjects_11Prop = data; break;
                case 12: view.LootObjects_12Prop = data; break;
                case 13: view.LootObjects_13Prop = data; break;
                case 14: view.LootObjects_14Prop = data; break;
                case 15: view.LootObjects_15Prop = data; break;
                case 16: view.LootObjects_16Prop = data; break;
                case 17: view.LootObjects_17Prop = data; break;
                case 18: view.LootObjects_18Prop = data; break;
                case 19: view.LootObjects_19Prop = data; break;
                case 20: view.LootObjects_20Prop = data; break;
                case 21: view.LootObjects_21Prop = data; break;
                case 22: view.LootObjects_22Prop = data; break;
                case 23: view.LootObjects_23Prop = data; break;
            }
        }
    }
}
