// EffectSweep: enumerate every status effect the client can PREDICT (reached from an ability
// activation chain through commands with allow_prediction=1) and flag the ones whose predicted
// copy cannot end on its own — no self-terminating duration command, removal only external.
//
// Before LocalEffectsController was written (see Docs/In-Game-Tests.md, D5h), every effect in
// that shape was a permanent client-side ghost: the client predicted a copy at keypress, the
// server's clear only touched the observers' StatusEffects_N array, and the prediction lived
// forever. Effect 15253 (Charge's camera lock) was the first confirmed case; this tool finds
// the rest of the class so they can be re-tested against the fix. Validation: 15253 must come
// out under ability 35366.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FauFau.Formats;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.apt;

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

if (string.IsNullOrEmpty(input) || !File.Exists(input))
{
    Console.Error.WriteLine($"Error: config 'input' missing or file not found: {input}");
    return 2;
}

Console.WriteLine($"EffectSweep: Reading SDB: {input}");
var sdb = new StaticDB();
sdb.Read(input);
Console.WriteLine("Initializing SDBInterface (the server's own loader)");
SDBInterface.Init(sdb);

// The table dictionaries are private statics on SDBInterface; a read-only tool has no business
// widening the server's API for them, so it reflects them out instead.
Dictionary<uint, T> Table<T>(string field) =>
    (Dictionary<uint, T>)typeof(SDBInterface)
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!
        .GetValue(null)!;

var effects = Table<StatusEffectData>("_statusEffectData");
var baseCmds = Table<BaseCommandDef>("_baseCommandDef");
var cmdTypes = Table<CommandType>("_commandType");
var abilities = Table<AbilityData>("_abilityData");

// Every loaded *CommandDef table merged into one commandId -> typed row map. Command ids are
// globally unique across tables (they key BaseCommandDef), so the merge is safe.
var defsById = new Dictionary<uint, object>();
foreach (var f in typeof(SDBInterface).GetFields(BindingFlags.NonPublic | BindingFlags.Static))
{
    if (!f.FieldType.IsGenericType || f.FieldType.GetGenericTypeDefinition() != typeof(Dictionary<,>))
    {
        continue;
    }

    var valType = f.FieldType.GetGenericArguments()[1];
    if (!valType.Name.EndsWith("CommandDef") || valType == typeof(BaseCommandDef))
    {
        continue;
    }

    var dict = (System.Collections.IDictionary)f.GetValue(null)!;
    foreach (System.Collections.DictionaryEntry e in dict)
    {
        defsById[(uint)e.Key] = e.Value!;
    }
}

// Ability -> the AbilityModule item ids that carry it. An ability with no module cannot be
// slotted by a player, so the client never activates it and never predicts its chain; those
// candidates are reported separately. The module id doubles as the createitem id for tests.
var modulesByAbility = new Dictionary<uint, List<uint>>();
if (typeof(SDBInterface).GetField("_abilityModule", BindingFlags.NonPublic | BindingFlags.Static)
        ?.GetValue(null) is System.Collections.IDictionary moduleDict)
{
    foreach (System.Collections.DictionaryEntry e in moduleDict)
    {
        var row = e.Value!;
        var abilityId = row.GetType().GetProperty("AbilityChainId")?.GetValue(row) as uint? ?? 0;
        var moduleId = row.GetType().GetProperty("Id")?.GetValue(row) as uint? ?? 0;
        if (abilityId != 0 && moduleId != 0)
        {
            if (!modulesByAbility.TryGetValue(abilityId, out var list))
            {
                modulesByAbility[abilityId] = list = [];
            }

            list.Add(moduleId);
        }
    }
}

Console.WriteLine($"{effects.Count} effects, {baseCmds.Count} base commands, {abilities.Count} abilities, {defsById.Count} typed command rows");

// Names come off the localization table directly since the server never loads it. The text
// column's name doesn't matter: it's the only string-typed column, so find it by probing a row.
var names = new Dictionary<uint, string>();
int locIdx = sdb.GetIndexByName("dblocalization::LocalizedText");
if (locIdx != -1)
{
    var loc = sdb.Tables[locIdx];
    int idCol = loc.GetColumnIndexByName("id");
    int textCol = -1;
    for (int c = 0; c < loc.Columns.Count; c++)
    {
        if (loc.Rows[0][c] is string)
        {
            textCol = c;
            break;
        }
    }

    if (idCol != -1 && textCol != -1)
    {
        foreach (var row in loc.Rows)
        {
            if (row[idCol] is uint id && row[textCol] is string s && s.Trim().Length > 0)
            {
                names[id] = s.Trim();
            }
        }
    }
}

string EffectName(uint effectId) =>
    effects.TryGetValue(effectId, out var e) && names.TryGetValue(e.NameId, out var n) ? n : "";

string AbilityName(uint abilityId) =>
    abilities.TryGetValue(abilityId, out var a) && names.TryGetValue(a.LocalizedNameId, out var n) ? n : "";

