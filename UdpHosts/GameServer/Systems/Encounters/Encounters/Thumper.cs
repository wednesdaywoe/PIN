using System;
using System.Collections.Generic;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Entities.Thumper;
using GameServer.Enums;
using GameServer.StaticDB;
using GameServer.Systems.Loot;
using GameServer.Systems.Resources;

namespace GameServer.Systems.Encounters.Encounters;

public class Thumper : BaseEncounter, IInteractionHandler, IDeathHandler, IDestructionHandler
{
    private const uint WaveMonsterTypeId = 528;

    /// <summary>
    ///     Where a wave stands up, measured out from the machine. Inside perception range (25m), so an
    ///     escort notices the defender immediately, and far enough out that the approach is watchable.
    ///     The ring is placed at the thumper's own Z because that's the only ground height the server
    ///     can vouch for; a thumper on a steep slope will stand its waves in the hillside, which is the
    ///     same bet every offset spawn makes on this server.
    /// </summary>
    private const float WaveSpawnRadius = 20f;

    /// <summary>How long a destroyed thumper stays visible so the client gets to see the wreck.</summary>
    private const uint DestroyedLingerMs = 6_000;

    /// <summary>
    ///     The escalation, keyed to drilling progress rather than to the clock so it survives any change
    ///     to the cycle length. Everything about it is invented in the DATA-10 sense, since retail's
    ///     wave tables lived server-side and never shipped, but the shape follows what the chain walk
    ///     established: the encounter's own state decides when, and each spawn is a one-shot verb.
    ///
    ///     Every member is a Melded Aranha (528). Single-species on purpose: Aranha and Chosen are
    ///     hostile to each other, and a mixed wave fights itself at the foot of the thumper, which is
    ///     the zone-448 lesson the spawn audit exists to catch. 528 is also one of only two creatures
    ///     ever seen rendering (CLIENT-3), so the waves introduce no unchecked model.
    /// </summary>
    private static readonly Wave[] _waves =
    {
        new(0.15f, Sappers: 2, Escorts: 0),
        new(0.40f, Sappers: 2, Escorts: 1),
        new(0.65f, Sappers: 3, Escorts: 1),
        new(0.90f, Sappers: 3, Escorts: 2),
    };

    private static readonly uint _updateFrequency = ThumperState.THUMPING.CountdownTime() / 100;
    private readonly ThumperEntity _thumper;
    private readonly HashSet<ulong> _members = [];
    private ulong _lastUpdate;
    private int _nextWave;
    private int _kills;
    private bool _ended;

    public Thumper(IShard shard, ulong entityId, HashSet<INetworkPlayer> participants, ThumperEntity thumperEntity)
        : base(shard, entityId, participants)
    {
        _thumper = thumperEntity;

        Shard.EncounterMan.StartUpdatingEncounter(this);
    }

