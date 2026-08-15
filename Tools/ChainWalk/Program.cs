// ChainWalk reads the shape of retail's aptitude scripts out of apt::BaseCommandDef.
//
// Every script step carries only (id, subtype, next). The parameter tables for server-side
// commands never shipped, so a chain reads as structure with blank leaves. That structure is
// what M7 wants: how many spawn steps a real encounter used, in what order, and which signals
// fired between them.
//
// Usage: ChainWalk <clientdb.sd2> [maxExamples]

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FauFau.Formats;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: ChainWalk <clientdb.sd2> [maxExamples]");
    return 1;
}

int maxExamples = args.Length > 1 ? int.Parse(args[1]) : 12;

var db = new StaticDB();
db.Read(args[0]);
Console.WriteLine($"patch {db.Patch}, built {db.Timestamp:u}, flags {db.Flags}, {db.Tables.Count} tables");

// The db's own apt::CommandType leaves the name columns blank on every server-side command, so
// the id-to-name map comes from the server's enum instead, parsed straight out of the source.
var enumPath = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "..", "UdpHosts", "GameServer", "Systems", "Aptitude", "CommandType.cs"));

if (!File.Exists(enumPath))
{
    Console.Error.WriteLine($"Error: CommandType.cs not found at {enumPath}");
    return 3;
}

var nameBySubtype = new Dictionary<uint, string>();

foreach (var line in File.ReadAllLines(enumPath))
{
    var m = Regex.Match(line, @"^\s*(\w+)\s*=\s*(\d+)\s*,?\s*$");
    if (m.Success)
    {
        nameBySubtype[uint.Parse(m.Groups[2].Value)] = m.Groups[1].Value;
    }
}

Console.WriteLine($"CommandType enum: {nameBySubtype.Count} named commands");

var bcIdx = db.GetIndexByName("apt::BaseCommandDef");

if (bcIdx == -1)
{
    Console.Error.WriteLine("Error: apt::BaseCommandDef is absent. Run against the original clientdb.sd2, not a pruned one.");
    return 2;
}

var bc = db.Tables[bcIdx];
int bId = bc.GetColumnIndexByName("id");
int bSub = bc.GetColumnIndexByName("subtype");
int bNext = bc.GetColumnIndexByName("next");

if (bId == -1 || bSub == -1 || bNext == -1)
{
    Console.Error.WriteLine("Error: apt::BaseCommandDef is missing an expected column.");
    return 2;
}

var subtypeOf = new Dictionary<uint, uint>();
var nextOf = new Dictionary<uint, uint>();

foreach (var row in bc.Rows)
{
    var id = Convert.ToUInt32(row[bId]);
    subtypeOf[id] = Convert.ToUInt32(row[bSub]);
    nextOf[id] = row[bNext] == null ? 0u : Convert.ToUInt32(row[bNext]);
}

Console.WriteLine($"apt::BaseCommandDef: {subtypeOf.Count} steps");

// Who points at whom. A step with no predecessor is a chain head.
var predsOf = new Dictionary<uint, List<uint>>();

foreach (var (id, next) in nextOf)
{
    if (next == 0 || !subtypeOf.ContainsKey(next))
    {
        continue;
    }

    if (!predsOf.TryGetValue(next, out var list))
    {
        predsOf[next] = list = new List<uint>();
    }

    list.Add(id);
}

// The population and encounter commands, named as the server enum spells them.
string[] interesting =
{
    "EncounterSignal",
    "ActivateSpawnTable",
    "NPCSpawn",
    "EncounterSpawn",
    "DeployableSpawn",
    "NPCBehaviorChange",
    "UpdateSpawnTable",
    "CreateSpawnPoint",
    "SpawnLoot",
};
var interestingSubtypes = new HashSet<uint>(
    nameBySubtype.Where(kv => interesting.Contains(kv.Value)).Select(kv => kv.Key));

Console.WriteLine($"target commands resolved: {interestingSubtypes.Count} of {interesting.Length}");
Console.WriteLine();

string NameOf(uint subtype) => nameBySubtype.TryGetValue(subtype, out var n) ? n : $"unknown({subtype})";

// Walk back from a step to its chain head. A step with several predecessors is a merge point;
// stop there and say so rather than pick one arbitrarily.
(uint Head, bool Merged) FindHead(uint id)
{
    var seen = new HashSet<uint> { id };
    var cur = id;

    while (true)
    {
        if (!predsOf.TryGetValue(cur, out var preds) || preds.Count == 0)
        {
            return (cur, false);
        }

        if (preds.Count > 1)
        {
            return (cur, true);
        }

        var p = preds[0];

        if (!seen.Add(p))
        {
            return (cur, false); // cycle guard
        }

        cur = p;
    }
}

