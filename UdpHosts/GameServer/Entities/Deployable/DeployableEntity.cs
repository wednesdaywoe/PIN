using System;
using System.Numerics;
using AeroMessages.Common;
using AeroMessages.GSS.V66;
using AeroMessages.GSS.V66.Deployable.View;
using GameServer.Entities.Character;
using GameServer.Entities.Turret;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Combat;

namespace GameServer.Entities.Deployable;

public sealed class DeployableEntity : BaseAptitudeEntity, IAptitudeTarget, IDamageable
{
    /// <summary>
    ///     How long a destroyed deployable hangs around before it's removed. Long enough for the client to
    ///     render the health bar hitting zero, short enough that a wreck isn't left standing.
    /// </summary>
    private const uint DestroyedLifetimeMs = 2_000;

    // TODO: Add Deployable Hardpoint support
    public DeployableEntity(IShard shard, ulong eid, uint type, uint abilitySrcId, CharacterEntity owner = null)
        : base(shard, eid, owner)
    {
        AeroEntityId = new EntityId() { Backing = EntityId, ControllerId = Controller.Deployable };
        Type = type;
        AbilitySrcId = abilitySrcId;
        InitFields();
        InitViews();
    }

    public ObserverView Deployable_ObserverView { get; set; }

    public INetworkPlayer Player { get; set; }
    public bool IsPlayerOwned => Player != null;
    public Vector3 AimPosition => Position;
    public Vector3 AimDirection { get; set; }
    public TurretEntity Turret { get; set; }

    public uint ConstructedTime { get; set; }
    public uint Type { get; set; }
    public uint AbilitySrcId { get; set; }
    public uint GibVisualsID { get; set; }
    public float Scale { get; set; }
    public int MaxHealth { get; private set; }
    public int CurrentHealth { get; private set; }

    public bool Destroyed { get; private set; }

    public bool IsAlive => !Destroyed;

    public uint PoweredOnAbility { get; set; }
    public uint PoweredOffAbility { get; set; }

    /// <summary>
    ///     dbcharacter::Deployable DeathAbilityid, run when this is destroyed. Whatever the game wanted a
    ///     wrecked deployable to do (gibs, an explosion, dropping loot) lives in that ability rather than here.
    /// </summary>
    public uint DeathAbility { get; set; }

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
        Logger.Debug("Deployable.SetStatusEffect Index {index}, Time {time}, Id {id}", index, time, data.Id);

        // Member
        GetType().GetProperty($"StatusEffectsChangeTime_{index}").SetValue(this, time, null);
        GetType().GetProperty($"StatusEffects_{index}").SetValue(this, data, null);

