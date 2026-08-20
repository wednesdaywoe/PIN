using GameServer.StaticDB;
using GameServer.Systems.AI;
using GameServer.Systems.Combat;
using Xunit;

namespace GameServer.Tests.AI;

/// <summary>
///     <see cref="AttackWindow"/> is the server's only model of how fast a weapon fires — a player's
///     client owns that decision and the server just reacts, so nothing else here had to know. These
///     pin the reading of the template fields, not whether the reading is right; that's an in-game
///     question (N5 in Docs/In-Game-Tests/NPC-Combat.html).
/// </summary>
public class AttackWindowTests
{
    [Fact]
    public void AWeaponWithNoReachIsNotUsable()
    {
        var window = AttackWindow.Resolve(new WeaponTemplateResult { MsPerBurst = 250 }, 1f);

        Assert.False(window.Usable);
        Assert.False(window.InRange(1f));
    }

    [Fact]
    public void ANullWeaponIsNotUsable()
    {
        Assert.False(AttackWindow.Resolve(null, 1f).Usable);
    }

    [Fact]
    public void TargetingRangeCoversATemplateThatLeavesRangeUnset()
    {
        var window = AttackWindow.Resolve(new WeaponTemplateResult { TargetingRange = 25f, MsPerBurst = 500 }, 1f);

        Assert.True(window.Usable);
        Assert.Equal(25f, window.Range);
    }

    [Fact]
    public void RangeIsInclusiveAndNothingBeyondItIsWorthShooting()
    {
        var window = AttackWindow.Resolve(Automatic(), 1f);

        Assert.True(window.InRange(60f));
        Assert.False(window.InRange(60.1f));
    }

    [Fact]
    public void AWeaponWithNoBurstDurationFiresAsOneCall()
    {
        // WeaponSim expands RoundsPerBurst itself in this case, exactly as it does for a client, so the
        // AI must not expand it a second time.
        var weapon = Automatic();
        weapon.RoundsPerBurst = 3;

        var window = AttackWindow.Resolve(weapon, 1f);

        Assert.Equal(1, window.ShotsPerBurst);
        Assert.Equal(0ul, window.ShotIntervalMs);
    }

    [Fact]
    public void AWeaponWithABurstDurationGetsOneCallPerRound()
    {
        var weapon = Automatic();
        weapon.MsPerBurst = 1200;
        weapon.MsBurstDuration = 300;
        weapon.RoundsPerBurst = 3;

        var window = AttackWindow.Resolve(weapon, 1f);

        Assert.Equal(3, window.ShotsPerBurst);
        Assert.Equal(100ul, window.ShotIntervalMs);
        Assert.Equal(1200ul, window.BurstIntervalMs);
    }

    [Fact]
    public void RateOfFireShortensBothIntervals()
    {
        var weapon = Automatic();
        weapon.MsPerBurst = 1200;
        weapon.MsBurstDuration = 600;
        weapon.RoundsPerBurst = 3;

        var window = AttackWindow.Resolve(weapon, 2f);

        Assert.Equal(600ul, window.BurstIntervalMs);
        Assert.Equal(100ul, window.ShotIntervalMs);
    }

    [Fact]
    public void ARateOfFireBelowOneLengthensTheCycleInstead()
    {
        // The direction creatures actually use it in. MonsterTier hands the AI a multiplier under 1 to
        // stretch a swing every 1280ms out to one every ~3270ms, which is the whole of the fix to a
        // thumper that could not outlast its own cycle. Nothing else in the game passes less than 1.
        var weapon = Automatic();
        weapon.MsPerBurst = 1280;

        var window = AttackWindow.Resolve(weapon, MonsterTier.RateOfFireMultiplier);

        // 3266 rather than 3267: the interval is truncated to whole milliseconds, not rounded. A third of
        // a millisecond a swing is not worth a rounding pass, but the test has to know which way it went.
        Assert.Equal(3266ul, window.BurstIntervalMs);
    }

    [Fact]
    public void ARateOfFireOfZeroIsIgnoredRatherThanDividedBy()
    {
        var window = AttackWindow.Resolve(Automatic(), 0f);

        Assert.Equal(250ul, window.BurstIntervalMs);
    }

    [Fact]
    public void ATemplateWithNoBurstTimingStillCannotFireEveryTick()
    {
        var weapon = Automatic();
        weapon.MsPerBurst = 0;

        var window = AttackWindow.Resolve(weapon, 1f);

        Assert.True(window.Usable);
        Assert.Equal(250ul, window.BurstIntervalMs);
    }

    [Fact]
    public void ABurstLongerThanItsOwnCyclePushesTheNextBurstOut()
    {
        // Otherwise the next burst is due before the current one has finished and the NPC never stops.
        var weapon = Automatic();
        weapon.MsPerBurst = 400;
        weapon.MsBurstDuration = 900;
        weapon.RoundsPerBurst = 3;

        var window = AttackWindow.Resolve(weapon, 1f);

        Assert.True(window.BurstIntervalMs > window.ShotIntervalMs * (ulong)window.ShotsPerBurst);
    }

    /// <summary>An assault-rifle shape: no burst duration, one round a trigger pull, 4 a second.</summary>
    private static WeaponTemplateResult Automatic()
    {
        return new WeaponTemplateResult
        {
            Range = 60f,
            MsPerBurst = 250,
            MsBurstDuration = 0,
            RoundsPerBurst = 1,
        };
    }
}
