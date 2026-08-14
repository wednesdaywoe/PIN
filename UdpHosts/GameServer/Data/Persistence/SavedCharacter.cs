using System;
using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Enums;

namespace GameServer.Data.Persistence;

/// <summary>
///     Everything about a character that survives a logout, and nothing that doesn't.
///
///     Two things are deliberately absent. Loadouts aren't saved, because every login regenerates all 20
///     battleframes and their modules out of <see cref="HardcodedCharacterData"/>: saving them would be
///     saving a copy of a constant, and the copy would go stale the first time the constant changed.
///     Items are saved only when that seed didn't produce them, which today means what an admin command
///     made or a kill paid out. What this costs is equipping. A saved item comes back in the bag rather
///     than in the slot it was in, because the loadout that referenced it was rebuilt from scratch.
///
///     Item guids aren't saved either, and that one is not a cut. <c>GuidService</c> packs a shard
///     timestamp and a counter that restarts at zero into every guid it issues, so a guid minted last
///     session can be handed out again this session to something else entirely. Restoring an item means
///     minting it a fresh guid. Nothing outside the server holds an item guid across a session: the
///     client is sent the whole inventory at login and takes the guids in it as given.
/// </summary>
public class SavedCharacter
{
    /// <summary>
    ///     Bumped when a field's meaning changes rather than when one is added. A save with an unknown
    ///     version is left alone rather than guessed at, see <see cref="Persistence.CharacterStore"/>.
    /// </summary>
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public ulong CharacterId { get; set; }

    /// <summary>Not read back. It's here so a save file can be read by a person.</summary>
    public string SavedAt { get; set; }

    public uint LastZoneId { get; set; }

    public uint LastOutpostId { get; set; }

    public uint TimePlayedSecs { get; set; }

    public List<SavedResource> Resources { get; set; } = [];

    public List<SavedItem> Items { get; set; } = [];

    public static SavedCharacter Snapshot(
        ulong characterId,
        uint zoneId,
        uint outpostId,
        uint timePlayedSecs,
        IEnumerable<Resource> resources,
        IEnumerable<Item> items,
        DateTimeOffset savedAt)
    {
        return new SavedCharacter
        {
            Version = CurrentVersion,
            CharacterId = characterId,
            SavedAt = savedAt.ToString("u"),
            LastZoneId = zoneId,
            LastOutpostId = outpostId,
            TimePlayedSecs = timePlayedSecs,

            // Ordered so two saves of the same inventory produce the same bytes, which is what lets the
            // store skip a write that would change nothing.
            Resources = [.. resources
                .Where(resource => resource.Quantity > 0)
                .OrderBy(resource => resource.SdbId)
                .Select(resource => new SavedResource { SdbId = resource.SdbId, Quantity = resource.Quantity })],
            Items = [.. items
                .OrderBy(item => item.SdbId)
                .ThenBy(item => item.TimestampEpoch)
                .Select(FromItem)]
        };
    }

    private static SavedItem FromItem(Item item)
    {
        return new SavedItem
        {
            SdbId = item.SdbId,
            SubInventory = item.SubInventory,
            Durability = item.Durability,

            // IsEquipped comes off on the way out. The loadout that slotted this item won't exist next
            // session, so an item that came back still claiming to be equipped would be equipped
            // according to nothing.
            DynamicFlags = (byte)(item.DynamicFlags & ~(byte)ItemDynamicFlags.IsEquipped),
            TimestampEpoch = item.TimestampEpoch,
            Modules = item.Modules ?? []
        };
    }
}

public class SavedResource
{
    public uint SdbId { get; set; }

    public uint Quantity { get; set; }
}

public class SavedItem
{
    public uint SdbId { get; set; }

    public byte SubInventory { get; set; }

    public ushort Durability { get; set; }

    public byte DynamicFlags { get; set; }

    /// <summary>When the item was first made, kept so "how long have I had this" survives a re-mint.</summary>
    public uint TimestampEpoch { get; set; }

    public uint[] Modules { get; set; } = [];
}
