using System.Linq;
using GameServer.Data;
using GameServer.Entities.Character;
using GameServer.Systems.Crafting;
using Xunit;

namespace GameServer.Tests.Inventory;

/// <summary>
///     graded-crafting-3: one material held as separate batches, by quality and stats, and spent from them.
/// </summary>
public class ResourceBatchTests
{
    private const uint Iron = 86667;

    private static readonly string Poor = ResourceStats.Pack(Iron, 200, 100, 120, 140, 160, 180);
    private static readonly string Good = ResourceStats.Pack(Iron, 900, 700, 750, 800, 850, 900);

    [Fact]
    public void TwoBatchesOfOneMaterialStayTwoStacks()
    {
        var inventory = Inventory();

        inventory.AddResource(Iron, 5, Poor);
        inventory.AddResource(Iron, 7, Good);
        inventory.AddResource(Iron, 3, Poor);

        Assert.Equal(15u, inventory.GetResourceQuantity(Iron));
        Assert.Equal(
            [(Poor, 8u), (Good, 7u)],
            inventory.GetResourceBatches(Iron).OrderBy(r => r.Quantity).Reverse().Select(r => (r.TextKey, r.Quantity)));
    }

    [Fact]
    public void AnUngradedAddIsItsOwnStack()
    {
        var inventory = Inventory();

        inventory.AddResource(Iron, 4);
        inventory.AddResource(Iron, 2, Good);

        Assert.Equal(2, inventory.GetResourceBatches(Iron).Count());
        Assert.Equal(4u, inventory.GetResourceBatches(Iron).Single(r => r.TextKey == string.Empty).Quantity);
    }

    [Fact]
    public void AnUnchosenSpendTakesUngradedThenTheWorstFirst()
    {
        var inventory = Inventory();
        inventory.AddResource(Iron, 7, Good);
        inventory.AddResource(Iron, 5, Poor);
        inventory.AddResource(Iron, 2);

        var spent = inventory.SpendResource(Iron, 9);

        Assert.Equal([(string.Empty, 2u), (Poor, 5u), (Good, 2u)], spent);
        Assert.Equal([(Good, 5u)], inventory.GetResourceBatches(Iron).Select(r => (r.TextKey, r.Quantity)));
    }

    [Fact]
    public void ANamedSpendTakesOnlyFromThatBatch()
    {
        var inventory = Inventory();
        inventory.AddResource(Iron, 5, Poor);
        inventory.AddResource(Iron, 7, Good);

        Assert.Null(inventory.SpendResource(Iron, 6, Poor));
        Assert.Equal(12u, inventory.GetResourceQuantity(Iron));

        Assert.Equal([(Good, 6u)], inventory.SpendResource(Iron, 6, Good));
        Assert.Equal(1u, inventory.GetResourceBatches(Iron).Single(r => r.TextKey == Good).Quantity);
    }

    [Fact]
    public void NotEnoughTakesNothing()
    {
        var inventory = Inventory();
        inventory.AddResource(Iron, 5, Poor);
        inventory.AddResource(Iron, 2);

        Assert.False(inventory.ConsumeResource(Iron, 8));
        Assert.Equal(7u, inventory.GetResourceQuantity(Iron));
    }

    [Fact]
    public void ABatchTextReadsBack()
    {
        var (sdbId, quality, stats) = ResourceStats.Unpack(Good)!.Value;
        Assert.Equal(Iron, sdbId);
        Assert.Equal(900, quality);
        Assert.Equal([700, 750, 800, 850, 900], stats);
        Assert.Null(ResourceStats.Unpack(string.Empty));
        Assert.Null(ResourceStats.Unpack("not a batch"));
    }

    private static CharacterInventory Inventory()
    {
        ItemArrivalFlagTests.SeedEmptyRootItems();
        var shard = new ItemArrivalFlagTests.FakeShard();
        return new CharacterInventory(shard, new ItemArrivalFlagTests.CapturingClient(shard), new CharacterEntity(shard, 0x1122334455667788UL));
    }
}
