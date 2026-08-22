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

    /// <summary>
    ///     What this particular vein multiplies its yield by. 1 is the shipped gradient untouched,
    ///     2 is twice as rich, 0.5 half.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     PIN's own, and it fills a hole rather than inventing a mechanic. The shipped table gives a
    ///     gradient <em>per node type</em> and nothing per deposit; retail's deposits were server-side
    ///     records rerolled on a cadence and never shipped, the same category of loss as spawn tables.
    ///     Whether one of those records carried a richness figure cannot be recovered, so this is the
    ///     charter's "restore the loop, not the artefact" case: without it every vein of a node type
    ///     is identical forever, and the only way to make a haul bigger is to move to a different tier
    ///     of the world.
    ///     </para>
    ///     <para>
    ///     Applied at payout alongside completion and defence, and only when the thumper is standing
    ///     in this deposit — a thumper on barren ground mines its node type's rim values and no
    ///     deposit's multiplier applies to it. It does not touch the scan overlay, which reports
    ///     composition as percentages of the whole and so is unchanged by scaling every share.
    ///     </para>
    ///     <para>
    ///     Zero or negative reads as 1 rather than as "pays nothing", because the likeliest way to get
    ///     one is a hand-edited or older JSON row that omits the field, and a silent zero would look
    ///     like the payout code breaking. Refuse the value at the command instead, where a person can
    ///     be told.
    ///     </para>
    /// </summary>
    public float Richness { get; set; } = 1f;
}
