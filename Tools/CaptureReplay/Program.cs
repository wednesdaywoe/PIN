using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CaptureReplay;
using Controllers = GameServer.Enums.GSS.Controllers;

var options = CommandLine.Parse(args);
if (options == null)
{
    CommandLine.PrintUsage();
    return 1;
}

Console.WriteLine($"Reading {options.CapturePath}");
var datagrams = PcapNg.ReadUdp(options.CapturePath).Where(d => d.Payload.Length > 0 && d.SourcePort != 53 && d.DestinationPort != 53).ToList();

if (datagrams.Count == 0)
{
    Console.Error.WriteLine("No UDP payloads found.");
    return 1;
}

var (client, server) = SessionReplay.IdentifyEndpoints(datagrams);
server = options.ServerAddress ?? server;
datagrams = datagrams.Where(d => d.Source == server || d.Destination == server).ToList();

var registry = MessageRegistry.Build();
Console.WriteLine($"client {client}  <->  server {server}   ({datagrams.Count} datagrams)");
Console.WriteLine($"registry: {registry.GssCount} GSS (controller, message) pairs from AeroMessages\n");

var replay = new SessionReplay(registry, deserialize: !options.NoDeserialize);
var histogram = new Dictionary<(byte Controller, byte Message, string Name), int>();
var unknownBytes = new Dictionary<(byte Controller, byte Message), int>();
var shown = 0;

foreach (var message in replay.Replay(datagrams, server))
{
    var key = (message.ControllerId, message.MessageId, message.MessageName);
    histogram[key] = histogram.GetValueOrDefault(key) + 1;

    if (message.Definition == null)
    {
        unknownBytes[(message.ControllerId, message.MessageId)] = message.Body.Length;
    }

    if (!options.Matches(message) || shown >= options.DumpCount)
    {
        continue;
    }

    shown++;
    Dump(message, shown);
}

var stats = replay.Stats;
Console.WriteLine("=== framing ===");
Console.WriteLine($"  datagrams            {stats.Datagrams}");
Console.WriteLine($"  handshake            {stats.Handshake}");
Console.WriteLine($"  framing failures     {stats.FramingFailures}");
Console.WriteLine($"  sub-packets          {stats.SubPackets}");
Console.WriteLine($"  resent (XOR undone)  {stats.ResentPackets}");
Console.WriteLine($"  split fragments      {stats.SplitFragments} -> {stats.SplitsReassembled} reassembled");

Console.WriteLine("\n=== channels ===");
foreach (var (channel, count) in stats.ByChannel.OrderByDescending(kv => kv.Value))
{
    Console.WriteLine($"  {channel,-16} {count,8}  {100.0 * count / Math.Max(stats.SubPackets, 1),5:F1}%");
}

Console.WriteLine("\n=== GSS messages ===");
Console.WriteLine($"  total                {stats.GssMessages}");
Console.WriteLine($"  matched a definition {stats.GssMessages - stats.UnknownPairs}  ({100.0 * (stats.GssMessages - stats.UnknownPairs) / Math.Max(stats.GssMessages, 1):F1}%)");
Console.WriteLine($"  deserialized clean   {stats.Deserialized}");
Console.WriteLine($"  deserialize errors   {stats.DeserializeErrors}");
Console.WriteLine($"  no AeroMessages type {stats.UnknownPairs}");

Console.WriteLine($"\n=== top {Math.Min(options.TopCount, histogram.Count)} of {histogram.Count} distinct (controller, message) pairs ===");
foreach (var (key, count) in histogram.OrderByDescending(kv => kv.Value).Take(options.TopCount))
{
    var name = MessageRegistry.ControllerName(key.Controller);
    Console.WriteLine($"  {count,8}  {name,-40} {key.Message,-4} {key.Name}");
}

if (unknownBytes.Count > 0)
{
    Console.WriteLine($"\n=== {unknownBytes.Count} pairs with no AeroMessages definition ===");
    foreach (var ((controller, message), length) in unknownBytes.OrderBy(kv => kv.Key.Controller).ThenBy(kv => kv.Key.Message).Take(options.TopCount))
    {
        Console.WriteLine($"  {MessageRegistry.ControllerName(controller),-40} msg {message,-4} ~{length} byte body");
    }
}

return 0;

void Dump(DecodedMessage message, int ordinal)
{
    var arrow = message.Direction == Direction.ClientToServer ? "C->S" : "S->C";
    Console.WriteLine($"--- [{ordinal}] {arrow} {message.Channel} seq {message.SequenceNumber} entity 0x{message.EntityId:X16}");
    Console.WriteLine($"    {message.ControllerName} / {message.MessageName} (controller {message.ControllerId}, message {message.MessageId})");
    Console.WriteLine($"    {message.Body.Length} bytes: {Convert.ToHexString(message.Body.AsSpan(0, Math.Min(48, message.Body.Length)))}{(message.Body.Length > 48 ? "..." : string.Empty)}");

    if (message.Instance != null)
    {
        PrintValue(message.Instance, "    ", 0);
    }
    else if (message.Definition != null)
    {
        Console.WriteLine("    (definition found but did not deserialize)");
    }

    Console.WriteLine();
}

