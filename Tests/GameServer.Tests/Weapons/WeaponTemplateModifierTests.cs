using GameServer.StaticDB;
using Xunit;

namespace GameServer.Tests.Weapons;

/// <summary>
///     One item's <c>WeaponTemplateModifiers</c> row applied to one template value. The rule under test
///     is the zero multiplier: the SDB tunes a stat through either the additive column or the
///     multiplier and leaves the other at 0, so a 0 multiplier means "this row doesn't set one".
///
///     Reading it literally is not a hypothetical. It shipped, and it resolved 269 weapons down to no
///     range and 220 to no damage — including every weapon two of the monsters used for in-game
///     testing carry, which is how NPC combat came to look broken in two different ways at once
///     (N2 and N6 in Docs/In-Game-Tests/NPC-Combat.html). The real rows are used as the cases below.
/// </summary>
public class WeaponTemplateModifierTests
{
    [Fact]
    public void NoModifierRowLeavesTheTemplateValueAlone()
    {
        Assert.Equal(150f, SDBUtils.WeaponTemplateModifier(150f, null, null));
    }

    [Fact]
    public void AnAdditiveModifierIsAddedAndAMultiplierMultiplies()
    {
        // Accord Guard Rifle 137167: damage_per_round 0, damage_per_round_mult 5, on a template of 46.
        Assert.Equal(230f, SDBUtils.WeaponTemplateModifier(46f, 0f, 5f));
    }

    [Fact]
    public void BothColumnsApplyTogetherWhenBothAreSet()
    {
        // Accord Famas 76108: ms_per_burst 120 over a template of 180, multiplier an explicit 1.
        Assert.Equal(300f, SDBUtils.WeaponTemplateModifier(180f, 120f, 1f));
    }

    [Fact]
    public void AZeroMultiplierMeansUnsetAndDoesNotZeroTheStat()
    {
        // Chosen Grunt Rifle 85953: range 80 over a template of 100, multiplier left at 0. Read
        // literally this is 0, and the NPC holds fire forever because its rifle cannot reach anything.
        Assert.Equal(180f, SDBUtils.WeaponTemplateModifier(100f, 80f, 0f));
    }

    [Fact]
    public void AZeroMultiplierDoesNotZeroDamageEither()
    {
        // Accord Famas 76108: damage_per_round 35 over a template of 30, multiplier left at 0. This one
        // fired, with muzzle flash, and every round landed for nothing.
        Assert.Equal(65f, SDBUtils.WeaponTemplateModifier(30f, 35f, 0f));
    }

    [Fact]
    public void AZeroTemplateValueWithNoModifiersStaysZero()
    {
        // Nothing here invents a number: a stat the data really does leave empty stays empty.
        Assert.Equal(0f, SDBUtils.WeaponTemplateModifier(0f, 0f, 0f));
    }

    [Fact]
    public void ANegativeModifierStillSubtracts()
    {
        // The range column runs down to -185, so modifiers genuinely do reduce stats. Only the
        // multiplier gets the unset treatment.
        Assert.Equal(65f, SDBUtils.WeaponTemplateModifier(150f, -85f, 1f));
    }
}
