using System.Linq;
using System.Numerics;
using Xunit;

namespace GameServer.Tests.Spawning;

/// <summary>
///     Reads the shipped <c>spawn_group.json</c> through the loader the server uses. A misspelt key
///     deserialises to a default rather than failing, so a typo here costs a client session to find
///     unless something reads the file back and looks at it. The server also writes this file now,
///     via the <c>spawngroup</c> command, which is a second way for it to come back malformed.
/// </summary>
public class SpawnGroupContentTests
{
    [Fact]
    public void ZoneFourFourEightHasGroupsAndEveryFieldSurvivedDeserialisation()
    {
        var groups = new CustomDBLoader().LoadSpawnGroup();

        Assert.True(groups.TryGetValue(448, out var zone), "zone 448 has no spawn groups");
        Assert.NotEmpty(zone);

        foreach (var (id, group) in zone)
        {
            Assert.Equal(id, group.Id);
            Assert.False(string.IsNullOrWhiteSpace(group.Name), $"group {id} has no name");
            Assert.True(group.RespawnDelayMs > 0, $"group {id} would respawn instantly");
            Assert.NotEmpty(group.Members);
            Assert.All(group.Members, m =>
            {
                Assert.True(m.MonsterTypeId > 0, $"group {id} has a member with no monster type");
                Assert.NotEqual(Vector3.Zero, m.Position);
            });
        }
    }

    [Fact]
    public void GroupIdsAreUniqueWithinAZone()
    {
        foreach (var (zoneId, zone) in new CustomDBLoader().LoadSpawnGroup())
        {
            Assert.Equal(zone.Count, zone.Values.Select(g => g.Id).Distinct().Count());
            Assert.All(zone.Values, g => Assert.Equal(zoneId, g.ZoneId));
        }
    }

    [Fact]
    public void NoTwoMonstersArePlacedOnTopOfEachOther()
    {
        // Nothing in the server separates NPCs, so two placed on one spot stay there. Easy to do by
        // accident with the spawngroup command, since standing still and adding twice is one keystroke
        // away from adding two monsters to a camp.
        foreach (var (_, zone) in new CustomDBLoader().LoadSpawnGroup())
        {
            var placements = zone.Values.SelectMany(g => g.Members).Select(m => m.Position).ToList();

            for (var i = 0; i < placements.Count; i++)
            {
                for (var j = i + 1; j < placements.Count; j++)
                {
                    var separation = Vector3.Distance(placements[i], placements[j]);
                    Assert.True(separation > 1f, $"two monsters are {separation:0.##}m apart at {placements[i]}");
                }
            }
        }
    }
}
