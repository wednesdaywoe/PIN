using System.Collections.Generic;
using System.Threading;
using GameServer.Entities.Character;
using GameServer.Systems.AI;
using Serilog;

namespace GameServer;

public class AIEngine
{
    /// <summary>
    ///     Shard.Tick runs as fast as the thread will go, and every NPC decision here costs a raycast per
    ///     candidate, so this runs on its own clock like <c>WeaponSim</c> and <c>ShieldSim</c> do. It also
    ///     sets the finest grain an NPC's rate of fire can be paced at.
    /// </summary>
    private const ulong UpdateIntervalMs = 50;

    private readonly IShard _shard;
    private readonly ILogger _logger;
    private readonly Dictionary<ulong, AIState> _stateByEntity = new();
    private ulong _lastUpdate;

    public AIEngine(IShard shard)
    {
        _shard = shard;
        _logger = shard.Logger.ForContext<AIEngine>();
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (currentTime < _lastUpdate + UpdateIntervalMs)
        {
            return;
        }

        // The tick's own deltaTime is the raw loop delta in milliseconds, not the seconds the threat
        // rates are written in, and currentTime is a wall clock timestamp, so the first interval would
        // decay everything by several decades. Same shape as ShieldSim, for the same reason.
        var elapsedSeconds = _lastUpdate == 0 ? 0f : (currentTime - _lastUpdate) / 1000f;
        _lastUpdate = currentTime;

        PruneDespawned();

        if (elapsedSeconds <= 0f)
        {
            return;
        }

        foreach (var entity in _shard.Entities.Values)
        {
            if (entity is not CharacterEntity npc || npc.IsPlayerControlled)
            {
                continue;
            }

            if (!npc.IsAlive)
            {
                // The corpse sticks around for 30s before despawning, and if it died mid-stride the
                // client is still holding a fire and a run it was never told the end of.
                if (_stateByEntity.TryGetValue(npc.EntityId, out var dead))
                {
                    NpcCombat.Disengage(npc, dead, currentTime);
                    NpcMovement.Halt(npc);
                    NpcPose.Publish(_shard, npc, dead);
                }

                continue;
            }

            if (!_stateByEntity.TryGetValue(npc.EntityId, out var state))
            {
                state = new AIState();
                _stateByEntity[npc.EntityId] = state;
            }

            var previousTarget = state.CurrentTargetId;
            TargetSelection.Tick(_shard, npc, state, elapsedSeconds);

            if (state.CurrentTargetId != previousTarget)
            {
                _logger.Debug("NPC {Npc} target {Previous} -> {Current}", npc.EntityId, previousTarget, state.CurrentTargetId);
            }

            // Before either pass, because both read the answer and movement reads it first: an NPC stands
            // at a fraction of its weapon's reach, so a movement pass with no window yet falls back to the
            // ranged default. That sent a melee Aranha to 12m before it closed to the 4m its claw needs.
            NpcCombat.TryResolveWeapon(npc, state);

            // Move before shooting, so the range and line-of-sight checks are made from where the NPC
            // ends the tick rather than from where it started it, and so the turn to face a target is
            // the last word on which way it's pointing.
            NpcMovement.Tick(_shard, npc, state, elapsedSeconds, _logger);
            NpcCombat.Tick(_shard, npc, state, currentTime, _logger);

            // Last, so one pose carries both where the NPC ended up and which way it turned to shoot.
            // Nothing else replicates an NPC's movement: the movement view is not flushed to scoped
            // clients, on the assumption that only a client ever moves a character.
            NpcPose.Publish(_shard, npc, state);
        }
    }

    public ulong? CurrentTargetOf(ulong npcEntityId)
    {
        return _stateByEntity.TryGetValue(npcEntityId, out var state) ? state.CurrentTargetId : null;
    }

    private void PruneDespawned()
    {
        if (_stateByEntity.Count == 0)
        {
            return;
        }

        List<ulong> stale = null;
        foreach (var id in _stateByEntity.Keys)
        {
            if (!_shard.Entities.ContainsKey(id))
            {
                (stale ??= new List<ulong>()).Add(id);
            }
        }

        if (stale == null)
        {
            return;
        }

        foreach (var id in stale)
        {
            _stateByEntity.Remove(id);
        }
    }
}
