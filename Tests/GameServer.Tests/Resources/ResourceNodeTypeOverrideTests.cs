using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.customdata;
using Shared.Common;
using Xunit;

namespace GameServer.Tests.Resources;

/// <summary>
///     PIN's own vein types, and the two ways they can quietly fail: a merge that leaves a stale
///     override behind after the file changes, and a JSON file whose nested rows deserialize into
///     zeroes because a property name did not match.
/// </summary>
/// <remarks>
///     These run against the merge in <see cref="SDBInterface"/> with no <c>clientdb.sd2</c> read
///     first, which is the same reason <see cref="DepositSamplerTests"/> copies its rows in by hand:
///     nothing here needs the shipped table to be present, only to be respected when it is.
/// </remarks>
public class ResourceNodeTypeOverrideTests
{
    private const uint CopperCY = 77703;

    [Fact]
    public void AnOverrideIsReadBackThroughTheOrdinarySdbAccessors()
    {
        SDBInterface.ApplyResourceNodeTypeOverrides([Vein(700, "Copper Vein", CopperCY)]);

        var nodeType = SDBInterface.GetResourceNodeType(700);
        Assert.NotNull(nodeType);
        Assert.Equal("Copper Vein", nodeType.Name);

        var row = Assert.Single(SDBInterface.GetResourceNodeTypeResources(700));
        Assert.Equal(CopperCY, row.ItemId);
        Assert.Equal(700u, row.NodeTypeId);
        Assert.Equal(25u, row.CenterLow);
        Assert.Equal(35u, row.CenterHigh);
        Assert.Equal(5u, row.EdgeHigh);
        Assert.Equal(400u, row.ItemQualityHigh);
    }

    /// <summary>
    ///     Dropping a type from the file has to take it out of the table too. Without this, editing
    ///     the file and reloading could only ever add payouts, and a vein removed on purpose would go
    ///     on paying until the next restart.
    /// </summary>
    [Fact]
    public void ReapplyingWithoutATypeTakesItBackOut()
    {
        SDBInterface.ApplyResourceNodeTypeOverrides([Vein(701, "Iron Vein", 77704)]);
        Assert.NotNull(SDBInterface.GetResourceNodeType(701));

        var applied = SDBInterface.ApplyResourceNodeTypeOverrides([]);

        Assert.Equal(0, applied);
        Assert.Null(SDBInterface.GetResourceNodeType(701));
        Assert.Empty(SDBInterface.GetResourceNodeTypeResources(701));
    }

    [Fact]
    public void ApplyingTwiceReplacesRatherThanAccumulates()
    {
        SDBInterface.ApplyResourceNodeTypeOverrides([Vein(702, "Aluminum Vein", 77705)]);
        SDBInterface.ApplyResourceNodeTypeOverrides([Vein(702, "Aluminum Vein, richer", 77705, centerHigh: 90)]);

        Assert.Equal("Aluminum Vein, richer", SDBInterface.GetResourceNodeType(702).Name);
        var row = Assert.Single(SDBInterface.GetResourceNodeTypeResources(702));
        Assert.Equal(90u, row.CenterHigh);
    }

    /// <summary>
    ///     The shipped file itself, deserialized exactly the way the loader does it. The failure this
    ///     catches is silent: a mismatched property name leaves every quantity at zero, every deposit
    ///     of every custom vein pays nothing, and it reads in game as broken payout code.
    /// </summary>
    [Fact]
    public void TheShippedFileDeserializesWithItsQuantitiesIntact()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = new SnakeCasePropertyNamingPolicy(),
            IncludeFields = true,
        };

        var rows = JsonSerializer.Deserialize<ResourceNodeTypeOverride[]>(File.ReadAllText(FindFile()), options);

        Assert.NotEmpty(rows);
        Assert.Equal(rows.Length, rows.Select(row => row.Id).Distinct().Count());

        foreach (var row in rows)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Name));
            Assert.NotEmpty(row.Resources);

            foreach (var resource in row.Resources)
            {
                Assert.NotEqual(0u, resource.ItemId);
                Assert.True(resource.CenterHigh > 0, $"{row.Name} pays nothing at its centre");
                Assert.True(resource.CenterHigh >= resource.CenterLow);
                Assert.True(resource.EdgeHigh >= resource.EdgeLow);
                Assert.True(resource.QualityHigh >= resource.QualityLow);
            }
        }
    }

    private static ResourceNodeTypeOverride Vein(uint id, string name, uint itemId, uint centerHigh = 35) =>
        new()
        {
            Id = id,
            Name = name,
            Resources = new List<ResourceNodeTypeOverrideResource>
            {
                new()
                {
                    ItemId = itemId,
                    CenterLow = 25,
                    CenterHigh = centerHigh,
                    EdgeLow = 0,
                    EdgeHigh = 5,
                    QualityLow = 0,
                    QualityHigh = 400,
                },
            },
        };

    /// <summary>Walks up from the test binary to the repo, which is where the real file lives.</summary>
    private static string FindFile()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "PIN.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "UdpHosts", "GameServer", "StaticDB", "CustomData", "resource_node_type.json");
    }
}
