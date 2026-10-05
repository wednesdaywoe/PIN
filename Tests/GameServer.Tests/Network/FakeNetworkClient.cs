using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;
using System.Threading;
using Shared.Udp;

namespace GameServer.Tests.Network;

/// <summary>
///     Records what a <see cref="Channel" /> sends back to the client so tests can inspect it.
/// </summary>
internal class FakeNetworkClient : INetworkClient
{
    public List<Memory<byte>> Sent { get; } = [];
    public List<(ChannelType Channel, ushort SequenceNumber)> Acks { get; } = [];

    public ClientStatus NetClientStatus => ClientStatus.Connected;
    public uint SocketId => 1;
    public IPEndPoint RemoteEndpoint => new(IPAddress.Loopback, 0);
    public DateTime NetLastActive => DateTime.Now;
    public ImmutableDictionary<ChannelType, Channel> NetChannels { get; set; } = ImmutableDictionary<ChannelType, Channel>.Empty;
    public IShard AssignedShard => throw new NotSupportedException();
    public ConcurrentQueue<Memory<byte>> SequencedMessages { get; } = new();

    public void Init(IPlayer player, IShard shard, IPacketSender sender) => throw new NotSupportedException();
    public void HandlePacket(ReadOnlyMemory<byte> data, Packet packet) => throw new NotSupportedException();
    public void NetworkTick(double deltaTime, ulong currentTime, CancellationToken ct) => throw new NotSupportedException();

    public void Send(Memory<byte> packet) => Sent.Add(packet);

    public void SendAck(ChannelType forChannel, ushort forSequenceNumber, DateTime? received = null) => Acks.Add((forChannel, forSequenceNumber));

    public void SendDebugChat(string message)
    {
    }

    public void SendDebugLog(string log)
    {
    }
}
