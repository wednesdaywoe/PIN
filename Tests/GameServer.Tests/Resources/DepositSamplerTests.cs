using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using GameServer.StaticDB.Records.customdata;
using GameServer.StaticDB.Records.dbzonemetadata;
using GameServer.Systems.Resources;
using Xunit;

namespace GameServer.Tests.Resources;

/// <summary>
///     The deposit gradient, checked against the real shipped rows so the sampler can be verified
///     without a <c>clientdb.sd2</c>.
///
///     The rows are node types 242 ("Crystite Only - (lvls 1-19)") and 239 ("Tier 1 - Rare - Crystite
///     and Raw Iron - Combo") out of <c>dbzonemetadata::ResourceNodeTypeResource</c>, copied verbatim.
///     The gradient reading itself — center values blending linearly to edge values over the radius —
///     is PIN's interpretation of the schema; nothing shipped documents the falloff curve, so if the
///     shape is wrong these tests still pass and the in-game piles are mis-sized, which is what
///     Docs/In-Game-Tests/Thump-Placement.html measures.
/// </summary>
public class DepositSamplerTests
{
    private const uint Crystite = 10;
    private const uint RawIron = 86668;

    [Fact]
    public void CenterOfTheCrystiteVeinPaysCenterValues()
    {
        var rng = new Random(1);

        for (var i = 0; i < 200; i++)
        {
            var yields = DepositSampler.Sample(CrystiteOnly(), 0f, rng);

            var crystite = Assert.Single(yields);
            Assert.Equal(Crystite, crystite.ItemId);
            Assert.True(crystite.Quantity is >= 40 and <= 100);
            Assert.True(crystite.Quality <= 400);
        }
    }

    [Fact]
    public void RimOfTheCrystiteVeinPaysEdgeValuesAndCanComeUpEmpty()
    {
        var rng = new Random(2);
        var sawEmpty = false;

        for (var i = 0; i < 200; i++)
        {
            var yields = DepositSampler.Sample(CrystiteOnly(), 1f, rng);

            if (yields.Count == 0)
            {
                // Edge runs 0..5, so a zero roll is a legitimate dry rim sample, dropped rather
                // than paid as nothing.
                sawEmpty = true;
                continue;
            }

            Assert.True(Assert.Single(yields).Quantity <= 5);
        }

        Assert.True(sawEmpty);
    }

    [Fact]
    public void HalfwayOutTheBoundsAreTheBlendOfCenterAndEdge()
    {
        var rng = new Random(3);

        for (var i = 0; i < 200; i++)
        {
            foreach (var yield in DepositSampler.Sample(CrystiteOnly(), 0.5f, rng))
            {
                // Low blends 40 -> 0 and high blends 100 -> 5, so halfway out the roll must sit
                // inside 20..53.
                Assert.True(yield.Quantity is >= 20 and <= 53);
            }
        }
    }

    [Fact]
    public void AComboVeinRollsEveryRowItCarries()
    {
        var rng = new Random(4);
        var sawIron = false;
        var sawCrystite = false;

        for (var i = 0; i < 200; i++)
        {
            foreach (var yield in DepositSampler.Sample(CrystiteAndIron(), 0f, rng))
            {
                sawIron |= yield.ItemId == RawIron;
                sawCrystite |= yield.ItemId == Crystite;
            }
        }

        Assert.True(sawIron);
        Assert.True(sawCrystite);
    }

    [Fact]
    public void TheDepositUnderYourFeetWinsAndBarrenGroundFindsNothing()
    {
        var deposits = new List<ResourceDeposit>
        {
            Deposit(1, x: 0, y: 0, radius: 30),
            Deposit(2, x: 40, y: 0, radius: 30),
        };

        // 25,0 is inside both discs, but deposit 2's center is 15m away against deposit 1's 25m.
        Assert.Equal(2u, DepositSampler.FindDeposit(deposits, new Vector3(25, 0, 0)).Id);
        Assert.Equal(1u, DepositSampler.FindDeposit(deposits, new Vector3(-5, 0, 0)).Id);
        Assert.Null(DepositSampler.FindDeposit(deposits, new Vector3(200, 200, 0)));
    }

    [Fact]
    public void HeightNeverDecidesWhichDepositCoversASpot()
    {
        var deposits = new List<ResourceDeposit> { Deposit(1, x: 0, y: 0, radius: 30) };

        // 500m above the center is still the center: deposits are discs in X and Y because the
        // server has no terrain to give them a height of their own.
        Assert.Equal(1u, DepositSampler.FindDeposit(deposits, new Vector3(0, 0, 500)).Id);
        Assert.Equal(0f, DepositSampler.DistanceFraction(deposits[0], new Vector3(0, 0, 500)));
    }

    [Fact]
    public void DistanceFractionRunsZeroAtCenterToOneAtRimAndStopsThere()
    {
        var deposit = Deposit(1, x: 0, y: 0, radius: 50);

        Assert.Equal(0f, DepositSampler.DistanceFraction(deposit, new Vector3(0, 0, 0)));
        Assert.Equal(0.5f, DepositSampler.DistanceFraction(deposit, new Vector3(25, 0, 0)), 3);
        Assert.Equal(1f, DepositSampler.DistanceFraction(deposit, new Vector3(50, 0, 0)));
        Assert.Equal(1f, DepositSampler.DistanceFraction(deposit, new Vector3(500, 0, 0)));
    }