    /// <summary>
    ///     What fraction of the sampled yield the defence kept, off the machine's remaining health.
    ///     Linear from half pay at one hit point to full pay untouched, so damage always costs
    ///     something but a battered survival is still clearly worth more than a loss. The 0.5 floor is
    ///     invented, like the wave schedule above it.
    /// </summary>
    public static float DefenceMultiplier(int currentHealth, int maxHealth)
    {
        if (maxHealth <= 0)
        {
            return 1f;
        }

        var fraction = Math.Clamp((float)currentHealth / maxHealth, 0f, 1f);
        return 0.5f + (0.5f * fraction);
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

            while (_nextWave < _waves.Length && _thumper.Progress >= _waves[_nextWave].AtProgress)
            {
                SpawnWave(_waves[_nextWave]);
                _nextWave++;
            }
        }
    }

    public void OnMemberDied(CharacterEntity victim, CharacterEntity killer)
    {
        if (!_members.Remove(victim.EntityId))
        {
            return;
        }

        _kills++;

        Shard.Logger.ForContext<Thumper>().Information(
            "Thumper {EncounterId} wave member {Victim} died to {Killer}, {Standing} standing, {Kills} down",
            EntityId,
            victim.EntityId,
            killer?.EntityId,
            _members.Count,
            _kills);
    }

    public void OnDestroyed(BaseEntity entity, CharacterEntity attacker)
    {
        Shard.Logger.ForContext<Thumper>().Information(
            "Thumper {EncounterId} was destroyed by {Attacker} at {Progress:0%} with {Kills} attacker(s) down",
            EntityId,
            attacker?.EntityId,
            _thumper.Progress,
            _kills);

        OnFailure();
    }

    /// <summary>
    ///     The losing exit: the machine is wreckage and the vein pays nothing. The entity has already
    ///     shown DESTROYED and run its death ability; this is the encounter's half. Tell the client the
    ///     run is over, clear the wave, and leave the wreck standing just long enough to be seen.
    /// </summary>
    public override void OnFailure()
    {
        if (_ended)
        {
            return;
        }

        _ended = true;

        Shard.EncounterMan.StopUpdatingEncounter(this);
        DespawnMembers();

        SendCompletedEvent(depositId: null, Math.Clamp(_thumper.Progress, 0f, 1f), total: 0, extracted: [], destroyed: 1);

        Shard.EntityMan.SetRemainingLifetime(_thumper, DestroyedLingerMs);

        base.OnFailure();
    }

    /// <summary>
    ///     Pays out what the ground under the thumper holds. Until M4 this was a flat 200-crystite
    ///     grant regardless of where the thumper stood; now the node type keys the shipped yield
    ///     gradient, sampled at the thumper's distance from the deposit's center.
    /// </summary>
    /// <remarks>
    ///     Quantities are scaled by the progress bar, which answers G4: collecting a thumper early
    ///     pays the fraction it managed, not the full vein. Since M7 they're also scaled by
    ///     <see cref="DefenceMultiplier"/>, so damage the waves got through costs part of the haul.
    ///     Non-resource rolls are logged and dropped, the same cut M3 and M5 made — delivering an item
    ///     still means answering the drop protocol.
    /// </remarks>
    public override void OnSuccess()
    {
        if (_ended)
        {
            return;
        }

        _ended = true;

        Shard.EncounterMan.StopUpdatingEncounter(this);
        DespawnMembers();

        Shard.EntityMan.Remove(_thumper);

        var deposit = Shard.Resources.FindDepositAt(_thumper.Position);
        var completion = Math.Clamp(_thumper.Progress, 0f, 1f);
        var defence = DefenceMultiplier(_thumper.CurrentHealth, (int)_thumper.MaxHealth);

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
            var quantity = (uint)Math.Round(yield.Quantity * completion * defence);
            if (quantity == 0)
            {
                continue;
            }

            extracted.Add(yield with { Quantity = quantity });
            total += quantity;
        }

        Shard.Logger.ForContext<Thumper>().Information(
            "Thumper {EncounterId} mined node type {NodeType} at distance fraction {DistanceFraction:0.00} of {Deposit}, completion {Completion:0.00}, defence {Defence:0.00} ({Health}/{MaxHealth} health, {Kills} attacker(s) down): {Kinds} resource kind(s), {Total} total",
            EntityId,
            _thumper.NodeType,
            distanceFraction,
            deposit == null ? "no deposit" : $"deposit [{deposit.Id}] {deposit.Name}",
            completion,
            defence,
            _thumper.CurrentHealth,
            _thumper.MaxHealth,
            _kills,
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
    ///     Stands one wave up on the ring. Sappers march on the machine and ignore return fire; escorts
    ///     fight whoever shows up and only chew the thumper when nobody does. Deaths come back through
    ///     <see cref="OnMemberDied"/> so the encounter always knows what's still standing.
    /// </summary>
    private void SpawnWave(Wave wave)
    {
        for (var i = 0; i < wave.Sappers + wave.Escorts; i++)
        {
            var angle = Rng.NextSingle() * 2f * MathF.PI;
            var offset = new System.Numerics.Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f) * WaveSpawnRadius;

            var npc = Shard.EntityMan.SpawnCharacter(WaveMonsterTypeId, _thumper.Position + offset);
            npc.ObjectiveId = _thumper.EntityId;
            npc.ObjectiveFirst = i < wave.Sappers;
            npc.Encounter = new EncounterComponent
            {
                EncounterId = EntityId,
                Instance = this,
                Events = EncounterComponent.Event.Death,
            };

            _members.Add(npc.EntityId);
        }

        Shard.Logger.ForContext<Thumper>().Information(
            "Thumper {EncounterId} wave {Wave} of {Waves} at {Progress:0%}: {Sappers} sapper(s) and {Escorts} escort(s) on a {Radius}m ring, {Standing} member(s) standing",
            EntityId,
            _nextWave + 1,
            _waves.Length,
            _thumper.Progress,
            wave.Sappers,
            wave.Escorts,
            WaveSpawnRadius,
            _members.Count);
    }

    /// <summary>
    ///     Waves end when the encounter ends, on either exit. Nothing owns these monsters afterwards,
    ///     no respawn slot and no encounter, so survivors would stand at the crater forever; despawning
    ///     them reads as the swarm dispersing once the thing it came for is gone.
    /// </summary>
    private void DespawnMembers()
    {
        var removed = 0;

        foreach (var memberId in _members)
        {
            if (Shard.Entities.ContainsKey(memberId))
            {
                Shard.EntityMan.Remove(memberId);
                removed++;
            }
        }

        _members.Clear();

        if (removed > 0)
        {
            Shard.Logger.ForContext<Thumper>().Information(
                "Thumper {EncounterId} released {Count} surviving wave member(s)",
                EntityId,
                removed);
        }
    }

    /// <summary>
    ///     Closes the loop the scan opened: the same <c>ScanId</c> the ground report carried, if the
    ///     owner's latest report was taken in this deposit, plus what actually came out. A destroyed
    ///     thumper sends the same event with the flag up and nothing aboard.
    /// </summary>
    private void SendCompletedEvent(uint? depositId, float completion, uint total, List<DepositYield> extracted, byte destroyed = 0)
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
            Destroyed = destroyed,
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

    private record Wave(float AtProgress, int Sappers, int Escorts);
}