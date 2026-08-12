using System;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Enums;
using GameServer.StaticDB;

namespace GameServer.Systems.Admin.Commands;

[ServerCommand("Add an item to your inventory", "createitem <typeId>", "createitem", "create_item", "giveitem", "give_item")]
public class CreateItemServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer?.Inventory == null)
        {
            SourceFeedback("Need a player inventory", context);
            return;
        }

        if (parameters.Length == 0)
        {
            SourceFeedback("No typeId was provided to create item command", context);
            return;
        }

        uint typeId = ParseUIntParameter(parameters[0]);
        var itemInfo = SDBInterface.GetRootItem(typeId);
        if (itemInfo == null)
        {
            SourceFeedback("No item data for this typeId", context);
            return;
        }

        uint quantity = 1;
        bool isResource = ((ItemFlags)itemInfo.Flags).HasFlag(ItemFlags.Resource);
        if (isResource && parameters.Length > 1)
        {
            quantity = Math.Max(1, ParseUIntParameter(parameters[1]));
        }

        // The live server announced the pickup first and only then sent the inventory it produced —
        // in the 2016 capture the SimulateLootPickup for item 82337 lands two sequence numbers ahead
        // of the InventoryUpdate that adds it. PIN had the two the other way around.
        var msg = new SimulateLootPickup()
        {
            Item = new()
            {
                SdbId = typeId,
                Quantity = (ushort)quantity,
            },
            RewardType = SimulateLootPickup.Type.General,
        };
        context.SourcePlayer.NetChannels[ChannelType.ReliableGss]
                   .SendMessage(msg, context.SourcePlayer.CharacterEntity.EntityId);

        if (isResource)
        {
            context.SourcePlayer.Inventory.AddResource(typeId, quantity);
        }
        else
        {
            context.SourcePlayer.Inventory.CreateItem(typeId);
        }

        // The toast fires off SimulateLootPickup alone, so seeing it says nothing about whether the
        // item landed. Log what was actually added; dbg_inventory prints the standing contents.
        Logger.Information(
            "createitem {TypeId} as {Kind}, item type {ItemType}, x{Quantity}",
            typeId,
            isResource ? "resource" : "item",
            (ItemType)itemInfo.Type,
            quantity);
    }
}