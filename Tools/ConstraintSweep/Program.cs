// ConstraintSweep: what the shipped battleframe loadouts actually cost in mass (951), power (952)
// and CPU (953).
//
// PIN already sums these without knowing it. ApplyItemStats adds every AttributeRange row on the
// chassis and each slotted item with no filter on the attribute id, and the totals go out in
// CharacterStatsData.ItemAttributes. So the used half of the beta's three-bar budget is already on
// the wire. What nothing knows is the numbers: whether the items a stock loadout slots carry the
// three attributes at all, what the totals come to, and therefore what a frame's capacity would
// have to be for a stock loadout to fit. Those numbers have to exist before per-frame capacities
// can be authored, and they can't be lifted from the beta screenshots because beta priced items
// differently (Docs/streams/battleframe-constraints.md).
//
// The walk mirrors CharacterLoadout exactly: chassis first, then every slot the server counts,
// through SDBInterface.GetItemAttributeRange rather than a reimplementation of it. Weapons are
// totalled separately because PIN leaves them out of the sum and the beta panel didn't.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FauFau.Formats;
using GameServer.Data;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbcharacter;
using GameServer.StaticDB.Records.dbitems;

const ushort MassAttr = 951;
const ushort PowerAttr = 952;
const ushort CpuAttr = 953;

// The one loadout the reference shots price in full: a Raptor at 18 of 30 unlocks.
// Docs/UI Reference/. Used only to state retail's costs as a fraction of a known beta budget.
const float BetaMassUsed = 691f;
const float BetaMassCap = 1400f;
const float BetaPowerUsed = 446f;
const float BetaPowerCap = 800f;
const float BetaCpuUsed = 8f;
const float BetaCpuCap = 13f;

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

Console.WriteLine($"ConstraintSweep: Reading SDB: {input}");
var sdb = new StaticDB();
sdb.Read(input);
Console.WriteLine("Initializing SDBInterface (the server's own loader)");
SDBInterface.Init(sdb);
Console.WriteLine($"  patch {sdb.Patch}, built {sdb.Timestamp:u}, flags {sdb.Flags}, {sdb.Tables.Count} tables");

// The table dictionaries are private statics on SDBInterface. A read-only tool has no business
// widening the server's API for them, so it reflects them out instead.
static TDict Table<TDict>(string field) =>
    (TDict)typeof(SDBInterface)
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Static)!
        .GetValue(null)!;

var attributeRange = Table<Dictionary<KeyValuePair<uint, ushort>, AttributeRange>>("_attributeRange");
var charCreateLoadouts = Table<Dictionary<uint, CharCreateLoadout>>("_charCreateLoadout");

var localized = ReadLocalizedText(sdb);
Console.WriteLine($"  {attributeRange.Count} attribute ranges, {charCreateLoadouts.Count} char-create loadouts, {localized.Count} English strings");

if (localized.Count == 0)
{
    Console.Error.WriteLine(
        "Error: no localized text. Point 'input' at the full clientdb.sd2. A pruned db has no dblocalization::LocalizedText and every name would come out blank.");
    return 2;
}

string Loc(uint id) => localized.GetValueOrDefault(id, string.Empty);

string ItemName(uint sdbId)
{
    var item = SDBInterface.GetRootItem(sdbId);
    var name = item != null ? Loc(item.NameId) : string.Empty;
    return name.Length > 0 ? name : $"item {sdbId}";
}

byte ItemTier(uint sdbId) => SDBInterface.GetRootItem(sdbId)?.TierId ?? 0;

// Validation. Everything below is keyed on three attribute ids that came out of a db inspection,
// not out of the code, so confirm them against the db before trusting a single number.
var expectedNames = new (ushort Id, string Label, string[] Tokens)[]
{
    (MassAttr, "Mass", ["mass", "weight"]),
    (PowerAttr, "Power", ["power"]),
    (CpuAttr, "CPU", ["cpu", "core"]),
};

var idsOk = true;
var idNotes = new List<string>();

foreach (var (id, label, tokens) in expectedNames)
{
    var def = SDBInterface.GetAttributeDefinition(id);
    if (def == null)
    {
        idNotes.Add($"{id} ({label}): NO ROW in dbitems::AttributeDefinition");
        idsOk = false;
        continue;
    }

    var internalName = def.Name ?? string.Empty;
    var displayName = Loc(def.LocalizedNameId);
    var matched = tokens.Any(t =>
        internalName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
        displayName.Contains(t, StringComparison.OrdinalIgnoreCase));

    idNotes.Add($"{id} ({label}): internal \"{internalName}\", display \"{displayName}\", category {def.AttributeCategory}, inverse={def.Inverse}{(matched ? string.Empty : "  <-- NAME DOES NOT LOOK RIGHT")}");
    idsOk &= matched;
}

