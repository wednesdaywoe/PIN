using System.Numerics;

namespace GameServer.StaticDB.Records.customdata;

/// <summary>
///     One underground deposit in a zone: a disc of ground that answers a scan and feeds a thumper.
///     Retail kept deposit positions server-side and rerolled them on a cadence, so nothing shipped
///     with the client to recover — like <see cref="SpawnGroup"/>, these are PIN's own content, and
///     the <c>deposit</c> admin command writes them where a player is standing.
/// </summary>
/// <remarks>
///     The deposit is two-dimensional on purpose. A thumper lands on the player's own footing and the
///     scan overlay projects onto the map, so the only coordinates that decide anything are X and Y;
///     Z is recorded for the overlay marker and nothing else. That is what makes this the one kind of
///     zone content that is safe to touch offline, unlike monster placements.
/// </remarks>
public record ResourceDeposit
{
    public uint Id { get; set; }
    public uint ZoneId { get; set; }

    /// <summary>What to call this deposit in the log. Never sent to a client.</summary>
    public string Name { get; set; }

    /// <summary>A <c>dbzonemetadata::ResourceNodeType</c> id, which keys the shipped yield gradient.</summary>
    public uint NodeTypeId { get; set; }

    public Vector3 Position { get; set; }

    /// <summary>
    ///     Metres from center to rim. The shipped gradient runs from the center values to the edge
    ///     values of every <c>ResourceNodeTypeResource</c> row over this distance; outside it the
    ///     deposit does not exist.
    /// </summary>
    public float Radius { get; set; }
}
