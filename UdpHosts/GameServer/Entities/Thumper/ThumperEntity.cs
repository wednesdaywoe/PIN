using System.Numerics;
using AeroMessages.Common;
using AeroMessages.GSS.V66;
using AeroMessages.GSS.V66.ResourceNode.View;
using GameServer.Entities.Character;
using GameServer.Enums;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Combat;
using GameServer.Systems.Encounters;

namespace GameServer.Entities.Thumper;

public sealed class ThumperEntity : BaseAptitudeEntity, IAptitudeTarget, IDamageable
{
    public ThumperEntity(
        IShard shard,
        ulong eid,
        uint nodeType,
        Vector3 position,
        CharacterEntity owner,
        ResourceNodeBeaconCalldownCommandDef commandDef)
        : base(shard, eid, owner)
    {
        AeroEntityId = new EntityId() { Backing = EntityId, ControllerId = Controller.ResourceNode };
        NodeType = nodeType;
        BeaconType = commandDef.ResourceNodeBeaconId;
        Scoping = new ScopingComponent() { Global = true };
        Position = position;
        LandedAbility = commandDef.LandedAbility;
        CompletedAbility = commandDef.CompletedAbility;
        DeathAbility = commandDef.DeathAbility;
        CalldownTimeMs = commandDef.CalldownTimeMs;
        MaxHealth = (uint)commandDef.Health;
        CurrentHealth = (int)MaxHealth;
        Interaction = new InteractionComponent()
          {
              Type = InteractionType.GenericHold,
              CompletedAbilityId = CompletedAbility,
          };
        InitFields();
        InitViews();
    }

    public ObserverView ResourceNode_ObserverView { get; set; }

    public INetworkPlayer Player { get; set; }
    public bool IsPlayerOwned => Player != null;

    public uint NodeType { get; set; }
    public uint BeaconType { get; set; }
    public float Progress { get; set; }
    public ThumpingCharacterInfoStruct ThumpingCharacterInfo { get; set; }
    public StateInfoStruct StateInfo { get; set; }
    public ScopeBubbleInfoData ScopeBubble { get; set; } = new ScopeBubbleInfoData()
    {
        Layer = 0,
        Unk2 = 1
    };
    public float Scale { get; set; }

    public uint LandedAbility { get; set; }
    public uint CompletedAbility { get; set; }
    public uint DeathAbility { get; set; }
    public uint CalldownTimeMs { get; set; }
    public uint MaxHealth { get; set; }
    public int CurrentHealth { get; private set; }
    public bool Destroyed { get; private set; }

    /// <summary>
    ///     Damageable from touchdown until liftoff. Once it's LEAVING the machine is airborne with the
    ///     cargo already won, so shots into it stop counting rather than voiding a finished run.
    /// </summary>
    public bool IsAlive => !Destroyed && CurrentHealth > 0 && StateInfo.State < (byte)ThumperState.LEAVING;

