using System.Collections.Generic;
using System.Linq;
using AeroMessages.GSS.V66.Character;

namespace GameServer.Systems.Aptitude;

/// <summary>
///     An entity's ability cooldowns, as the client's apt::CooldownSet: local cooldowns by ability id,
///     category cooldowns by category id, and one global cooldown.
///     Times are in shard milliseconds and compared by signed difference, so they survive wrapping.
/// </summary>
public class CooldownSet
{
    public const byte PreventResetFlag = 1;
    public const byte MainSlotFlag = 2;

    public Dictionary<uint, Cooldown> Local { get; } = [];
    public Dictionary<uint, Cooldown> Category { get; } = [];
    public uint GlobalActivatedTime { get; private set; }
    public uint GlobalReadyAgainTime { get; private set; }

    // Times are only comparable to recent ones, so a global cooldown that was never inflicted can't just be 0
    public bool HasGlobal { get; private set; }

    public static bool IsReady(uint readyAgainTime, uint now) => (int)(now - readyAgainTime) >= 0;

    public bool IsGlobalReady(uint now) => !HasGlobal || IsReady(GlobalReadyAgainTime, now);

    public bool IsLocalReady(uint abilityId, uint now) => !Local.TryGetValue(abilityId, out var cooldown) || IsReady(cooldown.ReadyAgainTime, now);

    public bool IsCategoryReady(uint category, uint now) => !Category.TryGetValue(category, out var cooldown) || IsReady(cooldown.ReadyAgainTime, now);

    public void InflictGlobal(uint now, uint duration)
    {
        GlobalActivatedTime = now;
        GlobalReadyAgainTime = now + duration;
        HasGlobal = true;
    }

    public void InflictLocal(uint abilityId, uint now, uint duration, uint precoolTime, byte flags)
    {
        Local[abilityId] = Extend(Local.GetValueOrDefault(abilityId), now, duration, precoolTime, flags);
    }

    public void InflictCategory(uint category, uint now, uint duration, uint precoolTime, byte flags)
    {
        Category[category] = Extend(Category.GetValueOrDefault(category), now, duration, precoolTime, flags);
    }

    public AbilityCooldownsData ToData(uint now)
    {
        return new AbilityCooldownsData
        {
            ActiveCooldowns_Group1 = Local.Select(pair => pair.Value.ToData(pair.Key)).ToArray(),
            ActiveCooldowns_Group2 = Category.Select(pair => pair.Value.ToData(pair.Key)).ToArray(),
            GlobalCooldown_Activated_Time = HasGlobal ? GlobalActivatedTime : now,
            GlobalCooldown_ReadyAgain_Time = HasGlobal ? GlobalReadyAgainTime : now,
        };
    }

    // As the client (FUN_013f12d0). The precool time is how far the cooldown may already have run, which gives
    // precool / duration extra charges: a use starts no earlier than the last ready time or now - precool,
    // and is ready again a duration later, or at the old ready time if that is later.
    // A missing cooldown counts as ready long ago, so it starts at now - precool.
    private static Cooldown Extend(Cooldown existing, uint now, uint duration, uint precoolTime, byte flags)
    {
        var earliest = now - precoolTime;
        if (existing == null)
        {
            return new Cooldown(earliest, earliest + duration, flags, precoolTime);
        }

        var start = Later(Earlier(existing.ReadyAgainTime, now), earliest);
        var readyAgain = Later(existing.ReadyAgainTime, start + duration);
        return new Cooldown(start, readyAgain, (byte)(existing.Flags | flags), precoolTime);
    }

    private static uint Earlier(uint a, uint b) => (int)(a - b) < 0 ? a : b;

    private static uint Later(uint a, uint b) => (int)(a - b) < 0 ? b : a;

    public record Cooldown(uint ActivatedTime, uint ReadyAgainTime, byte Flags, uint PrecoolTime)
    {
        // The 5 bytes after the times are the flags and the precool time (client FUN_013f1fb0)
        public ActiveCooldown ToData(uint id) => new()
        {
            AbilityId = id,
            Activated_Time = ActivatedTime,
            ReadyAgain_Time = ReadyAgainTime,
            Unk = [Flags, (byte)PrecoolTime, (byte)(PrecoolTime >> 8), (byte)(PrecoolTime >> 16), (byte)(PrecoolTime >> 24)],
        };
    }
}
