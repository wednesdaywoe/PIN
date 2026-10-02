using System.Collections.Generic;
using System.Linq;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;

namespace GameServer.Systems.Aptitude;

/// <summary>
///     Stance of one faction towards another, from -2 (hostile) to 2 (allied), worked out from the SDB faction tables
/// </summary>
public class FactionStances
{
    private readonly Dictionary<(uint From, uint To), sbyte> _relations = [];
    private readonly Dictionary<uint, Faction> _factions;

    public FactionStances(IEnumerable<Faction> factions, IEnumerable<FactionRelations> relations)
    {
        _factions = factions.ToDictionary(f => f.Id);

        // Directional rows go in first so they win over the reverse of a bidirectional one
        var ordered = relations.OrderBy(r => r.HostilityBidirectional).ToList();
        foreach (var relation in ordered)
        {
            _relations.TryAdd((relation.FactionA, relation.FactionB), relation.HostilityStance);
        }

        foreach (var relation in ordered.Where(r => r.HostilityBidirectional == 1))
        {
            _relations.TryAdd((relation.FactionB, relation.FactionA), relation.HostilityStance);
        }
    }

    public static FactionStances FromSDB() => new(SDBInterface.GetFactions(), SDBInterface.GetFactionRelations());

    public sbyte GetStance(uint fromFaction, uint toFaction)
    {
        if (_relations.TryGetValue((fromFaction, toFaction), out var stance))
        {
            return stance;
        }

        if (fromFaction == toFaction)
        {
            return 1;
        }

        // Without a relation the default stance of the faction with the higher priority applies,
        // e.g. bandits (priority 100, hostile) over the accord (priority 10, neutral)
        _factions.TryGetValue(fromFaction, out var from);
        _factions.TryGetValue(toFaction, out var to);
        var fromPriority = from?.DefaultStancePriority ?? 0;
        var toPriority = to?.DefaultStancePriority ?? 0;
        if (fromPriority == toPriority)
        {
            return (sbyte)System.Math.Min(from?.DefaultStance ?? 0, to?.DefaultStance ?? 0);
        }

        return (fromPriority > toPriority ? from?.DefaultStance : to?.DefaultStance) ?? 0;
    }

    public bool IsHostile(IAptitudeTarget from, IAptitudeTarget to) => GetStance(from.HostilityInfo.FactionId, to.HostilityInfo.FactionId) < 0;

    public bool IsFriendly(IAptitudeTarget from, IAptitudeTarget to) => GetStance(from.HostilityInfo.FactionId, to.HostilityInfo.FactionId) > 0;
}
