using AeroMessages.GSS.V66;
using GameServer.Entities;
using GameServer.Enums;
using GameServer.StaticDB;

namespace GameServer.Systems.Hostility;

/// <summary>
///     Faction based hostility rules. This is the single place that decides whether one entity is
///     allowed to damage another, so weapon fire, ability damage and splash all agree.
/// </summary>
public static class HostilityRules
{
    /// <summary>
    ///     How <paramref name="source"/> regards <paramref name="other"/>. Entities on the same team or in
    ///     the same faction are always friendly; everything else comes from the faction relations table.
    ///     Entities without hostility information are neutral to everyone.
    /// </summary>
    public static HostilityStance GetStance(IEntity source, IEntity other)
    {
        if (source == null || other == null)
        {
            return HostilityStance.Neutral;
        }

        if (TryGetTeam(source, out var sourceTeam) && TryGetTeam(other, out var otherTeam) && sourceTeam == otherTeam)
        {
            return HostilityStance.Friendly;
        }

        if (!TryGetFaction(source, out var sourceFaction) || !TryGetFaction(other, out var otherFaction))
        {
            return HostilityStance.Neutral;
        }

        return sourceFaction == otherFaction
            ? HostilityStance.Friendly
            : SDBUtils.GetFactionStance(sourceFaction, otherFaction);
    }

    public static bool AreHostile(IEntity source, IEntity other)
    {
        return GetStance(source, other) == HostilityStance.Hostile;
    }

    public static bool AreFriendly(IEntity source, IEntity other)
    {
        return GetStance(source, other) == HostilityStance.Friendly;
    }

    /// <summary>
    ///     Whether <paramref name="attacker"/> may damage <paramref name="target"/>. Friendlies are protected;
    ///     neutrals are not, matching the client's own friendly/hostile/neither split. A null attacker is
    ///     environmental damage and is always allowed through.
    /// </summary>
    public static bool CanDamage(IEntity attacker, IEntity target)
    {
        if (target == null)
        {
            return false;
        }

        if (attacker == null)
        {
            return true;
        }

        return GetStance(attacker, target) != HostilityStance.Friendly;
    }

    private static bool TryGetFaction(IEntity entity, out uint factionId)
    {
        var info = entity.HostilityInfo;
        factionId = info.FactionId;
        return info.Flags.HasFlag(HostilityInfoData.HostilityFlags.Faction);
    }

    private static bool TryGetTeam(IEntity entity, out byte teamId)
    {
        var info = entity.HostilityInfo;
        teamId = info.TeamId;
        return info.Flags.HasFlag(HostilityInfoData.HostilityFlags.Team);
    }
}
