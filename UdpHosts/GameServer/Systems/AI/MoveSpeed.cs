using GameServer.StaticDB;

namespace GameServer.Systems.AI;

/// <summary>
///     How fast a monster moves, in metres per second, resolved from the same tables the client reads.
///
///     The speeds are not on <c>dbcharacter::Monster</c> where you would look for them. That record does
///     carry <c>NormalSpeed</c> and <c>FastSpeed</c>, but of its 3109 rows only 14 name a run speed and
///     6 a walk speed; the rest hold -1 in both. Those columns are an override, and almost nothing
///     overrides. The real numbers come from the monster's <c>ChassisId</c>, which is a
///     <c>dbitems::Battleframe</c> — the same record a player's frame speed comes out of — and that
///     resolves a run speed for 2971 of them, leaving 124 with one named nowhere.
///
///     Reading -1 literally would give an NPC that walks backwards, which is DATA-11 again with a
///     different sentinel, so every step of the chain is checked for a positive value before it is
///     believed.
/// </summary>
public readonly record struct MoveSpeed(float Walk, float Run)
{
    /// <summary>
    ///     Stand-in for the 124 rows where neither the monster nor its chassis names a speed. Not
    ///     invented: 6 m/s is the most common chassis <c>FastSpeed</c> in the table by a wide margin
    ///     (1215 monsters), so a monster with no speed is given the speed most monsters have.
    /// </summary>
    public const float DefaultRun = 6f;

    /// <summary>Half of <see cref="DefaultRun"/>, in the same spirit. Nothing reads it yet.</summary>
    public const float DefaultWalk = 3f;

    public static MoveSpeed Default => new(DefaultWalk, DefaultRun);

    /// <summary>
    ///     Resolves the pair for a monster type id, preferring the monster's own override over its
    ///     chassis and falling back to <see cref="Default"/> when neither says anything usable.
    /// </summary>
    public static MoveSpeed Resolve(uint characterTypeId)
    {
        var monster = characterTypeId != 0 ? SDBInterface.GetMonster(characterTypeId) : null;
        if (monster == null)
        {
            return Default;
        }

        var chassis = monster.ChassisId != 0 ? SDBInterface.GetBattleframe(monster.ChassisId) : null;

        return new MoveSpeed(
            FirstPositive(monster.NormalSpeed, chassis?.NormalSpeed ?? 0f, DefaultWalk),
            FirstPositive(monster.FastSpeed, chassis?.FastSpeed ?? 0f, DefaultRun));
    }

    private static float FirstPositive(float preferred, float fallback, float last)
    {
        if (preferred > 0f)
        {
            return preferred;
        }

        return fallback > 0f ? fallback : last;
    }
}
