using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.Systems.AI;
using GameServer.Systems.Hostility;
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
    private bool _audited;
    private uint _zoneId;

    public SpawnGroupSim(Shard shard)
    {
        _shard = shard;
        _logger = shard.Logger.ForContext<SpawnGroupSim>();
    }

    /// <summary>
    ///     Flattens the zone's groups into one place per member. Called from
    ///     <c>EntityManager.SpawnZoneEntities</c>, alongside the other world content, and again by
    ///     <c>spawngroup reload</c> after the file is edited in game.
    /// </summary>
    public void Load(uint zoneId)
    {
        _zoneId = zoneId;
        _slots.Clear();
        _audited = false;

        var groups = CustomDBInterface.GetZoneSpawnGroups(zoneId);

        foreach (var group in groups.Values)
        {
            foreach (var member in group.Members)
            {
                if (SDBInterface.GetMonster(member.MonsterTypeId) == null)
                {
                    // Dropped rather than left to throw out of LoadMonster on the first tick, where it
                    // would read as a shard fault instead of a typo in the JSON.
                    _logger.Warning(
                        "Spawn group {Group} names monster {TypeId}, which isn't in the SDB. Skipping it.",
                        group.Name,
                        member.MonsterTypeId);
                    continue;
                }

                _slots.Add(new Slot
                {
                    GroupName = group.Name,
                    MonsterTypeId = member.MonsterTypeId,
                    Position = member.Position,
                    RespawnDelayMs = group.RespawnDelayMs,
                });
            }
        }

        _logger.Information("Loaded {Groups} spawn group(s) for zone {ZoneId}, {Slots} monster(s)", groups.Count, zoneId, _slots.Count);
    }

    /// <summary>
    ///     Despawns everything the groups put out and loads the file again. What makes placing a monster
    ///     in game worth doing: walk, place, look at it, move it, without a server restart between.
    /// </summary>
    public void Reload()
    {
        foreach (var slot in _slots)
        {
            if (slot.EntityId.HasValue && _shard.Entities.ContainsKey(slot.EntityId.Value))
            {
                _shard.EntityMan.Remove(slot.EntityId.Value);
            }
        }

        Load(_zoneId);
        _lastUpdate = 0;
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

        if (!_audited)
        {
            _audited = true;
            AuditNeighbours();
        }
    }

    /// <summary>
    ///     Reports any two standing NPCs that are close enough to see each other and hostile enough to
    ///     start shooting. Runs once, after the first fill.
    /// </summary>
    /// <remarks>
    ///     The first cut of zone 448 put Aranha, Chosen and Melded within 25m of each other because all
    ///     three were hostile to the player, which was the only relationship anyone checked. They are also
    ///     hostile to each other: the zone fought itself to a standstill in the first 20 seconds and killed
    ///     Aero on the way through, so a player logging in found a few survivors and empty ground. Nothing
    ///     in the content says who is friendly with whom, so a wrong pairing is invisible until someone
    ///     watches it happen, which is exactly the kind of mistake worth spending a startup loop on.
    ///
    ///     It runs over the shard rather than over the slots so that world content spawned outside a group
    ///     is included, which is how Aero would have been caught. It warns rather than refuses: two hostile
    ///     factions in sight of each other is a legitimate thing to want, just never by accident.
    /// </remarks>
    private void AuditNeighbours()
    {
        var npcs = new List<CharacterEntity>();

        foreach (var entity in _shard.Entities.Values)
        {
            if (entity is CharacterEntity npc && !npc.IsPlayerControlled)
            {
                npcs.Add(npc);
            }
        }

        var reported = 0;

        for (var i = 0; i < npcs.Count; i++)
        {
            for (var j = i + 1; j < npcs.Count; j++)
            {
                var separation = Vector3.Distance(npcs[i].Position, npcs[j].Position);

                if (separation > TargetSelection.PerceptionRange)
                {
                    continue;
                }

                if (!HostilityRules.AreHostile(npcs[i], npcs[j]) && !HostilityRules.AreHostile(npcs[j], npcs[i]))
                {
                    continue;
                }

                reported++;
                _logger.Warning(
                    "Spawned NPCs {A} (faction {FactionA}) and {B} (faction {FactionB}) are hostile and {Separation:0.#}m apart, inside the {Perception}m perception radius. They will fight each other on sight.",
                    npcs[i].EntityId,
                    npcs[i].HostilityInfo.FactionId,
                    npcs[j].EntityId,
                    npcs[j].HostilityInfo.FactionId,
                    separation,
                    TargetSelection.PerceptionRange);
            }
        }

        if (reported == 0)
        {
            _logger.Information("Spawn group audit: {Count} standing NPC(s), no hostile pairs within perception of each other", npcs.Count);
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
