namespace GameServer.Systems.Combat;

/// <summary>
///     Shared arithmetic for health pools. Several entity types replicate health as a percentage byte rather
///     than as points, and they all have to agree on how that percentage is worked out.
/// </summary>
public static class Vitals
{
    /// <summary>
    ///     <paramref name="current"/> as a percentage of <paramref name="max"/>, the way the client's health
    ///     bars want it. Truncates rather than rounds, so anything alive but under 1% reads as 0 and a pool
    ///     with no maximum reads as 0 rather than dividing by zero.
    /// </summary>
    public static byte HealthPercent(int current, int max)
    {
        if (max <= 0 || current <= 0)
        {
            return 0;
        }

        return (byte)(((float)current / max) * 100);
    }
}
