using System.Collections.Generic;
using System.Numerics;

namespace GameServer.StaticDB.Records.customdata;

/// <summary>
///     A standing pack of monsters at fixed places in a zone. Retail kept these in server-side spawn
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
    ///     How long a member's place stays empty before it's filled again, measured from the corpse
    ///     despawning rather than from the kill.
    /// </summary>
    public uint RespawnDelayMs { get; set; }

    public List<SpawnGroupMember> Members { get; set; } = new();
}

/// <summary>
///     One monster at one place.
/// </summary>
/// <remarks>
///     Every member carries its own position rather than being scattered around a group anchor. The
///     anchor-and-radius version of this put monsters inside hillsides: the server has no terrain to
///     sample, so a radius is a bet that the ground is flat, and around zone 448's valley anchor the
///     ground moves 12m vertically inside 9m horizontally. A position that a player has stood on is
///     the only kind this server can verify, and the <c>spawngroup</c> command writes exactly those.
/// </remarks>
public record SpawnGroupMember
{
    /// <summary>A <c>dbcharacter::Monster</c> id, the same thing the <c>npc</c> command takes.</summary>
    public uint MonsterTypeId { get; set; }

    public Vector3 Position { get; set; }
}