List<BaseCommandDef> ChainCmds(uint chainId)
{
    var list = new List<BaseCommandDef>();
    uint next = chainId;
    var guard = new HashSet<uint>();
    while (next != 0 && guard.Add(next) && baseCmds.TryGetValue(next, out var cmd))
    {
        list.Add(cmd);
        next = cmd.Next;
    }

    return list;
}

// Walk what the client's predictor can reach from each ability root. Server-env commands are
// skipped entirely — the client never runs them, which is exactly why an external
// ImpactRemoveEffect could never save a predicted copy. Client-env commands have no loaded def
// table, so any sub-chains they reference are invisible to this walk; that blind spot is
// counted and printed so a future reader knows how big it is.
var predictedApplies = new Dictionary<uint, List<(uint AbilityId, uint CmdId, object Def)>>();
int clientCmdBlindSpots = 0;

void WalkAbility(uint abilityId, uint rootChain)
{
    var seenChains = new HashSet<uint>();
    var seenEffects = new HashSet<uint>();
    var queue = new Queue<uint>();
    queue.Enqueue(rootChain);

    while (queue.Count > 0)
    {
        var chainId = queue.Dequeue();
        if (chainId == 0 || !seenChains.Add(chainId))
        {
            continue;
        }

        foreach (var cmd in ChainCmds(chainId))
        {
            var env = cmdTypes.GetValueOrDefault(cmd.Subtype)?.Environment ?? "?";
            if (env == "server")
            {
                continue;
            }

            if (!defsById.TryGetValue(cmd.Id, out var def))
            {
                if (env == "client")
                {
                    clientCmdBlindSpots++;
                }

                continue;
            }

            var defType = def.GetType();
            foreach (var cp in defType.GetProperties())
            {
                if (cp.PropertyType == typeof(uint) && cp.Name.Contains("Chain") && cp.Name != "AbilityChainId"
                    && cp.GetValue(def) is uint sub && sub != 0)
                {
                    queue.Enqueue(sub);
                }
            }

            // Only commands that actually APPLY their EffectId count. Requirement and target
            // commands (RequireHasEffect, TargetByEffect, ...) reference effects without applying.
            var (effId, predicted) = def switch
            {
                ImpactApplyEffectCommandDef a => (a.EffectId, a.AllowPrediction != 0),
                ImpactToggleEffectCommandDef t => (t.EffectId, t.AllowPrediction != 0),
                _ => (0u, false),
            };

            if (effId != 0 && predicted && effects.TryGetValue(effId, out var eff))
            {
                if (!predictedApplies.TryGetValue(effId, out var hits))
                {
                    predictedApplies[effId] = hits = [];
                }

                hits.Add((abilityId, cmd.Id, def));

                // The predicted copy runs the effect's own chains client-side, and those can
                // predict further effects (15252's apply chain is what predicts 15253).
                if (seenEffects.Add(effId))
                {
                    queue.Enqueue(eff.ApplyChain);
                    queue.Enqueue(eff.UpdateChain);
                    queue.Enqueue(eff.DurationChain);
                    queue.Enqueue(eff.RemoveChain);
                }
            }
        }
    }
}

foreach (var (id, ab) in abilities)
{
    if (ab.Chain != 0)
    {
        WalkAbility(id, ab.Chain);
    }
}

Console.WriteLine($"{predictedApplies.Count} distinct effects applied with prediction; {clientCmdBlindSpots} client-only commands with unloaded defs skipped");

// Shape analysis: a duration chain with a time-based command self-expires and the prediction
// heals itself; anything else waits on a condition that may never flip client-side.
string[] selfTerminating = ["TimeDuration", "ActivationDuration", "TimedActivation"];

(string Summary, bool CanSelfExpire) DurationSummary(StatusEffectData e)
{
    if (e.DurationChain == 0)
    {
        return ("(none)", false);
    }

    var parts = new List<string>();
    bool expires = false;
    foreach (var cmd in ChainCmds(e.DurationChain))
    {
        var t = cmdTypes.GetValueOrDefault(cmd.Subtype);
        var name = t?.Tblname ?? $"type{cmd.Subtype}";
        parts.Add($"{name}[{t?.Environment ?? "?"}]");
        if (selfTerminating.Any(s => name.Contains(s, StringComparison.OrdinalIgnoreCase)))
        {
            expires = true;
        }
    }

    return (string.Join(" → ", parts), expires);
}

string DurationClass(string durSummary)
{
    var tags = new List<string>();
    if (durSummary.Contains("serverconfirmed"))
    {
        tags.Add("SERVERCONFIRMED"); // duration waits on server say-so: the reconciliation channel itself
    }

    if (durSummary.Contains("battleframeduration"))
    {
        tags.Add("FRAME"); // lives until battleframe switch
    }

    if (durSummary.Contains("requirenotrespawned"))
    {
        tags.Add("RESPAWN"); // lives until death/respawn
    }

    if (durSummary.Contains("requirecstate"))
    {
        tags.Add("CSTATE"); // 15253's shape: a character-state check that may never flip
    }

    if (durSummary == "(none)")
    {
        tags.Add("NONE"); // no duration chain at all; external removal only
    }

    return tags.Count > 0 ? string.Join("+", tags) : "OTHER";
}

