using System.Collections.Generic;
using GameServer.StaticDB.Records.dbvisualrecords;

namespace GameServer.Systems.Hazards;

/// <summary>
///     What being in water does to you, in the order the water gets deeper.
/// </summary>
public enum WaterHazard : byte
{
    /// <summary>Dry, or wading shallow enough that only the client cares.</summary>
    None = 0,

    /// <summary>In over the head far enough to be losing air.</summary>
    Drowning = 1,

    /// <summary>Fully under. Kills faster, and faster the longer it goes on.</summary>
    Dying = 2,
}

/// <summary>
///     How deep in water a character is, as the client reports it.
///
///     The client owns this reading. The map files hold the water surfaces, but the server doesn't
///     measure against them; the client's reading is confirmed and nothing needs a second one. It arrives on
///     every movement pose as one byte packed <c>ddddllll</c>: the low nibble is submersion out of
///     <see cref="MaxLevel"/>, the high nibble picks which water description applies.
///
///     The client packs and unpacks it in FUN_00c84890 and FUN_00c84980. The level is scaled by exactly
///     1/15, and the description is the row's position in its in-memory <c>sttc_waterdesc</c> table,
///     which is <c>dbvisualrecords::WaterDesc</c> in table order. A body of water's description comes
///     from the zone file, and every one of zone 448's four carries 10001, row 0.
///
///     The reading is confirmed against the 2016 capture. 4730 of its 48649 <c>MovementInput</c>
///     messages carry a non-zero value, all of them 1 to 5 with the description nibble at 0, and 5/15 is
///     0.333 — which is exactly <c>moving_restricted_percent</c> for the standard water row. So the level
///     is a fraction of the character's height over 15, not over 16, and that session's player waded but
///     never swam.
/// </summary>
public readonly record struct Submersion(byte Level, byte DescIndex)
{
    /// <summary>
    ///     A full nibble, and the level at which a character is completely under. It has to be 15 rather
    ///     than 16 for <c>dying_percent</c> of 1.0 to be reachable at all.
    /// </summary>
    public const byte MaxLevel = 15;

    /// <summary>How much of the character is under, 0 to 1.</summary>
    public float Depth => Level / (float)MaxLevel;

    public bool InWater => Level > 0;

    public static Submersion Read(byte waterLevelAndDesc)
    {
        return new Submersion((byte)(waterLevelAndDesc & 0x0F), (byte)(waterLevelAndDesc >> 4));
    }

    /// <summary>
    ///     The water description the nibble names, read the way the client reads it: row
    ///     <see cref="DescIndex"/> of the table, or row 0 when the nibble is past its end. Null only when
    ///     the table is empty.
    /// </summary>
    public WaterDesc DescribedBy(IReadOnlyList<WaterDesc> table)
    {
        if (table == null || table.Count == 0)
        {
            return null;
        }

        return table[DescIndex < table.Count ? DescIndex : 0];
    }

    /// <summary>
    ///     <see cref="DescribedBy(IReadOnlyList{WaterDesc})"/>, believed only when the map agrees. A row
    ///     counts when the zone's water collision uses its physics material; anything else, and everything
    ///     when the map isn't loaded, reads as row 0, the standard water.
    ///
    ///     The check exists because of one reading. ENV-1 saw nibble 2 in zone 448, which by the table is
    ///     10003, water that drowns at a twelfth of your height and kills at a third. Zone 448's water
    ///     uses the materials of 10001, 10002 and 10008 only, so nibble 2 there is something not yet
    ///     understood, and the price of believing it is a player dying in a puddle.
    /// </summary>
    public (WaterDesc Water, bool Corroborated) DescribedBy(IReadOnlyList<WaterDesc> table, IReadOnlySet<uint> zoneWaterMaterials)
    {
        var named = DescribedBy(table);
        if (named == null)
        {
            return (null, false);
        }

        var corroborated = zoneWaterMaterials != null && zoneWaterMaterials.Contains(named.PhysicsMaterialId);
        return corroborated ? (named, true) : (table[0], false);
    }

    /// <summary>
    ///     Reads the depth against the thresholds the water itself carries. A <paramref name="water"/> of
    ///     null is treated as harmless rather than as a default, because guessing at a threshold is how a
    ///     character drowns standing in a puddle.
    /// </summary>
    public WaterHazard Against(WaterDesc water)
    {
        if (water == null || Level == 0)
        {
            return WaterHazard.None;
        }

        if (water.DyingPercent > 0f && Depth >= water.DyingPercent)
        {
            return WaterHazard.Dying;
        }

        if (water.DrowningPercent > 0f && Depth >= water.DrowningPercent)
        {
            return WaterHazard.Drowning;
        }

        return WaterHazard.None;
    }
}
