using GameServer.Systems.Combat;
using Xunit;

namespace GameServer.Tests.Combat;

/// <summary>
///     Pins the arithmetic behind <see cref="CombatLog"/>'s kill rollup: how hits on one target add up, and
///     when a run of hits stops being the same fight. The file writing is covered by
///     <see cref="CombatLogWriterTests"/>.
/// </summary>
[Collection("CombatLog")]
public class CombatLogTests
{
    private const ulong Gap = 10_000;

    public CombatLogTests()
    {
        CombatLog.ResetForTests();
    }

    [Fact]
    public void HitsOnOneTargetAddUp()
    {
        CombatLog.Track(1, Shot("rifle"), 40, 1000);
        CombatLog.Track(1, Shot("rifle"), 35, 1300);
        var engagement = CombatLog.Track(1, Shot("rifle"), 25, 1600);

        Assert.Equal(3, engagement.Hits);
        Assert.Equal(100f, engagement.TotalDamage);
        Assert.Equal(1000ul, engagement.FirstHitMs);
        Assert.Equal(1600ul, engagement.LastHitMs);
    }

    [Fact]
    public void TwoTargetsAreCountedSeparately()
    {
        CombatLog.Track(1, Shot("rifle"), 40, 1000);
        var other = CombatLog.Track(2, Shot("rifle"), 10, 1100);

        Assert.Equal(1, other.Hits);
        Assert.Equal(10f, other.TotalDamage);
    }

    [Fact]
    public void AGapLongerThanTheThresholdStartsAFreshFight()
    {
        CombatLog.Track(1, Shot("rifle"), 40, 1000);
        var engagement = CombatLog.Track(1, Shot("rifle"), 40, 1000 + Gap + 1);

        Assert.Equal(1, engagement.Hits);
        Assert.Equal(40f, engagement.TotalDamage);
        Assert.Equal(1000 + Gap + 1, engagement.FirstHitMs);
    }

    [Fact]
    public void AGapExactlyAtTheThresholdIsStillTheSameFight()
    {
        CombatLog.Track(1, Shot("rifle"), 40, 1000);
        var engagement = CombatLog.Track(1, Shot("rifle"), 40, 1000 + Gap);

        Assert.Equal(2, engagement.Hits);
    }

    [Fact]
    public void EveryWeaponThatContributedIsNamed()
    {
        CombatLog.Track(1, Shot("crossbow"), 10, 1000);
        CombatLog.Track(1, Shot("rifle"), 10, 1100);
        var engagement = CombatLog.Track(1, Shot("rifle"), 10, 1200);

        // Commonest first, so the summary leads with what actually did the work
        Assert.Equal("rifle x2 + crossbow x1", engagement.WeaponSummary());
    }

    [Fact]
    public void DamageFromSomethingThatIsNotAWeaponSaysSo()
    {
        var engagement = CombatLog.Track(1, new DamageInfo { Amount = 5 }, 5, 1000);

        Assert.Equal("(no weapon) x1", engagement.WeaponSummary());
    }

    private static DamageInfo Shot(string weapon)
    {
        return new DamageInfo { WeaponName = weapon, WeaponId = 1 };
    }
}
