using System;
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

    /// <summary>
    ///     Adds threat, up to <paramref name="maxThreat"/>. The cap is what bounds how long an NPC stays
    ///     interested: uncapped, a target that stood in front of one for a minute banked enough score
    ///     that it took minutes of decay to fall back under the engage threshold, so the NPC never
    ///     really let go and re-engaged the instant that target came back into reach.
    /// </summary>
    public void AddThreat(ulong entityId, float amount, float maxThreat)
    {
        if (amount <= 0f)
        {
            return;
        }

        var updated = _threatByEntity.GetValueOrDefault(entityId) + amount;
        _threatByEntity[entityId] = maxThreat > 0f ? MathF.Min(updated, maxThreat) : updated;
    }

    /// <summary>
    ///     Drops an entity from the table outright, for when it stops being this NPC's problem however
    ///     much threat it built up earlier — walking out past the leash, most of the time.
    /// </summary>
    public void Forget(ulong entityId)
    {
        _threatByEntity.Remove(entityId);
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
