using GameServer.Enums;
using GameServer.StaticDB;

namespace GameServer.Systems.Loot;

public static class ItemRules
{
    /// <summary>
    ///     Whether a dropped id goes in the resource pane or the bag.
    /// </summary>
    /// <remarks>
    ///     The flag on the item's own <c>RootItem</c> row, which is what <c>createitem</c> has always used
    ///     and what [G1](Docs/In-Game-Tests/Resource-Payout.md) confirmed against the client.
    ///
    ///     The first cut of this asked <c>dbitems::ResourceItem</c> instead, which was wrong in a way that
    ///     looked right: that table exists, has 111 rows, and is the gatherable-materials list — Brimstone,
    ///     Ferrite and the rest, ids 75537 and up. Crystite is id 10 and is not in it. So every kill on
    ///     K1's first run rolled crystite correctly and then filed it as an unpayable item.
    /// </remarks>
    public static bool IsResource(uint itemId)
    {
        var item = SDBInterface.GetRootItem(itemId);

        return item != null && ((ItemFlags)item.Flags).HasFlag(ItemFlags.Resource);
    }
}
