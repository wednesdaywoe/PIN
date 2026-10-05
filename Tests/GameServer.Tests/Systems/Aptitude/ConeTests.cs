using System.Numerics;
using GameServer.Systems.AI;
using GameServer.Systems.Aptitude.Commands.Target;
using Xunit;

namespace GameServer.Tests.Systems.Aptitude;

public class ConeTests
{
    // Aiming along +Y from the origin, as the shapes below all do
    private static ConeShape Cone(float length, float angle, float startRadius = 0f, float maxRadius = 0f, bool roundEnds = false) =>
        new(Vector3.Zero, Vector3.UnitY, length, angle, startRadius, maxRadius, roundEnds);

    private static bool Inside(ConeShape shape, float x, float y, float z = 0f) => !float.IsNaN(shape.AlongIfInside(new Vector3(x, y, z)));

    [Fact]
    public void Angle_IsTheFullSpread()
    {
        // Healing Wave's 45 degrees: 22.5 either side, so 10 m out the edge is 10 * tan(22.5) = 4.14 m off the line
        var shape = Cone(20f, 45f);

        Assert.Equal(20f * 0.41421f, shape.EndRadius, 0.01f);
        Assert.True(Inside(shape, 4f, 10f));
        Assert.False(Inside(shape, 4.3f, 10f));
    }

    [Fact]
    public void BehindAndBeyond_AreOutside()
    {
        var shape = Cone(10f, 90f);

        Assert.False(Inside(shape, 0f, -1f));
        Assert.False(Inside(shape, 0f, 10.5f));
        Assert.True(Inside(shape, 0f, 9.5f));
    }

    [Fact]
    public void NoAngle_IsATubeThatWidens()
    {
        // Thermal Wave: 0.5 m at the origin growing to 1.5 m at the end
        var shape = Cone(10f, 0f, 0.5f, 1.5f);

        Assert.False(Inside(shape, 0.6f, 0.5f));
        Assert.True(Inside(shape, 0.9f, 5f));
        Assert.False(Inside(shape, 1.1f, 5f));
        Assert.True(Inside(shape, 1.4f, 9.9f));
    }

    [Fact]
    public void AngleWidensOnTopOfMaxRadius()
    {
        var shape = Cone(10f, 90f, 0f, 2f);

        Assert.Equal(12f, shape.EndRadius, 0.01f);
    }

    [Fact]
    public void RoundEnds_ReachJustBehindAndPastTheEnd()
    {
        var flat = Cone(3.5f, 0f, 1f, 1f);
        var round = Cone(3.5f, 0f, 1f, 1f, roundEnds: true);

        Assert.False(Inside(flat, 0f, -0.5f));
        Assert.True(Inside(round, 0f, -0.5f));
        Assert.True(Inside(round, 0f, 4.2f));
        Assert.False(Inside(round, 0f, 4.8f));
    }

    [Fact]
    public void HalfCircleOrMore_IsEverythingInRange()
    {
        var shape = Cone(5f, 360f);

        Assert.True(Inside(shape, 0f, -4f));
        Assert.True(Inside(shape, 3f, 3f, 2f));
        Assert.False(Inside(shape, 0f, -6f));
    }

    [Fact]
    public void Pick_SkipsSelfAndCountsTheBody()
    {
        var self = new FakeTarget("self") { Position = Vector3.Zero };

        // Feet 2.3 m below a narrow line at shoulder height: only the head reaches it
        var tall = new FakeTarget("tall") { Position = new Vector3(0f, 5f, -2.3f) };
        var low = new FakeTarget("low") { Position = new Vector3(0f, 5f, -4f) };
        var shape = new ConeShape(Vector3.Zero, Vector3.UnitY, 10f, 0f, 0.2f, 0.2f, false);

        var hits = TargetConeAECommand.Pick(shape, [self, tall, low], self, false, 0);

        Assert.Equal([tall], hits);
    }

    [Fact]
    public void Pick_KeepsTheNearestUpToMaxTargets()
    {
        var self = new FakeTarget("self");
        var far = new FakeTarget("far") { Position = new Vector3(0f, 8f, 0f) };
        var near = new FakeTarget("near") { Position = new Vector3(0f, 2f, 0f) };
        var middle = new FakeTarget("middle") { Position = new Vector3(0f, 5f, 0f) };

        var hits = TargetConeAECommand.Pick(Cone(10f, 45f), [far, near, middle], self, false, 2);

        Assert.Equal([near, middle], hits);
    }

    [Fact]
    public void Pick_SortByAngle_PrefersTheOneOnTheAimLine()
    {
        var self = new FakeTarget("self");
        var nearOffLine = new FakeTarget("nearOffLine") { Position = new Vector3(2f, 3f, -0.9f) };
        var farOnLine = new FakeTarget("farOnLine") { Position = new Vector3(0f, 9f, -0.9f) };

        var hits = TargetConeAECommand.Pick(Cone(10f, 120f), [nearOffLine, farOnLine], self, true, 1);

        Assert.Equal([farOnLine], hits);
    }

    [Fact]
    public void Pick_DropsWhatIsOutOfSightBeforeCountingMaxTargets()
    {
        var self = new FakeTarget("self");
        var behindWall = new FakeTarget("behindWall") { Position = new Vector3(0f, 2f, 0f) };
        var near = new FakeTarget("near") { Position = new Vector3(0f, 5f, 0f) };
        var far = new FakeTarget("far") { Position = new Vector3(0f, 8f, 0f) };

        var hits = TargetConeAECommand.Pick(Cone(10f, 45f), [behindWall, near, far], self, false, 1, target => target != behindWall);

        Assert.Equal([near], hits);
    }

    [Fact]
    public void WallCheck_AWallTallerThanTheBodyHides_ALowOneDoesNot()
    {
        var from = new Vector3(0f, 0f, 1f);
        var target = new Vector3(0f, 10f, 0f);

        Assert.False(WallCheck.InSight(from, target, Wall(height: 3f)));
        Assert.True(WallCheck.InSight(from, target, Wall(height: 1f)));
        Assert.True(WallCheck.InSight(from, new Vector3(0f, 3f, 0f), Wall(height: 3f)));
    }

    /// <summary>
    ///     A wall across the aim at y = 5 standing <paramref name="height" /> metres tall from z = 0
    /// </summary>
    private static NpcKnockback.WallProbe Wall(float height) =>
        (Vector3 origin, Vector3 direction, float maxDistance, out float distance) =>
        {
            distance = maxDistance;
            if (direction.Y <= 0f)
            {
                return false;
            }

            var t = (5f - origin.Y) / direction.Y;
            if (t < 0f || t > maxDistance || origin.Z + (direction.Z * t) > height)
            {
                return false;
            }

            distance = t;
            return true;
        };
}
