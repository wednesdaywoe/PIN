using System.Collections.Generic;
using System.Linq;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.Systems.Combat;
using Xunit;

namespace GameServer.Tests.Combat;

/// <summary>
///     Holds <see cref="MonsterTier"/> to the mapping it claims to implement: a creature's shipped
///     difficulty grade picks a row of the shipped power curve, and health and damage both come off that
///     row. Whether the resulting fights feel right is an in-game question (D7 in
///     Docs/In-Game-Tests/Damage-Loop.md) — these pin the arithmetic and, more importantly, the two
///     promises that keep a bad grade from breaking a spawn: ungraded creatures behave exactly as they
///     did before tiering existed, and a missing curve costs tiering rather than throwing.
/// </summary>
public class MonsterTierTests
{
    /// <summary>
    ///     A verbatim slice of dbcharacter::MonsterScaling as it ships in clientdb.sd2, read 2026-08-15.
    ///     Levels 1-45 of 80, which covers every grade the Monster table actually uses — the highest grade
    ///     in the game is 1000 on a single row and everything else lands at level 42 or below. Damage is
    ///     exactly health/2 on all 80 shipped rows without exception; that is the table's own shape, not a
    ///     simplification made here.
    /// </summary>
    private static readonly (byte Level, uint Health)[] ShippedCurve =
    [
        (1, 100), (2, 125), (3, 156), (4, 195), (5, 244), (6, 305), (7, 381), (8, 477), (9, 596),
        (10, 745), (11, 879), (12, 1037), (13, 1224), (14, 1445), (15, 1705), (16, 2011), (17, 2373),
        (18, 2801), (19, 3305), (20, 3900), (21, 4289), (22, 4718), (23, 5190), (24, 5709), (25, 6280),
        (26, 6908), (27, 7599), (28, 8359), (29, 9195), (30, 10114), (31, 10923), (32, 11797),
        (33, 12741), (34, 13760), (35, 14861), (36, 16050), (37, 17334), (38, 18721), (39, 20219),
        (40, 21836), (41, 22928), (42, 24074), (43, 25278), (44, 26542), (45, 27869),
    ];

    private static List<MonsterScaling> Curve() => ShippedCurve
        .Select(row => new MonsterScaling { Level = row.Level, Health = row.Health, Damage = row.Health / 2 })
        .ToList();

    [Fact]
    public void TheAnchorGradeLandsOnTheLevelItsTestedHealthImplies()
    {
        var result = MonsterTier.Resolve(MonsterTier.AnchorGrade, Curve());

        // 20 grade points x 60 health each = 1200, and level 13 is the nearest shipped row at 1224.
        Assert.Equal(13, result.Level);
        Assert.Equal(1224, result.Health);
        Assert.True(result.Graded);
    }

    [Fact]
    public void TheAnchorGradeTakesNoDamageScalarAtAll()
    {
        // The whole point of expressing the multiplier relative to the anchor. Monster 528 is grade 20, so
        // it must come out of tiering hitting exactly as hard as it did before tiering existed - which is
        // what makes it usable as the control in D6 and D7.
        Assert.Equal(1f, MonsterTier.Resolve(MonsterTier.AnchorGrade, Curve()).DamageMultiplier);
    }

    [Theory]
    [InlineData(20u, 13, 1224)]   // 33 creature types, the anchor band
    [InlineData(35u, 16, 2011)]   // 36 types, incl. monster 1196 - the D7 subject
    [InlineData(50u, 18, 2801)]   // 150 types, the single most common grade in the game
    [InlineData(100u, 25, 6280)]  // 117 types
    [InlineData(150u, 29, 9195)]  // 40 types, where the *MiniBoss behaviour scripts start
    [InlineData(300u, 37, 17334)] // 35 types, incl. GiantAranhaMiniBoss's larger grade
    public void GradesResolveToTheShippedRowNearestSixtyHealthPerPoint(uint grade, byte level, int health)
    {
        var result = MonsterTier.Resolve(grade, Curve());

        Assert.Equal(level, result.Level);
        Assert.Equal(health, result.Health);
    }

    [Fact]
    public void HealthRisesWithGradeAcrossEveryGradeTheGameActuallyUses()
    {
        // Every distinct non-zero difficulty_cost in dbcharacter::Monster, read 2026-08-15.
        uint[] shippedGrades = [1, 5, 10, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 100, 120, 140, 150, 160, 200, 300, 400];

        var health = shippedGrades.Select(g => MonsterTier.Resolve(g, Curve()).Health).ToList();

        // Not strictly increasing: the curve is coarser than the grade ladder, so neighbouring grades can
        // share a row (45 and 50 both land on level 18). Monotonic is the real promise - a creature the
        // designers rated more dangerous must never come out weaker.
        Assert.Equal(health.OrderBy(h => h), health);
    }

    [Fact]
    public void DamageTracksHealthBecauseTheShippedCurveMakesItSo()
    {
        var basic = MonsterTier.Resolve(MonsterTier.AnchorGrade, Curve());
        var miniBoss = MonsterTier.Resolve(300, Curve());

        var healthRatio = (float)miniBoss.Health / basic.Health;

        // damage = health/2 on every shipped row, so the two ratios are the same number. A miniboss is
        // ~14x the creature at the anchor in both, which is what "larger ones are individually more
        // dangerous" has to mean if it is to mean anything mechanical.
        Assert.Equal(healthRatio, miniBoss.DamageMultiplier, 3);
        Assert.True(miniBoss.DamageMultiplier > 14f);
    }

    [Fact]
    public void UngradedCreaturesGetExactlyWhatTheyGotBeforeTieringExisted()
    {
        var ungraded = MonsterTier.Resolve(0, Curve());
        var anchor = MonsterTier.Resolve(MonsterTier.AnchorGrade, Curve());

        // 2203 of 3109 creature types rate 0, and the rating orders creatures without pricing them - a 0
        // means unrated, not harmless (EliteWanderer rates 0). So they fall back to the anchor row rather
        // than to the bottom of the curve, and the fallback must be indistinguishable from the old flat
        // behaviour or this change regresses 2203 creature types to buy 906.
        Assert.Equal(anchor.Health, ungraded.Health);
        Assert.Equal(1f, ungraded.DamageMultiplier);
        Assert.False(ungraded.Graded);
    }

    [Fact]
    public void AMissingCurveCostsTieringRatherThanBreakingTheSpawn()
    {
        foreach (var absent in new List<MonsterScaling>[] { null, [] })
        {
            var result = MonsterTier.Resolve(300, absent);

            Assert.Equal(MonsterTier.AnchorHealth, result.Health);
            Assert.Equal(1f, result.DamageMultiplier);
            Assert.False(result.Graded);
        }
    }

    [Fact]
    public void TheAnchorIsTheOnlyFreeParameterAndItIsAQuotient()
    {
        // Guards the claim the file makes about itself. If someone edits AnchorHealth to retune fights,
        // the ladder must move with it and keep its shape, rather than the anchor drifting away from the
        // grade it was measured against.
        Assert.Equal(60d, MonsterTier.HealthPerGradePoint);
        Assert.Equal(MonsterTier.AnchorHealth / (double)MonsterTier.AnchorGrade, MonsterTier.HealthPerGradePoint);
    }
}
