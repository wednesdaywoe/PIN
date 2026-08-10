using System.Numerics;
using AeroMessages.GSS.V66;
using GameServer.Entities;

namespace GameServer.Tests.Hostility;

/// <summary>
///     The smallest thing <see cref="GameServer.Systems.Hostility.HostilityRules"/> will accept. It only ever
///     reads HostilityInfo, so nothing else here needs to do anything.
/// </summary>
internal sealed class FakeEntity : IEntity
{
    public ulong EntityId => 0;

    public AeroMessages.Common.EntityId AeroEntityId => default;

    public IShard Shard => null;

    public Vector3 Position { get; set; }

    public Quaternion Orientation { get; set; }

    public HostilityInfoData HostilityInfo { get; set; }

    /// <summary>
    ///     An entity that belongs to <paramref name="factionId"/> and no team, which is how NPCs and players
    ///     are set up outside of squads.
    /// </summary>
    public static FakeEntity InFaction(byte factionId)
    {
        return new FakeEntity
        {
            HostilityInfo = new HostilityInfoData
            {
                Flags = HostilityInfoData.HostilityFlags.Faction,
                FactionId = factionId,
            },
        };
    }

    public static FakeEntity InFactionAndTeam(byte factionId, byte teamId)
    {
        return new FakeEntity
        {
            HostilityInfo = new HostilityInfoData
            {
                Flags = HostilityInfoData.HostilityFlags.Faction | HostilityInfoData.HostilityFlags.Team,
                FactionId = factionId,
                TeamId = teamId,
            },
        };
    }

    /// <summary>
    ///     An entity carrying no hostility flags at all, which is what an entity type nobody has set up yet
    ///     looks like.
    /// </summary>
    public static FakeEntity WithNoHostilityInfo()
    {
        return new FakeEntity { HostilityInfo = default };
    }

    public bool IsInteractable() => false;

    public bool CanBeInteractedBy(IEntity other) => false;

    public byte GetInteractionType() => 0;

    public uint GetInteractionDuration() => 0;

    public bool IsGlobalScope() => false;

    public float GetScopeRange() => 0f;
}
