using System;
using System.Collections.Generic;
using GameServer.StaticDB;
using GameServer.Systems.SystemEvents;
using Serilog;

namespace GameServer.Systems.Loot;

/// <summary>
///     Pays the player who killed an NPC out of the monster's own loot tables.
/// </summary>
/// <remarks>
///     <para>
///     This is the first subscriber <c>CharacterDiedEvent</c> has ever had. The event has been published
///     to nobody since M2 landed.
///     </para>
///     <para>
///     Only resources are paid, and in practice that means crystite. The same tables also roll powerups
///     and equipment, and both are items rather than resources — putting one in a player's hands means
///     answering how the client wants a drop represented, which nothing in the codebase establishes.
///     So items are counted in the log and dropped on the floor, figuratively. That is the same cut M3
///     made: pay the resource directly, leave the item route to the milestone that can afford it.
///     </para>
///     <para>
///     Paying the killer directly is also a shortcut. Retail spawned a pickup the killer walked over,
///     and shared credit with a squad; this credits one character with no pickup and no sharing.
///     </para>
/// </remarks>
public class KillRewardSim
{
    private readonly ILogger _logger;
    private readonly LootRoller _roller;
    private readonly IDisposable _subscription;

    public KillRewardSim(Shard shard)
    {
        _logger = shard.Logger.ForContext<KillRewardSim>();
        _roller = new LootRoller(SdbLootTableSource.Instance, new Random());
        _subscription = shard.EventBus.Subscribe<CharacterDiedEvent>(OnCharacterDied);
    }

    private static void Merge(Dictionary<uint, uint> into, Dictionary<uint, uint> from)
    {
        foreach (var (itemId, quantity) in from)
        {
            into[itemId] = into.GetValueOrDefault(itemId) + quantity;
        }
    }

    private void OnCharacterDied(CharacterDiedEvent evt)
    {
        var victim = evt.Victim;
        var killer = evt.Killer;

        // Environmental deaths have no killer, players carry no loot table, and a monster that killed
        // another monster has nobody to pay.
        if (killer == null || victim == null || victim.IsPlayerControlled || !killer.IsPlayerControlled)
        {
            return;
        }

        var typeId = victim.StaticInfo.CharacterTypeId;
        var monster = typeId != 0 ? SDBInterface.GetMonster(typeId) : null;
        if (monster == null)
        {
            return;
        }

        if (monster.LootTableId == 0 && monster.LootTable2Id == 0)
        {
            _logger.Debug("Monster type {TypeId} names no loot table, so its death pays nothing", typeId);
            return;
        }

        var drops = new Dictionary<uint, uint>();
        Merge(drops, _roller.Roll(monster.LootTableId));
        Merge(drops, _roller.Roll(monster.LootTable2Id));

        uint resourceKinds = 0;
        uint itemKinds = 0;

        foreach (var (itemId, quantity) in drops)
        {
            if (ItemRules.IsResource(itemId))
            {
                killer.Player.Inventory.AddResource(itemId, quantity);
                resourceKinds++;
                _logger.Information(
                    "Kill of monster type {TypeId} paid {Quantity} of resource {ResourceId} to {Killer}",
                    typeId,
                    quantity,
                    itemId,
                    killer.EntityId);
            }
            else
            {
                itemKinds++;
                _logger.Information(
                    "Kill of monster type {TypeId} rolled item {ItemId} x{Quantity}, not paid — item drops are unbuilt",
                    typeId,
                    itemId,
                    quantity);
            }
        }

        if (resourceKinds == 0 && itemKinds == 0)
        {
            // A dry roll is the normal case, not a fault: a small creature's crystite subtable is only
            // reached a quarter of the time. Logged so "the reward is broken" and "the roll missed" are
            // distinguishable from the log alone, which is the lesson the thumper's participant count
            // taught.
            _logger.Information("Kill of monster type {TypeId} by {Killer} rolled nothing", typeId, killer.EntityId);
        }
    }
}
