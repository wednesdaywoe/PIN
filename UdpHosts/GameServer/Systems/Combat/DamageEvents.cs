using AeroMessages.GSS.V66;
using GameServer.Entities;
using GameServer.Enums;
using DealtHitEvent = AeroMessages.GSS.V66.Character.Event.DealtHit;

namespace GameServer.Systems.Combat;

/// <summary>
///     Telling clients about a hit that landed. The attacker's side of it is the same whatever was hit, so it
///     lives here rather than once per <see cref="IDamageable"/>; the target's side isn't, because only
///     characters have a TookHit event to receive.
/// </summary>
public static class DamageEvents
{
    /// <summary>
    ///     The hit as the client wants it described: who took it, who dealt it, and how much after
    ///     mitigation. <paramref name="amount"/> is the whole hit rather than the part that reached health,
    ///     which is what makes a shot into a full shield read as a real number instead of a zero.
    /// </summary>
    public static DamageHitStruct Describe(IEntity target, DamageInfo damage, int amount)
    {
        return new DamageHitStruct
        {
            Target = target.AeroEntityId,
            HaveDealer = (byte)(damage.Attacker != null ? 1 : 0),
            Dealer = damage.Attacker?.AeroEntityId ?? default,
            DamageValue = amount,
            DamageType = damage.DamageType,
        };
    }

    /// <summary>
    ///     Sends the attacker the damage number it just caused. Does nothing when the damage came from an NPC
    ///     or from the world, since there's no client waiting to draw it.
    /// </summary>
    public static void SendDealtHit(DamageInfo damage, DamageHitStruct hit)
    {
        if (damage.Attacker is not { IsPlayerControlled: true })
        {
            return;
        }

        var dealtHit = new DealtHitEvent
        {
            HaveDamage = 1,
            DamageData = hit,
            RepeatHitIdx = 0,
            DamageFlags = damage.Flags,
        };

        damage.Attacker.Player.NetChannels[ChannelType.ReliableGss].SendMessage(dealtHit, damage.Attacker.EntityId);
    }
}
