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
    ///     The ring is drawn flat at the thumper's own Z and then each point is dropped onto the ground
    ///     under it, which is possible since terrain loaded on 2026-08-16. Before that the flat ring was
    ///     the whole story and a thumper on a slope stood its waves in the hillside — harmless while
    ///     bullets passed through the ground too, and a defect the moment they stopped.
    ///     <para>
    ///     "Watchable" turned out to be generous. An Aranha runs at 11m/s — shipped movement data, not
    ///     ours — and stops 4m from the machine, so it crosses this ring in <b>1.5 seconds</b>. There is
    ///     no interception window; a wave is on the thumper before the player has turned around. The
    ///     radius stays at 20m anyway because widening it past 25m costs the escorts their perception of
    ///     the defender, which is the behaviour the ring was sized for in the first place. The wave table
    ///     below absorbs it instead, by never standing up more attackers than one player can work through.
    ///     Untangling the two wants a spawn distance and a perception range that are set independently.
    ///     </para>
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
    ///     <para>
    ///     <b>This table is the starting-zone stock thumper and nothing else.</b> A solo player has to be
    ///     able to finish it. Retuned 2026-08-16 from a run that lost the machine at 93% having killed ten
    ///     attackers — the previous table (2 / 2+1 / 3+1 / 3+2, ten sappers) put more damage on the thumper
    ///     than it has health, so completing it solo was not possible rather than merely hard. See
    ///     <c>Docs/Design/Combat-Scale.md</c> for the arithmetic and for the harder tables this one is
    ///     eventually the easiest of.
    ///     </para>
    ///     <para>
    ///     What the numbers are, all measured in game on 2026-08-16 rather than assumed. A sapper does 49 a
    ///     swing every 1280ms, so <b>38 damage a second</b> while it stands at the machine. The player's
    ///     effective output against these is <b>~234 a second</b> including aim and reposition time, so one
    ///     1224-health Aranha takes <b>~5 seconds of fire</b>. The machine has 4000 health and that figure
    ///     is shipped and invariant — 61 of 61 calldowns carry it, so it is the fixed point everything else
    ///     is tuned against.
    ///     </para>
    ///     <para>
    ///     The driver is not how many attackers arrive, it is <b>how many stand there at once</b>, because a
    ///     player can only shoot one at a time and the rest keep swinging while they wait. Two sappers cost
    ///     (5 + 10) seconds of chewing between them, three cost (5 + 10 + 15). That is why the old table's
    ///     threes were fatal and why this one never exceeds two.
    ///     </para>
    ///     <para>
    ///     Against a player who engages promptly this table lands roughly 2000 of the machine's 4000, so it
    ///     finishes at about half health and the haul is visibly dented — the defence is worth something
    ///     without being required. A player who is slow to react doubles that and loses it, which is the
    ///     tension the event is for.
    ///     </para>
    /// </summary>
    private static readonly Wave[] _waves =
    {
        new(0.20f, Sappers: 1, Escorts: 0),
        new(0.45f, Sappers: 2, Escorts: 0),
        new(0.70f, Sappers: 2, Escorts: 1),
        new(0.90f, Sappers: 2, Escorts: 1),
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
            var stand = _thumper.Position + offset;

            // The ring is drawn flat at the machine's height, so on a slope an arc of it is inside the
            // hillside. That was cosmetic until terrain loaded and the ground started stopping bullets: a
            // buried sapper cannot be shot and goes on hitting the machine anyway, which is how a tester
            // met it on 2026-08-17. Where there is ground under the ring point, stand on it.
            if (Shard.Physics.TryGetGroundHeight(stand, out var groundZ))
            {
                stand.Z = groundZ;
            }

            var npc = Shard.EntityMan.SpawnCharacter(WaveMonsterTypeId, stand);
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