foreach (var note in idNotes)
{
    Console.WriteLine("  " + note);
}

if (!idsOk)
{
    Console.Error.WriteLine("VALIDATION FAILED: attribute 951/952/953 don't resolve to mass/power/cpu in this db. Every total below would be measuring something else.");
    return 3;
}

// The cost table as a whole, which doubles as a check against work already done on this db.
// Docs/Wiki/Reference/Attributes.md, generated from it, counts 1081 items on mass, 1108 on power
// and 373 on CPU, and Restoration's audit found power at exactly half of mass in 1,006 of 1,015
// tuned rows. If those come back the same, the sweep is reading what they read.
var itemCosts = new Dictionary<uint, Cost>();

foreach (var (key, range) in attributeRange)
{
    if (key.Value is not (MassAttr or PowerAttr or CpuAttr))
    {
        continue;
    }

    itemCosts.TryGetValue(key.Key, out var acc);
    itemCosts[key.Key] = key.Value switch
    {
        MassAttr => acc with { Mass = range.Base },
        PowerAttr => acc with { Power = range.Base },
        _ => acc with { Cpu = range.Base },
    };
}

var negativeRows = attributeRange.Count(p => p.Key.Value is MassAttr or PowerAttr or CpuAttr && p.Value.Base < 0);
var positiveRows = attributeRange.Count(p => p.Key.Value is MassAttr or PowerAttr or CpuAttr && p.Value.Base > 0);

// Costs are stored negative, so a magnitude is what a bar would draw. Everything downstream works
// in magnitudes and this is the only place the sign is read.
static float Mag(float v) => Math.Abs(v);

string Histogram(Func<Cost, float> pick) => string.Join(", ", itemCosts.Values
    .Select(c => Mag(pick(c)))
    .Where(v => v != 0)
    .GroupBy(v => v)
    .OrderBy(g => g.Key)
    .Select(g => $"{g.Key:0.##} ({g.Count()})"));

var pricedInMass = itemCosts.Count(p => Mag(p.Value.Mass) != 0);
var pricedInPower = itemCosts.Count(p => Mag(p.Value.Power) != 0);
var pricedInCpu = itemCosts.Count(p => Mag(p.Value.Cpu) != 0);
var powerWithoutMass = itemCosts.Count(p => Mag(p.Value.Power) != 0 && Mag(p.Value.Mass) == 0);
var massWithoutPower = itemCosts.Count(p => Mag(p.Value.Mass) != 0 && Mag(p.Value.Power) == 0);

var bothPriced = itemCosts.Where(p => Mag(p.Value.Mass) != 0 && Mag(p.Value.Power) != 0).ToList();
var halfRule = bothPriced.Where(p => Math.Abs(Mag(p.Value.Power) - (Mag(p.Value.Mass) / 2f)) < 0.001f).ToList();
var halfRuleBreaks = bothPriced.Except(halfRule)
    .OrderBy(p => p.Key)
    .Select(p => $"{p.Key} \"{ItemName(p.Key)}\" tier {ItemTier(p.Key)}: mass {Mag(p.Value.Mass):0.##}, power {Mag(p.Value.Power):0.##}, cpu {Mag(p.Value.Cpu):0.##}")
    .ToList();

Console.WriteLine($"  {itemCosts.Count} items carry at least one of the three; {negativeRows} rows negative, {positiveRows} positive");
Console.WriteLine($"  priced in mass {pricedInMass}, power {pricedInPower}, cpu {pricedInCpu} (reference doc says 1081 / 1108 / 373)");
Console.WriteLine($"  power == mass/2 in {halfRule.Count} of {bothPriced.Count} items priced in both");

// Per-item lookup, memoised because GetItemAttributeRange is a linear scan of the whole table.
var costCache = new Dictionary<uint, Cost>();

Cost CostsOf(uint itemId)
{
    if (costCache.TryGetValue(itemId, out var cached))
    {
        return cached;
    }

    var ranges = SDBInterface.GetItemAttributeRange(itemId);
    float Pick(ushort attr) => ranges.TryGetValue(attr, out var r) ? r.Base : 0f;
    var cost = new Cost(Pick(MassAttr), Pick(PowerAttr), Pick(CpuAttr));
    costCache[itemId] = cost;
    return cost;
}

