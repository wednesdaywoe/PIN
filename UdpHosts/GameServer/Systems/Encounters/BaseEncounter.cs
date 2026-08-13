using System;
using System.Collections.Generic;
using System.Linq;
using Aero.Gen;
using AeroMessages.Common;
using AeroMessages.GSS.V66.Generic;

namespace GameServer.Systems.Encounters;

public abstract class BaseEncounter : IEncounter
{
    protected static readonly Random Rng = new();

    protected BaseEncounter(IShard shard, ulong entityId, HashSet<INetworkPlayer> participants)
    {
        Shard = shard;
        EntityId = entityId;
        AeroEntityId = new EntityId() { Backing = EntityId, ControllerId = Controller.Encounter };

        // Every caller builds this set out of some entity's Player, and CharacterEntity.Player is null
        // for an NPC, so an encounter an NPC starts arrives holding a null. It costs nothing until
        // something finally reaches through it, which for the Coral Forest thumper was the completion
        // payout seven minutes later, and that unhandled NRE took the whole shard tick down. Stripped
        // here rather than at each use because there are eight places that walk this set and only one
        // that fills it.
        Participants = participants == null ? [] : [.. participants.Where(p => p != null)];
    }

    protected BaseEncounter(IShard shard, ulong entityId, INetworkPlayer soloParticipant)
        : this(shard, entityId, [soloParticipant])
    {
    }

    public IShard Shard { get; protected set; }
    public ulong EntityId { get; }
    public EntityId AeroEntityId { get; protected set; }

    public HashSet<INetworkPlayer> Participants { get; }
    public INetworkPlayer SoloParticipant => Participants.Single();
    public virtual IAeroEncounter View => null;

    public virtual void OnUpdate(ulong currentTime)
    {
    }

    public virtual void OnSignal()
    {
    }

    public virtual void OnSuccess()
    {
        // todo rewards
        Shard.EncounterMan.Remove(this);
    }

    public virtual void OnFailure()
    {
        Shard.EncounterMan.Remove(this);
    }

    protected void PlayDialog(uint id)
    {
        var msg = new PlayDialogScriptMessage() { DialogId = id, Unk1 = [0] };

        foreach (var p in Participants)
        {
            p.NetChannels[ChannelType.ReliableGss].SendMessage(msg, p.CharacterEntity.EntityId);
        }
    }

    /// <summary>
    ///     Pays every participant, and says so in the log.
    /// </summary>
    /// <remarks>
    ///     An encounter with no participants pays nobody and is not an error — the Coral Forest debug
    ///     thumper is owned by an NPC and reaches here with an empty set. The count is logged for
    ///     exactly that reason: "nothing arrived in my inventory" and "nobody was there to pay" look
    ///     identical from the screen.
    /// </remarks>
    protected void RewardWithResource(uint resourceId, uint quantity)
    {
        Shard.Logger.ForContext(GetType()).Information(
            "Encounter {EncounterId} paying {Quantity} of resource {ResourceId} to {ParticipantCount} participant(s)",
            EntityId,
            quantity,
            resourceId,
            Participants.Count);

        foreach (var p in Participants)
        {
            p.Inventory.AddResource(resourceId, quantity);
        }
    }
}