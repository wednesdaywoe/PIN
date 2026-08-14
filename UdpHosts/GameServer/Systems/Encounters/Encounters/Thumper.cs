using System;
using System.Collections.Generic;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities;
using GameServer.Entities.Thumper;
using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.Systems.Loot;
using GameServer.Systems.Resources;

namespace GameServer.Systems.Encounters.Encounters;

public class Thumper : BaseEncounter, IInteractionHandler
{
    private static readonly uint _updateFrequency = ThumperState.THUMPING.CountdownTime() / 100;
    private readonly ThumperEntity _thumper;
    private ulong _lastUpdate;

    public Thumper(IShard shard, ulong entityId, HashSet<INetworkPlayer> participants, ThumperEntity thumperEntity)
        : base(shard, entityId, participants)
    {
        _thumper = thumperEntity;

        Shard.EncounterMan.StartUpdatingEncounter(this);
    }

    public void OnInteraction(BaseEntity actingEntity, BaseEntity target)
    {
        switch ((ThumperState)_thumper.StateInfo.State)
        {
            case ThumperState.THUMPING:
                Shard.Abilities.HandleActivateAbility(Shard, _thumper, _thumper.CompletedAbility);

                _thumper.TransitionToState(ThumperState.LEAVING);
                break;
            case ThumperState.COMPLETED:
                _thumper.StateInfo = _thumper.StateInfo with { CountdownTime = Shard.CurrentTime };
                break;
        }
    }

    public override void OnUpdate(ulong currentTime)
    {
        if (Shard.CurrentTime >= _thumper.StateInfo.CountdownTime)
        {
            switch ((ThumperState)_thumper.StateInfo.State)
            {
                case ThumperState.LANDING:
                    Shard.Abilities.HandleActivateAbility(Shard, _thumper, _thumper.LandedAbility);
                    break;
                case ThumperState.WARMINGUP:
                    Shard.Abilities.HandleActivateAbility(Shard, _thumper, 34579);
                    break;
                case ThumperState.THUMPING:
                    _thumper.SetProgress(1);
                    Shard.Abilities.HandleActivateAbility(Shard, _thumper, 34215);
                    break;
                case ThumperState.CLOSING:
                    break;
                case ThumperState.COMPLETED:
                    Shard.Abilities.HandleActivateAbility(Shard, _thumper, 34216);
                    break;
                case ThumperState.LEAVING:
                    OnSuccess();
                    break;
            }

            if (_thumper.StateInfo.State < (byte)ThumperState.LEAVING)
            {
                _thumper.TransitionToState((ThumperState)(_thumper.StateInfo.State + 1));
            }
        }
        else if (_thumper.StateInfo.State == (byte)ThumperState.THUMPING && currentTime > _lastUpdate + _updateFrequency)
        {
            _thumper.SetProgress((float)(Shard.CurrentTime - _thumper.StateInfo.Time)
                                / (_thumper.StateInfo.CountdownTime - _thumper.StateInfo.Time));

            _lastUpdate = currentTime;
        }
    }

    /// <summary>
    ///     Pays out what the ground under the thumper holds. Until M4 this was a flat 200-crystite
    ///     grant regardless of where the thumper stood; now the node type keys the shipped yield
    ///     gradient, sampled at the thumper's distance from the deposit's center.
    /// </summary>
    /// <remarks>
    ///     Quantities are scaled by the progress bar, which answers G4: collecting a thumper early
    ///     pays the fraction it managed, not the full vein. Non-resource rolls are logged and dropped,
    ///     the same cut M3 and M5 made — delivering an item still means answering the drop protocol.
    /// </remarks>
    public override void OnSuccess()
    {
        Shard.EncounterMan.StopUpdatingEncounter(this);

        Shard.EntityMan.Remove(_thumper);

        var deposit = Shard.Resources.FindDepositAt(_thumper.Position);
        var completion = Math.Clamp(_thumper.Progress, 0f, 1f);

        // The gradient only means something inside the deposit the node type came from. A thumper
        // on barren ground (or whose deposit vanished mid-cycle) mines its node type's rim values.
        var distanceFraction = deposit != null && deposit.NodeTypeId == _thumper.NodeType
            ? DepositSampler.DistanceFraction(deposit, _thumper.Position)
            : 1f;

        var rolled = DepositSampler.Sample(
            SDBInterface.GetResourceNodeTypeResources(_thumper.NodeType),
            distanceFraction,
            Random.Shared);

        var extracted = new List<DepositYield>();
        uint total = 0;
        foreach (var yield in rolled)
        {
            var quantity = (uint)Math.Round(yield.Quantity * completion);
            if (quantity == 0)
            {
                continue;
            }

            extracted.Add(yield with { Quantity = quantity });
            total += quantity;
        }

        Shard.Logger.ForContext<Thumper>().Information(
            "Thumper {EncounterId} mined node type {NodeType} at distance fraction {DistanceFraction:0.00} of {Deposit}, completion {Completion:0.00}: {Kinds} resource kind(s), {Total} total",
            EntityId,
            _thumper.NodeType,
            distanceFraction,
            deposit == null ? "no deposit" : $"deposit [{deposit.Id}] {deposit.Name}",
            completion,
            extracted.Count,
            total);

        foreach (var yield in extracted)
        {
            if (ItemRules.IsResource(yield.ItemId))
            {
                RewardWithResource(yield.ItemId, yield.Quantity);
            }
            else
            {
                Shard.Logger.ForContext<Thumper>().Information(
                    "Thumper {EncounterId} extracted item {ItemId} x{Quantity}, not paid — item drops are unbuilt",
                    EntityId,
                    yield.ItemId,
                    yield.Quantity);
            }
        }

        SendCompletedEvent(deposit?.Id, completion, total, extracted);

        base.OnSuccess();
    }

    /// <summary>
    ///     Closes the loop the scan opened: the same <c>ScanId</c> the ground report carried, if the
    ///     owner's latest report was taken in this deposit, plus what actually came out.
    /// </summary>
    private void SendCompletedEvent(uint? depositId, float completion, uint total, List<DepositYield> extracted)
    {
        var lastReport = Shard.Resources.GetLastReport(_thumper.Owner.EntityId);
        var scanId = depositId != null && lastReport != null && lastReport.DepositId == depositId
            ? lastReport.ScanId
            : 0;

        var completed = new ResourceNodeCompletedEvent
        {
            ResourceNodeId = _thumper.AeroEntityId,
            ThumpingCharacterInfo = _thumper.ThumpingCharacterInfo,
            Completion = completion,
            Destroyed = 0,
            ScanId = scanId,
            Unk9 = 0,
            Quantity = total,
            Composition = DepositSampler.ToComposition(extracted),
        };

        foreach (var p in Participants)
        {
            p.NetChannels[ChannelType.ReliableGss].SendMessage(completed, p.CharacterEntity.EntityId);
        }
    }
}