LoadoutLine SweepLoadout(uint loadoutId, uint chassisId, bool isGranted)
{
    var row = SDBInterface.GetCharCreateLoadout(loadoutId);
    var slots = SDBUtils.GetDefaultLoadoutSlots(loadoutId);
    var lines = new List<SlotLine>();

    var chassis = CostsOf(chassisId);
    var counted = new Cost();
    var weapons = new Cost();
    var other = new Cost();

    if (slots != null)
    {
        foreach (var (slotByte, record) in slots.OrderBy(p => p.Key))
        {
            var itemId = record.DefaultPveModule;
            if (itemId == 0)
            {
                continue;
            }

            var slot = (LoadoutSlotType)slotByte;
            var isCounted = CharacterLoadout.LoadoutAbilitySlots.Contains(slot) || CharacterLoadout.LoadoutChassisSlots.Contains(slot);
            var isWeapon = CharacterLoadout.LoadoutWeaponSlots.Contains(slot);
            var cost = CostsOf(itemId);

            if (isCounted)
            {
                counted += cost;
            }
            else if (isWeapon)
            {
                weapons += cost;
            }
            else
            {
                other += cost;
            }

            lines.Add(new SlotLine(
                slotByte,
                Enum.IsDefined(slot) ? slot.ToString() : $"slot {slotByte}",
                itemId,
                ItemName(itemId),
                ItemTier(itemId),
                cost,
                isCounted,
                isWeapon));
        }
    }

    return new LoadoutLine(
        loadoutId,
        row?.Name ?? string.Empty,
        chassisId,
        ItemName(chassisId),
        row != null && row.FrameId != chassisId ? row.FrameId : 0,
        row == null,
        isGranted,
        row?.IsDev == 1,
        row?.IsExperimental == 1,
        row?.IsStartingLoadout == 1,
        chassis,
        counted,
        weapons,
        other,
        lines);
}

// The loadouts PIN actually grants. TempCharCreateLoadouts is the map from char-create loadout to
// chassis item that every frame purchase goes through, so this is the shipped set in the only sense
// that matters to a running server.
var granted = HardcodedCharacterData.TempCharCreateLoadouts
    .OrderBy(p => p.Key)
    .Select(p => SweepLoadout(p.Key, p.Value, true))
    .ToList();

var grantedIds = granted.Select(l => l.LoadoutId).ToHashSet();
var rest = charCreateLoadouts.Values
    .Where(l => !grantedIds.Contains(l.Id))
    .OrderBy(l => l.Id)
    .Select(l => SweepLoadout(l.Id, l.FrameId, false))
    .ToList();

Console.WriteLine($"  swept {granted.Count} granted loadouts and {rest.Count} others");

var md = new StringBuilder();
md.AppendLine("# What the shipped loadouts cost in mass, power and CPU");
md.AppendLine();
md.AppendLine("Generated by `Tools/ConstraintSweep`. Every number is retail 1962 data read through the");
md.AppendLine("server's own loader, summed the way `CharacterLoadout.ApplyItemStats` sums it.");
md.AppendLine();
md.AppendLine("Costs are stored negative in `dbitems::AttributeRange` because they spend against a budget.");
md.AppendLine("They're printed here as magnitudes, which is what a bar would draw.");
md.AppendLine();
md.AppendLine("## The three attribute ids");
md.AppendLine();

foreach (var note in idNotes)
{
    md.AppendLine($"- {note}");
}

md.AppendLine();
md.AppendLine("## The cost table as a whole");
md.AppendLine();
md.AppendLine($"- {itemCosts.Count} items carry at least one of the three.");
md.AppendLine($"- Priced in mass {pricedInMass}, in power {pricedInPower}, in CPU {pricedInCpu}. `Docs/Wiki/Reference/Attributes.md`, generated from this db, says 1081 / 1108 / 373.");
md.AppendLine($"- {powerWithoutMass} items are priced in power but not mass, {massWithoutPower} the other way round.");
md.AppendLine($"- Sign: {negativeRows} rows negative, {positiveRows} positive.");
md.AppendLine($"- Mass values: {Histogram(c => c.Mass)}");
md.AppendLine($"- Power values: {Histogram(c => c.Power)}");
md.AppendLine($"- CPU values: {Histogram(c => c.Cpu)}");
md.AppendLine($"- Power is exactly half of mass in {halfRule.Count} of {bothPriced.Count} items priced in both.");
md.AppendLine();

