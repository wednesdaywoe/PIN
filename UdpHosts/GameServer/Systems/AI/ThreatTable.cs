using System.Collections.Generic;
using System.Linq;

namespace GameServer.Systems.AI;

/// <summary>
///     Per-NPC decaying threat scores, keyed by entity id. Target selection adds threat for
///     whatever an NPC can currently see and is hostile to, so a target sticks around for a beat
///     after it breaks line of sight instead of flickering to whoever is nearest that tick.
/// </summary>
public sealed class ThreatTable
{
    private readonly Dictionary<ulong, float> _threatByEntity = new();

    public void AddThreat(ulong entityId, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        _threatByEntity[entityId] = _threatByEntity.GetValueOrDefault(entityId) + amount;
    }

    public void Decay(float amount)
    {
        if (amount <= 0f || _threatByEntity.Count == 0)
        {
            return;
        }

        List<ulong> expired = null;
        foreach (var id in _threatByEntity.Keys)
        {
            var remaining = _threatByEntity[id] - amount;
            if (remaining <= 0f)
            {
                (expired ??= new List<ulong>()).Add(id);
            }
            else
            {
                _threatByEntity[id] = remaining;
            }
        }

        if (expired == null)
        {
            return;
        }

        foreach (var id in expired)
        {
            _threatByEntity.Remove(id);
        }
    }

    public IEnumerable<(ulong EntityId, float Score)> EntriesByThreatDescending()
    {
        return _threatByEntity
            .OrderByDescending(entry => entry.Value)
            .Select(entry => (entry.Key, entry.Value));
    }
}
