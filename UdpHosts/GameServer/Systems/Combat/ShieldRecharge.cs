namespace GameServer.Systems.Combat;

/// <summary>
///     Turns a per-second shield recharge rate into whole shield points over ticks a lot shorter than a
///     second. Truncating each tick on its own would mean any rate below one point per tick restored
///     nothing at all, so the leftover fraction is carried until it adds up to a point.
/// </summary>
public sealed class ShieldRecharge
{
    private float _remainder;

    /// <summary>
    ///     How many whole points <paramref name="elapsedSeconds"/> at <paramref name="perSecond"/> is worth,
    ///     keeping whatever is left over for the next call.
    /// </summary>
    public int Accumulate(int perSecond, float elapsedSeconds)
    {
        var restored = _remainder + (perSecond * elapsedSeconds);
        var points = (int)restored;
        _remainder = restored - points;

        return points;
    }

    /// <summary>
    ///     Drops the carried fraction. Called whenever recharging isn't allowed to run, so a fight doesn't
    ///     leave a part-built point waiting to land the moment shields come back.
    /// </summary>
    public void Reset()
    {
        _remainder = 0f;
    }
}