        // ObserverView
        Deployable_ObserverView.GetType().GetProperty($"StatusEffectsChangeTime_{index}Prop").SetValue(Deployable_ObserverView, time, null);
        Deployable_ObserverView.GetType().GetProperty($"StatusEffects_{index}Prop").SetValue(Deployable_ObserverView, data, null);
    }

    public override void ClearStatusEffect(byte index, ushort time, uint debugEffectId)
    {
        Logger.Debug("Deployable.ClearStatusEffect Index {index}, Time {time}, Id {debugEffectId}", index, time, debugEffectId);

        // Member
        GetType().GetProperty($"StatusEffectsChangeTime_{index}").SetValue(this, time, null);
        GetType().GetProperty($"StatusEffects_{index}").SetValue(this, null, null);

        // ObserverView
        Deployable_ObserverView.GetType().GetProperty($"StatusEffectsChangeTime_{index}Prop").SetValue(Deployable_ObserverView, time, null);
        Deployable_ObserverView.GetType().GetProperty($"StatusEffects_{index}Prop").SetValue(Deployable_ObserverView, null, null);
    }

    public void SetPosition(Vector3 newPosition)
    {
        Position = newPosition;
        Deployable_ObserverView.PositionProp = Position;
    }

    public void SetOrientation(Quaternion newOrientation)
    {
        Orientation = newOrientation;
        Deployable_ObserverView.OrientationProp = Orientation;
    }

    public void SetAimDirection(Vector3 newDirection)
    {
        AimDirection = newDirection;
        Deployable_ObserverView.AimDirectionProp = AimDirection;
    }

    public void SetHostilityInfo(HostilityInfoData newValue)
    {
        HostilityInfo = newValue;
        Deployable_ObserverView.HostilityInfoProp = HostilityInfo;
    }

    /// <summary>
    ///     Sets the pool this deployable can lose. A deployable whose SDB record carries no health keeps a
    ///     maximum of zero, which <see cref="TakeDamage"/> reads as indestructible; that's the behaviour
    ///     every deployable had before any of this, so a missing column costs nothing.
    /// </summary>
    public void SetMaxHealth(int newValue, bool resetCurrent)
    {
        MaxHealth = Math.Max(0, newValue);
        Deployable_ObserverView.MaxHealthProp = MaxHealth;

        SetCurrentHealth(resetCurrent ? MaxHealth : Math.Min(MaxHealth, CurrentHealth));
    }

    public void SetCurrentHealth(int newValue)
    {
        CurrentHealth = Math.Clamp(newValue, 0, MaxHealth);
        Deployable_ObserverView.CurrentHealthPctProp = Vitals.HealthPercent(CurrentHealth, MaxHealth);
    }

    public void TakeDamage(DamageInfo damage)
    {
        // Nothing to lose means nothing to hurt, so a deployable the SDB gave no health survives anything
        if (!IsAlive || MaxHealth <= 0)
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
            "Deployable {Type} took {Amount} damage from {Attacker}, {Health} of {MaxHealth} health left",
            Type,
            amount,
            damage.Attacker,
            CurrentHealth,
            MaxHealth);

        DamageEvents.SendDealtHit(damage, DamageEvents.Describe(this, damage, amount));

        if (CurrentHealth <= 0)
        {
            Destroy();
        }
    }

    /// <summary>
    ///     A destroyed deployable stops being damageable immediately, runs its <see cref="DeathAbility"/>, and
    ///     is removed a moment later so the client has time to see the health bar reach zero. Its turret goes
    ///     with it, since a turret is scoped through its parent and would otherwise be left pointing at
    ///     nothing.
    /// </summary>
    public void Destroy()
    {
        if (Destroyed)
        {
            return;
        }

        Destroyed = true;

        if (Turret != null)
        {
            Turret.SetControllingPlayer(null);
            Shard.EntityMan.Remove(Turret);
        }

        if (DeathAbility != 0)
        {
            Shard.Abilities.HandleActivateAbility(Shard, this, DeathAbility);
        }

        Shard.EntityMan.SetRemainingLifetime(this, DestroyedLifetimeMs);
    }

    public override bool IsInteractable()
    {
        if (Encounter != null && !Encounter.Handles(EncounterComponent.Event.Interaction))
        {
            return false;
        }

        return Interaction != null && Interaction.Type != 0;
    }

    public override bool CanBeInteractedBy(IEntity other)
    {
        return IsInteractable();
    }

    private void InitFields()
    {
        Position = new Vector3();
        Orientation = Quaternion.Identity;
        AimDirection = new Vector3(0.70707911253f, 0.707134246826f, 1f);
        HostilityInfo = new HostilityInfoData { Flags = 0 | HostilityInfoData.HostilityFlags.Faction, FactionId = 1 };
        ConstructedTime = Shard.CurrentTime;
    }

    private void InitViews()
    {
        Deployable_ObserverView = new ObserverView
        {
            TypeProp = Type,
            OwningEntityProp = Owner?.AeroEntityId ?? new EntityId { Backing = 0 },
            AbilitySrcIdProp = AbilitySrcId,
            PositionProp = Position,
            OrientationProp = Orientation,
            AimPositionProp = AimPosition,
            AimDirectionProp = AimDirection,
            ConstructedTimeProp = ConstructedTime,
            CurrentHealthPctProp = 100,
            MaxHealthProp = MaxHealth,
            LevelProp = 0,
            ScalingLevelProp = 0,
            GibVisualsIDProp = GibVisualsID,
            HostilityInfoProp = HostilityInfo,
            AttachedToProp = new EntityId { Backing = 0 },
            CharacterStatsProp = new CharacterStatsData
            {
                ItemAttributes = [],
                Unk1 = 0,
                WeaponA = [],
                Unk2 = 0,
                WeaponB = [],
                Unk3 = 0,
                AttributeCategories1 = [],
                AttributeCategories2 = []
            },
            WarpaintColorsProp = [],
            PersonalFactionStanceProp = null,
            SinFlagsProp = 0,
            SinFactionsAcquiredByProp = null,
            SinTeamsAcquiredByProp = null,
            SinCardTypeProp = 0,
            SinCardFields_0Prop = null,
            SinCardFields_1Prop = null,
            SinCardFields_2Prop = null,
            SinCardFields_3Prop = null,
            SinCardFields_4Prop = null,
            SinCardFields_5Prop = null,
            SinCardFields_6Prop = null,
            SinCardFields_7Prop = null,
            SinCardFields_8Prop = null,
            SinCardFields_9Prop = null,
            SinCardFields_10Prop = null,
            SinCardFields_11Prop = null,
            SinCardFields_12Prop = null,
            SinCardFields_13Prop = null,
            SinCardFields_14Prop = null,
            SinCardFields_15Prop = null,
            SinCardFields_16Prop = null,
            SinCardFields_17Prop = null,
            SinCardFields_18Prop = null,
            SinCardFields_19Prop = null,
        };
    }
}