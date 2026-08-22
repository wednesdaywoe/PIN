using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB;
using Serilog;

namespace GameServer.Systems.Loot;

/// <summary>
///     What a powerup does when you walk over it.
/// </summary>
/// <remarks>
///     <para>
///     Retail's answer is gone. A powerup's effect lived in an aptitude chain whose parameters were
///     server-side, so the 1962 client db carries the item, its name and its loot rows and nothing about
///     what it grants. The two figures below are PIN's, chosen to be worth picking up without ending a
///     fight on their own.
///     </para>
///     <para>
///     Ids rather than a flag, because there is no flag. 33815 and 33816 are the two the kill tables
///     actually drop — "Health Powerup" and "Ammo Powerup", named in the client's own text table — and
///     30286/30294 are their duplicates from an earlier build, kept here because the tables do reach
///     them. Anything else of type <c>Powerup</c> is a title unlock or an XP boost wearing the same type
///     id, and goes to the bag like any other item.
///     </para>
/// </remarks>
public static class Powerups
{
    /// <summary>Fraction of a full health bar a health powerup returns.</summary>
    public const float HealFraction = 0.25f;

    private static readonly uint[] HealthPowerups = [33815, 30286];
    private static readonly uint[] AmmoPowerups = [33816, 30294];

    /// <summary>
    ///     Applies <paramref name="itemId" /> if it is one of the two the ground drops. Returns false for
    ///     anything else, which is the caller's signal to treat it as an ordinary item.
    /// </summary>
    public static bool Apply(CharacterEntity character, uint itemId, ILogger logger)
    {
        if (System.Array.IndexOf(HealthPowerups, itemId) >= 0)
        {
            var restored = character.Heal((int)(character.MaxHealth.Value * HealFraction));
            logger.Information(
                "{Character} took health powerup {ItemId}: {Restored} health restored, now {Current}/{Max}",
                character.EntityId,
                itemId,
                restored,
                character.CurrentHealth,
                character.MaxHealth.Value);
            return true;
        }

        if (System.Array.IndexOf(AmmoPowerups, itemId) >= 0)
        {
            character.RestockAmmo();
            logger.Information("{Character} took ammo powerup {ItemId}: magazines refilled", character.EntityId, itemId);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Whether an item is one of the world's own pickups. Used to decide who may take a drop —
    ///     a powerup belongs to nobody, which is how retail wrote it on the wire.
    /// </summary>
    public static bool IsPowerup(uint itemId)
    {
        if (System.Array.IndexOf(HealthPowerups, itemId) >= 0 || System.Array.IndexOf(AmmoPowerups, itemId) >= 0)
        {
            return true;
        }

        var root = SDBInterface.GetRootItem(itemId);
        return root != null && (ItemType)root.Type == ItemType.Powerup;
    }
}
