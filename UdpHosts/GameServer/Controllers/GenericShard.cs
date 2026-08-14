using System;
using System.Linq;
using AeroMessages.Control;
using AeroMessages.GSS.V66.Generic;
using GameServer.Entities;
using GameServer.Enums.GSS.Generic;
using GameServer.Extensions;
using GameServer.GRPC;
using GameServer.Packets;
using GameServer.Systems.Aptitude;
using Serilog;

namespace GameServer.Controllers;

[ControllerID(Enums.GSS.Controllers.GenericShard)]
public class GenericShard : Base
{
    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
    }

    [MessageID((byte)Commands.ScheduleUpdateRequest)]
    public void ScheduleUpdateRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var updateRequest = packet.Unpack<ScheduleUpdateRequest>();

        player.LastRequestedUpdate = client.AssignedShard.CurrentTime;
        player.RequestedClientTime = Math.Max(updateRequest.Time, player.RequestedClientTime);

        if (!player.FirstUpdateRequested)
        {
            player.FirstUpdateRequested = true;
            player.Respawn();
        }
    }

    [MessageID((byte)Commands.UIToEncounterMessage)]
    public void UiToEncounter(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
    }

    [MessageID((byte)Commands.ServerProfiler_RequestNames)]
    public void ServerProfiler_RequestNames(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
    }

    [MessageID((byte)Commands.LocalProximityAbilitySuccess)]
    public void LocalProximityAbilitySuccess(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var shard = client.AssignedShard;
        var abilities = client.AssignedShard.Abilities;

        var message = packet.Unpack<LocalProximityAbilitySuccess>();
        shard.Entities.TryGetValue(message.Source.Backing & 0xffffffffffffff00, out IEntity sourceEntity);
        var source = (IAptitudeTarget)sourceEntity;
        var targets = message.Targets
        .Where(entityId =>
        {
            try
            {
                return shard.Entities[entityId.Backing & 0xffffffffffffff00] != null;
            }
            catch
            {
                return false;
            }
        })
        .Select(entityId => (IAptitudeTarget)shard.Entities[entityId.Backing & 0xffffffffffffff00])
        .ToArray();

        abilities.HandleLocalProximityAbilitySuccess(shard, source, message.ClientProximityCommandId, message.Time, new AptitudeTargets(targets));
    }

    [MessageID((byte)Commands.RemoteProximityAbilitySuccess)]
    public void RemoteProximityAbilitySuccess(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
    }

    [MessageID((byte)Commands.TrailRequest)]
    public void TrailRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
    }

    [MessageID((byte)Commands.RequestLeaveZone)]
    public void RequestLeaveZone(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
    }

    [MessageID((byte)Commands.RequestLogout)]
    public void RequestLogout(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var resp = new CloseConnection { Unk = [0, 0, 0, 0] };
        client.NetChannels[ChannelType.Control].SendMessage(resp);

        // The tidy exit. It saves for completeness rather than because anything depends on it: most
        // sessions never send this message, so Shard.MigrateOut is what actually catches them.
        client.AssignedShard.CharacterSaves.SaveIfChanged(player, "logout");

        var zone = player.CurrentZone;

        if (!zone.IsOpenWorld)
        {
            return;
        }

        // Still this session's duration rather than the character's total. RIN has never answered a
        // request here, so which of the two it expects has never been observed, and guessing at it would
        // change a contract nobody can check.
        _ = GRPCService.SaveCharacterSessionDataAsync(
              player.CharacterId + 0xFE,
              zone.ID,
              player.ClosestOutpostToPosition(),
              (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - player.ConnectedAt);
    }

    [MessageID((byte)Commands.RequestEncounterInfo)]
    public void RequestEncounterInfo(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
    }

    [MessageID((byte)Commands.RequestActiveEncounters)]
    public void RequestActiveEncounters(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
    }

    [MessageID((byte)Commands.VotekickRequest)]
    public void VotekickRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
    }

    [MessageID((byte)Commands.VotekickResponse)]
    public void VotekickResponse(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
    }

    [MessageID((byte)Commands.GlobalCounterRequest)]
    public void GlobalCounterRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
    }

    [MessageID((byte)Commands.CurrentLoadoutRequest)]
    public void CurrentLoadoutRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
    }

    [MessageID((byte)Commands.VendorProductRequest)]
    public void VendorProductRequest(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
    }
}