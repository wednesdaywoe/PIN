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

    // Last change time sent per effect slot. Consecutive transitions on the same slot can
    // land within the same millisecond (e.g. a remove chain immediately applying a follow-up
    // effect), which would repeat the previous change time; a client deduplicating on that
    // field would drop the second transition, so bump it to keep it strictly increasing.
    private readonly ushort[] _statusEffectChangeTimes = new ushort[MaxEffectCount];

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

        var time = NextStatusEffectChangeTime(state.Index, unchecked((ushort)state.Time));
        var data = new StatusEffectData
        {
            Id = state.Effect.Id,
            Initiator = state.Context.Initiator.AeroEntityId,
            Time = state.Time,
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
        var time = NextStatusEffectChangeTime(state.Index, unchecked((ushort)state.Context.Shard.CurrentTime));
        ClearStatusEffect(state.Index, time, state.Effect.Id);
        Shard.EntityMan.FlushChanges(this); // Force flush so that we communicate every change
    }

    private ushort NextStatusEffectChangeTime(byte index, ushort time)
    {
        if (time == _statusEffectChangeTimes[index])
        {
            time++;
        }

        _statusEffectChangeTimes[index] = time;
        return time;
    }

    public abstract void SetStatusEffect(byte index, ushort time, StatusEffectData data);
    public abstract void ClearStatusEffect(byte index, ushort time, uint debugEffectId);
}