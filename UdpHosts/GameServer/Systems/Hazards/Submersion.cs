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
///     The client owns this reading — it is the only thing in the session that knows where the water
///     surface is, since <c>LoadMapsCollision</c> is off and the server holds no terrain. It arrives on
///     every movement pose as one byte packed <c>ddddllll</c>: the low nibble is submersion out of
///     <see cref="MaxLevel"/>, the high nibble picks which water description applies.
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