    [Fact]
    public void CompositionPercentagesAccountForTheWholePile()
    {
        var yields = new List<DepositYield>
        {
            new(Crystite, 300, 30),
            new(RawIron, 500, 10),
        };

        var composition = DepositSampler.ToComposition(yields);

        Assert.Equal(2, composition.Length);
        Assert.Equal(100f, composition.Sum(c => c.Percent), 3);
        Assert.Equal(75f, composition[0].Percent, 3);
        Assert.Equal(Crystite, composition[0].ItemTypeId);
    }

    [Fact]
    public void AnEmptyPileHasNoComposition()
    {
        Assert.Empty(DepositSampler.ToComposition([]));
    }

    [Fact]
    public void ASingleResourceVeinAdvertisesAllOfItself()
    {
        var share = Assert.Single(DepositSampler.AdvertisedShares(CrystiteOnly()));

        Assert.Equal(Crystite, share.ItemId);
        Assert.Equal(100, share.Percent);
    }

    [Fact]
    public void AComboVeinAdvertisesCenterMidpointShares()
    {
        // Center midpoints are 11.5 crystite and 24 iron out of 35.5, so the advertisement
        // rounds to 32% and 68% — and being midpoints, it never varies between requests.
        var shares = DepositSampler.AdvertisedShares(CrystiteAndIron());

        Assert.Equal(2, shares.Count);
        Assert.Equal(new DepositShare(Crystite, 32), shares[0]);
        Assert.Equal(new DepositShare(RawIron, 68), shares[1]);
    }

    [Fact]
    public void AVeinWithNothingAtTheCenterAdvertisesNothing()
    {
        Assert.Empty(DepositSampler.AdvertisedShares([]));
    }

    [Fact]
    public void AnOutpostAdvertisesEveryResourceInReachMostAbundantFirst()
    {
        // Two deposits in reach of a 100m outpost and one far outside it. Shares add up across
        // deposits, so the pure crystite vein (100%) plus the combo's crystite (32%) outweighs the
        // combo's iron (68%) and crystite leads the readout.
        var deposits = new List<ResourceDeposit>
        {
            Combo(1, x: 50, y: 0, radius: 35),
            Crystite242(2, x: -40, y: 0, radius: 30),
            Combo(3, x: 900, y: 0, radius: 35),
        };

        var items = DepositSampler.ResourcesWithin(deposits, new Vector3(0, 0, 0), 100f, RowsFor);

        Assert.Equal([Crystite, RawIron], items);
    }

    [Fact]
    public void AnOutpostWithNothingInReachAdvertisesNothing()
    {
        var deposits = new List<ResourceDeposit> { Combo(1, x: 900, y: 0, radius: 35) };

        Assert.Empty(DepositSampler.ResourcesWithin(deposits, new Vector3(0, 0, 0), 100f, RowsFor));
    }

    [Fact]
    public void AnOutpostAdvertisesAtMostSixteenResources()
    {
        // The observer view has sixteen slots; a zone with more resources in reach must not overrun it.
        var deposits = new List<ResourceDeposit>();
        for (uint i = 0; i < 20; i++)
        {
            deposits.Add(Deposit(i + 1, x: 0, y: 0, radius: 30, nodeTypeId: 900 + i));
        }

        var items = DepositSampler.ResourcesWithin(
            deposits,
            new Vector3(0, 0, 0),
            100f,
            nodeType => [Row(nodeType, itemId: 1000 + nodeType, centerLow: 10, centerHigh: 10)]);

        Assert.Equal(16, items.Count);
    }

    private static IReadOnlyList<ResourceNodeTypeResource> RowsFor(uint nodeTypeId) => nodeTypeId switch
    {
        239 => CrystiteAndIron(),
        242 => CrystiteOnly(),
        _ => [],
    };

    private static ResourceNodeTypeResource Row(uint nodeTypeId, uint itemId, uint centerLow, uint centerHigh) => new()
    {
        NodeTypeId = nodeTypeId,
        ItemId = itemId,
        CenterLow = centerLow,
        CenterHigh = centerHigh,
    };

    /// <summary>Node type 242, "Crystite Only - (lvls 1-19)", its one row verbatim.</summary>
    private static List<ResourceNodeTypeResource> CrystiteOnly() =>
    [
        new()
        {
            NodeTypeId = 242,
            ItemId = Crystite,
            CenterLow = 40,
            CenterHigh = 100,
            EdgeLow = 0,
            EdgeHigh = 5,
            ItemQualityLow = 0,
            ItemQualityHigh = 400,
        },
    ];

    /// <summary>Node type 239, "Tier 1 - Rare - Crystite and Raw Iron - Combo", both rows verbatim.</summary>
    private static List<ResourceNodeTypeResource> CrystiteAndIron() =>
    [
        new()
        {
            NodeTypeId = 239,
            ItemId = Crystite,
            CenterLow = 8,
            CenterHigh = 15,
            EdgeLow = 0,
            EdgeHigh = 3,
            ItemQualityLow = 0,
            ItemQualityHigh = 400,
        },
        new()
        {
            NodeTypeId = 239,
            ItemId = RawIron,
            CenterLow = 16,
            CenterHigh = 32,
            EdgeLow = 0,
            EdgeHigh = 5,
            ItemQualityLow = 0,
            ItemQualityHigh = 750,
        },
    ];

    private static ResourceDeposit Deposit(uint id, float x, float y, float radius, uint nodeTypeId = 242) => new()
    {
        Id = id,
        ZoneId = 448,
        Name = $"test deposit {id}",
        NodeTypeId = nodeTypeId,
        Position = new Vector3(x, y, 0),
        Radius = radius,
    };

    private static ResourceDeposit Crystite242(uint id, float x, float y, float radius) => Deposit(id, x, y, radius);

    private static ResourceDeposit Combo(uint id, float x, float y, float radius) => Deposit(id, x, y, radius, nodeTypeId: 239);
}
