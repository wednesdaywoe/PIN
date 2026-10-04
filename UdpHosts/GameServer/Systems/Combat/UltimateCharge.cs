using System;
using System.Threading;
using GameServer.Entities.Character;

namespace GameServer.Systems.Combat;

/// <summary>
///     Fills players' ultimate meter (SuperCharge, 0 to 100; the client lights the ultimate at 100). What filled it in
///     1962 was the original server's and never shipped; the ultimates' own text says only that it "slowly gains charge
///     while you are in combat". This is PIN's: a steady fill while in combat, plus a bonus for damage dealt measured
///     against the target's health so it means the same at every level. Both scale by the equipped ultimate's Charge
///     Speed (attribute 959, 1 to 1.2). Starts empty each session and survives death.
/// </summary>
public class UltimateCharge
{
    public const float Full = 100f;

    /// <summary>Seconds of combat to fill an empty meter at Charge Speed 1, without any damage bonus</summary>
    public const float SecondsToFill = 120f;

    /// <summary>How long after the last hit, dealt or taken, a player still counts as in combat</summary>
    public const ulong CombatWindowMs = 10_000;

    /// <summary>Meter gained for dealing a whole target's worth of health in damage</summary>
    public const float PerTargetHealthDealt = 10f;

    /// <summary>Most one hit can add, so one big hit on a weak target doesn't fill the meter</summary>
    public const float MaxPerHit = 5f;

    public const ushort ChargeSpeedAttribute = 959;

    private const ulong UpdateIntervalMs = 250;

    private readonly Shard _shard;
    private ulong _lastUpdate;

    public UltimateCharge(Shard shard)
    {
        _shard = shard;
    }

    public static float ChargeSpeed(CharacterEntity character)
    {
        var speed = character.GetItemAttribute(ChargeSpeedAttribute);
        return speed > 0f ? speed : 1f;
    }

    /// <summary>Called for every hit that lands. Both sides are in combat; the attacker earns the damage bonus.</summary>
    public static void OnHit(CharacterEntity attacker, CharacterEntity target, float amount, ulong now)
    {
        if (target.IsPlayerControlled)
        {
            target.LastCombatTime = now;
        }

        if (attacker is not { IsPlayerControlled: true } || ReferenceEquals(attacker, target))
        {
            return;
        }

        attacker.LastCombatTime = now;
        var maxHealth = target.MaxHealth.Value;
        if (maxHealth > 0)
        {
            var bonus = MathF.Min(PerTargetHealthDealt * amount / maxHealth, MaxPerHit) * ChargeSpeed(attacker);
            attacker.AddSuperCharge(bonus);
        }
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (currentTime < _lastUpdate + UpdateIntervalMs)
        {
            return;
        }

        var elapsedSeconds = _lastUpdate == 0 ? 0f : (currentTime - _lastUpdate) / 1000f;
        _lastUpdate = currentTime;
        if (elapsedSeconds <= 0f)
        {
            return;
        }

        foreach (var entity in _shard.Entities.Values)
        {
            if (entity is CharacterEntity { IsPlayerControlled: true, IsAlive: true } player
                && player.SuperCharge < Full
                && currentTime < player.LastCombatTime + CombatWindowMs)
            {
                player.AddSuperCharge(Full / SecondsToFill * elapsedSeconds * ChargeSpeed(player));
            }
        }
    }
}
