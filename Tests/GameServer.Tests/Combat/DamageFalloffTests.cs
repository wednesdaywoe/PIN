using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.ProjectileSim;
using Xunit;

namespace GameServer.Tests.Combat;

/// <summary>
///     Pins the range decay model in <see cref="DamageFalloff"/>. Whether the model matches the client is
///     an in-game question (R1-R5 in Docs/In-Game-Tests.md); these only hold it to what it means to do,
///     including the promise that a wrong guess degrades to no decay rather than to weaker weapons.
/// </summary>
public class DamageFalloffTests
{
    private const float BaseDamage = 100f;

    [Fact]
    public void ResolveReadsTheCurveOffTheWeaponAndAmmo()
    {
        var curve = DamageFalloff.Resolve(BaseDamage, Weapon(), Ammunition());

        Assert.True(curve.Enabled);
        Assert.Equal(BaseDamage, curve.BaseDamage);
        Assert.Equal(25f, curve.MinDamage);
        Assert.Equal(20f, curve.FullDamageRange);
        Assert.Equal(100f, curve.MaxRange);
    }

    [Fact]
    public void DamageIsFlatInsideTheFullDamageRange()
    {
        var curve = DamageFalloff.Resolve(BaseDamage, Weapon(), Ammunition());

        Assert.Equal(BaseDamage, curve.DamageAt(0f));
        Assert.Equal(BaseDamage, curve.DamageAt(19.9f));
        Assert.Equal(BaseDamage, curve.DamageAt(20f));
    }

    [Fact]
    public void DamageFallsLinearlyToTheFloorAndStaysThere()
    {
        var curve = DamageFalloff.Resolve(BaseDamage, Weapon(), Ammunition());

        // Halfway between 20m and 100m is halfway between 100 and 25
        Assert.Equal(62.5f, curve.DamageAt(60f), 3);
        Assert.Equal(25f, curve.DamageAt(100f), 3);
        Assert.Equal(25f, curve.DamageAt(500f), 3);
    }

    [Fact]
    public void TheHigherOfTheTwoFloorsWins()
    {
        // The template's 40/100 beats the ammo's 0.25
        var curve = DamageFalloff.Resolve(BaseDamage, Weapon(minDamage: 40), Ammunition(minFrac: 0.25f));

        Assert.Equal(40f, curve.MinDamage, 3);
    }

    /// <summary>
    ///     The R5 property: both floors are read as a fraction of a round rather than as absolute points, so
    ///     a weapon damage buff carries the long-range floor up with it. Doubling the shooter's damage has to
    ///     double what lands at max range, not decay back to the unbuffed minimum.
    /// </summary>
    [Fact]
    public void BuffingWeaponDamageCarriesTheFloorUpWithIt()
    {
        var unbuffed = DamageFalloff.Resolve(BaseDamage, Weapon(minDamage: 40), Ammunition());
        var buffed = DamageFalloff.Resolve(BaseDamage * 2f, Weapon(minDamage: 40), Ammunition());

        Assert.Equal(40f, unbuffed.DamageAt(100f), 3);
        Assert.Equal(80f, buffed.DamageAt(100f), 3);
    }

    [Theory]
    [InlineData(0, 0.2f, 0.25f)] // DamageDecay unset: the ammo doesn't decay at all
    [InlineData(1, 0.2f, 1f)]    // floor is a full round, nothing to decay towards
    [InlineData(1, 0.2f, 1.5f)]  // floor above a full round, which would otherwise amplify damage
    [InlineData(1, 1f, 0.25f)]   // decay starts at max range, no distance to decay over
    [InlineData(1, 1.5f, 0.25f)] // decay starts past max range
    public void NonsenseInputsLeaveDecayDisabled(byte decay, float rangeFrac, float minFrac)
    {
        var curve = DamageFalloff.Resolve(BaseDamage, Weapon(), Ammunition(decay, rangeFrac, minFrac));

        Assert.False(curve.Enabled);
    }

    [Fact]
    public void MissingOrZeroedInputsLeaveDecayDisabled()
    {
        Assert.False(DamageFalloff.Resolve(BaseDamage, null, Ammunition()).Enabled);
        Assert.False(DamageFalloff.Resolve(BaseDamage, Weapon(), null).Enabled);
        Assert.False(DamageFalloff.Resolve(BaseDamage, Weapon(range: 0f), Ammunition()).Enabled);
        Assert.False(DamageFalloff.Resolve(0f, Weapon(), Ammunition()).Enabled);
    }

    /// <summary>
    ///     What being wrong has to cost: a disabled curve is full damage at every range, which is exactly how
    ///     the weapon behaved before decay existed.
    /// </summary>
    [Fact]
    public void ADisabledCurveDealsFullDamageAtEveryRange()
    {
        var curve = DamageFalloff.Resolve(BaseDamage, Weapon(), Ammunition(decay: 0));

        Assert.Equal(BaseDamage, curve.DamageAt(0f));
        Assert.Equal(BaseDamage, curve.DamageAt(100f));
        Assert.Equal(BaseDamage, curve.DamageAt(10000f));
    }

    /// <summary>
    ///     A template with no damage per round can't express its floor as a fraction. It has to fall back to
    ///     the ammo's rather than dividing by zero.
    /// </summary>
    [Fact]
    public void AZeroDamagePerRoundTemplateFallsBackToTheAmmoFloor()
    {
        var curve = DamageFalloff.Resolve(BaseDamage, Weapon(damagePerRound: 0, minDamage: 40), Ammunition(minFrac: 0.25f));

        Assert.True(curve.Enabled);
        Assert.Equal(25f, curve.MinDamage, 3);
    }

    /// <summary>
    ///     A rifle-ish pair: full damage to 20m, decaying to a quarter of a round at 100m.
    /// </summary>
    private static WeaponTemplateResult Weapon(int damagePerRound = 100, int minDamage = 0, float range = 100f)
    {
        return new WeaponTemplateResult { DamagePerRound = damagePerRound, MinDamage = minDamage, Range = range };
    }

    private static Ammo Ammunition(byte decay = 1, float rangeFrac = 0.2f, float minFrac = 0.25f)
    {
        return new Ammo { DamageDecay = decay, DamageDecayRangefrac = rangeFrac, MinDamageFrac = minFrac };
    }
}
