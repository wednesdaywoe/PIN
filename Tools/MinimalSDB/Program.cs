using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using FauFau.Formats;

var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "prune";

if (mode is not ("prune" or "dump"))
{
    Console.Error.WriteLine($"Error: unknown mode '{args[0]}'. Expected 'prune' (default) or 'dump'.");
    return 6;
}

var cfgPath = new[] { Environment.CurrentDirectory, AppContext.BaseDirectory }
    .Select(dir => Path.Combine(dir, "config.json"))
    .FirstOrDefault(File.Exists);

if (cfgPath == null)
{
    Console.Error.WriteLine("Error: config.json not found in current or base directory.");
    return 1;
}

var json = JsonDocument.Parse(File.ReadAllText(cfgPath)).RootElement;
string input = json.TryGetProperty("input", out var inp) ? inp.GetString() : null;
string output = json.TryGetProperty("output", out var outP) ? outP.GetString() : null;

if (string.IsNullOrEmpty(input) || (mode == "prune" && string.IsNullOrEmpty(output)))
{
    Console.Error.WriteLine("Error: Invalid config.json. 'input' is always required, 'output' is required for prune mode.");
    return 2;
}

// Built as separate segments rather than one literal, so the separators come out right off Windows too.
var loaderPath = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "..", "UdpHosts", "GameServer", "StaticDB", "Loaders", "StaticDBLoader.cs"));

if (mode == "prune" && !File.Exists(loaderPath))
{
    Console.Error.WriteLine("Error: Missing loader file.");
    return 3;
}

if (!File.Exists(input))
{
    Console.Error.WriteLine("Error: Missing input file.");
    return 4;
}

Console.WriteLine($"MinimalSDB: Reading SDB: {input}");
var sdb = new StaticDB();
sdb.Read(input);

if (mode == "dump")
{
    return Dump(sdb, json);
}

var requiredTableIds = new HashSet<uint>();
var tableRegex = TableNameRegex();

foreach (var line in File.ReadLines(loaderPath))
{
    var match = tableRegex.Match(line);
    if (!match.Success)
    {
        continue;
    }

    var tableName = match.Groups[1].Value;
    int idx = sdb.GetIndexByName(tableName);

    if (idx == -1)
    {
        Console.Error.WriteLine($"Unknown table in loader: {tableName}\n  {line.Trim()}");
        return 5;
    }

    requiredTableIds.Add(sdb.Tables[idx].Id);
}

int initialCount = sdb.Tables.Count;
sdb.Tables.RemoveAll(t => !requiredTableIds.Contains(t.Id));
Console.WriteLine($"Pruned {initialCount - sdb.Tables.Count} unused tables. Keeping {sdb.Tables.Count}.");

Console.WriteLine($"MinimalSDB: Writing SDB: {output}");
sdb.Write(output);
return 0;

// Reports whether the tables behind an unfinished server feature actually shipped with usable numbers.
// A table can fail three ways and they call for different work: absent means the client never got it,
// empty means the schema shipped without rows, and all-zero means the column is there but was never
// tuned. Only the fourth case, real spread in the values, means you can build against the data.
int Dump(StaticDB db, JsonElement cfg)
{
    Console.WriteLine($"  patch {db.Patch}, built {db.Timestamp:u}, flags {db.Flags}, {db.Tables.Count} tables");
    Console.WriteLine();

    var sampleRows = 3;
    var tables = DefaultTables();

    if (cfg.TryGetProperty("dump", out var dumpCfg) && dumpCfg.ValueKind == JsonValueKind.Object)
    {
        if (dumpCfg.TryGetProperty("sampleRows", out var sr) && sr.TryGetInt32(out var configured))
        {
            sampleRows = Math.Max(0, configured);
        }

        if (dumpCfg.TryGetProperty("tables", out var configuredTables) && configuredTables.ValueKind == JsonValueKind.Object)
        {
            tables = configuredTables.EnumerateObject().ToDictionary(
                p => p.Name,
                p => p.Value.ValueKind == JsonValueKind.Array
                    ? p.Value.EnumerateArray().Select(v => v.GetString()).Where(s => !string.IsNullOrEmpty(s)).ToArray()
                    : []);
        }
    }

    foreach (var (tableName, columnNames) in tables)
    {
        DumpTable(db, tableName, columnNames, sampleRows);
    }

    return 0;
}

