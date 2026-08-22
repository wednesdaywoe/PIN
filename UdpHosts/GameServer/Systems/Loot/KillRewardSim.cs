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
///     Both halves of a roll are paid now, and they take different routes. Resources go straight to the
///     resource inventory, which is the path K1 verified. Items are put on the ground where the creature
///     fell, through <see cref="WorldLoot" />, and only reach an inventory when somebody walks over and
///     takes them — which is what retail did, read off the 2016 capture's 41 pickups.
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
    private readonly Shard _shard;

    public KillRewardSim(Shard shard)
    {
        _shard = shard;
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
                // Counted on the roll rather than on the delivery, so "rolled nothing" below keeps
                // meaning the tables came up dry rather than the item failing to resolve.
                itemKinds++;

                // On the ground where it died, not into the bag. A powerup belongs to nobody — that is
                // how retail wrote it on the wire, and it is the only kind of drop a squadmate can
                // reasonably take. Everything else is the killer's until it expires.
                _shard.Loot.Drop(
                    victim.Position,
                    itemId,
                    quantity,
                    Powerups.IsPowerup(itemId) ? null : killer,
                    $"Kill of monster type {typeId} by {killer.EntityId}");
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