if (halfRuleBreaks.Count > 0)
{
    md.AppendLine("The items that break the half rule are the interesting ones: a ratio the formula can't");
    md.AppendLine("produce marks a row that was hand-authored and survived the rebalances.");
    md.AppendLine();

    foreach (var line in halfRuleBreaks)
    {
        md.AppendLine($"- {line}");
    }

    md.AppendLine();
}

AppendSection("The loadouts PIN grants", granted);
AppendSection("Every other char-create loadout", rest);

md.AppendLine("## Capacity arithmetic");
md.AppendLine();
md.AppendLine("The beta Raptor at 18 of 30 unlocks sat at 691/1400 mass, 446/800 power, 8/13 CPU, so it");
md.AppendLine("spent 49% of its mass budget, 56% of its power and 62% of its CPU. Applying those same");
md.AppendLine("fractions to what retail's items cost gives the capacity each frame would need for its own");
md.AppendLine("stock loadout to sit where the beta's did. This is arithmetic on one screenshot, not a");
md.AppendLine("recommendation, and it says nothing about how much room a fully-built loadout needs.");
md.AppendLine();
md.AppendLine("| Frame | Mass used | -> capacity | Power used | -> capacity | CPU used | -> capacity |");
md.AppendLine("|---|---|---|---|---|---|---|");

foreach (var l in granted)
{
    var used = l.Total;
    md.AppendLine(
        $"| {l.FrameName} | {Mag(used.Mass):0.##} | {Mag(used.Mass) * BetaMassCap / BetaMassUsed:0} | " +
        $"{Mag(used.Power):0.##} | {Mag(used.Power) * BetaPowerCap / BetaPowerUsed:0} | " +
        $"{Mag(used.Cpu):0.##} | {Mag(used.Cpu) * BetaCpuCap / BetaCpuUsed:0} |");
}

md.AppendLine();

void AppendSection(string title, List<LoadoutLine> lines)
{
    md.AppendLine($"## {title} ({lines.Count})");
    md.AppendLine();

    if (lines.Count == 0)
    {
        md.AppendLine("None.");
        md.AppendLine();
        return;
    }

    md.AppendLine("`counted` is what PIN sums today: the chassis plus every ability and gear slot.");
    md.AppendLine("`+weapons` adds the primary and secondary, which go out in their own arrays instead and");
    md.AppendLine("which the beta panel charged for. `priced` is how many filled slots carry any cost at all.");
    md.AppendLine();
    md.AppendLine("| Loadout | Frame | Mass | Power | CPU | Mass +wpn | Power +wpn | CPU +wpn | priced |");
    md.AppendLine("|---|---|---|---|---|---|---|---|---|");

    foreach (var l in lines)
    {
        var c = l.Chassis + l.Counted;
        var t = l.Total;
        md.AppendLine(
            $"| {l.LoadoutId} {l.LoadoutName} | {l.FrameName} | {Mag(c.Mass):0.##} | {Mag(c.Power):0.##} | {Mag(c.Cpu):0.##} | " +
            $"{Mag(t.Mass):0.##} | {Mag(t.Power):0.##} | {Mag(t.Cpu):0.##} | {l.PricedSlots}/{l.Slots.Count} |");
    }

    md.AppendLine();

    foreach (var l in lines)
    {
        var flags = new List<string>();
        if (l.IsDev)
        {
            flags.Add("dev");
        }

        if (l.IsExperimental)
        {
            flags.Add("experimental");
        }

        if (l.IsStarting)
        {
            flags.Add("starting");
        }

        if (l.FrameIdMismatch != 0)
        {
            flags.Add($"db frame_id {l.FrameIdMismatch} != chassis {l.ChassisId}");
        }

        if (l.MissingRow)
        {
            flags.Add("no dbcharacter::CharCreateLoadout row for this id");
        }

        md.AppendLine($"### {l.LoadoutId} {l.LoadoutName} on {l.FrameName} ({l.ChassisId}){(flags.Count > 0 ? $" [{string.Join(", ", flags)}]" : string.Empty)}");
        md.AppendLine();

        if (l.Slots.Count == 0)
        {
            md.AppendLine("No default PvE modules on any slot.");
            md.AppendLine();
            continue;
        }

        md.AppendLine("| Slot | Item | Tier | Mass | Power | CPU | in sum |");
        md.AppendLine("|---|---|---|---|---|---|---|");
        md.AppendLine($"| chassis | {l.FrameName} ({l.ChassisId}) | {ItemTier(l.ChassisId)} | {Mag(l.Chassis.Mass):0.##} | {Mag(l.Chassis.Power):0.##} | {Mag(l.Chassis.Cpu):0.##} | yes |");

        foreach (var s in l.Slots)
        {
            var inSum = s.Counted ? "yes" : s.Weapon ? "no, weapon" : "no";
            md.AppendLine($"| {s.SlotName} | {s.ItemName} ({s.ItemId}) | {s.Tier} | {Mag(s.Cost.Mass):0.##} | {Mag(s.Cost.Power):0.##} | {Mag(s.Cost.Cpu):0.##} | {inSum} |");
        }

        md.AppendLine();
    }
}