var report = new List<(bool Equippable, object Row, string MdBlock, uint EffectId)>();

foreach (var (effId, hits) in predictedApplies.OrderBy(kv => kv.Key))
{
    var e = effects[effId];
    var (durSummary, canExpire) = DurationSummary(e);
    if (canExpire)
    {
        continue;
    }

    // Abilities come with hundreds of module item variants (tiers, qualities); any one of them
    // is a valid createitem id for a test, so keep the first few and the count.
    var abilityHits = hits
        .Select(h => h.AbilityId)
        .Distinct()
        .OrderBy(a => a)
        .Select(a =>
        {
            var modules = modulesByAbility.GetValueOrDefault(a, []);
            return new
            {
                Id = a,
                Name = AbilityName(a),
                Modules = modules.Order().Take(3).ToList(),
                ModuleCount = modules.Count,
            };
        })
        .ToList();

    var applyFlags = hits
        .Select(h =>
        {
            var t = h.Def.GetType();
            string Flag(string p) => t.GetProperty(p)?.GetValue(h.Def)?.ToString() ?? "-";
            return $"cmd {h.CmdId} ({t.Name.Replace("CommandDef", string.Empty)}, self={Flag("ApplyToSelf")}, initiator←target={Flag("OverrideInitiatorWithTarget")})";
        })
        .Distinct()
        .ToList();

    var durClass = DurationClass(durSummary);
    var anyEquippable = abilityHits.Any(a => a.ModuleCount > 0);

    var row = new
    {
        EffectId = effId,
        Name = EffectName(effId),
        e.MaxStackCount,
        e.RemoveChain,
        DurationClass = durClass,
        DurationChain = durSummary,
        Abilities = abilityHits,
        ApplyCommands = applyFlags,
    };

    var block = new StringBuilder();
    block.AppendLine($"## Effect {effId}{(EffectName(effId) is { Length: > 0 } n ? $" — {n}" : string.Empty)} [{durClass}]");
    block.AppendLine($"- max_stack_count={e.MaxStackCount}, remove_chain={e.RemoveChain}, duration: {durSummary}");
    block.AppendLine($"- applies: {string.Join("; ", applyFlags)}");
    block.AppendLine($"- abilities: {string.Join(", ", abilityHits.Select(a => $"{a.Id}{(a.Name.Length > 0 ? $" \"{a.Name}\"" : string.Empty)}{(a.ModuleCount > 0 ? $" [module {a.Modules[0]}, {a.ModuleCount} variants]" : string.Empty)}"))}");
    block.AppendLine();

    report.Add((anyEquippable, row, block.ToString(), effId));
}

var md = new StringBuilder();
md.AppendLine("# Prediction-shaped effects (the 15253 signature sweep)");
md.AppendLine();
md.AppendLine("Effects the client predicts (reached from an ability chain via allow_prediction=1) whose");
md.AppendLine("predicted copy cannot end on its own: no self-terminating duration command, removal only");
md.AppendLine("external. Every one of these was a permanent client-side ghost before the");
md.AppendLine("LocalEffectsController fix (Docs/In-Game-Tests.md, D5h). Generated by Tools/EffectSweep.");
md.AppendLine();
md.AppendLine("Duration classes: SERVERCONFIRMED = duration waits on server confirmation (the");
md.AppendLine("reconciliation channel itself); FRAME = until battleframe switch; RESPAWN = until death;");
md.AppendLine("CSTATE = character-state check that may never flip (15253's shape); NONE = no duration");
md.AppendLine("chain, external removal only. [modules: N] is the AbilityModule item id — the createitem");
md.AppendLine("id that puts the ability in reach of a real predicted keypress.");
md.AppendLine();
md.AppendLine($"# Reachable from an equippable ability ({report.Count(r => r.Equippable)})");
md.AppendLine();
foreach (var r in report.Where(r => r.Equippable))
{
    md.Append(r.MdBlock);
}

md.AppendLine($"# Not on any equippable ability — NPC/system chains, never player-predicted ({report.Count(r => !r.Equippable)})");
md.AppendLine();
foreach (var r in report.Where(r => !r.Equippable))
{
    md.Append(r.MdBlock);
}

Console.WriteLine($"{report.Count} ghost-shaped candidates ({report.Count(r => r.Equippable)} on equippable abilities)");

var e15253 = report.FirstOrDefault(r => r.EffectId == 15253);
Console.WriteLine(e15253.Row != null
    ? $"VALIDATION OK: 15253 found:\n{e15253.MdBlock}"
    : "VALIDATION FAILED: 15253 not in candidate list — the walk logic is wrong somewhere");

File.WriteAllText("candidates.md", md.ToString());
File.WriteAllText("candidates.json", JsonSerializer.Serialize(report.Select(r => r.Row), new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote candidates.md / candidates.json to {Environment.CurrentDirectory}");
return e15253.Row != null ? 0 : 3;
