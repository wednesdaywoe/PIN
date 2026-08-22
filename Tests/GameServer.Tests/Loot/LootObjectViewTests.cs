using System.Numerics;
using AeroMessages.Common;
using AeroMessages.GSS.V66.AreaVisualData.View;
using Xunit;

namespace GameServer.Tests.Loot;

/// <summary>
///     The half of world loot that cannot be checked by reading the server: whether a slot that has been
///     emptied travels to the client as an emptied slot.
/// </summary>
/// <remarks>
///     A drop appears by writing one of the view's 24 nullable slots and disappears by writing null back.
///     Appearing is obviously fine; vanishing is the direction nothing else in PIN exercises, and a
///     pickup that stays drawn on everyone else's ground is the failure it would produce. This pins it at
///     the wire, with no server and no client involved.
/// </remarks>
public class LootObjectViewTests
{
    [Fact]
    public void ADroppedItemTravelsAsAFilledSlot()
    {
        var server = new LootObjectView();
        var client = new LootObjectView();

        server.LootObjects_0Prop = Drop(33815);
        Assert.True(server.GetPackedChangesSize() > 0);

        Apply(server, client);

        Assert.NotNull(client.LootObjects_0Prop);
        Assert.Equal(33815u, client.LootObjects_0Prop.Value.LootSdbId);
    }

    [Fact]
    public void ACollectedItemTravelsAsAnEmptiedSlot()
    {
        var server = new LootObjectView();
        var client = new LootObjectView();

        server.LootObjects_7Prop = Drop(33816);
        Apply(server, client);
        Assert.NotNull(client.LootObjects_7Prop);

        server.LootObjects_7Prop = null;
        Assert.True(server.GetPackedChangesSize() > 0);

        Apply(server, client);

        Assert.Null(client.LootObjects_7Prop);
    }

    [Fact]
    public void SlotsAreIndependent()
    {
        var server = new LootObjectView();
        var client = new LootObjectView();

        server.LootObjects_3Prop = Drop(33815);
        server.LootObjects_4Prop = Drop(33816);
        Apply(server, client);

        server.LootObjects_3Prop = null;
        Apply(server, client);

        Assert.Null(client.LootObjects_3Prop);
        Assert.NotNull(client.LootObjects_4Prop);
        Assert.Equal(33816u, client.LootObjects_4Prop.Value.LootSdbId);
    }

    private static void Apply(LootObjectView from, LootObjectView to)
    {
        var changes = new byte[from.GetPackedChangesSize()];
        from.PackChanges(changes);
        to.UnpackChanges(changes);
    }

    private static LootObjectData Drop(uint itemId)
    {
        return new LootObjectData
        {
            Time = 1,
            HaveEntity = 0,
            Entity = new EntityId { Backing = 0 },
            HaveUnk3 = 1,
            Unk3 = new LootObjectUnkOptionalData { Unk1 = 1, Unk2 = 0 },
            Unk4 = default,
            Position = new Vector3(1, 2, 3),
            LootSdbId = itemId,
            Quantity = 1,
            Unk5 = 0,
            Unk6 = 0,
            Unk7 = [0, 0],
        };
    }
}
