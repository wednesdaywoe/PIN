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
///     It is added to the world without a scope set, so clients are never sent it as an entity of its own. Instead it
///     is listed in a slot of its owner's TinyObjectView (<see cref="Slot" />): the client builds its own copy from that
///     and runs the same status effect, whose client-only steps play the particles and sounds. A row that names a
///     particle effect directly also gets <see cref="Visual" />, an area visual carrying it.
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

    /// <summary>The owner's TinyObjectView slot listing this object to clients, or -1 when it isn't listed.</summary>
    public int Slot { get; set; } = -1;

    // Tiny objects have no status effect fields on the wire
    public override void SetStatusEffect(byte index, ushort time, StatusEffectData data)
    {
    }

    public override void ClearStatusEffect(byte index, ushort time, uint debugEffectId)
    {
    }
}