List<uint> WalkForward(uint head)
{
    var chain = new List<uint>();
    var seen = new HashSet<uint>();
    var cur = head;

    while (cur != 0 && subtypeOf.ContainsKey(cur) && seen.Add(cur) && chain.Count < 2000)
    {
        chain.Add(cur);
        cur = nextOf[cur];
    }

    return chain;
}

// Collect every chain that contains at least one population or encounter step.
var chainsByHead = new Dictionary<uint, List<uint>>();
var mergedHeads = new HashSet<uint>();

foreach (var (id, sub) in subtypeOf)
{
    if (!interestingSubtypes.Contains(sub))
    {
        continue;
    }

    var (head, merged) = FindHead(id);

    if (chainsByHead.ContainsKey(head))
    {
        continue;
    }

    chainsByHead[head] = WalkForward(head);

    if (merged)
    {
        mergedHeads.Add(head);
    }
}

Console.WriteLine($"chains containing a population/encounter step: {chainsByHead.Count} ({mergedHeads.Count} start at a merge point, head ambiguous)");

var lengths = chainsByHead.Values.Select(c => c.Count).OrderBy(n => n).ToList();
Console.WriteLine($"chain length: min {lengths.First()}, median {lengths[lengths.Count / 2]}, max {lengths.Last()}");

var lenHist = chainsByHead.Values.GroupBy(c =>
        c.Count <= 3 ? "1-3" : c.Count <= 6 ? "4-6" : c.Count <= 10 ? "7-10" : c.Count <= 20 ? "11-20" : "21+")
    .OrderBy(g => g.Key);
Console.WriteLine("length histogram: " + string.Join(", ", lenHist.Select(g => $"{g.Key}: {g.Count()}")));
Console.WriteLine();

var companion = new Dictionary<string, int>();

foreach (var chain in chainsByHead.Values)
{
    foreach (var name in chain.Select(id => NameOf(subtypeOf[id])).Distinct())
    {
        companion[name] = companion.GetValueOrDefault(name) + 1;
    }
}

Console.WriteLine("commands appearing in these chains (by number of chains):");

foreach (var (name, count) in companion.OrderByDescending(kv => kv.Value).Take(40))
{
    Console.WriteLine($"  {count,5}  {name}");
}

Console.WriteLine();

// Identical command sequences counted once, so the common shapes surface.
var signatures = new Dictionary<string, (int Count, uint ExampleHead)>();

foreach (var (head, chain) in chainsByHead)
{
    var sig = string.Join(" > ", chain.Select(id => NameOf(subtypeOf[id])));
    signatures[sig] = signatures.TryGetValue(sig, out var v) ? (v.Count + 1, v.ExampleHead) : (1, head);
}

Console.WriteLine($"distinct chain shapes: {signatures.Count}");
Console.WriteLine("top shapes:");

foreach (var (sig, (count, exampleHead)) in signatures.OrderByDescending(kv => kv.Value.Count).Take(maxExamples))
{
    Console.WriteLine($"  x{count,-4} (e.g. head {exampleHead})");
    Console.WriteLine($"        {sig}");
}

Console.WriteLine();

// The wave question: does any single chain both spawn something and drive encounter state?
uint SubOf(string name) => nameBySubtype.First(kv => kv.Value == name).Key;
var spawnSubs = new HashSet<uint> { SubOf("NPCSpawn"), SubOf("ActivateSpawnTable"), SubOf("EncounterSpawn") };
var signalSub = SubOf("EncounterSignal");

var waveChains = chainsByHead.Values
    .Where(c => c.Any(id => spawnSubs.Contains(subtypeOf[id])) && c.Any(id => subtypeOf[id] == signalSub))
    .ToList();

Console.WriteLine($"chains that both spawn and signal: {waveChains.Count}");

foreach (var chain in waveChains.OrderByDescending(c => c.Count).Take(maxExamples))
{
    Console.WriteLine($"  head {chain[0]}, {chain.Count} steps:");
    Console.WriteLine($"        {string.Join(" > ", chain.Select(id => NameOf(subtypeOf[id])))}");
}

Console.WriteLine();
Console.WriteLine("longest chains overall:");

foreach (var chain in chainsByHead.Values.OrderByDescending(c => c.Count).Take(maxExamples))
{
    Console.WriteLine($"  head {chain[0]}, {chain.Count} steps:");
    Console.WriteLine($"        {string.Join(" > ", chain.Select(id => NameOf(subtypeOf[id])))}");
}

var spawnCounts = chainsByHead.Values
    .Select(c => c.Count(id => spawnSubs.Contains(subtypeOf[id])))
    .Where(n => n > 0)
    .GroupBy(n => n)
    .OrderBy(g => g.Key);
Console.WriteLine();
Console.WriteLine("spawn steps per chain: " + string.Join(", ", spawnCounts.Select(g => $"{g.Key}: {g.Count()} chains")));

return 0;
