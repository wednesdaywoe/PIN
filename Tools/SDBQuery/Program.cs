using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.apt;
using AptCommandType = GameServer.Systems.Aptitude.CommandType;

// Queries the Aptitude data in a clientdb.sd2 through the GameServer's own SDB loader.
// The SDB path comes from --sdb <path> or the PIN_SDB environment variable.
var argList = args.ToList();
var sdbArg = argList.IndexOf("--sdb");
string sdbPath = Environment.GetEnvironmentVariable("PIN_SDB");
if (sdbArg >= 0 && sdbArg + 1 < argList.Count)
{
    sdbPath = argList[sdbArg + 1];
    argList.RemoveRange(sdbArg, 2);
}

if (argList.Count == 0 || string.IsNullOrEmpty(sdbPath) || !File.Exists(sdbPath))
{
    Console.Error.WriteLine("""
        Usage: SDBQuery [--sdb <clientdb.sd2>] <query> [args]   (or set PIN_SDB)
          chain <chainId>                       Print a chain and the chains it calls
          ability <abilityId>                   Print an ability's chain and the chains it calls
          ctx <commandId>                       Print the chain a command belongs to
          find <Type> <Property> <value> [text] List <Type>CommandDefs where Property == value, optionally containing text
          after <Type> [depth]                  Most common server-side commands following <Type>
          ops                                   Uses of the non-commutative register operands (exponentiate, subtract, divide)
          unimpl [idFile]                       Command types used by abilities that the Factory does not create, by ability count;
                                                with idFile, only the ability ids listed in it, and each type's ids are printed
          attr <attributeId>...                 Print attribute definitions
          attrs <text>                          Attribute definitions whose name contains <text>, ignoring case
          using <Type>                          Abilities whose chain (or a chain it calls) runs <Type>, with the steps in order
          ammo <ammoId>                         Print an ammo type and the ability chains it runs
          item <itemId>...                      Print an item's attribute ranges with their names
          table <field>                         Print every row of an SDBInterface table, by its field name (e.g. _tinyObject)
          tinyfor <statId>...                   Tiny objects whose spawn status effect's chains read any of these item stats
          effect <effectId>                     Print a status effect's chains
          refs <id>                             Every SDB record with a numeric field equal to <id>, other than its own key
          text <id>...                          English text for localized string ids (e.g. a NameId)
          where <Type>                          Where <Type> runs: an ability's own chain or which phase of an effect, by count
        """);
    return 1;
}

var sdb = new FauFau.Formats.StaticDB();
sdb.Read(sdbPath);
SDBInterface.Init(sdb);

