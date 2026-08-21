using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GameServer.Entities;
using GameServer.Entities.Character;
using Serilog;

namespace GameServer.Systems.Combat;

/// <summary>
///     Writes every hit and every kill to CSV, so what a weapon actually does can be measured. The static
///     data only predicts it: the client decides the real rate of fire, sending one message per round with
///     no obligation to match the round interval in the db.
///
///     Two files a day. Hits carry what one round did, kills carry the rollup (rounds, elapsed, damage a
///     second) accumulated over every hit that led to the kill, including ones written hours earlier.
///
///     Writes on its own rather than through Serilog, because the general log is level-filtered and rotated
///     out from under the server on every restart. Nothing here is conditional on log level.
/// </summary>
public static class CombatLog
{
    /// <summary>
    ///     How long a target can go unhit before the next hit counts as a fresh fight. Without it, a
    ///     creature shot once and killed a minute later averages its damage a second over the idle minute.
    /// </summary>
    private const ulong EngagementGapMs = 10_000;

    private static readonly object Gate = new();
    private static readonly Dictionary<ulong, Engagement> Engagements = [];

    private static ILogger _logger = Log.ForContext(typeof(CombatLog));
    private static string _directory;
    private static StreamWriter _hits;
    private static StreamWriter _kills;
    private static DateTime _openedForDay = DateTime.MinValue;

    private static bool Enabled => _directory != null;

    private static string HitsHeader =>
        "time,shard_ms,attacker,attacker_kind,weapon_id,weapon,target,target_kind,target_level,damage_type,raw,applied,absorbed,health_left,hit_number";

    private static string KillsHeader =>
        "time,target,target_kind,target_level,target_max_health,killer,killer_kind,weapons,hits,damage,elapsed_ms,damage_per_second";

    /// <summary>
    ///     Starts writing to <paramref name="directory"/>, creating it if needed. Called once at startup;
    ///     calling it again repoints the log and leaves what's already written alone.
    /// </summary>
    public static void Init(string directory, ILogger logger)
    {
        lock (Gate)
        {
            _logger = logger?.ForContext(typeof(CombatLog)) ?? _logger;

            // Whatever was open belongs to the old directory. On Linux writes to a moved or deleted file
            // still succeed, so leaving these open would silently keep filling a file nobody reads.
            _hits?.Dispose();
            _kills?.Dispose();
            _hits = null;
            _kills = null;
            _openedForDay = DateTime.MinValue;

            try
            {
                Directory.CreateDirectory(directory);
                _directory = directory;
                _logger.Information("Combat log writing to {Directory}", Path.GetFullPath(directory));
            }
            catch (Exception ex)
            {
                // A server that can't write the combat log is still a server. Say so once and carry on.
                _directory = null;
                _logger.Error(ex, "Could not open the combat log directory {Directory}, combat logging is off", directory);
            }
        }
    }

    /// <summary>
    ///     Records one landed hit and folds it into the running total for that target.
    /// </summary>
    /// <param name="target">What was hit.</param>
    /// <param name="damage">The hit as the attacker's side described it.</param>
    /// <param name="applied">Points actually taken off, after mitigation.</param>
    /// <param name="absorbed">How much of <paramref name="applied"/> the shields ate.</param>
    /// <param name="healthLeft">The target's health once this hit was done with it.</param>
    /// <param name="currentTimeMs">Shard time, which is what elapsed-time arithmetic here is done in.</param>
    public static void RecordHit(IEntity target, DamageInfo damage, int applied, int absorbed, int healthLeft, ulong currentTimeMs)
    {
        if (!Enabled)
        {
            return;
        }

        lock (Gate)
        {
            var engagement = Track(target.EntityId, damage, applied, currentTimeMs);

            Write(
                ref _hits,
                "hits",
                HitsHeader,
                [
                    Stamp(),
                    currentTimeMs.ToString(CultureInfo.InvariantCulture),
                    Describe(damage.Attacker),
                    Kind(damage.Attacker),
                    damage.WeaponId.ToString(CultureInfo.InvariantCulture),
                    damage.WeaponName ?? string.Empty,
                    Describe(target),
                    Kind(target),
                    Level(target),
                    damage.DamageType.ToString(CultureInfo.InvariantCulture),
                    damage.Amount.ToString("0.##", CultureInfo.InvariantCulture),
                    applied.ToString(CultureInfo.InvariantCulture),
                    absorbed.ToString(CultureInfo.InvariantCulture),
                    healthLeft.ToString(CultureInfo.InvariantCulture),
                    engagement.Hits.ToString(CultureInfo.InvariantCulture),
                ]);
        }
    }

