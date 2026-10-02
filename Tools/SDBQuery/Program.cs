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
          ctx <commandId>                       Print the chain a command belongs to
          find <Type> <Property> <value> [text] List <Type>CommandDefs where Property == value, optionally containing text
          after <Type> [depth]                  Most common server-side commands following <Type>
          ops                                   Uses of the non-commutative register operands (exponentiate, subtract, divide)
          unimpl                                Command types used by abilities that the Factory does not create, by ability count
          attr <attributeId>...                 Print attribute definitions
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
        query.Unimplemented();
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
            Console.WriteLine($"{new string(' ', depth * 2)}{name} [{SDBInterface.GetCommandType(b.Subtype)?.Environment}] {Describe(def)}");
            foreach (var (property, chain) in NestedChains(def))
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

    public void Unimplemented()
    {
        var factoryPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "UdpHosts", "GameServer", "Systems", "Aptitude", "Factory.cs"));
        var implemented = File.ReadLines(factoryPath).Select(l => FactoryCaseRegex().Match(l)).Where(m => m.Success).Select(m => m.Groups[1].Value).ToHashSet();

        var abilitiesByType = new Dictionary<string, HashSet<uint>>();
        var environmentByType = new Dictionary<string, string>();
        var abilities = Table<AbilityData>("_abilitydata").ToList();
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
