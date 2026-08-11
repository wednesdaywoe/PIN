using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace CaptureReplay;

/// <summary>
///     One UDP datagram lifted out of a capture file.
/// </summary>
public readonly record struct Datagram(ulong Timestamp, string Source, ushort SourcePort, string Destination, ushort DestinationPort, byte[] Payload)
{
    public string SourceEndpoint => $"{Source}:{SourcePort}";

    public string DestinationEndpoint => $"{Destination}:{DestinationPort}";
}

/// <summary>
///     Minimal pcapng reader. Handles the three block types Wireshark writes for a plain
///     capture (section header, interface description, enhanced packet) and unwraps
///     Ethernet/IPv4/UDP. Anything else is skipped rather than treated as an error, since a
///     capture taken on a live machine carries unrelated traffic.
/// </summary>
public static class PcapNg
{
    private const uint BlockSectionHeader = 0x0A0D0D0A;
    private const uint BlockInterfaceDescription = 0x00000001;
    private const uint BlockEnhancedPacket = 0x00000006;
    private const uint ByteOrderMagic = 0x1A2B3C4D;

    private const ushort LinkTypeEthernet = 1;
    private const ushort LinkTypeRaw = 101;

    public static IEnumerable<Datagram> ReadUdp(string path)
    {
        var data = ReadAllBytes(path);
        var linkTypes = new List<ushort>();
        var littleEndian = true;
        var offset = 0;

        while (offset + 12 <= data.Length)
        {
            var blockType = ReadUInt32(data, offset, littleEndian);

            if (blockType == BlockSectionHeader)
            {
                littleEndian = ReadUInt32(data, offset + 8, true) == ByteOrderMagic;
                linkTypes.Clear();
            }

            var blockLength = (int)ReadUInt32(data, offset + 4, littleEndian);
            if (blockLength < 12 || offset + blockLength > data.Length)
            {
                yield break;
            }

            var body = offset + 8;

            if (blockType == BlockInterfaceDescription)
            {
                linkTypes.Add(ReadUInt16(data, body, littleEndian));
            }
            else if (blockType == BlockEnhancedPacket)
            {
                var interfaceId = (int)ReadUInt32(data, body, littleEndian);
                var timestamp = ((ulong)ReadUInt32(data, body + 4, littleEndian) << 32) | ReadUInt32(data, body + 8, littleEndian);
                var capturedLength = (int)ReadUInt32(data, body + 12, littleEndian);
                var linkType = interfaceId < linkTypes.Count ? linkTypes[interfaceId] : LinkTypeEthernet;

                if (capturedLength > 0 && body + 20 + capturedLength <= data.Length)
                {
                    var frame = new ReadOnlySpan<byte>(data, body + 20, capturedLength);
                    if (TryParseUdp(frame, linkType, timestamp, out var datagram))
                    {
                        yield return datagram;
                    }
                }
            }

            offset += blockLength;
        }
    }

    private static bool TryParseUdp(ReadOnlySpan<byte> frame, ushort linkType, ulong timestamp, out Datagram datagram)
    {
        datagram = default;
        ReadOnlySpan<byte> ip;

        switch (linkType)
        {
            case LinkTypeEthernet:
                if (frame.Length < 14)
                {
                    return false;
                }

                var etherType = BinaryPrimitives.ReadUInt16BigEndian(frame[12..]);
                var ipOffset = 14;

                // Walk past any VLAN tags
                while ((etherType == 0x8100 || etherType == 0x88A8) && frame.Length >= ipOffset + 4)
                {
                    etherType = BinaryPrimitives.ReadUInt16BigEndian(frame[(ipOffset + 2)..]);
                    ipOffset += 4;
                }

                if (etherType != 0x0800 || frame.Length <= ipOffset)
                {
                    return false;
                }

                ip = frame[ipOffset..];
                break;

            case LinkTypeRaw:
                ip = frame;
                break;

            default:
                return false;
        }

        if (ip.Length < 20 || (ip[0] >> 4) != 4 || ip[9] != 17)
        {
            return false;
        }

        var headerLength = (ip[0] & 0x0F) * 4;
        var totalLength = BinaryPrimitives.ReadUInt16BigEndian(ip[2..]);
        if (headerLength < 20 || totalLength < headerLength || totalLength > ip.Length)
        {
            return false;
        }

        var udp = ip[headerLength..totalLength];
        if (udp.Length < 8)
        {
            return false;
        }

        var udpLength = BinaryPrimitives.ReadUInt16BigEndian(udp[4..]);
        var payloadLength = Math.Clamp(udpLength - 8, 0, udp.Length - 8);

        datagram = new Datagram(
            timestamp,
            $"{ip[12]}.{ip[13]}.{ip[14]}.{ip[15]}",
            BinaryPrimitives.ReadUInt16BigEndian(udp),
            $"{ip[16]}.{ip[17]}.{ip[18]}.{ip[19]}",
            BinaryPrimitives.ReadUInt16BigEndian(udp[2..]),
            udp.Slice(8, payloadLength).ToArray());

        return true;
    }

    private static byte[] ReadAllBytes(string path)
    {
        if (!path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            return File.ReadAllBytes(path);
        }

        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var buffer = new MemoryStream();
        gzip.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static uint ReadUInt32(byte[] data, int offset, bool littleEndian)
        => littleEndian
               ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset))
               : BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));

    private static ushort ReadUInt16(byte[] data, int offset, bool littleEndian)
        => littleEndian
               ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset))
               : BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));
}