    /// <summary>
    ///     Closes the books on a target and writes the rollup. The killing hit is already in the totals:
    ///     hits are recorded before the target checks whether it's still alive.
    /// </summary>
    public static void RecordDeath(IEntity target, CharacterEntity killer, int maxHealth, ulong currentTimeMs)
    {
        if (!Enabled)
        {
            return;
        }

        lock (Gate)
        {
            if (!Engagements.Remove(target.EntityId, out var engagement))
            {
                // Died without going through TakeDamage, so there's no rollup to write and an empty one
                // would just pollute the series.
                return;
            }

            var elapsedMs = engagement.LastHitMs - engagement.FirstHitMs;

            // A one-shot kill has no elapsed time to divide by. The round's own damage is the honest answer.
            var seconds = elapsedMs > 0 ? elapsedMs / 1000f : 0f;
            var dps = seconds > 0 ? engagement.TotalDamage / seconds : engagement.TotalDamage;

            Write(
                ref _kills,
                "kills",
                KillsHeader,
                [
                    Stamp(),
                    Describe(target),
                    Kind(target),
                    Level(target),
                    maxHealth.ToString(CultureInfo.InvariantCulture),
                    Describe(killer),
                    Kind(killer),
                    engagement.WeaponSummary(),
                    engagement.Hits.ToString(CultureInfo.InvariantCulture),
                    engagement.TotalDamage.ToString("0.##", CultureInfo.InvariantCulture),
                    elapsedMs.ToString(CultureInfo.InvariantCulture),
                    dps.ToString("0.##", CultureInfo.InvariantCulture),
                ]);
        }
    }

    /// <summary>
    ///     Drops a target's running total without writing a rollup, for anything that leaves the fight
    ///     without dying.
    /// </summary>
    public static void Forget(ulong entityId)
    {
        if (!Enabled)
        {
            return;
        }

        lock (Gate)
        {
            Engagements.Remove(entityId);
        }
    }

    /// <summary>
    ///     Folds one hit into the target's running total, starting fresh if the last hit is old enough to
    ///     belong to a different fight. Public so the arithmetic is testable without a server; the file
    ///     writing deliberately isn't.
    /// </summary>
    public static Engagement Track(ulong entityId, DamageInfo damage, int applied, ulong currentTimeMs)
    {
        if (!Engagements.TryGetValue(entityId, out var engagement) || currentTimeMs - engagement.LastHitMs > EngagementGapMs)
        {
            engagement = new Engagement { FirstHitMs = currentTimeMs };
            Engagements[entityId] = engagement;
        }

        engagement.LastHitMs = currentTimeMs;
        engagement.Hits++;
        engagement.TotalDamage += applied;

        var weapon = string.IsNullOrEmpty(damage.WeaponName) ? "(no weapon)" : damage.WeaponName;
        engagement.PerWeapon[weapon] = engagement.PerWeapon.GetValueOrDefault(weapon) + 1;

        return engagement;
    }

    public static void ResetForTests()
    {
        lock (Gate)
        {
            Engagements.Clear();
        }
    }

    private static string Stamp()
    {
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    private static string Describe(IEntity entity)
    {
        return entity == null ? "(none)" : $"{entity} {entity.EntityId:X}";
    }

    private static string Kind(IEntity entity)
    {
        return entity switch
        {
            null => "none",
            CharacterEntity character => character.IsPlayerControlled ? "player" : "npc",
            _ => entity.GetType().Name.Replace("Entity", string.Empty).ToLowerInvariant(),
        };
    }

    private static string Level(IEntity entity)
    {
        return entity is CharacterEntity character && !character.IsPlayerControlled
            ? character.MonsterLevel.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    /// <summary>
    ///     Appends one row, opening today's file first if the day turned over since the last write. Long
    ///     sittings cross midnight often enough that this isn't an edge case.
    /// </summary>
    private static void Write(ref StreamWriter writer, string name, string header, string[] fields)
    {
        try
        {
            var today = DateTime.Now.Date;

            // Both files are named for the day, so both close together even though only one is being
            // written right now.
            if (_openedForDay != today)
            {
                _hits?.Dispose();
                _kills?.Dispose();
                _hits = null;
                _kills = null;
                _openedForDay = today;
            }

            writer ??= Open(name, header, today);

            writer.WriteLine(string.Join(",", fields.Select(Escape)));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Combat log write failed, turning combat logging off for this session");
            _directory = null;
        }
    }

    private static StreamWriter Open(string name, string header, DateTime day)
    {
        var path = Path.Combine(_directory, $"{name}-{day:yyyy-MM-dd}.csv");
        var isNew = !File.Exists(path) || new FileInfo(path).Length == 0;

        // Append, not truncate: the series has to survive restarts.
        var writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8)
        {
            AutoFlush = true,
        };

        if (isNew)
        {
            writer.WriteLine(header);
        }

        return writer;
    }

    /// <summary>
    ///     CSV quoting. Entity names are player-chosen and weapon names ship with commas in them, so
    ///     "Main 87243 (Type 12161 - Photon Lance)" would otherwise become three columns.
    /// </summary>
    private static string Escape(string field)
    {
        if (string.IsNullOrEmpty(field))
        {
            return string.Empty;
        }

        return field.Contains(',') || field.Contains('"') || field.Contains('\n')
            ? $"\"{field.Replace("\"", "\"\"")}\""
            : field;
    }

    /// <summary>One target's running total for the fight it is currently in.</summary>
    public class Engagement
    {
        public ulong FirstHitMs { get; init; }

        public ulong LastHitMs { get; set; }

        public int Hits { get; set; }

        public float TotalDamage { get; set; }

        public Dictionary<string, int> PerWeapon { get; } = [];

        /// <summary>
        ///     The weapons that did the work, commonest first, so a kill row still names a culprit when
        ///     several weapons were involved.
        /// </summary>
        public string WeaponSummary()
        {
            return string.Join(
                " + ",
                PerWeapon
                    .OrderByDescending(pair => pair.Value)
                    .Select(pair => $"{pair.Key} x{pair.Value}"));
        }
    }
}
