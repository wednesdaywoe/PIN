using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Combat;
using Xunit;

namespace GameServer.Tests.Combat;

/// <summary>
///     Pins <see cref="WeaponSplash"/>'s reading of <c>dbitems::Ammo</c>. Whether a grenade feels right is
///     an in-game question (V7 and D3 in Docs/In-Game-Tests/Deployables-And-Vehicles.md and Damage-Loop.md);
///     these hold the two decisions that would silently change every weapon in the game if they were wrong —
///     that a rifle still does not splash, and that -1 means "no blast" rather than a one-metre one.
///
///     The figures in the comments are from a dump of the real clientdb.sd2, read 2026-08-16: 1264 ammo
///     rows, 612 with a positive radius, 649 at zero, 3 at -1.
/// </summary>
public class WeaponSplashTests
{
    [Fact]
    public void AnOrdinaryBulletDoesNotExplode()
    {
        // "Normal Bullet (assault rifle)" ships impact_radius 0, and so do 648 other rows. This is the test
        // that says the change was aimed at explosives: if it ever fails, every rifle in the game just
        // became an area weapon.
        var splash = WeaponSplash.Resolve(new Ammo { Name = "Normal Bullet (assault rifle)", ImpactRadius = 0f });

        Assert.False(splash.Enabled);
        Assert.Equal(0f, splash.ScaleAt(0f));
    }

    [Fact]
    public void MinusOneIsASentinelRatherThanARadius()
    {
        // Three rows carry it, all "Desecrated" ammo. Read as a distance it would be a blast that reaches
        // nothing, which happens to look the same from outside - but it would also make Enabled true and
        // send every shot round the whole entity loop for nothing.
        var splash = WeaponSplash.Resolve(new Ammo { Name = "Desecrated Ammo", ImpactRadius = -1f });

        Assert.False(splash.Enabled);
    }

    [Fact]
    public void NullAmmoIsNotAnExplosion()
    {
        Assert.False(WeaponSplash.Resolve(null).Enabled);
    }

    [Fact]
    public void AThrownGrenadeFallsOffFromTheCentreOutwards()
    {
        // "Thrown Frag Grenade": 5m radius, no inner core, which is the shape 497 of the 612 exploding rows
        // have. Full damage only exactly at the impact point, nothing at the rim.
        var splash = WeaponSplash.Resolve(new Ammo { Name = "Thrown Frag Grenade", ImpactRadius = 5f, MinImpactRadius = 0f });

        Assert.True(splash.Enabled);
        Assert.Equal(5f, splash.Radius);
        Assert.Equal(0f, splash.PointBlankRange);
        Assert.Equal(1f, splash.ScaleAt(0f));
        Assert.Equal(0.5f, splash.ScaleAt(2.5f), 3);
        Assert.Equal(0f, splash.ScaleAt(5f));
    }

    [Fact]
    public void ACoreTakesTheWholeHitAndOnlyTheRestFallsOff()
    {
        // "Heal (Nanite) Grenade": 5m radius with a 2.5m core, one of the 115 rows that have one.
        var splash = WeaponSplash.Resolve(new Ammo { Name = "Heal (Nanite) Grenade", ImpactRadius = 5f, MinImpactRadius = 2.5f });

        Assert.Equal(2.5f, splash.PointBlankRange);
        Assert.Equal(1f, splash.ScaleAt(2.5f));
        Assert.Equal(0.5f, splash.ScaleAt(3.75f), 3);
    }

    [Fact]
    public void ACoreWiderThanTheBlastIsIgnoredRatherThanTrusted()
    {
        // Nothing in the shipped table does this, so the reading is unevidenced either way. Dropping the
        // core keeps a falloff that still falls off; honouring it would make the blast do full damage
        // everywhere, which is the more surprising of the two if the data ever changes.
        var splash = WeaponSplash.Resolve(new Ammo { ImpactRadius = 2f, MinImpactRadius = 30f });

        Assert.True(splash.Enabled);
        Assert.Equal(0f, splash.PointBlankRange);
        Assert.Equal(0.5f, splash.ScaleAt(1f), 3);
    }

    [Fact]
    public void NothingBeyondTheRimTakesAnything()
    {
        var splash = WeaponSplash.Resolve(new Ammo { ImpactRadius = 3f });

        Assert.Equal(0f, splash.ScaleAt(3.01f));
        Assert.Equal(0f, splash.ScaleAt(500f));
    }
}
