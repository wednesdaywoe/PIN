using System.Collections.Generic;
using AeroMessages.GSS.V66;
using GameServer.Entities.Character;
using GameServer.Systems.Aptitude;

namespace GameServer.Entities;

public abstract class BaseAptitudeEntity : BaseEntity, IAptitudeTarget
{
    public const byte MaxEffectCount = 32;
    public const byte InvalidIndex = 255;

    protected EffectState[] ActiveEffects = new EffectState[MaxEffectCount];

    // How far back a change time looks before we call it a collision rather than a clock wrap
    private const ushort CollisionWindow = 1000;

    // Last status effect change time sent for this entity, across every slot
    private ushort _lastStatusEffectChangeTime;

    public BaseAptitudeEntity(IShard shard, ulong eid, CharacterEntity owner = null)
    : base(shard, eid)
    {
        Owner = owner;
    }

    public CharacterEntity Owner { get; }

    public List<EffectState> GetActiveEffects() => [.. ActiveEffects];

    public override string ToString()
    {
        return $"{GetType().Name} ({EntityId})";
    }

    public EffectState AddEffect(Effect effect, Context context)
    {
        byte firstFreeIndex = InvalidIndex;
        for (byte i = 0; i < MaxEffectCount; i++)
        {
            if (ActiveEffects[i] == null)
            {
                if (firstFreeIndex == InvalidIndex)
                {
                    firstFreeIndex = i;
                }
            }

            if (ActiveEffects[i]?.Effect.Id == effect.Id)
            {
                if (ActiveEffects[i].Stacks < effect.MaxStackCount)
                {
                    ActiveEffects[i].Stacks += 1;
                }
                else
                {
                    return new EffectState() { MaxStacksExceeded = true };
                }

                return ActiveEffects[i];
            }
        }

        if (firstFreeIndex == InvalidIndex)
        {
            // fail!
            Logger.Warning("AddEffect but there are too many active effects!");
            firstFreeIndex = 31; // Lets not crash
        }

        var state = new EffectState
        {
            Effect = effect,
            Context = context,
            Time = Shard.CurrentTime,
            Stacks = 1,
            Index = firstFreeIndex
        };

        ActiveEffects[firstFreeIndex] = state;

        var time = NextStatusEffectChangeTime(unchecked((ushort)state.Time));

        // A predicted apply already exists client-side, stamped with the activation time the
        // client sent and one stack. The netfield data has to look like that instance or the
        // client keeps both: the clear then only tears down the server's copy and whatever the
        // predicted chain took (camera, aim lock) leaks. Echo the activation time when the
        // apply came from one, and send the real stack count instead of the default 0.
        var data = new StatusEffectData
        {
            Id = state.Effect.Id,
            Stack = state.Stacks,
            Initiator = state.Context.Initiator.AeroEntityId,
            Time = state.Context.InitTime != 0 ? state.Context.InitTime : state.Time,
            MoreDataFlag = 0
        };
        var index = state.Index;
        SetStatusEffect(index, time, data);
        Shard.EntityMan.FlushChanges(this); // Force flush so that we communicate every change

        return state;
    }

    public void ClearEffect(EffectState state)
    {
        ActiveEffects[state.Index] = null;
        var time = NextStatusEffectChangeTime(unchecked((ushort)state.Context.Shard.CurrentTime));
        ClearStatusEffect(state.Index, time, state.Effect.Id);
        Shard.EntityMan.FlushChanges(this); // Force flush so that we communicate every change
    }

    public abstract void SetStatusEffect(byte index, ushort time, StatusEffectData data);

    public abstract void ClearStatusEffect(byte index, ushort time, uint debugEffectId);

    private ushort NextStatusEffectChangeTime(ushort time)
    {
        // Only collisions get nudged. The field is the low 16 bits of a unix millisecond clock,
        // so it wraps about once a minute and a large apparent jump is that wrap, not a stale
        // time: treating it as stale would leave every change time trailing the real clock by
        // however far behind it started, which is worse than the collision this guards against.
        var elapsed = (ushort)(time - _lastStatusEffectChangeTime);
        if (elapsed == 0 || elapsed > ushort.MaxValue - CollisionWindow)
        {
            time = (ushort)(_lastStatusEffectChangeTime + 1);
        }

        _lastStatusEffectChangeTime = time;
        return time;
    }
}