    public ushort StatusEffectsChangeTime_0 { get; set; }
    public ushort StatusEffectsChangeTime_1 { get; set; }
    public ushort StatusEffectsChangeTime_2 { get; set; }
    public ushort StatusEffectsChangeTime_3 { get; set; }
    public ushort StatusEffectsChangeTime_4 { get; set; }
    public ushort StatusEffectsChangeTime_5 { get; set; }
    public ushort StatusEffectsChangeTime_6 { get; set; }
    public ushort StatusEffectsChangeTime_7 { get; set; }
    public ushort StatusEffectsChangeTime_8 { get; set; }
    public ushort StatusEffectsChangeTime_9 { get; set; }
    public ushort StatusEffectsChangeTime_10 { get; set; }
    public ushort StatusEffectsChangeTime_11 { get; set; }
    public ushort StatusEffectsChangeTime_12 { get; set; }
    public ushort StatusEffectsChangeTime_13 { get; set; }
    public ushort StatusEffectsChangeTime_14 { get; set; }
    public ushort StatusEffectsChangeTime_15 { get; set; }
    public ushort StatusEffectsChangeTime_16 { get; set; }
    public ushort StatusEffectsChangeTime_17 { get; set; }
    public ushort StatusEffectsChangeTime_18 { get; set; }
    public ushort StatusEffectsChangeTime_19 { get; set; }
    public ushort StatusEffectsChangeTime_20 { get; set; }
    public ushort StatusEffectsChangeTime_21 { get; set; }
    public ushort StatusEffectsChangeTime_22 { get; set; }
    public ushort StatusEffectsChangeTime_23 { get; set; }
    public ushort StatusEffectsChangeTime_24 { get; set; }
    public ushort StatusEffectsChangeTime_25 { get; set; }
    public ushort StatusEffectsChangeTime_26 { get; set; }
    public ushort StatusEffectsChangeTime_27 { get; set; }
    public ushort StatusEffectsChangeTime_28 { get; set; }
    public ushort StatusEffectsChangeTime_29 { get; set; }
    public ushort StatusEffectsChangeTime_30 { get; set; }
    public ushort StatusEffectsChangeTime_31 { get; set; }
    public StatusEffectData? StatusEffects_0 { get; set; }
    public StatusEffectData? StatusEffects_1 { get; set; }
    public StatusEffectData? StatusEffects_2 { get; set; }
    public StatusEffectData? StatusEffects_3 { get; set; }
    public StatusEffectData? StatusEffects_4 { get; set; }
    public StatusEffectData? StatusEffects_5 { get; set; }
    public StatusEffectData? StatusEffects_6 { get; set; }
    public StatusEffectData? StatusEffects_7 { get; set; }
    public StatusEffectData? StatusEffects_8 { get; set; }
    public StatusEffectData? StatusEffects_9 { get; set; }
    public StatusEffectData? StatusEffects_10 { get; set; }
    public StatusEffectData? StatusEffects_11 { get; set; }
    public StatusEffectData? StatusEffects_12 { get; set; }
    public StatusEffectData? StatusEffects_13 { get; set; }
    public StatusEffectData? StatusEffects_14 { get; set; }
    public StatusEffectData? StatusEffects_15 { get; set; }
    public StatusEffectData? StatusEffects_16 { get; set; }
    public StatusEffectData? StatusEffects_17 { get; set; }
    public StatusEffectData? StatusEffects_18 { get; set; }
    public StatusEffectData? StatusEffects_19 { get; set; }
    public StatusEffectData? StatusEffects_20 { get; set; }
    public StatusEffectData? StatusEffects_21 { get; set; }
    public StatusEffectData? StatusEffects_22 { get; set; }
    public StatusEffectData? StatusEffects_23 { get; set; }
    public StatusEffectData? StatusEffects_24 { get; set; }
    public StatusEffectData? StatusEffects_25 { get; set; }
    public StatusEffectData? StatusEffects_26 { get; set; }
    public StatusEffectData? StatusEffects_27 { get; set; }
    public StatusEffectData? StatusEffects_28 { get; set; }
    public StatusEffectData? StatusEffects_29 { get; set; }
    public StatusEffectData? StatusEffects_30 { get; set; }
    public StatusEffectData? StatusEffects_31 { get; set; }

    public override void SetStatusEffect(byte index, ushort time, StatusEffectData data)
    {
        Logger.Debug("Thumper.SetStatusEffect Index {index}, Time {time}, Id {id}", index, time, data.Id);

        // Member
        GetType().GetProperty($"StatusEffectsChangeTime_{index}").SetValue(this, time, null);
        GetType().GetProperty($"StatusEffects_{index}").SetValue(this, data, null);

        // ObserverView
        ResourceNode_ObserverView.GetType().GetProperty($"StatusEffectsChangeTime_{index}Prop").SetValue(ResourceNode_ObserverView, time, null);
        ResourceNode_ObserverView.GetType().GetProperty($"StatusEffects_{index}Prop").SetValue(ResourceNode_ObserverView, data, null);
    }

    public override void ClearStatusEffect(byte index, ushort time, uint debugEffectId)
    {
        Logger.Debug("Thumper.ClearStatusEffect Index {index}, Time {time}, Id {debugEffectId}", index, time, debugEffectId);

        // Member
        GetType().GetProperty($"StatusEffectsChangeTime_{index}").SetValue(this, time, null);
        GetType().GetProperty($"StatusEffects_{index}").SetValue(this, null, null);

        // ObserverView
        ResourceNode_ObserverView.GetType().GetProperty($"StatusEffectsChangeTime_{index}Prop").SetValue(ResourceNode_ObserverView, time, null);
        ResourceNode_ObserverView.GetType().GetProperty($"StatusEffects_{index}Prop").SetValue(ResourceNode_ObserverView, null, null);
    }

    public void SetProgress(float newProgress)
    {
        Progress = newProgress;
        ResourceNode_ObserverView.ProgressProp = Progress;
    }

    public void SetCurrentHealth(int newValue)
    {
        CurrentHealth = System.Math.Clamp(newValue, 0, (int)MaxHealth);
        ResourceNode_ObserverView.CurrentHealthPctProp = Vitals.HealthPercent(CurrentHealth, (int)MaxHealth);
    }

