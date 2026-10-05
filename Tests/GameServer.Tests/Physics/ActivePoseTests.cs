using System.Collections.Generic;
using System.Numerics;
using BepuPhysics.Collidables;
using GameServer.Physics;
using GameServer.Physics.PoseLoader;
using GameServer.Systems.SystemEvents;
using Xunit;
using static GameServer.Physics.PoseLoader.PoseData;

namespace GameServer.Tests.Physics;

/// <summary>
///     A ray hit on a pose names a compound child index, and the hit's body part, damage mod and headshot
///     flag come from looking that index up in the pose data. These pin that the two line up (NET-7).
/// </summary>
public class ActivePoseTests
{
    [Fact]
    public void EveryCompoundChildMapsToTheShapeItWasBuiltFrom()
    {
        var engine = new PhysicsEngine(new EventBus(), 0, isDebugPipeClient: true);
        var pose = new PoseData
        {
            Name = "test",
            Shapes = new Dictionary<string, ShapeDef>
            {
                ["CatchAll"] = Sphere("CatchAll", 3f, Vector3.Zero),
                ["Body"] = new CapsuleShapeDef { Name = "Body", Radius = 0.4f, Height = 1.2f, DamageMod = 1f, HitTagType = "Default", Flags = new(), Material = 1, Origin = new Vector3(0, 0, 1), Rotation = Quaternion.Identity },

                // No asset db, so this takes the HKX route's placeholder and still adds exactly one child
                ["Hull"] = new HKXShapeDef { Name = "Hull", Filename = "00000001", DamageMod = 0.5f, HitTagType = "Default", Flags = new(), Material = 2, Origin = new Vector3(0, 0, 0.5f), Rotation = Quaternion.Identity },
                ["NPCWarning"] = Sphere("NPCWarning", 5f, Vector3.Zero),
                ["Head"] = Sphere("Head", 0.25f, new Vector3(0, 0, 2), headshot: true),
            },
        };

        var (entry, shapes) = engine.CreateActivePose(pose, Vector3.Zero);
        var compound = engine.Simulation.Shapes.GetShape<Compound>(entry.ShapeIndex.Index);

        Assert.Equal(compound.ChildCount, shapes.Count);
        for (var i = 0; i < compound.ChildCount; i++)
        {
            Assert.Equal(compound.Children[i].ShapeIndex, shapes[i].ShapeId);
        }

        Assert.Equal(["Body", "Hull", "Head"], [shapes[0].Name, shapes[1].Name, shapes[2].Name]);
        Assert.True(shapes[2].ShapeFlags.Headshot);
        Assert.False(shapes[0].ShapeFlags.Headshot);
    }

    private static SphereShapeDef Sphere(string name, float radius, Vector3 origin, bool headshot = false) => new()
    {
        Name = name,
        Radius = radius,
        DamageMod = 1f,
        HitTagType = "Default",
        Flags = new ShapeFlags { Headshot = headshot },
        Material = 1,
        Origin = origin,
        Rotation = Quaternion.Identity,
    };
}