var query = new Query();
var rest = argList.Skip(1).ToArray();
switch (argList[0])
{
    case "chain":
        query.DumpChain(uint.Parse(rest[0]), 0, []);
        break;
    case "ability":
        var chainId = SDBInterface.GetAbilityData(uint.Parse(rest[0])).Chain;
        Console.WriteLine($"Ability {rest[0]}: chain {chainId}");
        query.DumpChain(chainId, 0, []);
        break;
    case "ctx":
        query.ChainOf(uint.Parse(rest[0]));
        break;
    case "find":
        query.Find(rest[0], rest[1], rest[2], rest.Length > 3 ? rest[3] : null);
        break;
    case "after":
        query.After(rest[0], rest.Length > 1 ? int.Parse(rest[1]) : 2);
        break;
    case "ops":
        query.Ops();
        break;
    case "unimpl":
        query.Unimplemented(rest.Length > 0 ? File.ReadAllText(rest[0]).Split((char[])[' ', '\n', '\r', ','], StringSplitOptions.RemoveEmptyEntries).Select(uint.Parse).ToHashSet() : null);
        break;
    case "text":
        {
            var index = sdb.GetIndexByName("dblocalization::LocalizedText");
            var table = sdb.Tables[index];
            int idColumn = table.GetColumnIndexByName("id");
            int englishColumn = table.GetColumnIndexByName("english");
            var wanted = rest.Select(uint.Parse).ToHashSet();
            foreach (var row in table.Rows)
            {
                if (row[idColumn] != null && wanted.Contains(Convert.ToUInt32(row[idColumn])) && row[englishColumn] is string text)
                {
                    Console.WriteLine($"{row[idColumn]}: {text.Trim('\0', ' ')}");
                }
            }

            break;
        }
    case "where":
        query.Where(rest[0]);
        break;
    case "using":
        query.Using(rest[0]);
        break;
    case "ammo":
        var ammo = SDBInterface.GetAmmo(uint.Parse(rest[0]));
        Console.WriteLine(Query.Describe(ammo));
        foreach (var (slot, abilityId) in new[] { ("impact", ammo.AbilityId), ("touch", ammo.TouchAbilityId), ("period", ammo.PeriodAbilityId), ("airburst", ammo.AirburstAbilityId) })
        {
            if (abilityId != 0)
            {
                var chain = SDBInterface.GetAbilityData(abilityId)?.Chain ?? 0;
                Console.WriteLine($"-- {slot} ability {abilityId}, chain {chain}");
                query.DumpChain(chain, 1, []);
            }
        }

        break;
    case "item":
        foreach (var id in rest)
        {
            Console.WriteLine($"== item {id}");
            foreach (var (attributeId, range) in SDBInterface.GetItemAttributeRange(uint.Parse(id)).OrderBy(pair => pair.Key))
            {
                var name = SDBInterface.GetAttributeDefinition(attributeId)?.Name?.Trim();
                Console.WriteLine($"  {attributeId,5} {name,-40} base {range.Base} module {range.ModuleMin}..{range.ModuleMax} per level {range.PerLevel}");
            }
        }

        break;
    case "table":
        query.DumpTable(rest[0]);
        break;
    case "tinyfor":
        query.TinyFor(rest.Select(uint.Parse).ToHashSet());
        break;
    case "effect":
        var effectData = SDBInterface.GetStatusEffectData(uint.Parse(rest[0]));
        Console.WriteLine(Query.Describe(effectData));
        foreach (var (name, effectChain) in new[] { ("Apply", effectData.ApplyChain), ("Update", effectData.UpdateChain), ("Duration", effectData.DurationChain), ("Remove", effectData.RemoveChain) })
        {
            if (effectChain != 0)
            {
                Console.WriteLine($"-- {name} chain {effectChain}");
                query.DumpChain(effectChain, 1, []);
            }
        }

        break;
    case "refs":
        query.Refs(uint.Parse(rest[0]));
        break;
    case "attrs":
        query.Attrs(string.Join(" ", rest));
        break;
    case "attr":
        foreach (var id in rest)
        {
            Console.WriteLine($"{id}: {Query.Describe(SDBInterface.GetAttributeDefinition(uint.Parse(id)))}");
        }

        break;
    default:
        Console.Error.WriteLine($"Unknown query {argList[0]}");
        return 1;
}

return 0;

