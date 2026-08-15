using System;
using System.Collections.Generic;
using System.Linq;
using GameServer.StaticDB.Records.dbcharacter;

namespace GameServer.Systems.Combat;

/// <summary>
///     Gives a creature a power tier, which PIN has never had — every NPC in the game shares one health
///     number today.
/// </summary>
/// <remarks>
///     <para>
///     Two shipped pieces do the work and neither needed inventing. <c>dbcharacter::MonsterScaling</c> is
///     the whole creature power curve, 80 rows of level, health and damage. <c>Monster.difficulty_cost</c>
///     is a per-creature threat grade carried by 906 of the 3109 creature types. What was missing is the
///     join between them: the curve is keyed by level, and a monster's level was server content that
///     never shipped. This class is that join, and it is the only invented step in the chain.
///     </para>
///     <para>
///     The grade reads as a spawn currency — an encounter with a budget spends it on bodies — so a
///     creature's grade is what the designers thought it was worth, which makes it proportional to
///     power. Health is taken as proportional to grade for that reason, and the constant of
///     proportionality is fixed by a single tested anchor rather than chosen: monster 528 is grade
///     <see cref="AnchorGrade"/> and <see cref="AnchorHealth"/> is the value D6 confirmed in game for it
///     on 2026-08-15. That gives <see cref="HealthPerGradePoint"/> and nothing else here is a preference.
///     </para>
///     <para>
///     The grade does not become health directly. It picks the nearest row of the shipped curve and the
///     row supplies both numbers, which buys three things a direct multiply would not: every creature
///     lands on a level Red 5 actually shipped instead of an interpolated invention, damage arrives
///     alongside health from the same row, and the quantisation is the curve's own — dense at the bottom
///     where ordinary creatures sit, coarse at the top where bosses do.
///     </para>
///     <para>
///     Levels stay internal. The project's goal is the beta's leveless feel, and this does for creatures
///     what PIN already does for players: carry a level as a scalar and never show it. Nothing here is
///     sent to the client.
///     </para>
///     <para>
///     Closes the health half of DATA-6 and the damage half of DATA-20. See
///     <c>Docs/gaps/data.md</c>.
///     </para>
/// </remarks>
public static class MonsterTier
{
    /// <summary>
    ///     The grade the anchor creature carries. Monster 528, a Melded Aranha, near the bottom of the
    ///     shipped ladder — 33 creature types share this grade and only 4 rate below it.
    /// </summary>
    public const uint AnchorGrade = 20;

    /// <summary>
    ///     Health for a creature at <see cref="AnchorGrade"/>, confirmed in game rather than reasoned to.
    ///     D6 killed monster 528 six times across two weapons at 31 hits of 39 damage every time. It
    ///     replaced an earlier 500 that turned out to be 0.83 seconds of chaingun fire once the beta
    ///     video it came from was converted through a real rate of fire.
    /// </summary>
    public const int AnchorHealth = 1200;

    /// <summary>
    ///     The one free parameter in this file, and it is a quotient of the two constants above rather
    ///     than a number anybody picked. Moving the anchor moves the whole ladder together and keeps its
    ///     shape, which is the point of expressing it this way.
    /// </summary>
    public const double HealthPerGradePoint = (double)AnchorHealth / AnchorGrade;

    /// <summary>
    ///     Resolves a creature's <c>difficulty_cost</c> against the shipped curve.
    /// </summary>
    /// <param name="difficultyCost">
    ///     The creature's grade. Zero means ungraded, which 2203 of 3109 creature types are — including
    ///     dangerous ones like <c>EliteWanderer</c>, so zero must not be read as harmless. Ungraded
    ///     creatures get the anchor row, which is exactly the flat behaviour PIN had before this class
    ///     existed. Nothing regresses; 906 creature types stop being identical.
    /// </param>
    /// <param name="curve">
    ///     <c>dbcharacter::MonsterScaling</c>. An empty curve falls back to <see cref="AnchorHealth"/> at
    ///     level 0, so a missing table costs tiering rather than breaking spawns.
    /// </param>
    public static Result Resolve(uint difficultyCost, IReadOnlyCollection<MonsterScaling> curve)
    {
        if (curve == null || curve.Count == 0)
        {
            return new Result(0, AnchorHealth, AnchorHealth / 2, 1f, false);
        }

        bool graded = difficultyCost > 0;
        uint grade = graded ? difficultyCost : AnchorGrade;

        var row = NearestByHealth(grade * HealthPerGradePoint, curve);
        var anchorRow = NearestByHealth(AnchorGrade * HealthPerGradePoint, curve);

        // Relative to the anchor rather than absolute, so a creature at the anchor grade comes out at
        // exactly 1.0 and behaves the way it did before tiering existed. That makes the anchor creature a
        // usable control in game: if 528 changes, the change came from somewhere other than this file.
        float damageMultiplier = anchorRow.Damage == 0
            ? 1f
            : (float)row.Damage / anchorRow.Damage;

        return new Result(row.Level, (int)row.Health, (int)row.Damage, damageMultiplier, graded);
    }

    private static MonsterScaling NearestByHealth(double targetHealth, IReadOnlyCollection<MonsterScaling> curve)
    {
        return curve
            .OrderBy(row => Math.Abs(row.Health - targetHealth))
            .ThenBy(row => row.Level)
            .First();
    }

    /// <summary>
    ///     What a creature's grade resolved to. <see cref="Graded"/> is false when the creature carries no
    ///     grade at all, which is the majority case — see <see cref="Resolve"/>.
    /// </summary>
    public readonly record struct Result(byte Level, int Health, int Damage, float DamageMultiplier, bool Graded);
}