void PrintValue(object value, string indent, int depth)
{
    if (depth > 3 || value == null)
    {
        return;
    }

    foreach (var member in Members(value.GetType()))
    {
        object member_value;
        try
        {
            member_value = member.Get(value);
        }
        catch (Exception)
        {
            continue;
        }

        if (member_value == null)
        {
            continue;
        }

        var type = member_value.GetType();

        // Never walk into framework types -- a stray System.Type property expands forever
        if (!type.IsPrimitive && !type.IsEnum && member_value is not (string or decimal)
            && (type.Namespace?.StartsWith("System", StringComparison.Ordinal) ?? false)
            && member_value is not IEnumerable)
        {
            continue;
        }

        if (type.IsPrimitive || member_value is string or decimal || type.IsEnum)
        {
            Console.WriteLine($"{indent}  {member.Name} = {member_value}");
        }
        else if (member_value is byte[] bytes)
        {
            Console.WriteLine($"{indent}  {member.Name} = [{bytes.Length}] {Convert.ToHexString(bytes.AsSpan(0, Math.Min(24, bytes.Length)))}");
        }
        else if (member_value is IEnumerable list and not string)
        {
            var items = list.Cast<object>().ToList();
            Console.WriteLine($"{indent}  {member.Name} = [{items.Count}]");
            foreach (var item in items.Take(4))
            {
                PrintValue(item, indent + "    ", depth + 1);
            }
        }
        else
        {
            Console.WriteLine($"{indent}  {member.Name}:");
            PrintValue(member_value, indent + "  ", depth + 1);
        }
    }
}

IEnumerable<(string Name, Func<object, object> Get)> Members(Type type)
{
    // Aero bolts its read-tracing onto every generated type; dumping it buries the message
    // itself under reflection metadata.
    var noise = new HashSet<string> { "DiagLogs", "ReadLogs" };

    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        if (property.GetIndexParameters().Length == 0 && property.CanRead && !noise.Contains(property.Name))
        {
            yield return (property.Name, property.GetValue);
        }
    }

    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
    {
        if (!noise.Contains(field.Name))
        {
            yield return (field.Name, field.GetValue);
        }
    }
}

internal sealed class Options
{
    public string CapturePath { get; init; }

    public string ServerAddress { get; init; }

    public byte? Controller { get; init; }

    public byte? Message { get; init; }

    public ulong? Entity { get; init; }

    public Direction? Direction { get; init; }

    public int DumpCount { get; init; }

    public int TopCount { get; init; } = 25;

    public bool NoDeserialize { get; init; }

    public bool Matches(DecodedMessage message)
        => (Controller == null || message.ControllerId == Controller)
           && (Message == null || message.MessageId == Message)
           && (Entity == null || message.EntityId == Entity)
           && (Direction == null || message.Direction == Direction);
}

internal static class CommandLine
{
    public static Options Parse(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            return null;
        }

        string path = null, server = null;
        byte? controller = null, message = null;
        ulong? entity = null;
        Direction? direction = null;
        var dump = 0;
        var top = 25;
        var noDeserialize = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--server": server = args[++i]; break;
                case "--controller": controller = ParseController(args[++i]); break;
                case "--message": message = byte.Parse(args[++i]); break;
                case "--entity": entity = Convert.ToUInt64(args[++i].Replace("0x", string.Empty), 16); break;
                case "--dump": dump = int.Parse(args[++i]); break;
                case "--top": top = int.Parse(args[++i]); break;
                case "--direction": direction = args[++i] is "c2s" ? CaptureReplay.Direction.ClientToServer : CaptureReplay.Direction.ServerToClient; break;
                case "--no-deserialize": noDeserialize = true; break;
                default:
                    if (path != null)
                    {
                        Console.Error.WriteLine($"Unexpected argument: {args[i]}");
                        return null;
                    }

                    path = args[i];
                    break;
            }
        }

        if (path == null)
        {
            return null;
        }

        // Asking for a filter without a dump count almost always means "show me these"
        if (dump == 0 && (controller != null || message != null || entity != null))
        {
            dump = 10;
        }

        return new Options
               {
                   CapturePath = path,
                   ServerAddress = server,
                   Controller = controller,
                   Message = message,
                   Entity = entity,
                   Direction = direction,
                   DumpCount = dump,
                   TopCount = top,
                   NoDeserialize = noDeserialize
               };
    }

    private static byte ParseController(string value)
        => byte.TryParse(value, out var numeric)
               ? numeric
               : (byte)Enum.Parse<Controllers>(value, ignoreCase: true);

    public static void PrintUsage()
    {
        Console.WriteLine("""
                          CaptureReplay -- decode a Firefall packet capture through PIN's own wire format.

                            CaptureReplay <capture.pcapng[.gz]> [options]

                          Options:
                            --controller <name|id>   Only show this controller, by Enums.GSS.Controllers name or number
                            --message <id>           Only show this message id
                            --entity <0xHEX>         Only show this entity
                            --direction <c2s|s2c>    Only show one side of the conversation
                            --dump <n>               Print field-level detail for the first n matches (default 10 when filtering)
                            --top <n>                Rows in the summary histogram (default 25)
                            --server <ip>            Override server detection
                            --no-deserialize         Framing and histogram only, skip Aero

                          Examples:
                            CaptureReplay capture.pcapng
                            CaptureReplay capture.pcapng --controller Character_LocalEffectsController --dump 5
                            CaptureReplay capture.pcapng --controller Character_CombatController --direction s2c
                          """);
    }
}