var payload = new
{
    Source = input,
    AttributeIds = idNotes,
    Table = new
    {
        ItemsWithAnyCost = itemCosts.Count,
        PricedInMass = pricedInMass,
        PricedInPower = pricedInPower,
        PricedInCpu = pricedInCpu,
        PowerWithoutMass = powerWithoutMass,
        MassWithoutPower = massWithoutPower,
        NegativeRows = negativeRows,
        PositiveRows = positiveRows,
        PricedInBoth = bothPriced.Count,
        PowerIsHalfOfMass = halfRule.Count,
        HalfRuleBreaks = halfRuleBreaks,
    },
    Granted = granted,
    Others = rest,
};

File.WriteAllText("constraints.md", md.ToString());
File.WriteAllText("constraints.json", JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Wrote constraints.md / constraints.json to {Environment.CurrentDirectory}");
Console.WriteLine();

Console.WriteLine($"{"Frame",-22} {"mass",8} {"power",8} {"cpu",6}   priced");
foreach (var l in granted)
{
    var t = l.Total;
    Console.WriteLine($"{l.FrameName,-22} {Mag(t.Mass),8:0.##} {Mag(t.Power),8:0.##} {Mag(t.Cpu),6:0.##}   {l.PricedSlots}/{l.Slots.Count}");
}

var anyCost = granted.Any(l => !l.Total.IsZero);
if (!anyCost)
{
    Console.WriteLine();
    Console.WriteLine("Every granted loadout totals zero. The 1,125 priced items are all off the stock loadouts,");
    Console.WriteLine("which means authored capacities can't be calibrated against stock and the bars would read 0.");
}

return 0;

static Dictionary<uint, string> ReadLocalizedText(StaticDB db)
{
    var result = new Dictionary<uint, string>();
    int index = db.GetIndexByName("dblocalization::LocalizedText");

    if (index == -1)
    {
        return result;
    }

    var table = db.Tables[index];
    int idColumn = table.GetColumnIndexByName("id");
    int englishColumn = table.GetColumnIndexByName("english");

    if (idColumn == -1 || englishColumn == -1)
    {
        return result;
    }

    foreach (var row in table.Rows)
    {
        if (row[idColumn] == null || row[englishColumn] is not string text)
        {
            continue;
        }

        var id = Convert.ToUInt32(row[idColumn], CultureInfo.InvariantCulture);
        text = text.Trim('\0', ' ');

        if (id != 0 && text.Length > 0)
        {
            result.TryAdd(id, text);
        }
    }

    return result;
}

internal readonly record struct Cost(float Mass, float Power, float Cpu)
{
    public bool IsZero => Mass == 0 && Power == 0 && Cpu == 0;

    public static Cost operator +(Cost a, Cost b) => new(a.Mass + b.Mass, a.Power + b.Power, a.Cpu + b.Cpu);
}

internal record SlotLine(byte SlotType, string SlotName, uint ItemId, string ItemName, byte Tier, Cost Cost, bool Counted, bool Weapon);

internal record LoadoutLine(
    uint LoadoutId,
    string LoadoutName,
    uint ChassisId,
    string FrameName,
    uint FrameIdMismatch,
    bool MissingRow,
    bool Granted,
    bool IsDev,
    bool IsExperimental,
    bool IsStarting,
    Cost Chassis,
    Cost Counted,
    Cost Weapons,
    Cost Other,
    List<SlotLine> Slots)
{
    public Cost Total => Chassis + Counted + Weapons;

    public int PricedSlots => Slots.Count(s => !s.Cost.IsZero);
}
