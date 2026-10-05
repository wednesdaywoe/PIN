using System;
using System.Numerics;
using GameServer.Systems.Aptitude.Commands.Damage;
using Xunit;

namespace GameServer.Tests.Aptitude;

public class ScatterTests
{
    [Fact]
    public void StaysInsideTheCone_AndNeverPointsBelowLevel()
    {
        for (var i = 0; i < 1000; i++)
        {
            // Fungal Bloom's spores: straight up, Spread 100
            var shot = FireProjectileCommand.Scatter(Vector3.UnitZ, 100f);

            var angle = MathF.Acos(Math.Clamp(Vector3.Dot(shot, Vector3.UnitZ), -1f, 1f)) * 180f / MathF.PI;
            Assert.InRange(angle, 0f, 50.01f);
            Assert.True(shot.Z > 0f);
            Assert.InRange(shot.Length(), 0.999f, 1.001f);
        }
    }
}
