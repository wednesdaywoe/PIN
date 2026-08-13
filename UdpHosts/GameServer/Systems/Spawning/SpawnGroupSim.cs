using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using GameServer.StaticDB;
using Serilog;

namespace GameServer.Systems.Spawning;

/// <summary>
///     Keeps the zone's spawn groups populated. Every member of a group owns a place, and a place that
///     has stood empty for the group's respawn delay gets filled again.
/// </summary>
/// <remarks>
///     A place counts as empty once its entity is gone from the shard, not once the monster died.
///     <c>CharacterEntity.Die</c> leaves the corpse up for 30 seconds before <c>EntityManager</c> removes
///     it, so a kill costs those 30 seconds plus the delay. Watching for removal rather than for death
///     keeps this off <c>CharacterDiedEvent</c>, which M5 and M7 are the ones that want.
/// </remarks>
public class SpawnGroupSim
{
    /// <summary>
    ///     Respawn delays are tens of seconds, so there's nothing to gain from looking more often than
    ///     this. Every other sim in the shard runs its own clock for the same reason.
    /// </summary>
    private const ulong UpdateIntervalMs = 1000;

    private readonly Shard _shard;
    private readonly ILogger _logger;
    private readonly List<Slot> _slots = new();
    private ulong _lastUpdate;

    public SpawnGroupSim(Shard shard)
    {
        _shard = shard;
        _logger = shard.Logger.ForContext<SpawnGroupSim>();
    }

    /// <summary>
    ///     Flattens the zone's groups into one place per member. Called from
    ///     <c>EntityManager.SpawnZoneEntities</c>, alongside the other world content.
    /// </summary>
    public void Load(uint zoneId)
    {
        _slots.Clear();

        var groups = CustomDBInterface.GetZoneSpawnGroups(zoneId);

        foreach (var group in groups.Values)
        {
            // Positions are assigned across the whole group rather than per member entry, so a mixed
            // pack interleaves instead of putting each monster type in its own arc.
            var slotCount = 0;
            foreach (var member in group.Members)
            {
                slotCount += member.Count;
            }

            var slotIndex = 0;

            foreach (var member in group.Members)
            {
                if (SDBInterface.GetMonster(member.MonsterTypeId) == null)
                {
                    // Dropped rather than left to throw out of LoadMonster on the first tick, where it
                    // would read as a shard fault instead of a typo in the JSON.
                    _logger.Warning(
                        "Spawn group {Group} names monster {TypeId}, which isn't in the SDB. Skipping {Count}.",
                        group.Name,
                        member.MonsterTypeId,
                        member.Count);
                    slotIndex += member.Count;
                    continue;
                }

                for (var i = 0; i < member.Count; i++, slotIndex++)
                {
                    _slots.Add(new Slot
                    {
                        GroupName = group.Name,
                        MonsterTypeId = member.MonsterTypeId,
                        Position = SpawnScatter.Placement(group.Anchor, group.Radius, slotIndex, slotCount),
                        RespawnDelayMs = group.RespawnDelayMs,
                    });
                }
            }
        }

        _logger.Information("Loaded {Groups} spawn group(s) for zone {ZoneId}, {Slots} monster(s)", groups.Count, zoneId, _slots.Count);
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (_slots.Count == 0 || currentTime < _lastUpdate + UpdateIntervalMs)
        {
            return;
        }

        _lastUpdate = currentTime;

        foreach (var slot in _slots)
        {
            if (slot.EntityId.HasValue)
            {
                if (_shard.Entities.ContainsKey(slot.EntityId.Value))
                {
                    continue;
                }

                // Note the vacancy and let the next pass fill it, so the delay is always measured from a
                // time this loop actually observed rather than from whenever removal happened to run.
                _logger.Debug("Spawn group {Group}: {EntityId} is gone, respawning in {Delay}ms", slot.GroupName, slot.EntityId.Value, slot.RespawnDelayMs);
                slot.EntityId = null;
                slot.VacantSince = currentTime;
                continue;
            }

            if (slot.VacantSince != 0 && currentTime < slot.VacantSince + slot.RespawnDelayMs)
            {
                continue;
            }

            var npc = _shard.EntityMan.SpawnCharacter(slot.MonsterTypeId, slot.Position);
            slot.EntityId = npc.EntityId;
            slot.VacantSince = 0;

            _logger.Debug("Spawn group {Group}: monster {TypeId} as {EntityId} at {Position}", slot.GroupName, slot.MonsterTypeId, npc.EntityId, slot.Position);
        }
    }

    /// <summary>One monster's place in a group: where it stands and what's standing there now.</summary>
    private sealed class Slot
    {
        public string GroupName { get; init; }

        public uint MonsterTypeId { get; init; }

        public Vector3 Position { get; init; }

        public uint RespawnDelayMs { get; init; }

        /// <summary>What's standing here, or null if nothing is.</summary>
        public ulong? EntityId { get; set; }

        /// <summary>Shard time the place was seen empty. Zero before anything has ever stood here.</summary>
        public ulong VacantSince { get; set; }
    }
}
