using System;
using System.Collections.Generic;
using System.Linq;
using GameServer;
using GameServer.Enums;
using GameServer.Packets;
using Shared.Udp;

namespace CaptureReplay;

/// <summary>
///     Reads the transport underneath the messages: what got resent, how long the sender waited
///     before resending it, and how the far side acked. PIN has no outbound reliability at all
///     (NET-1), so a real session's numbers are the only thing an implementation can be calibrated
///     against instead of guessed at.
/// </summary>
public sealed class TransportReport
{
    // pcapng timestamps are microseconds unless an interface option says otherwise, and PcapNg
    // doesn't read those options. Every capture in Documentation/ uses the default.
    private const double MicrosecondsPerMs = 1000.0;

    private readonly Dictionary<(Direction Direction, ChannelType Channel), ChannelStats> _channels = new();

    // Keyed by the sequence number rather than by anything monotonic, so a wrap overwrites the
    // older sighting instead of measuring a resend against a packet 65536 sequences ago. The body
    // is kept as a length and a hash so a resend can be compared to its original without holding
    // 20MB of capture in memory.
    private readonly Dictionary<(Direction Direction, ChannelType Channel, ushort Sequence), Sighting> _lastSeen = new();

    private readonly List<Resend> _resends = [];
    private readonly Dictionary<(Direction Direction, ChannelType Channel), AckStats> _acks = new();

    public void Observe(ulong timestamp, Direction direction, GamePacketHeader header, bool sequenced, ushort sequenceNumber, byte[] body)
    {
        var stats = Stats(_channels, (direction, header.Channel));
        stats.Packets++;
        stats.Bytes += header.Length;

        if (header.IsSplit)
        {
            stats.Split++;
        }

        if (sequenced)
        {
            var key = (direction, header.Channel, sequenceNumber);

            var sighting = new Sighting(timestamp, body.Length, Hash(body));

            if (header.ResendCount > 0)
            {
                stats.Resends[header.ResendCount - 1]++;
                var found = _lastSeen.TryGetValue(key, out var original);
                _resends.Add(new Resend(
                    direction,
                    header.Channel,
                    sequenceNumber,
                    header.ResendCount,
                    found ? (timestamp - original.Timestamp) / MicrosecondsPerMs : null,
                    found ? original.Length == sighting.Length && original.Hash == sighting.Hash : null));
            }

            _lastSeen[key] = sighting;
        }

        if (header.Channel == ChannelType.Control)
        {
            ObserveControl(timestamp, direction, body);
        }
    }

    public void Write()
    {
        Console.WriteLine("\n=== transport, by direction and channel ===");
        Console.WriteLine($"  {"dir",-6} {"channel",-14} {"packets",9} {"bytes",12}  {"resends 1/2/3",-18} {"split",6}");
        foreach (var ((direction, channel), stats) in _channels.OrderBy(kv => kv.Key.Direction).ThenBy(kv => kv.Key.Channel))
        {
            Console.WriteLine($"  {Arrow(direction),-6} {channel,-14} {stats.Packets,9} {stats.Bytes,12}  "
                              + $"{stats.Resends[0],5} /{stats.Resends[1],4} /{stats.Resends[2],4}   {stats.Split,6}");
        }

        Console.WriteLine($"\n=== {_resends.Count} resends, and how long the sender waited ===");
        if (_resends.Count == 0)
        {
            Console.WriteLine("  none in this capture");
        }
        else
        {
            foreach (var resend in _resends)
            {
                var delay = resend.DelayMs == null
                                ? "the original never reached the capture point"
                                : $"{resend.DelayMs.Value,8:F1} ms after the original, {(resend.SameBytes == true ? "same bytes" : "DIFFERENT BYTES")}";
                Console.WriteLine($"  {Arrow(resend.Direction),-6} {resend.Channel,-14} seq {resend.Sequence,6}  count {resend.ResendCount}  {delay}");
            }

            var paired = _resends.Where(r => r.SameBytes != null).ToList();
            Console.WriteLine($"\n  {paired.Count(r => r.SameBytes == true)} of {paired.Count} resends whose original is in the capture "
                              + "carry byte-identical payloads once the XOR is undone");

            var measured = _resends.Where(r => r.DelayMs != null).Select(r => r.DelayMs.Value).OrderBy(d => d).ToList();
            if (measured.Count > 0)
            {
                Console.WriteLine($"\n  delay to first resend: min {measured[0]:F0} ms, median {Percentile(measured, 0.5):F0} ms, "
                                  + $"p90 {Percentile(measured, 0.9):F0} ms, max {measured[^1]:F0} ms  ({measured.Count} measured)");
            }
        }

        Console.WriteLine("\n=== acks ===");
        Console.WriteLine("  An ack travels on Control in the opposite direction to the packet it names, so the");
        Console.WriteLine("  counts below pair with the sequenced channel rows above.");
        foreach (var ((direction, channel), stats) in _acks.OrderBy(kv => kv.Key.Direction).ThenBy(kv => kv.Key.Channel))
        {
            var sent = _channels.TryGetValue((Opposite(direction), channel), out var origin) ? origin.Packets : 0;
            Console.WriteLine($"\n  {Arrow(direction)} acking {channel}");
            Console.WriteLine($"    acks sent            {stats.Count}");
            Console.WriteLine($"    packets to ack       {sent}  ({(sent == 0 ? 0 : 100.0 * stats.Count / sent):F1}% acked, one ack per packet would be 100%)");
            Console.WriteLine($"    NextSeqNum == AckForNum + 1 on {stats.NextIsAckPlusOne} of {stats.Count}");

            if (stats.Gaps.Count > 0)
            {
                var gaps = stats.Gaps.OrderBy(g => g).ToList();
                var ones = stats.Gaps.Count(g => g == 1);
                Console.WriteLine($"    step from the last ack: {ones} of {gaps.Count} advance by exactly 1"
                                  + $"  (median {Percentile(gaps.Select(g => (double)g).ToList(), 0.5):F0}, max {gaps[^1]})");
            }

            if (stats.LatenciesMs.Count > 0)
            {
                var latencies = stats.LatenciesMs.OrderBy(l => l).ToList();
                Console.WriteLine($"    ack latency: min {latencies[0]:F1} ms, median {Percentile(latencies, 0.5):F1} ms, "
                                  + $"p90 {Percentile(latencies, 0.9):F1} ms, max {latencies[^1]:F1} ms  ({latencies.Count} matched)");
            }

            if (stats.Unmatched > 0)
            {
                Console.WriteLine($"    {stats.Unmatched} acks named a sequence this capture never saw sent");
            }
        }
    }