/// <summary>
///     Reads the SDBInterface tables by reflection, since it only exposes lookups by id
/// </summary>
internal partial class Query
{
    private readonly Dictionary<string, FieldInfo> _fields = typeof(SDBInterface).GetFields(BindingFlags.NonPublic | BindingFlags.Static)
        .Concat(typeof(CustomDBInterface).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
        .GroupBy(f => f.Name.ToLowerInvariant())
        .ToDictionary(g => g.Key, g => g.First());

    public static string Describe(object def) => def == null ? string.Empty : string.Join(" ", def.GetType().GetProperties().Select(p => $"{p.Name}={p.GetValue(def)}"));

    public void DumpChain(uint chainId, int depth, HashSet<uint> seen)
    {
        if (!seen.Add(chainId) || depth > 6)
        {
            return;
        }

        foreach (var b in Commands(chainId))
        {
            var name = TypeName(b);
            var def = Def(name, b.Id);
            Console.WriteLine($"{new string(' ', depth * 2)}{name} [{SDBInterface.GetCommandType(b.Subtype)?.Environment}] {(def == null ? $"Id={b.Id}" : Describe(def))}");
            foreach (var (property, chain) in NestedChains(def).Concat(EffectChains(def)))
            {
                Console.WriteLine($"{new string(' ', (depth * 2) + 1)}{property}:");
                DumpChain(chain, depth + 1, seen);
            }
        }
    }

    public void ChainOf(uint commandId)
    {
        var previous = new Dictionary<uint, uint>();
        foreach (var b in Table<BaseCommandDef>("_basecommanddef").Where(b => b.Next != 0))
        {
            previous[b.Next] = b.Id;
        }

        var head = commandId;
        while (previous.TryGetValue(head, out var p))
        {
            head = p;
        }

        Console.WriteLine($"chain {head}:");
        DumpChain(head, 0, []);
    }

    public void Find(string type, string property, string value, string text)
    {
        foreach (var def in Defs(type))
        {
            var description = Describe(def);
            if (def.GetType().GetProperty(property)?.GetValue(def)?.ToString() == value && (text == null || description.Contains(text, StringComparison.Ordinal)))
            {
                Console.WriteLine(description);
            }
        }
    }

    public void After(string type, int depth)
    {
        var counts = new Dictionary<string, int>();
        foreach (var b in Table<BaseCommandDef>("_basecommanddef").Where(b => TypeName(b) == type))
        {
            var sequence = Commands(b.Next).Where(IsServerSide).Take(depth).Select(TypeName);
            var key = string.Join(" > ", sequence);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        Console.WriteLine($"{type}: {counts.Values.Sum()}");
        foreach (var (sequence, count) in counts.OrderByDescending(kv => kv.Value).Take(20))
        {
            Console.WriteLine($"{count,6}  {sequence}");
        }
    }

    public void Ops()
    {
        foreach (var field in _fields.Values.Where(f => f.Name.EndsWith("commanddef", StringComparison.OrdinalIgnoreCase)))
        {
            if (field.GetValue(null) is not IDictionary table || table.Count == 0)
            {
                continue;
            }

            var defs = table.Values.Cast<object>().ToList();
            var type = defs[0].GetType();
            foreach (var op in type.GetProperties().Where(p => p.Name.EndsWith("regop", StringComparison.OrdinalIgnoreCase)))
            {
                var valueProperty = type.GetProperties().FirstOrDefault(p => op.Name.Length > 5 && p.Name.Equals(op.Name[..^5], StringComparison.OrdinalIgnoreCase))
                                    ?? type.GetProperties().FirstOrDefault(p => p.Name is "RegisterVal" or "Trimsize" or "Value");
                var rows = defs.Select(d => (Op: Convert.ToByte(op.GetValue(d)), Value: valueProperty?.GetValue(d)?.ToString())).Where(r => r.Op is 3 or 4 or 5).ToList();
                if (rows.Count == 0)
                {
                    continue;
                }

                Console.WriteLine($"{type.Name}.{op.Name} (value {valueProperty?.Name ?? "from elsewhere"})");
                foreach (var group in rows.GroupBy(r => r.Op))
                {
                    var values = group.GroupBy(r => r.Value).OrderByDescending(g => g.Count()).Take(10).Select(g => $"{g.Key}x{g.Count()}");
                    Console.WriteLine($"  {(GameServer.Enums.Operand)group.Key}: {group.Count()}  values: {string.Join(", ", values)}");
                }
            }
        }
    }

    public void Where(string type)
    {
        // Every chain reached from an ability or an effect, labelled with the nearest root above it: the ability's
        // own chain (and the branches it calls), or the effect phase it runs in
        var label = new Dictionary<uint, string>();
        var queue = new Queue<(uint Chain, string Label)>();
        foreach (var ability in Table<AbilityData>("_abilitydata").Where(a => a.Chain != 0))
        {
            queue.Enqueue((ability.Chain, "ability"));
        }

        foreach (var effect in Table<StatusEffectData>("_statuseffectdata"))
        {
            foreach (var (name, chain) in new[] { ("Apply", effect.ApplyChain), ("Update", effect.UpdateChain), ("Duration", effect.DurationChain), ("Remove", effect.RemoveChain) })
            {
                if (chain != 0)
                {
                    queue.Enqueue((chain, $"effect {name}"));
                }
            }
        }

        while (queue.Count > 0)
        {
            var (chain, chainLabel) = queue.Dequeue();
            if (!label.TryAdd(chain, chainLabel))
            {
                continue;
            }

            foreach (var b in Commands(chain))
            {
                foreach (var (_, nested) in NestedChains(Def(TypeName(b), b.Id)))
                {
                    queue.Enqueue((nested, chainLabel));
                }
            }
        }

        var counts = new Dictionary<string, int>();
        foreach (var (chain, chainLabel) in label)
        {
            var hits = Commands(chain).Count(b => TypeName(b) == type);
            if (hits > 0)
            {
                counts[chainLabel] = counts.GetValueOrDefault(chainLabel) + hits;
            }
        }

        foreach (var (where, count) in counts.OrderByDescending(kv => kv.Value))
        {
            Console.WriteLine($"{count,6}  {where}");
        }
    }

    public void Using(string type)
    {
        foreach (var ability in Table<AbilityData>("_abilitydata").Where(a => a.Chain != 0).OrderBy(a => a.Id))
        {
            var steps = new List<string>();
            Collect(ability.Chain, steps, [], 0);
            if (steps.Contains(type))
            {
                Console.WriteLine($"{ability.Id}: {string.Join(" > ", steps)}");
            }
        }
    }

    private void Collect(uint chainId, List<string> steps, HashSet<uint> seen, int depth)
    {
        if (!seen.Add(chainId) || depth > 6)
        {
            return;
        }

        foreach (var b in Commands(chainId))
        {
            var name = TypeName(b);
            steps.Add(name);
            var def = Def(name, b.Id);
            foreach (var (_, chain) in NestedChains(def).Concat(EffectChains(def)))
            {
                Collect(chain, steps, seen, depth + 1);
            }
        }
    }

    public void Attrs(string text)
    {
        foreach (var field in _fields.Where(f => f.Key.Contains("attributedefinition", StringComparison.OrdinalIgnoreCase)).Select(f => f.Value))
        {
            if (field.GetValue(null) is not IDictionary table)
            {
                continue;
            }

            foreach (var def in table.Values.Cast<object>())
            {
                var name = def.GetType().GetProperty("Name")?.GetValue(def) as string;
                if (name != null && name.Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine(Describe(def));
                }
            }
        }
    }

    public void DumpTable(string field)
    {
        var match = _fields.FirstOrDefault(f => f.Key.Equals(field, StringComparison.OrdinalIgnoreCase)).Value;
        if (match?.GetValue(null) is not IDictionary table)
        {
            Console.Error.WriteLine($"No table {field}");
            return;
        }

        foreach (var row in table.Values.Cast<object>())
        {
            Console.WriteLine(Describe(row));
        }
    }

    public void TinyFor(HashSet<uint> stats)
    {
        foreach (var tiny in Table<GameServer.StaticDB.Records.dbcharacter.TinyObject>("_tinyobject").OrderBy(t => t.Id))
        {
            var effect = SDBInterface.GetStatusEffectData(tiny.SpawnStatusfxId);
            if (effect == null)
            {
                continue;
            }

            var seen = new HashSet<uint>();
            var read = new List<uint>();
            foreach (var chain in new[] { effect.ApplyChain, effect.UpdateChain, effect.DurationChain, effect.RemoveChain })
            {
                CollectStats(chain, seen, read, 0);
            }

            if (read.Any(stats.Contains))
            {
                Console.WriteLine($"tiny {tiny.Id} effect {effect.Id} (update every {effect.UpdateFrequency} ms) reads stats {string.Join(",", read.Distinct())}  {Describe(tiny)}");
            }
        }
    }

    private void CollectStats(uint chainId, HashSet<uint> seen, List<uint> read, int depth)
    {
        if (chainId == 0 || !seen.Add(chainId) || depth > 6)
        {
            return;
        }

        foreach (var b in Commands(chainId))
        {
            var def = Def(TypeName(b), b.Id);
            if (def?.GetType().GetProperty("Stat")?.GetValue(def) is { } stat)
            {
                read.Add(Convert.ToUInt32(stat));
            }

            foreach (var (_, chain) in NestedChains(def).Concat(EffectChains(def)))
            {
                CollectStats(chain, seen, read, depth + 1);
            }
        }
    }

    public void Refs(uint id)
    {
        foreach (var (name, field) in _fields)
        {
            if (field.GetValue(null) is not IDictionary table)
            {
                continue;
            }

            foreach (DictionaryEntry entry in table)
            {
                var records = entry.Value is IEnumerable list and not string ? list.Cast<object>() : [entry.Value];
                foreach (var record in records)
                {
                    var hits = record?.GetType().GetProperties()
                        .Where(property => property.Name != "Id" && property.GetIndexParameters().Length == 0)
                        .Where(property => property.GetValue(record) is { } value && value is byte or sbyte or short or ushort or int or uint or long or ulong && Convert.ToDecimal(value) == id)
                        .Select(property => property.Name)
                        .ToList();
                    if (hits is { Count: > 0 })
                    {
                        Console.WriteLine($"{name}[{entry.Key}].{string.Join(",", hits)}: {Describe(record)}");
                    }
                }
            }
        }
    }

    public void Unimplemented(HashSet<uint> only)
    {
        var factoryPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "UdpHosts", "GameServer", "Systems", "Aptitude", "Factory.cs"));
        var implemented = File.ReadLines(factoryPath).Select(l => FactoryCaseRegex().Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value).ToHashSet();

        var abilitiesByType = new Dictionary<string, HashSet<uint>>();
        var environmentByType = new Dictionary<string, string>();
        var abilities = Table<AbilityData>("_abilitydata").Where(a => only == null || only.Contains(a.Id)).ToList();
        foreach (var ability in abilities)
        {
            Visit(ability.Chain, ability.Id, [], 0);
        }

        var instancesByType = Table<BaseCommandDef>("_basecommanddef").GroupBy(TypeName).ToDictionary(g => g.Key, g => g.Count());

        Console.WriteLine($"{abilities.Count} abilities, {implemented.Count} command types created by the Factory");
        Console.WriteLine("abilities  instances  env     type");
        foreach (var (name, ids) in abilitiesByType.Where(kv => !implemented.Contains(kv.Key)).OrderByDescending(kv => kv.Value.Count))
        {
            Console.WriteLine($"{ids.Count,9}  {instancesByType.GetValueOrDefault(name),9}  {environmentByType[name],-6}  {name}");
            if (only != null)
            {
                Console.WriteLine($"                             {string.Join(' ', ids.Order())}");
            }
        }

        // Follows nested chains, effects and called abilities so each command type is credited to the abilities that reach it
        void Visit(uint chainId, uint abilityId, HashSet<uint> seen, int depth)
        {
            if (chainId == 0 || !seen.Add(chainId) || depth > 12)
            {
                return;
            }

            foreach (var b in Commands(chainId).Where(IsServerSide))
            {
                var name = TypeName(b);
                environmentByType[name] = SDBInterface.GetCommandType(b.Subtype).Environment;
                if (!abilitiesByType.TryGetValue(name, out var ids))
                {
                    abilitiesByType[name] = ids = [];
                }

                ids.Add(abilityId);

                var def = Def(name, b.Id);
                foreach (var property in def?.GetType().GetProperties().Where(p => p.PropertyType == typeof(uint)) ?? [])
                {
                    var value = (uint)property.GetValue(def);
                    if (property.Name.EndsWith("Chain", StringComparison.Ordinal))
                    {
                        Visit(value, abilityId, seen, depth + 1);
                    }
                    else if (property.Name == "EffectId" && SDBInterface.GetStatusEffectData(value) is { } effect)
                    {
                        foreach (var chain in new[] { effect.ApplyChain, effect.UpdateChain, effect.RemoveChain, effect.DurationChain })
                        {
                            Visit(chain, abilityId, seen, depth + 1);
                        }
                    }
                    else if (property.Name == "AbilityId" && SDBInterface.GetAbilityData(value) is { } called)
                    {
                        Visit(called.Chain, abilityId, seen, depth + 1);
                    }
                }
            }
        }
    }

