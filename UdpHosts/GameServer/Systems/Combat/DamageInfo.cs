using System;
using AeroMessages.GSS.V66;
using GameServer.Entities.Character;

namespace GameServer.Systems.Combat;

/// <summary>
///     One incoming hit, described before the target has had any say in it.
///
///     Anything that wants to hurt something builds one of these and hands it to
///     <see cref="CharacterEntity.TakeDamage"/>. Mitigation lives there and nowhere else, so shields,
///     resistances and the ModifyDamageBy* commands only have to be written once. Callers cover what the
///     attacker's side knows about, meaning range decay, hit location and splash falloff, and stop there.
/// </summary>
public readonly record struct DamageInfo
{
    /// <summary>
    ///     Damage before mitigation. It stays a float so the rounding to whole points happens once, after
    ///     the target's side is done with it.
    /// </summary>
    public float Amount { get; init; }

    /// <summary>
    ///     Who caused it, or null for environmental damage.
    /// </summary>
    public CharacterEntity Attacker { get; init; }

    /// <summary>
    ///     dbcharacter::DamageType id. This is what the DamageResponse multipliers will key off.
    /// </summary>
    public byte DamageType { get; init; }

    /// <summary>
    ///     How the client should draw the hit, which today is only whether it crit.
    /// </summary>
    public DamageResponseFlags Flags { get; init; }

    /// <summary>
    ///     The weapon this came out of, for <see cref="CombatLog"/>. Zero and null when nothing fired it, so
    ///     the log can say so instead of inventing an attribution. Nothing but the log reads these.
    /// </summary>
    public uint WeaponId { get; init; }

    /// <summary>Human-readable weapon name, already built by the template resolver.</summary>
    public string WeaponName { get; init; }

    /// <summary>
    ///     <see cref="Amount"/> as whole points. Health pools are integers, so every
    ///     <see cref="IDamageable"/> rounds at the same moment and in the same direction, and a hit that
    ///     rounds away to nothing is one every target agrees to ignore.
    /// </summary>
    public int Points => (int)MathF.Round(Amount);
}
