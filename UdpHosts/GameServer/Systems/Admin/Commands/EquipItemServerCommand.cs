using System;
using System.Linq;
using GameServer.Data;
using GameServer.StaticDB;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Slots an item the character already owns into the loadout it is wearing, which is the only way
///     to hold something PIN made — the client equips from its own inventory list, so an item it
///     never displayed can never be picked up and put on.
/// </summary>
/// <remarks>
///     Weapons and gear land in the Gear sub-inventory among the couple of hundred pieces every
///     hardcoded loadout brings, so "it isn't in my inventory" and "I cannot find it" look identical
///     from the client. Equipping settles it: the item either appears in the character's hands or the
///     command says why not.
/// </remarks>
[ServerCommand("Equip an owned item into a loadout slot", "equipitem <typeId> [slot]", "equipitem", "equip_item", "equip")]
public class EquipItemServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var character = context.SourcePlayer?.CharacterEntity;
        if (character?.CurrentLoadout == null || context.SourcePlayer.Inventory == null)
        {
            SourceFeedback("Need a player character with a loadout", context);
            return;
        }

        if (parameters.Length == 0)
        {
            SourceFeedback($"equipitem <typeId> [slot] — slots: {string.Join(", ", Enum.GetNames<LoadoutSlotType>())}", context);
            return;
        }

        var typeId = ParseUIntParameter(parameters[0]);
        var item = context.SourcePlayer.Inventory.GetItems().FirstOrDefault(i => i.SdbId == typeId);
        if (item.SdbId != typeId)
        {
            SourceFeedback($"You are not carrying item {typeId} — createitem it first", context);
            return;
        }

        var slot = LoadoutSlotType.Secondary;
        if (parameters.Length > 1 && !Enum.TryParse(parameters[1], ignoreCase: true, out slot))
        {
            SourceFeedback($"No slot named '{parameters[1]}' — slots: {string.Join(", ", Enum.GetNames<LoadoutSlotType>())}", context);
            return;
        }

        var loadoutId = character.CurrentLoadout.LoadoutID;
        character.EquipItemByGUID(loadoutId, slot, item.GUID);

        var itemInfo = SDBInterface.GetRootItem(typeId);
        var itemType = itemInfo != null ? ((Enums.ItemType)itemInfo.Type).ToString() : "unknown type";
        SourceFeedback($"Equipped {typeId} ({itemType}) into {slot} of loadout {loadoutId}", context);
        Logger.Information("equipitem {TypeId} guid {Guid:X} into {Slot} of loadout {LoadoutId}", typeId, item.GUID, slot, loadoutId);
    }
}
