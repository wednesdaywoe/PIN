using System.Collections.Generic;
using System.Threading;
using GameServer.Entities.Character;
using GameServer.Systems.AI;

namespace GameServer;

public class AIEngine
{
    private readonly IShard _shard;
    private readonly Dictionary<ulong, AIState> _stateByEntity = new();

    public AIEngine(IShard shard)
    {
        _shard = shard;
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        PruneDespawned();

        foreach (var entity in _shard.Entities.Values)
        {
            if (entity is not CharacterEntity npc || npc.IsPlayerControlled || !npc.IsAlive)
            {
                continue;
            }

            if (!_stateByEntity.TryGetValue(npc.EntityId, out var state))
            {
                state = new AIState();
                _stateByEntity[npc.EntityId] = state;
            }

            TargetSelection.Tick(_shard, npc, state, deltaTime);
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