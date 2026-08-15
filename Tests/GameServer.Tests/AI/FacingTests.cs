using System;
using System.Numerics;
using GameServer.Systems.AI;
using Xunit;

namespace GameServer.Tests.AI;

/// <summary>
///     Pins the orientation convention <see cref="Facing.Towards"/> produces to the one the client
///     actually renders: stored orientation is the inverse of the world rotation, and the character's
///     local forward is +Y. The +Y half is measured off the 2016 retail capture (CaptureReplay
///     --facing: every rig retail oriented sits at -90° under a +X reading), and it spent two days as
///     +X first — a humanoid's rendered body follows its aim closely enough that N1 couldn't tell,
///     and only a creature rig (CLIENT-3's sideways Aranha) exposed it. These tests exist so the
///     convention can't quietly drift back.
/// </summary>
public class FacingTests
{
    private static Vector3 WorldForward(Quaternion stored)
        => Vector3.Transform(Vector3.UnitY, Quaternion.Inverse(stored));

    [Fact]
    public void LocalForwardIsPlusYThroughTheInverse()
    {
        var direction = Vector3.Normalize(new Vector3(0.6f, -0.8f, 0f));

        var forward = WorldForward(Facing.Towards(direction, Quaternion.Identity));

        Assert.Equal(direction.X, forward.X, 3);
        Assert.Equal(direction.Y, forward.Y, 3);
    }

    [Fact]
    public void FacingIsYawOnly()
    {
        var upward = Vector3.Normalize(new Vector3(1f, 0f, 5f));

        var forward = WorldForward(Facing.Towards(upward, Quaternion.Identity));

        // A steep aim still stands the character up straight, pointed along the flat part.
        Assert.Equal(0f, forward.Z, 3);
        Assert.True(forward.X > 0.99f);
    }

    [Fact]
    public void AVerticalDirectionSaysNothingAndKeepsTheCurrentOrientation()
    {
        var current = Facing.Towards(new Vector3(0f, 1f, 0f), Quaternion.Identity);

        Assert.Equal(current, Facing.Towards(new Vector3(0f, 0f, -1f), current));
    }

    [Fact]
    public void TheWireQuaternionSitsAQuarterTurnShortOfTheBearing()
    {
        // The capture measurement, restated as arithmetic: reading the stored quaternion with a +X
        // convention lands 90° short of the true bearing, which is exactly what the 2016 pose stream
        // shows for every retail rig.
        var east = Facing.Towards(new Vector3(1f, 0f, 0f), Quaternion.Identity);

        var xReading = Vector3.Transform(Vector3.UnitX, Quaternion.Inverse(east));
        var offset = MathF.Atan2(xReading.Y, xReading.X) * 180f / MathF.PI;

        Assert.Equal(-90f, offset, 1);
    }
}
