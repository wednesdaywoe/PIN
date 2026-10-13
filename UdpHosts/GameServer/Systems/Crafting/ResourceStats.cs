using System;
using System.Linq;
using System.Text;

namespace GameServer.Systems.Crafting;

/// <summary>
///     The packed text a material stack carries its quality and five stats in, sent as
///     <c>InventoryUpdate.Resource.TextKey</c>. The client unpacks it in <c>lib_Items.lua</c>
///     (<c>LIB_ITEMS.GetResourceStats</c>): <c>"{Version}-{SDB id}-{Quality}-{Stat1}..{Stat5}-{unused}-{unused}-"</c>,
///     every value base 36 in <c>lib_math.lua</c>'s lowercase digits, each followed by a dash because the
///     reader matches <c>(%w+)(%-)</c>. Quality runs 0..1000 (<c>GetResourceQualityColor</c>'s bands).
/// </summary>
public static class ResourceStats
{
    private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

    public static string Pack(uint sdbId, int quality, params int[] stats)
    {
        if (stats.Length > 5)
        {
            throw new ArgumentException("A material has at most five stats", nameof(stats));
        }

        var values = new long[] { 1, sdbId, quality }
                     .Concat(Enumerable.Range(0, 5).Select(i => i < stats.Length ? (long)stats[i] : 0))
                     .Concat([0, 0]);
        var text = new StringBuilder();
        foreach (var value in values)
        {
            text.Append(ToBase36(value)).Append('-');
        }

        return text.ToString();
    }

    /// <summary>
    ///     The quality and five stats packed into <paramref name="text"/>, or null when it is empty or isn't
    ///     that format. An ungraded stack (Crystite, anything mined before batches) carries empty text.
    /// </summary>
    public static (uint SdbId, int Quality, int[] Stats)? Unpack(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var parts = text.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 8 || parts.Any(p => p.Any(c => !Digits.Contains(c))))
        {
            return null;
        }

        var values = parts.Select(FromBase36).ToArray();
        return ((uint)values[1], (int)values[2], values.Skip(3).Take(5).Select(v => (int)v).ToArray());
    }

    private static long FromBase36(string text)
    {
        return text.Aggregate(0L, (value, c) => value * 36 + Digits.IndexOf(c));
    }

    internal static string ToBase36(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The client's reader drops a minus sign; stats are never negative");
        }

        var text = new StringBuilder();
        do
        {
            text.Insert(0, Digits[(int)(value % 36)]);
            value /= 36;
        }
        while (value > 0);

        return text.ToString();
    }
}
