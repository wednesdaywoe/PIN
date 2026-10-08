using System.Linq;
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities;
using BepuUtilities.Memory;
using GameServer.Physics;
using Shared.Collision.Tagfile;
using Shared.Collision.Tagfile.Models;
using Xunit;

namespace GameServer.Tests.Physics;

/// <summary>
///     DATA-22: 300 of zone 448's <c>hkpConvexVerticesShape</c>s used to become a placeholder box 400m
///     underground, so the rocks they describe had no collision. Two causes, both shaped like the data:
///     point sets with coincident points, which Bepu's gift wrapping gives up on, and flat point sets,
///     which Havok makes solid with its convex radius and Bepu can't hull at all. These build both,
///     hundreds of metres from the origin as chunk-space scenery is, and check the hull covers its points.
/// </summary>
public class ConvexVerticesTests
{
    private const float Radius = 0.05f;
    private static readonly Vector3 _farAway = new(612f, 487f, 401f);

    /// <summary>
    ///     A rock from zone 448, recentred, exactly as the chunk ships it. Two of its pairs are 3.8
    ///     micrometres apart and a few more are under half a millimetre, which is enough to stop Bepu's
    ///     gift wrapping after its first face.
    /// </summary>
    private static readonly Vector3[] _rock =
    [
        new(1.203804f, 0.6400909f, -0.715332f),
        new(-0.3401184f, -1.361389f, -0.3271484f),
        new(-0.7732163f, -0.603363f, -0.7597046f),
        new(-0.995018f, -0.7668152f, -0.4534912f),
        new(0.9419479f, 0.8613739f, 0.4820557f),
        new(0.3016434f, 1.361374f, 0.1230469f),
        new(-0.6029434f, -1.139252f, 0.8746948f),
        new(-1.204762f, -0.5895691f, 0.5057373f),
        new(-1.09901f, -0.3291016f, 0.7280884f),
        new(0.5114403f, 1.184082f, -0.8364258f),
        new(0.4056969f, 0.923645f, -1.058777f),
        new(-0.3397865f, -1.361145f, -0.3275757f),
        new(-0.3388748f, -1.360443f, -0.3288574f),
        new(-0.1527176f, -1.21701f, -0.5856934f),
        new(-0.7730751f, -0.6034851f, -0.7596435f),
        new(0.9417496f, 0.8612366f, 0.4823608f),
        new(0.9385071f, 0.8588104f, 0.4867554f),
        new(0.07997513f, 1.197968f, 0.4289551f),
        new(0.07997894f, 1.197968f, 0.4289551f),
        new(0.7496376f, 0.7213593f, 0.7385864f),
        new(-0.6015434f, -1.135895f, 0.8775024f),
        new(-0.6029396f, -1.139252f, 0.8746948f),
        new(-1.097675f, -0.3304748f, 0.7288208f),
        new(-0.5128136f, -0.9137878f, 1.058838f),
        new(0.4066658f, 0.9229584f, -1.058594f),
        new(1.109634f, 0.4181213f, -0.9060059f),
        new(1.204765f, 0.639267f, -0.7199707f),
        new(1.204315f, 0.6396332f, -0.7200317f)
    ];

    [Fact]
    public void ARockWithNearCoincidentPointsHulls()
    {
        var rock = _rock.Select(p => p + _farAway).ToArray();

        var (min, max) = WorldBounds(rock);

        AssertNear(rock.Aggregate(Vector3.Min), min);
        AssertNear(rock.Aggregate(Vector3.Max), max);
    }

    [Fact]
    public void AFlatQuadBecomesASlabTheConvexRadiusThick()
    {
        // The commonest failure in zone 448: four points, 14m by half a metre, perfectly flat.
        var quad = new[]
        {
            new Vector3(-7f, -0.25f, 0f), new Vector3(7f, -0.25f, 0f),
            new Vector3(7f, 0.25f, 0f), new Vector3(-7f, 0.25f, 0f),
        }.Select(p => p + _farAway).ToArray();

        var (min, max) = WorldBounds(quad);

        AssertNear(_farAway - new Vector3(7f, 0.25f, Radius), min);
        AssertNear(_farAway + new Vector3(7f, 0.25f, Radius), max);
    }

    [Fact]
    public void AnOrdinaryBoxIsUnchanged()
    {
        var corners = Box(new Vector3(1f, 1f, 1f)).Select(p => p + _farAway).ToArray();

        var (min, max) = WorldBounds(corners);

        AssertNear(_farAway - new Vector3(0.5f), min);
        AssertNear(_farAway + new Vector3(0.5f), max);
    }

    private static Vector3[] Box(Vector3 size)
    {
        var h = size / 2;
        return
        [
            new(-h.X, -h.Y, -h.Z), new(h.X, -h.Y, -h.Z), new(h.X, h.Y, -h.Z), new(-h.X, h.Y, -h.Z),
            new(-h.X, -h.Y, h.Z), new(h.X, -h.Y, h.Z), new(h.X, h.Y, h.Z), new(-h.X, h.Y, h.Z),
        ];
    }

    /// <summary>Runs the points through the loader and returns the world bounds of what came out.</summary>
    private static (Vector3 Min, Vector3 Max) WorldBounds(Vector3[] points)
    {
        var pool = new BufferPool();
        var simulation = Simulation.Create(pool, new NarrowPhaseCallbacks(), new PoseIntegratorCallbacks(Vector3.Zero), new SolveDescription(8, 1));
        var loader = new TagfileLoader(simulation, pool, null);

        ITagfileExternalStorage layer = null;
        var statics = loader.ProcessObject(Shape(points), ref layer);

        var only = Assert.Single(statics);
        Assert.NotEqual(loader.PlaceholderBox, only.Shape);
        Assert.Equal(ConvexHull.Id, only.Shape.Type);

        var hull = simulation.Shapes.GetShape<ConvexHull>(only.Shape.Index);
        hull.ComputeBounds(Quaternion.Identity, out var min, out var max);
        return (min + only.Pose.Position, max + only.Pose.Position);
    }

    /// <summary>Packs points the way Havok stores them: transposed in fours, the last group padded.</summary>
    private static HkpConvexVerticesShapeObject Shape(Vector3[] points)
    {
        var groups = (points.Length + 3) / 4;
        var rotated = new Vector4[groups][];
        for (var g = 0; g < groups; g++)
        {
            Vector3 P(int lane) => points[System.Math.Min((g * 4) + lane, points.Length - 1)];
            rotated[g] =
            [
                new Vector4(P(0).X, P(1).X, P(2).X, P(3).X),
                new Vector4(P(0).Y, P(1).Y, P(2).Y, P(3).Y),
                new Vector4(P(0).Z, P(1).Z, P(2).Z, P(3).Z),
            ];
        }

        return new HkpConvexVerticesShapeObject
        {
            Name = "#test",
            Radius = Radius,
            NumVertices = (uint)points.Length,
            RotatedVertices = rotated,
        };
    }

    private static void AssertNear(Vector3 expected, Vector3 actual)
    {
        Assert.True(Vector3.Distance(expected, actual) < 1e-3f, $"expected {expected}, got {actual}");
    }
}