void DumpTable(StaticDB db, string tableName, string[] columnNames, int sampleRows)
{
    Console.WriteLine(tableName);

    int idx = db.GetIndexByName(tableName);
    if (idx == -1)
    {
        Console.WriteLine("  ABSENT: no table by this name. Either it never shipped to the client, or this is an already-pruned db.");
        Console.WriteLine();
        return;
    }

    var table = db.Tables[idx];
    Console.WriteLine($"  {table.Rows.Count} rows, {table.Columns.Count} columns");

    if (table.Rows.Count == 0)
    {
        Console.WriteLine("  EMPTY: schema shipped, rows didn't. Anything built on this has to be improvised.");
        Console.WriteLine();
        return;
    }

    var matched = new Dictionary<string, int>();

    foreach (var columnName in columnNames)
    {
        int col = table.GetColumnIndexByName(columnName);
        if (col == -1)
        {
            Console.WriteLine($"    {columnName,-26} MISSING");
            continue;
        }

        matched[columnName] = col;
        Console.WriteLine($"    {columnName,-26} col {col,3}  {table.Columns[col].Type,-9} {Summarize(table.Rows.Select(r => r[col]).ToList())}");
    }

    if (columnNames.Length == 0)
    {
        var histogram = table.Columns.GroupBy(c => c.Type)
                             .OrderByDescending(g => g.Count())
                             .Select(g => $"{g.Count()} {g.Key}");
        Console.WriteLine($"    column types: {string.Join(", ", histogram)}");
    }
    else if (table.Columns.Count > matched.Count)
    {
        // Column names live in the file as hashes, so there's no way to list the ones you didn't ask for.
        Console.WriteLine($"    ({table.Columns.Count - matched.Count} more column(s) here, only reachable by guessing the name)");
    }

    if (sampleRows > 0 && matched.Count > 0)
    {
        Console.WriteLine($"    first {Math.Min(sampleRows, table.Rows.Count)} row(s):");
        foreach (var row in table.Rows.Take(sampleRows))
        {
            Console.WriteLine("      " + string.Join("  ", matched.Select(c => $"{c.Key}={Format(row[c.Value])}")));
        }
    }

    Console.WriteLine();
}

// Column names use the same snake_case the loader derives from record property names, so these line up
// with the record classes under StaticDB/Records. Override the whole set from config.json.
Dictionary<string, string[]> DefaultTables()
{
    return new Dictionary<string, string[]>
    {
        // Player vitals. Health and shields are hardcoded in HardcodedCharacterData today.
        ["dbitems::Battleframe"] =
        [
            "base_health", "base_shields", "shield_recharge_per_sec", "shield_recharge_delay_ms",
            "base_energy", "energy_recharge_per_sec", "energy_recharge_delay_ms", "damage_response", "scaling_table_id",
        ],

        // Damage type resistance. Both tables load already, nothing reads them.
        ["dbcharacter::DamageResponse"] = ["id", "name", "default_multiplier"],
        ["dbcharacter::DamageResponseDamageType"] = ["damageresponse", "damagetype", "multiplier"],

        // NPC vitals, behind the TODO in CharacterEntity.SetupMonster.
        ["dbcharacter::MonsterScaling"] = ["level", "health", "damage"],
        ["dbcharacter::MonsterAttributeRange"] = ["monster_id", "attribute_id", "base", "per_level", "module_effect"],
        ["dbcharacter::Monster"] = ["damage_response_id", "scaling_table_id", "health_regen"],
        ["dbencounterdata::ScalingTableEntry"] = ["scaling_table_id", "player_count", "health_scale", "damage_scale"],

        // Range decay inputs, so the curve in DamageFalloff can be checked against the numbers it assumes.
        ["dbitems::Ammo"] = ["damage_decay", "damage_decay_rangefrac", "min_damage_frac", "damage_response", "damagetype"],
        ["dbitems::WeaponTemplates"] = ["damage_per_round", "min_damage", "range", "headshot_mult"],
    };
}

static string Summarize(List<object> values)
{
    var present = values.Where(v => v != null).ToList();

    if (present.Count == 0)
    {
        return "every row null";
    }

    if (present.All(IsNumeric))
    {
        var numbers = present.Select(Convert.ToDouble).ToList();
        int nonZero = numbers.Count(n => n != 0d);
        string verdict = nonZero == 0 ? "   <-- ALL ZERO, never tuned" : string.Empty;
        return $"{nonZero}/{values.Count} non-zero, {numbers.Distinct().Count()} distinct, {Number(numbers.Min())}..{Number(numbers.Max())}{verdict}";
    }

    if (present.All(v => v is string))
    {
        var strings = present.Cast<string>().Select(s => s.Replace("\0", string.Empty)).ToList();
        var examples = strings.Where(s => s.Length > 0).Distinct().Take(3).Select(s => $"\"{s}\"");
        return $"{strings.Count(s => s.Length > 0)}/{values.Count} non-empty, {strings.Distinct().Count()} distinct, e.g. {string.Join(", ", examples)}";
    }

    return $"{present.Count}/{values.Count} non-null";
}

static bool IsNumeric(object value) => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double;

static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

static string Format(object value) => value switch
{
    null => "null",
    string s => $"\"{s.Replace("\0", string.Empty)}\"",
    _ when IsNumeric(value) => Number(Convert.ToDouble(value)),
    _ => value.ToString(),
};

partial class Program
{
    [GeneratedRegex(@">\(""([^""]+)""\)")]
    private static partial Regex TableNameRegex();
}
