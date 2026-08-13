using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServer.Systems.Spawning;
using Xunit;

namespace GameServer.Tests.Spawning;

/// <summary>
///     <see cref="SpawnScatter"/> decides where every member of a spawn group stands. The two properties
///     that matter can't be seen on screen: that a group lays out identically every run, so a logged
///     position means something, and that nothing is moved in Z, because the server has no terrain and
///     the anchor's height is the only one known to be ground.
/// </summary>
public class SpawnScatterTests
{
    private static readonly Vector3 Anchor = new(185.7f, 247.1f, 491.87f);

    [Fact]
    public void ASoleMemberStandsOnTheAnchor()
    {
        Assert.Equal(Anchor, SpawnScatter.Placement(Anchor, radius: 6f, slotIndex: 0, slotCount: 1));
    }

    [Fact]
    public void AZeroRadiusStacksEveryMemberOnTheAnchor()
    {
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(Anchor, SpawnScatter.Placement(Anchor, radius: 0f, slotIndex: i, slotCount: 4));
        }
    }

    [Fact]
    public void EveryMemberKeepsTheAnchorHeight()
    {
        for (var i = 0; i < 6; i++)
        {
            var placement = SpawnScatter.Placement(Anchor, radius: 12f, slotIndex: i, slotCount: 6);
            Assert.Equal(Anchor.Z, placement.Z);
        }
    }

    [Fact]
    public void NoMemberStandsFurtherOutThanTheRadius()
    {
        const float Radius = 6f;

        for (var i = 0; i < 5; i++)
        {
            var placement = SpawnScatter.Placement(Anchor, Radius, i, slotCount: 5);
            Assert.True(Vector2.Distance(new Vector2(placement.X, placement.Y), new Vector2(Anchor.X, Anchor.Y)) <= Radius);
        }
    }

    [Fact]
    public void TheSameSlotResolvesToTheSamePlaceEveryTime()
    {
        var first = SpawnScatter.Placement(Anchor, radius: 12f, slotIndex: 3, slotCount: 6);
        var second = SpawnScatter.Placement(Anchor, radius: 12f, slotIndex: 3, slotCount: 6);

        Assert.Equal(first, second);
    }

    [Fact]
    public void MembersDoNotStandInsideEachOther()
    {
        // A pack that shares one position is the failure this is guarding: nothing in the server
        // separates NPCs, so two on the same spot stay there.
        var placements = new List<Vector3>();
        for (var i = 0; i < 6; i++)
        {
            placements.Add(SpawnScatter.Placement(Anchor, radius: 12f, slotIndex: i, slotCount: 6));
        }

        var closest = (from a in placements from b in placements where a != b select Vector3.Distance(a, b)).Min();

        Assert.True(closest > 1f, $"closest pair was {closest:0.##}m apart");
    }
}
