using System.Collections.Generic;
using System.Numerics;

namespace GameServer.StaticDB.Records.customdata;

/// <summary>
///     A standing pack of monsters at a fixed place in a zone. Retail kept these in server-side spawn
///     tables that never shipped to the client, so unlike <see cref="Deployable"/> and <see cref="Melding"/>
///     the contents here aren't recovered from anything. They're PIN's own content.
/// </summary>
public record SpawnGroup
{
    public uint Id { get; set; }
    public uint ZoneId { get; set; }

    /// <summary>What to call this group in the log. Never sent to a client.</summary>
    public string Name { get; set; }

    /// <summary>
    ///     Where the group stands. Members scatter around this in X and Y and keep its Z, because the
    ///     server holds no terrain to sample and the anchor's height is the only one known to be ground.
    /// </summary>
    public Vector3 Anchor { get; set; }

    /// <summary>How far from the anchor the outermost member stands. Zero stacks them all on it.</summary>
    public float Radius { get; set; }

    /// <summary>
    ///     How long a member's place stays empty before it's filled again, measured from the corpse
    ///     despawning rather than from the kill.
    /// </summary>
    public uint RespawnDelayMs { get; set; }

    public List<SpawnGroupMember> Members { get; set; } = new();
}

public record SpawnGroupMember
{
    /// <summary>A <c>dbcharacter::Monster</c> id, the same thing the <c>npc</c> command takes.</summary>
    public uint MonsterTypeId { get; set; }

    public byte Count { get; set; }
}