    /// <summary>
    ///     Same funnel as every other <see cref="IDamageable"/>: shots and damage commands land here.
    ///     No mitigation. The machine has no shields and no armour, just the pool retail gave it.
    /// </summary>
    public void TakeDamage(DamageInfo damage)
    {
        if (!IsAlive || MaxHealth == 0)
        {
            return;
        }

        var amount = damage.Points;
        if (amount <= 0)
        {
            return;
        }

        SetCurrentHealth(CurrentHealth - amount);
        CombatLog.RecordHit(this, damage, amount, 0, CurrentHealth, Shard.CurrentTimeLong);

        Logger.Debug(
            "Thumper {EntityId} took {Amount} damage from {Attacker}, {Health} of {MaxHealth} health left",
            EntityId,
            amount,
            damage.Attacker?.EntityId,
            CurrentHealth,
            MaxHealth);

        DamageEvents.SendDealtHit(damage, DamageEvents.Describe(this, damage, amount));

        if (CurrentHealth <= 0)
        {
            Destroy(damage.Attacker);
        }
    }

    public void SetHostilityInfo(HostilityInfoData newValue)
    {
        HostilityInfo = newValue;
        ResourceNode_ObserverView?.HostilityInfoProp = HostilityInfo;
    }

    /// <summary>
    ///     Moves the thumper to its next state and starts that state's countdown.
    /// </summary>
    /// <remarks>
    ///     Logged because a full cycle is about seven and a half minutes of a machine standing still,
    ///     and nothing on screen distinguishes "thumping" from "stalled". The line is the only way to
    ///     tell which of those you are watching.
    /// </remarks>
    public void TransitionToState(ThumperState newState)
    {
        StateInfo = new StateInfoStruct()
                    {
                        State = (byte)newState,
                        Time = Shard.CurrentTime,
                        CountdownTime = Shard.CurrentTime + newState.CountdownTime(),
                    };
        ResourceNode_ObserverView.StateInfoProp = StateInfo;

        Shard.Logger.ForContext<ThumperEntity>().Information(
            "Thumper {EntityId} entered {State}, for {CountdownMs}ms",
            EntityId,
            newState,
            newState.CountdownTime());
    }

    public override bool IsInteractable()
    {
        return StateInfo.State is (byte)ThumperState.THUMPING or (byte)ThumperState.COMPLETED;
    }

    public override bool CanBeInteractedBy(IEntity other)
    {
        return IsInteractable()
               && other is CharacterEntity character
               && Encounter.Instance.Participants.Contains(character.Player);
    }

    /// <summary>
    ///     The entity-side half of destruction: mark it dead, show the DESTROYED state, run the death
    ///     ability retail authored for the explosion. The encounter-side half (the failure event, the
    ///     wave cleanup, removing the entity) belongs to the encounter, which is told last.
    /// </summary>
    private void Destroy(CharacterEntity attacker)
    {
        Destroyed = true;
        TransitionToState(ThumperState.DESTROYED);

        if (DeathAbility != 0)
        {
            Shard.Abilities.HandleActivateAbility(Shard, this, DeathAbility);
        }

        (Encounter?.Instance as IDestructionHandler)?.OnDestroyed(this, attacker);
    }

    private void InitFields()
    {
        HostilityInfo = new HostilityInfoData { Flags = 0 | HostilityInfoData.HostilityFlags.Faction, FactionId = 1 };
        ThumpingCharacterInfo = new ThumpingCharacterInfoStruct
        {
            OwnerId1 = Owner.AeroEntityId,
            OwnerId2 = Owner.AeroEntityId,
            Owner = Owner.ToString(),
            Unk = 0f,
        };
        StateInfo = new StateInfoStruct
        {
            State = (byte)ThumperState.LANDING,
            Time = Shard.CurrentTime,
            CountdownTime = Shard.CurrentTime + CalldownTimeMs
        };
    }

    private void InitViews()
    {
        ResourceNode_ObserverView = new ObserverView
        {
            NodeTypeProp = NodeType,
            BeaconIdProp = BeaconType,
            PositionProp = Position,
            ThumpingCharacterInfoProp = ThumpingCharacterInfo,
            StateInfoProp = StateInfo,
            CurrentHealthPctProp = 100,
            MaxHealthProp = MaxHealth,
            ProgressProp = Progress,
            HostilityInfoProp = HostilityInfo,
            PersonalFactionStanceProp = null,
            ScopeBubbleInfoProp = ScopeBubble
        };
    }
}