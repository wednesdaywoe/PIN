using System;
using System.Linq;
using System.Text.RegularExpressions;
using GameServer.Systems.Crafting;
using Xunit;

namespace GameServer.Tests.Crafting;

public class ResourceStatsTests
{
    // lib_Items.lua GetResourceStats: gmatch "(%w+)(%-)", each value BaseNToBase10(value, 36)
    private static long[] ReadLikeTheClient(string text) =>
        Regex.Matches(text, "([0-9A-Za-z]+)(-)")
             .Select(m => m.Groups[1].Value.Aggregate(0L, (n, c) => (n * 36) + "0123456789abcdefghijklmnopqrstuvwxyz".IndexOf(c)))
             .ToArray();

    [Fact]
    public void ClientReadsBackQualityAndStats()
    {
        var values = ReadLikeTheClient(ResourceStats.Pack(77705, 900, 700, 750, 800, 850, 900));

        Assert.Equal([1L, 77705, 900, 700, 750, 800, 850, 900, 0, 0], values);
    }

    [Fact]
    public void MissingStatsAreZero_SoTheTooltipSkipsThem()
    {
        var values = ReadLikeTheClient(ResourceStats.Pack(77705, 0, 5));

        Assert.Equal([1L, 77705, 0, 5, 0, 0, 0, 0, 0, 0], values);
    }

    [Fact]
    public void NegativeStatsAreRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ResourceStats.Pack(77705, 100, -1));
    }
}
