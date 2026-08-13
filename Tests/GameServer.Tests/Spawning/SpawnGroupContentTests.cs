using System.Linq;
using Xunit;

namespace GameServer.Tests.Spawning;

/// <summary>
///     Reads the shipped <c>spawn_group.json</c> through the loader the server uses. A misspelt key
///     deserialises to a default rather than failing, so a typo here costs a client session to find
///     unless something reads the file back and looks at it.
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
            Assert.NotEqual(default, group.Anchor);
            Assert.True(group.RespawnDelayMs > 0, $"group {id} would respawn instantly");
            Assert.NotEmpty(group.Members);
            Assert.All(group.Members, m =>
            {
                Assert.True(m.MonsterTypeId > 0, $"group {id} has a member with no monster type");
                Assert.True(m.Count > 0, $"group {id} has a member with a count of zero");
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
}
