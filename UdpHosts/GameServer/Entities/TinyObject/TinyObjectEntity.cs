using AeroMessages.GSS.V66;
using GameServer.Entities.AreaVisualData;
using GameServer.Entities.Character;

namespace GameServer.Entities.TinyObject;

/// <summary>
///     A lingering object an ability leaves in the world, such as Creeping Death's poison cloud. Its behaviour is the
///     status effect its dbcharacter::TinyObject row names (SpawnStatusfxId), which the server's effect system runs
///     like any other: the effect's update chain does the work and its remove chain destroys the object.
/// </summary>
/// <remarks>
///     Server-only. It is added to the world without a scope set, so clients are never sent it; what they see is
///     <see cref="Visual" />, an ordinary area visual carrying the row's particle effect.
/// </remarks>
public sealed class TinyObjectEntity : BaseAptitudeEntity
{
    public TinyObjectEntity(IShard shard, ulong eid, uint typeId, CharacterEntity owner)
        : base(shard, eid, owner)
    {
        TypeId = typeId;

        // Stances are worked out against the object, so it fights on its owner's side
        HostilityInfo = owner?.HostilityInfo ?? new HostilityInfoData();
    }

    public uint TypeId { get; }

    public AreaVisualDataEntity Visual { get; set; }

    // Tiny objects have no status effect fields on the wire
    public override void SetStatusEffect(byte index, ushort time, StatusEffectData data)
    {
    }

    public override void ClearStatusEffect(byte index, ushort time, uint debugEffectId)
    {
    }
}
