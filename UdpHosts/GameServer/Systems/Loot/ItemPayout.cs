using System;
using GameServer.Data;
using GameServer.Enums;
using GameServer.StaticDB;
using Serilog;

namespace GameServer.Systems.Loot;

/// <summary>
///     Puts a non-resource drop in a player's bag. One <see cref="CharacterInventory.CreateItem"/> per
///     copy, because a 1962 item is a guid and a quantity of three is three of them.
/// </summary>
/// <remarks>
///     <para>
///     This is the route M3 and M5 both deferred as "the drop protocol". There isn't one for putting an
///     item in a bag: the client accepts a single-item <c>InventoryUpdate</c> and lists it unprompted,
///     confirmed in game 2026-08-20 once the <c>0x02</c> arrival flag was right (NET-18), and crafting
///     has handed items back that way since CRAFT2. Kill loot reaches here through
///     <see cref="WorldLoot" /> instead of directly — it lies on the ground first and only lands in a bag
///     when somebody picks it up.
///     </para>
///     <para>
///     Two ways a paid item still doesn't reach the player, both logged rather than silently swallowed:
///     an id with no <c>RootItem</c> row (data that outlived its item), and an item without the two low
///     <c>flags</c> bits, which the inventory panel never draws whatever the server sends
///     (DATA-25). Neither is a delivery fault, and telling them apart from the log is the whole point
///     of the wording.
///     </para>
/// </remarks>
public static class ItemPayout
{
    /// <summary>
    ///     Copies one drop may deliver. The shipped tables' quantities are small — item rows mostly carry
    ///     no quantity at all, which reads as one — so this only ever catches a table nobody has read.
    ///     A clip is a Warning because 40 guids and 40 packets out of one kill is data telling us
    ///     something, not a payout.
    /// </summary>
    public const uint MaxCopies = 20;

    /// <summary>The two <c>RootItem.flags</c> bits every item the client lists carries. See DATA-25.</summary>
    private const uint ListingBits = (uint)(ItemFlags.IsTradable | ItemFlags.IsMailable);

    /// <summary>
    ///     Delivers <paramref name="quantity" /> copies of <paramref name="itemId" />. Returns how many
    ///     were actually created, which is zero when the id resolves to nothing.
    /// </summary>
    /// <param name="context">What paid, phrased to open a log line — "Thumper 1234", "Kill of monster type 528".</param>
    public static uint Pay(CharacterInventory inventory, uint itemId, uint quantity, ILogger logger, string context)
    {
        var root = SDBInterface.GetRootItem(itemId);
        if (root == null)
        {
            logger.Warning("{Context} rolled item {ItemId}, which has no RootItem row — nothing to give", context, itemId);
            return 0;
        }

        var copies = Math.Min(quantity, MaxCopies);
        if (copies < quantity)
        {
            logger.Warning(
                "{Context} rolled item {ItemId} x{Quantity}, clipped to {Copies} — a loot row that large has never been read",
                context,
                itemId,
                quantity,
                copies);
        }

        for (var i = 0; i < copies; i++)
        {
            inventory.CreateItem(itemId);
        }

        var drawable = (root.Flags & ListingBits) == ListingBits;
        logger.Information(
            "{Context} paid item {ItemId} ({ItemType}) x{Copies} into the bag{Caveat}",
            context,
            itemId,
            (ItemType)root.Type,
            copies,
            drawable ? string.Empty : $" — flags 0x{root.Flags:X} lack the listing bits, so the panel will not draw it (DATA-25)");

        return copies;
    }
}
