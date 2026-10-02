using System.Collections.Generic;

namespace GameServer.StaticDB.Records.customdata;

/// <summary>
///     A vein type PIN defines itself: a name and the list of resources a deposit of it pays, in the
///     same shape as the shipped <c>dbzonemetadata::ResourceNodeType</c> and its
///     <c>ResourceNodeTypeResource</c> rows.
/// </summary>
/// <remarks>
///     <para>
///     The client shipped 46 vein types and they are the only reason thumping pays anything at all,
///     but they are a snapshot of one economy: 42 of them pay ore in the Raw Metals class, which by
///     1962 exactly one blueprint consumes, and none of them pays any of the sixteen materials the
///     graded crafting block is built around. Deposits were already PIN's own content — see
///     <see cref="ResourceDeposit"/> — and this is the other half of the same hole. Positions without
///     the right payouts still leave fifteen of sixteen materials unobtainable.
///     </para>
///     <para>
///     An id that matches a shipped vein type replaces it wholesale, name and rows both; an id that
///     matches nothing adds a type the client never had. Replacing is the safer of the two, because
///     the thumper's observer view carries this id to the client and what its engine does with an id
///     it does not recognise is untested. No surviving Lua reads it.
///     </para>
///     <para>
///     Applied once at startup by <c>SDBInterface.ApplyResourceNodeTypeOverrides</c>, which keeps the
///     shipped rows aside so a reload can put them back. Everything downstream — the sampler, the
///     scan, the outpost radar, the thumper payout — reads through the ordinary SDB accessors and
///     cannot tell an overridden type from a shipped one.
///     </para>
/// </remarks>
public record ResourceNodeTypeOverride
{
    /// <summary>A <c>dbzonemetadata::ResourceNodeType</c> id, shipped or invented.</summary>
    public uint Id { get; set; }

    /// <summary>What the admin commands and the log call this vein. Never sent to a client.</summary>
    public string Name { get; set; }

    public List<ResourceNodeTypeOverrideResource> Resources { get; set; } = new();
}

/// <summary>
///     One resource a vein type pays, as a quantity range at the deposit's centre and another at its
///     rim. <c>DepositSampler</c> blends the two linearly across the deposit's radius and rolls inside
///     the result, so a rim row of 0 to 5 is what makes the edge of a vein worth less than its heart.
/// </summary>
public record ResourceNodeTypeOverrideResource
{
    /// <summary>A <c>dbitems::RootItem</c> sdb id. The item has to exist or the row pays nothing.</summary>
    public uint ItemId { get; set; }

    public uint CenterLow { get; set; }
    public uint CenterHigh { get; set; }
    public uint EdgeLow { get; set; }
    public uint EdgeHigh { get; set; }

    /// <summary>
    ///     The quality band rolled per unit. Shipped rows use 0 to 400 for ordinary veins and up to
    ///     1000 for the melded ones; nothing downstream reads it yet, because the five per-instance
    ///     stats it belongs to were never static data and PIN has still to generate them.
    /// </summary>
    public uint QualityLow { get; set; }

    public uint QualityHigh { get; set; }
}
