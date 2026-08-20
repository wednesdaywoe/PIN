using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GameServer;
using GameServer.Data;
using GameServer.Data.Persistence;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Entities.Outpost;
using GameServer.Enums;
using GameServer.Physics;
using GameServer.StaticDB;
using GameServer.Systems.Admin;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Chat;
using GameServer.Systems.Encounters;
using GameServer.Systems.EntityManager;
using GameServer.Systems.Hazards;
using GameServer.Systems.MovementRelay;
using GameServer.Systems.Persistence;
using GameServer.Systems.ProjectileSim;
using GameServer.Systems.Resources;
using GameServer.Systems.Spawning;
using GameServer.Systems.SystemEvents;
using GameServer.Systems.WeaponSim;
using Serilog;
using Shared.Udp;
using Xunit;

namespace GameServer.Tests.Inventory;

/// <summary>
///     NET-18 — the arrival flag on a created item. A created item is real and equippable but the
///     client never lists it until the whole inventory is redrawn (it only shows after a full
///     <c>dbg_inventory resend</c>). The partial <c>InventoryUpdate</c> the client declines is
///     missing its arrival flag: retail's ordinary single-item add carries
///     <c>DynamicFlags = 2</c> (the 0x02 "is new" bit an inventory list reads to draw a new row),
///     which is what <c>CreateItem</c> now sends.
/// </summary>
/// <remarks>
///     This reads the item the server stored, which is an exact proxy for the wire value:
///     <c>SendItemUpdate</c> builds <c>ItemsPart1 = [item]</c> and Aero serialises that item's
///     <c>DynamicFlags</c> byte straight onto the wire, so the stored byte is the sent byte. The
///     client actually drawing the row is a live-client question this cannot answer, which is why
///     the run sheet still asks for it.
/// </remarks>
public class ItemArrivalFlagTests
{
    [Fact]
    public void ACreatedItemCarriesTheArrivalFlagThatRetailUses()
    {
        // CreateItem asks SDBInterface for the item's type id. The real SDB reads Firefall's
        // clientdb.sd2 (proprietary, not in the repo), so seed just the RootItem table empty: a
        // null-returning GetRootItem makes CreateItem file the item in the bag, which is all this
        // test needs to reach the flag it checks.
        SeedEmptyRootItems();

        var shard = new FakeShard();
        var client = new CapturingClient(shard);
        var character = new CharacterEntity(shard, 0x1122334455667788UL);

        var inventory = new CharacterInventory(shard, client, character)
        {
            EnablePartialUpdates = true,
        };

        var guid = inventory.CreateItem(1);

        var item = inventory.GetItems().Single(i => i.GUID == guid);

        // Retail's ordinary single-item add carries DynamicFlags = 2 (the 0x02 "is new" arrival
        // flag). PIN used to send 1 (IsBound) and the client never listed the item. The item is
        // new but not bound, so the IsBound bit is deliberately not set.
        Assert.Equal((byte)ItemDynamicFlags.Unk_0x02, item.DynamicFlags);
        Assert.Equal(0, item.DynamicFlags & (byte)ItemDynamicFlags.IsBound);

        // The partial update actually went out — the item was sent, not just stored.
        Assert.NotEmpty(client.Outgoing);
    }

    private static void SeedEmptyRootItems()
    {
        var field = typeof(SDBInterface).GetField("_rootItem", BindingFlags.NonPublic | BindingFlags.Static);
        field!.SetValue(null, Activator.CreateInstance(field.FieldType));
    }

    /// <summary>
    ///     The smallest <see cref="IShard"/> <c>CreateItem</c> needs: a guid source and the clock
    ///     <c>CharacterEntity</c> reads at construction. Everything else throws — nothing in this
    ///     path reaches it.
    /// </summary>
    private sealed class FakeShard : IShard
    {
        private ushort _guid;

