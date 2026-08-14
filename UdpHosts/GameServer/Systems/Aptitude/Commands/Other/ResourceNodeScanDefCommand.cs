using System.Linq;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Resources;

namespace GameServer.Systems.Aptitude.Commands.Other;

/// <summary>
///     The scan half of thumping: sends the initiating player every deposit within the def's range,
///     which the client draws as its resource overlay.
/// </summary>
/// <remarks>
///     <c>Unk4</c> on each area is a guess. The field is unidentified — radius and richness are the
///     candidates — and PIN sends the deposit's radius in metres, on the reasoning that an overlay
///     needs a size before it needs a shade. If overlay blobs render at a wrong but uniform size,
///     this guess is the first thing to revisit.
/// </remarks>
public class ResourceNodeScanDefCommand : Command, ICommand
{
    private ResourceNodeScanDefCommandDef Params;

    public ResourceNodeScanDefCommand(ResourceNodeScanDefCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Initiator is not CharacterEntity { IsPlayerControlled: true } character)
        {
            return true;
        }

        var origin = character.Position;
        var areas = CustomDBInterface.GetZoneResourceDeposits(context.Shard.ZoneId).Values
            .Where(deposit => DepositSampler.DistanceXY(deposit.Position, origin) <= Params.Range + deposit.Radius)
            .Select(deposit => new ResourceArea
            {
                Center = deposit.Position,
                Unk4 = (uint)deposit.Radius,
                NodeTypeId = deposit.NodeTypeId,
            })
            .ToArray();

        Logger.Information(
            "Resource scan {CommandId} by {Character}: {AreaCount} deposit(s) within {Range}m",
            Params.Id,
            character.EntityId,
            areas.Length,
            Params.Range);

        var message = new FoundResourceAreas { Data = areas };
        character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);

        return true;
    }
}
