using System.Numerics;
using GameServer.Systems.Hazards;
using Xunit;

namespace GameServer.Tests.Hazards;

/// <summary>
///     Two different things are pinned here. The synthetic cases pin the geometry — a straight wall, a
///     degenerate one, which side is which. The zone 448 cases pin the two readings that aren't in the
///     data: that the offsets are Hermite tangents, and that melded ground is to the left of the directed
///     curve. Those are only as good as the shipped positions they're checked against, and the in-game
///     confirmation is E3 in Docs/In-Game-Tests/Environment.md.
/// </summary>
public class MeldingFieldTests
{
    /// <summary>
    ///     "New Eden Melding_SW", straight out of <c>StaticDB/CustomData/melding.json</c>. Seven control
    ///     points hooking west then south then east, enclosing a pocket in the south-west of the zone.
    /// </summary>
    private static Vector3[] SouthWestPoints =>
    [
        new Vector3(-1359.1992f, -1307.6326f, 480.0437f),
        new Vector3(-1519.6389f, -1249.7942f, 709.3470f),
        new Vector3(-1699.2969f, -1194.4788f, 604.1259f),
        new Vector3(-1927.0443f, -1207.8525f, 729.0336f),
        new Vector3(-2162.4766f, -1211.4391f, 657.1696f),
        new Vector3(-2147.8625f, -1972.7493f, 472.2768f),
        new Vector3(-1355.3960f, -2033.3040f, 546.5955f),
    ];

    private static Vector3[] SouthWestTangents =>
    [
        new Vector3(-275.7974f, 184.0880f, 0f),
        new Vector3(-226.6974f, 12.8743f, 69.2896f),
        new Vector3(-204.3038f, 30.2824f, 10.8161f),
        new Vector3(-276.9000f, -63.7462f, 0f),
        new Vector3(-403.0415f, -182.6267f, 0f),
        new Vector3(100.0000f, 0f, 0f),
        new Vector3(290.9473f, 136.6732f, 81.5457f),
    ];

    /// <summary>A wall running due east with tangents along it, so it stays a straight line.</summary>
    private static Vector2[] StraightWall => MeldingField.Tessellate(
        [new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f)],
        [new Vector3(100f, 0f, 0f), new Vector3(100f, 0f, 0f)]);

    [Fact]
    public void MeldedIsToTheLeftOfTheDirectionOfTravel()
    {
        Assert.True(MeldingField.Locate(StraightWall, new Vector3(50f, 10f, 0f)).Melded);
        Assert.False(MeldingField.Locate(StraightWall, new Vector3(50f, -10f, 0f)).Melded);
    }

    [Fact]
    public void DistanceIsMeasuredToTheWallAndIgnoresAltitude()
    {
        var bearing = MeldingField.Locate(StraightWall, new Vector3(50f, -10f, 900f));

        Assert.Equal(10f, bearing.Distance, 3);
    }

    [Fact]
    public void PastTheEndOfAWallTheNearestPointIsItsEnd()
    {
        var bearing = MeldingField.Locate(StraightWall, new Vector3(130f, 0f, 0f));

        Assert.Equal(30f, bearing.Distance, 3);
    }

    [Fact]
    public void TangentsAlongASegmentLeaveItStraight()
    {
        foreach (var point in StraightWall)
        {
            Assert.Equal(0f, point.Y, 3);
        }
    }

    [Fact]
    public void ATessellatedSegmentKeepsBothOfItsEnds()
    {
        Assert.Equal(new Vector2(0f, 0f), StraightWall[0]);
        Assert.Equal(new Vector2(100f, 0f), StraightWall[^1]);
    }

    [Theory]
    [InlineData(0)] // no points at all
    [InlineData(1)] // a point is not a wall
    public void APerimeterTooShortToBeAWallIsNotOne(int count)
    {
        var points = new Vector3[count];
        var curve = MeldingField.Tessellate(points, points);

        Assert.Empty(curve);
        Assert.False(MeldingField.Locate(curve, Vector3.Zero).Melded);
    }

    [Fact]
    public void MissingTangentsAreNotImprovised()
    {
        Assert.Empty(MeldingField.Tessellate(SouthWestPoints, [Vector3.Zero]));
        Assert.Empty(MeldingField.Tessellate(SouthWestPoints, null));
        Assert.Empty(MeldingField.Tessellate(null, SouthWestTangents));
    }

    [Fact]
    public void NoWallAtAllLeavesYouClear()
    {
        var bearing = MeldingField.Locate([], Vector3.Zero);

        Assert.False(bearing.Melded);
        Assert.Equal(float.MaxValue, bearing.Distance);
    }

    /// <summary>
    ///     The pocket the south-west hook encloses. If the winding convention were backwards this whole
    ///     region would read as safe and the rest of the zone as lethal, which is the failure this catches.
    /// </summary>
    [Theory]
    [InlineData(-1800f, -1600f)]
    [InlineData(-1750f, -1500f)]
    [InlineData(-1900f, -1700f)]
    [InlineData(-2000f, -1400f)]
    public void InsideTheSouthWestHookIsMelded(float x, float y)
    {
        var curve = MeldingField.Tessellate(SouthWestPoints, SouthWestTangents);

        Assert.True(MeldingField.Locate(curve, new Vector3(x, y, 500f)).Melded);
    }

    /// <summary>
    ///     Real outposts, from <c>StaticDB/CustomData/melding.json</c>'s neighbour <c>outpost.json</c>.
    ///     Outpost 18 is 477m from this wall and outpost 22 is 527m; the second is the interesting one,
    ///     because measuring against the straight polyline instead of the tessellated curve puts it on the
    ///     melded side.
    /// </summary>
    [Theory]
    [InlineData(-1075.734f, -923.4863f, 446.9358f)]  // outpost 18
    [InlineData(-908.93f, -1581.81f, 473.79f)]       // outpost 22
    [InlineData(-552.32f, -1180.16f, 560.92f)]       // outpost 26
    public void OutpostsNearTheSouthWestWallAreNotInTheMelding(float x, float y, float z)
    {
        var curve = MeldingField.Tessellate(SouthWestPoints, SouthWestTangents);

        Assert.False(MeldingField.Locate(curve, new Vector3(x, y, z)).Melded);
    }
}