        public ulong InstanceId => 0;
        public IDictionary<uint, INetworkPlayer> Clients => throw new NotImplementedException();
        public IDictionary<ulong, IEntity> Entities => throw new NotImplementedException();
        public IDictionary<ulong, IEncounter> Encounters => throw new NotImplementedException();
        public IDictionary<uint, IDictionary<uint, OutpostEntity>> Outposts => throw new NotImplementedException();
        public PhysicsEngine Physics => throw new NotImplementedException();
        public AIEngine AI => throw new NotImplementedException();
        public EventBus EventBus => throw new NotImplementedException();
        public MovementRelay Movement => throw new NotImplementedException();
        public EntityManager EntityMan => throw new NotImplementedException();
        public EncounterManager EncounterMan => throw new NotImplementedException();
        public SpawnGroupSim Spawns => throw new NotImplementedException();
        public AbilitySystem Abilities => throw new NotImplementedException();
        public ProjectileSim ProjectileSim => throw new NotImplementedException();
        public WeaponSim WeaponSim => throw new NotImplementedException();
        public HazardSim Hazards => throw new NotImplementedException();
        public ResourceMapSim Resources => throw new NotImplementedException();
        public ChatService Chat => throw new NotImplementedException();
        public AdminService Admin => throw new NotImplementedException();
        public CharacterStore CharacterStore => throw new NotImplementedException();
        public CharacterSaveSim CharacterSaves => throw new NotImplementedException();
        public uint ZoneId => 0;
        public ILogger Logger => new LoggerConfiguration().CreateLogger();
        public ulong CurrentTimeLong => 0;
        public IDictionary<ushort, Tuple<IEntity, Enums.GSS.Controllers>> EntityRefMap => throw new NotImplementedException();

        public ulong GetNextGuid(byte type) => (ulong)(++_guid);
        public Task<bool> SendAsync(Memory<byte> packet, IPEndPoint endPoint) => Task.FromResult(true);
        public void Run(CancellationToken ct) => throw new NotImplementedException();
        public bool Tick(double deltaTime, ulong currentTime, CancellationToken ct) => throw new NotImplementedException();
        public void NetworkTick(double deltaTime, ulong currentTime, CancellationToken ct) => throw new NotImplementedException();
        public bool MigrateOut(INetworkPlayer player) => throw new NotImplementedException();
        public bool MigrateIn(INetworkPlayer player) => throw new NotImplementedException();
        public ushort AssignNewRefId(IEntity entity, Enums.GSS.Controllers controller) => throw new NotImplementedException();
    }

    /// <summary>
    ///     The smallest <see cref="INetworkClient"/> <c>SendItemUpdate</c> needs: a
    ///     <c>ReliableGss</c> channel to send on and a queue to catch the bytes that land on it.
    /// </summary>
    private sealed class CapturingClient : INetworkClient
    {
        private readonly Channel _reliable;

        public CapturingClient(IShard shard)
        {
            var logger = new LoggerConfiguration().CreateLogger();
            var channels = Channel.GetChannels(this, logger);
            NetChannels = channels.ToImmutableDictionary();
            _reliable = channels[ChannelType.ReliableGss];
        }

        public ImmutableDictionary<ChannelType, Channel> NetChannels { get; }
        public ConcurrentQueue<Memory<byte>> SequencedMessages { get; } = new();

        /// <summary>The bytes the server put on the reliable channel, in order.</summary>
        public IReadOnlyList<Memory<byte>> Outgoing => SequencedMessages.ToArray();

        public ClientStatus NetClientStatus => ClientStatus.Connected;
        public uint SocketId => 0;
        public IPEndPoint RemoteEndpoint => null!;
        public DateTime NetLastActive => default;
        public IShard AssignedShard => null!;
        public void Init(IPlayer player, IShard shard, IPacketSender sender) => throw new NotImplementedException();
        public void HandlePacket(ReadOnlyMemory<byte> data, Packet packet) => throw new NotImplementedException();
        public void NetworkTick(double deltaTime, ulong currentTime, CancellationToken ct) => throw new NotImplementedException();
        public void Send(Memory<byte> packet)
        {
        }

        public void SendAck(ChannelType forChannel, ushort forSequenceNumber, DateTime? received = null) => throw new NotImplementedException();
        public void SendDebugChat(string message) => throw new NotImplementedException();
        public void SendDebugLog(string log) => throw new NotImplementedException();
    }
}