    private static string TypeName(BaseCommandDef b) => SDBInterface.GetCommandType(b.Subtype) is { } type ? ((AptCommandType)type.Id).ToString() : $"Subtype{b.Subtype}";

    private static bool IsServerSide(BaseCommandDef b) => SDBInterface.GetCommandType(b.Subtype) is { } type && type.Environment != "client";

    private static IEnumerable<BaseCommandDef> Commands(uint chainId)
    {
        for (var next = chainId; next != 0;)
        {
            var b = SDBInterface.GetBaseCommandDef(next);
            if (b == null)
            {
                yield break;
            }

            yield return b;
            next = b.Next;
        }
    }

    // A command that applies an effect runs that effect's chains, which hold most of what an ability does
    private static IEnumerable<(string Property, uint Chain)> EffectChains(object def)
    {
        if (def?.GetType().GetProperty("EffectId")?.GetValue(def) is not uint effectId || SDBInterface.GetStatusEffectData(effectId) is not { } effect)
        {
            yield break;
        }

        foreach (var (name, chain) in new[] { ("Apply", effect.ApplyChain), ("Update", effect.UpdateChain), ("Duration", effect.DurationChain), ("Remove", effect.RemoveChain) })
        {
            if (chain != 0)
            {
                yield return ($"effect {effectId} {name} (every {effect.UpdateFrequency} ms)", chain);
            }
        }
    }

    private static IEnumerable<(string Property, uint Chain)> NestedChains(object def)
    {
        return def?.GetType().GetProperties()
                   .Where(p => p.PropertyType == typeof(uint) && p.Name.EndsWith("Chain", StringComparison.Ordinal))
                   .Select(p => (p.Name, (uint)p.GetValue(def)))
                   .Where(c => c.Item2 != 0)
               ?? [];
    }

    [GeneratedRegex(@"^\s+case CommandType\.(\w+):")]
    private static partial Regex FactoryCaseRegex();

    private IEnumerable<T> Table<T>(string field) => ((IDictionary)_fields[field].GetValue(null)).Values.Cast<T>();

    private IEnumerable<object> Defs(string type) => _fields.TryGetValue($"_{type.ToLowerInvariant()}commanddef", out var field) && field.GetValue(null) is IDictionary table ? table.Values.Cast<object>() : [];

    private object Def(string type, uint id) => _fields.TryGetValue($"_{type.ToLowerInvariant()}commanddef", out var field) && field.GetValue(null) is IDictionary table && table.Contains(id) ? table[id] : null;
}