    private static double Percentile(IReadOnlyList<double> sorted, double fraction)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Clamp(Math.Round(fraction * (sorted.Count - 1)), 0, sorted.Count - 1);
        return sorted[index];
    }

    private static string Arrow(Direction direction) => direction == Direction.ClientToServer ? "C->S" : "S->C";

    private static Direction Opposite(Direction direction)
        => direction == Direction.ClientToServer ? Direction.ServerToClient : Direction.ClientToServer;

    private static TValue Stats<TKey, TValue>(Dictionary<TKey, TValue> map, TKey key)
        where TValue : new()
    {
        if (!map.TryGetValue(key, out var value))
        {
            value = new TValue();
            map[key] = value;
        }

        return value;
    }

    private void ObserveControl(ulong timestamp, Direction direction, byte[] body)
    {
        if (body.Length < 5)
        {
            return;
        }

        var acked = (ControlPacketType)body[0] switch
        {
            ControlPacketType.MatrixAck => ChannelType.Matrix,
            ControlPacketType.ReliableGSSAck => ChannelType.ReliableGss,
            _ => (ChannelType?)null
        };

        if (acked == null)
        {
            return;
        }

        // MatrixAck and GSSAck have the same two fields in the same order, NextSeqNum then
        // AckForNum, both endian-flipped like every other sequence number on the wire.
        var nextSeqNum = Utils.SimpleFixEndianness(BitConverter.ToUInt16(body, 1));
        var ackForNum = Utils.SimpleFixEndianness(BitConverter.ToUInt16(body, 3));

        var stats = Stats(_acks, (direction, acked.Value));
        stats.Count++;

        if (nextSeqNum == unchecked((ushort)(ackForNum + 1)))
        {
            stats.NextIsAckPlusOne++;
        }

        if (stats.PreviousAck != null)
        {
            // Serial arithmetic, so the step across a wrap reads as 1 rather than -65535
            var step = unchecked((short)(ackForNum - stats.PreviousAck.Value));
            if (step > 0)
            {
                stats.Gaps.Add(step);
            }
        }

        stats.PreviousAck = ackForNum;

        if (_lastSeen.TryGetValue((Opposite(direction), acked.Value, ackForNum), out var sent))
        {
            stats.LatenciesMs.Add((timestamp - sent.Timestamp) / MicrosecondsPerMs);
        }
        else
        {
            stats.Unmatched++;
        }
    }

    private static ulong Hash(byte[] body)
    {
        var hash = 14695981039346656037ul;
        foreach (var b in body)
        {
            hash = (hash ^ b) * 1099511628211ul;
        }

        return hash;
    }

    private readonly record struct Sighting(ulong Timestamp, int Length, ulong Hash);

    private sealed record Resend(Direction Direction, ChannelType Channel, ushort Sequence, byte ResendCount, double? DelayMs, bool? SameBytes);

    private sealed class ChannelStats
    {
        public int Packets { get; set; }

        public long Bytes { get; set; }

        public int Split { get; set; }

        public int[] Resends { get; } = new int[3];
    }

    private sealed class AckStats
    {
        public int Count { get; set; }

        public int NextIsAckPlusOne { get; set; }

        public int Unmatched { get; set; }

        public ushort? PreviousAck { get; set; }

        public List<int> Gaps { get; } = [];

        public List<double> LatenciesMs { get; } = [];
    }
}
