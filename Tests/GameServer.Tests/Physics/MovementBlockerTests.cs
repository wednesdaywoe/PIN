using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using GameServer.Physics;
using GameServer.Systems.SystemEvents;
using Xunit;

namespace GameServer.Tests.Physics;

/// <summary>
///     The map's movement blockers live in a world of their own, so only movement can meet them. These
///     put one sphere in it, the commonest blocker shape after boxes, and walk lines past and through it.
/// </summary>
public class MovementBlockerTests
{
    [Fact]
    public void ALineThroughABlockerIsBlocked()
    {
        var engine = WithBlockerAt(new Vector3(10f, 0f, 1f), 2f);

        Assert.True(engine.IsMovementBlocked(new Vector3(0f, 0f, 1f), new Vector3(20f, 0f, 1f)));
    }

    [Fact]
    public void ALineThatStopsShortIsNot()
    {
        var engine = WithBlockerAt(new Vector3(10f, 0f, 1f), 2f);

        Assert.False(engine.IsMovementBlocked(new Vector3(0f, 0f, 1f), new Vector3(7f, 0f, 1f)));
    }

    [Fact]
    public void SomethingInsideABlockerCanWalkOut()
    {
        var engine = WithBlockerAt(new Vector3(10f, 0f, 1f), 2f);

        Assert.False(engine.IsMovementBlocked(new Vector3(10f, 0f, 1f), new Vector3(15f, 0f, 1f)));
    }

    [Fact]
    public void BlockersAreInvisibleToTheWorld()
    {
        var engine = WithBlockerAt(new Vector3(10f, 0f, 1f), 2f);

        Assert.False(engine.TryHitWorld(new Vector3(0f, 0f, 1f), Vector3.UnitX, 20f, out _));
        Assert.False(engine.TryGetGroundHeight(new Vector3(10f, 0f, 1f), out _));
    }

    private static PhysicsEngine WithBlockerAt(Vector3 centre, float radius)
    {
        var engine = new PhysicsEngine(new EventBus(), 0, isDebugPipeClient: true);
        engine.Blockers.Statics.Add(new StaticDescription(centre, engine.Blockers.Shapes.Add(new Sphere(radius))));
        return engine;
    }
}
