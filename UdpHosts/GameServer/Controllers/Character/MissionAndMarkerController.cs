using System.Linq;
using GameServer.Enums.GSS.Character;
using GameServer.Extensions;
using GameServer.Packets;
using Serilog;
using ShoppingListUpdate = AeroMessages.GSS.V66.Character.Command.UpdateShoppingList;

namespace GameServer.Controllers.Character;

[ControllerID(Enums.GSS.Controllers.Character_MissionAndMarkerController)]
public class MissionAndMarkerController : Base
{
    private ILogger _logger;

    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
        _logger = logger.ForContext<MissionAndMarkerController>();
    }

    [MessageID((byte)Commands.RequestAllAchievements)]
    public void RequestAllAchievements(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        // TODO: Implement
    }

    [MessageID((byte)Commands.TryResumeTutorialChain)]
    public void TryResumeTutorialChain(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        // TODO: Implement
    }

    /// <summary>
    ///     The tracked-recipe list, sent whenever a recipe goes into or out of the crafting cart. The only
    ///     crafting-shaped thing the 1962 client still sends, and it took <c>Client/Addons/FabCart</c> to
    ///     provoke one. It's a wishlist, not a build order: the whole list every time, never followed by a
    ///     <c>Fabrication_Start</c>. Logged rather than stored because nothing consumes it yet, and because
    ///     the second list has never been seen occupied.
    /// </summary>
    [MessageID((byte)Commands.UpdateShoppingList)]
    public void UpdateShoppingList(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var update = packet.Unpack<ShoppingListUpdate>();

        _logger.Information(
            "Shopping list from 0x{EntityId:X8}: [{List1}] [{List2}]",
            entityId,
            string.Join(", ", (update.List1 ?? []).Select(entry => $"{entry.Unk2}x{entry.Unk1}")),
            string.Join(", ", (update.List2 ?? []).Select(entry => $"{entry.Unk2}x{entry.Unk1}")));
    }
}
