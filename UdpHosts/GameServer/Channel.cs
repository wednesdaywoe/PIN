#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Aero.Gen;
using Aero.Gen.Attributes;
using GameServer.Extensions;
using GameServer.Packets;
using Serilog;
using Shared.Udp;

namespace GameServer;

public class Channel
{
    private const int _protocolHeaderSize = 80; // UDP + IP
    private const int _gameSocketHeaderSize = 4;
    private const int _totalHeaderSize = _protocolHeaderSize + _gameSocketHeaderSize;
    private const int _maxPacketSize = PacketServer.MTU - _totalHeaderSize;

    private static readonly byte[] _xorByte = [0xFF, 0xAA, 0xCC];

    private readonly ILogger _logger;

    private readonly INetworkClient _client;
    private readonly ConcurrentQueue<GamePacket> _incomingPackets;
    private readonly ConcurrentQueue<Memory<byte>> _outgoingPackets;
    private readonly SortedDictionary<ushort, GamePacket> _incomingSplitMessagePackets;

    // Only a sequenced reliable channel has anything to retransmit: an unsequenced one has no way
    // to name the packet it wants back, and an unreliable one is never acked in the first place.
    private readonly RetransmitQueue? _retransmits;

    private readonly DeliveredSequences _delivered = new();

    private Channel(ChannelType channelType, bool isSequenced, bool isReliable,  bool isGSS, INetworkClient networkClient, ILogger logger)
    {
        Type = channelType;
        IsSequenced = isSequenced;
        IsReliable = isReliable;
        IsGSS = isGSS;
        _client = networkClient;
        _logger = logger;
        CurrentSequenceNumber = 1;
        LastAck = 0;

        _incomingPackets = new ConcurrentQueue<GamePacket>();
        _outgoingPackets = new ConcurrentQueue<Memory<byte>>();
        _incomingSplitMessagePackets = [];

        if (isSequenced && isReliable)
        {
            _retransmits = new RetransmitQueue();
        }
    }

    public delegate void PacketAvailableDelegate(GamePacket packet);

    public event PacketAvailableDelegate? PacketAvailable;

    private ChannelType Type { get; }
    private bool IsSequenced { get; }
    private bool IsReliable { get; }
    private bool IsGSS { get; }
    private ushort CurrentSequenceNumber { get; set; }
    private DateTime LastActivity { get; set; }
    private ushort LastAck { get; set; }
    private bool InSplitMode { get; set; }

    public static Dictionary<ChannelType, Channel> GetChannels(INetworkClient client, ILogger logger)
    {
        return new Dictionary<ChannelType, Channel>
               {
                   { ChannelType.Control, new Channel(ChannelType.Control, false, false, false, client, logger) },
                   { ChannelType.Matrix, new Channel(ChannelType.Matrix, true, true,  false, client, logger) },
                   { ChannelType.ReliableGss, new Channel(ChannelType.ReliableGss, true, true, true, client, logger) },
                   { ChannelType.UnreliableGss, new Channel(ChannelType.UnreliableGss, true, false, true, client, logger) }
               };
    }

    public void HandlePacket(GamePacket packet)
    {
        _incomingPackets.Enqueue(packet);
    }

    /// <summary>
    ///     Retire everything a client ack covers. An ack is cumulative, so this clears the whole
    ///     run up to the sequence it names rather than that one packet.
    /// </summary>
    public void Acknowledge(ushort ackForNum)
    {
        var retired = _retransmits?.Acknowledge(ackForNum) ?? 0;
        if (retired > 0)
        {
            _logger.Verbose("--> {Channel} ack for {SeqNum} retired {Retired}, {Pending} still unacked", Type, ackForNum, retired, _retransmits!.Pending);
        }
    }

    public void Process(CancellationToken ct)
    {
        while (_outgoingPackets.TryDequeue(out var qi))
        {
            _client.Send(qi);
            LastActivity = DateTime.Now;
        }

        SendOverdue();

        while (_incomingPackets.TryDequeue(out var packet))
        {
            ushort sequenceNumber = 0;
            if (IsSequenced)
            {
                sequenceNumber = Utils.SimpleFixEndianness(packet.Read<ushort>());
            }

            // Confirmed against the 2016 capture rather than reasoned about: it holds 24 resent
            // packets, and the 15 whose original also survives decode to byte-identical payloads
            // once this XOR is undone. All 24 carry a resend count of 3, in both directions.
            if (packet.Header.ResendCount > 0)
            {
                var xorIndex = packet.Header.ResendCount - 1;
                var data = packet.Peek(packet.BytesRemaining).ToArray();
                for (var i = 0; i < data.Length; i++)
                {
                    data[i] ^= _xorByte[xorIndex];
                }

                packet = new GamePacket(packet.Header, new ReadOnlyMemory<byte>(data));
                _logger.Debug("---> Resent packet C:{Channel} SeqNum {SeqNum}: {PacketBytes} bytes", Type, sequenceNumber, packet.TotalBytes);

                // A client only resends what it believes went unacked, so a resend of something
                // already taken means the ack was lost rather than the packet. Ack it again to stop
                // the loop, and drop the copy: handing the same message to a controller twice fires
                // whatever it does twice. Tested against what was actually taken rather than against
                // LastAck, because LastAck skips over a gap (NET-25) and everything under a gap
                // would then be read as a duplicate of something never received.
                if (IsReliable && _delivered.Contains(sequenceNumber))
                {
                    _client.SendAck(Type, sequenceNumber, packet.Received);
                    _logger.Debug("---> Already had C:{Channel} SeqNum {SeqNum}, re-acked without handling it again", Type, sequenceNumber);
                    LastActivity = DateTime.Now;
                    continue;
                }
            }

            if (IsReliable)
            {
                _delivered.Remember(sequenceNumber);
            }

            if (InSplitMode)
            {
                // Assigned rather than added, because a resent fragment arriving mid-run carries a
                // sequence number already in the buffer and Add throws on one. An exception here
                // takes the shard thread with it, which is how NET-21 played out.
                _incomingSplitMessagePackets[sequenceNumber] = packet;
                if (!packet.Header.IsSplit)
                {
                    // Finish split mode
                    InSplitMode = false;

                    var combined = _incomingSplitMessagePackets
                    .SelectMany((pair) => pair.Value.Peek(pair.Value.BytesRemaining).ToArray())
                    .ToArray();
                    _incomingSplitMessagePackets.Clear();

                    var combinedPacket = new GamePacket(packet.Header, new ReadOnlyMemory<byte>(combined));

                    _client.SendAck(Type, sequenceNumber, packet.Received);
                    LastAck = sequenceNumber;
                    PacketAvailable?.Invoke(combinedPacket);
                }
            }
            else if (packet.Header.IsSplit)
            {
                // Enter split mode
                InSplitMode = true;
                _incomingSplitMessagePackets[sequenceNumber] = packet;
                _client.SendAck(Type, sequenceNumber, packet.Received);
                LastAck = sequenceNumber;
            }
            else
            {
                if (IsReliable && Sequence.IsAfter(sequenceNumber, LastAck))
                {
                    _client.SendAck(Type, sequenceNumber, packet.Received);
                    LastAck = sequenceNumber;
                }

                PacketAvailable?.Invoke(packet);
            }

            LastActivity = DateTime.Now;
        }
    }

    /// <summary>
    ///     Send a GSS Update message (View or Controller) using Aero's change tracking
    /// </summary>
    /// <typeparam name="TViewOrController">The Aero Class of AeroGenTypes.View or AeroGenTypes.Controller</typeparam>
    /// <param name="view">The Aero View or Controller</param>
    /// <param name="entityId">Id of the entity the View represents</param>
    /// <returns>true if the operation succeeded, false in all other cases</returns>
    /// <exception cref="ArgumentException">The passed view does not have the AeroMessageIdAttribute</exception>
    public bool SendChanges<TViewOrController>(TViewOrController view, ulong entityId)
        where TViewOrController : class, IAeroViewInterface
    {
        if (typeof(TViewOrController).GetCustomAttributes(typeof(AeroMessageIdAttribute), false).FirstOrDefault() is not AeroMessageIdAttribute aeroMsgAttr)
        {
            throw new ArgumentException($"The passed Aero class is required to be annotated with {nameof(AeroMessageIdAttribute)} (Type: {typeof(TViewOrController).FullName})");
        }

        // Generate and send
        var typeCode = (Enums.GSS.Controllers)aeroMsgAttr.ControllerId;
        view.SerializeChangesToMemory(out var packetMemory);
        return SendPacketMemory(entityId, 1, typeCode, ref packetMemory);
    }

    /// <summary>
    ///     Send a custom GSS View Update message (View or Controller)
    /// </summary>
    /// <typeparam name="TViewOrController">The Aero Class of AeroGenTypes.View or AeroGenTypes.Controller</typeparam>
    /// <param name="view">The Aero View</param>
    /// <param name="entityId">Id of the entity the View represents</param>
    /// <param name="packetMemory">The update message</param>
    /// <returns>true if the operation succeeded, false in all other cases</returns>
    /// <exception cref="ArgumentException">The passed view does not have the AeroMessageIdAttribute</exception>
    public bool SendChanges<TViewOrController>(TViewOrController view, ulong entityId, Memory<byte> packetMemory)
        where TViewOrController : class, IAeroViewInterface
    {
        if (typeof(TViewOrController).GetCustomAttributes(typeof(AeroMessageIdAttribute), false).FirstOrDefault() is not AeroMessageIdAttribute aeroMsgAttr)
        {
            throw new ArgumentException($"The passed Aero class is required to be annotated with {nameof(AeroMessageIdAttribute)} (Type: {typeof(TViewOrController).FullName})");
        }

        // Send
        var typeCode = (Enums.GSS.Controllers)aeroMsgAttr.ControllerId;
        return SendPacketMemory(entityId, 1, typeCode, ref packetMemory);
    }

    /// <summary>
    ///     Send a GSS View Checksum message
    /// </summary>
    /// <param name="entityId">Id of the entity the View represents</param>
    /// <param name="controllerId">View type the checksum is for</param>
    /// <param name="checksum">Checksum value</param>
    /// <returns>true if the operation succeeded, false in all other cases</returns>
    public bool SendChecksum(ulong entityId, Enums.GSS.Controllers controllerId, uint checksum)
    {
        var messageData = Serializer.WritePrimitive(checksum);
        return SendPacketMemory(entityId, 2, controllerId, ref messageData);
    }

    /// <summary>
    ///     Send a GSS View Keyframe message
    /// </summary>
    /// <typeparam name="TView">The Aero Class of AeroGenTypes.View</typeparam>
    /// <param name="view">The Aero View</param>
    /// <param name="entityId">Id of the entity the View represents</param>
    /// <returns>true if the operation succeeded, false in all other cases</returns>
    /// <exception cref="ArgumentException">The passed view does not have the AeroMessageIdAttribute or AeroAttribute, or it is not a AeroGenTypes.View</exception>
    public bool SendViewKeyframe<TView>(TView view, ulong entityId)
        where TView : class, IAero
    {
        if (typeof(TView).GetCustomAttributes(typeof(AeroMessageIdAttribute), false).FirstOrDefault() is not AeroMessageIdAttribute aeroMsgAttr)
        {
            throw new ArgumentException($"The passed Aero view is required to be annotated with {nameof(AeroMessageIdAttribute)} (Type: {typeof(TView).FullName})");
        }

        if (typeof(TView).GetCustomAttributes(typeof(AeroAttribute), false).FirstOrDefault() is not AeroAttribute aeroAttr)
        {
            throw new ArgumentException($"The passed Aero view is required to be annotated with {nameof(AeroAttribute)} (Type: {typeof(TView).FullName})");
        }

        if (aeroAttr.AeroType != AeroGenTypes.View)
        {
            throw new ArgumentException($"The AeroType should be View to send View Keyframe. Received {aeroAttr.AeroType} instead. (Type: {typeof(TView).FullName})");
        }

        // Generate and send
        view.SerializeToMemory(out var packetMemory);
        var typecode = (Enums.GSS.Controllers)aeroMsgAttr.ControllerId;
        return SendPacketMemory(entityId, 3, typecode, ref packetMemory);
    }

    /// <summary>
    ///     Send a GSS View ScopeOut/Remove message
    /// </summary>
    /// <typeparam name="TView">The Aero Class of AeroGenTypes.View</typeparam>
    /// <param name="view">The Aero View</param>
    /// <param name="entityId">Id of the entity the View represents</param>
    /// <returns>true if the operation succeeded, false in all other cases</returns>
    /// <exception cref="ArgumentException">The passed view does not have the AeroMessageIdAttribute or AeroAttribute, or it is not a AeroGenTypes.View</exception>
    public bool SendViewScopeOut<TView>(TView view, ulong entityId)
        where TView : class, IAero
    {
        if (typeof(TView).GetCustomAttributes(typeof(AeroMessageIdAttribute), false).FirstOrDefault() is not AeroMessageIdAttribute aeroMsgAttr)
        {
            throw new ArgumentException($"The passed Aero view is required to be annotated with {nameof(AeroMessageIdAttribute)} (Type: {typeof(TView).FullName})");
        }

        if (typeof(TView).GetCustomAttributes(typeof(AeroAttribute), false).FirstOrDefault() is not AeroAttribute aeroAttr)
        {
            throw new ArgumentException($"The passed Aero view is required to be annotated with {nameof(AeroAttribute)} (Type: {typeof(TView).FullName})");
        }

        if (aeroAttr.AeroType != AeroGenTypes.View)
        {
            throw new ArgumentException($"The AeroType should be View to send View ScopeOut. Received {aeroAttr.AeroType} instead. (Type: {typeof(TView).FullName})");
        }

        // Generate and send
        return SendViewScopeOut(entityId, (Enums.GSS.Controllers)aeroMsgAttr.ControllerId);
    }

    /// <summary>
    ///     Send a GSS View ScopeOut/Remove message for a view given its typecode rather than an instance.
    /// </summary>
    /// <remarks>
    ///     The generic overload reads the typecode off the view object, which a caller only has while the
    ///     entity is alive. Telling a client to drop an entity the shard has already removed is exactly
    ///     the case with nothing left to reflect on, so the typecode is passed in instead.
    /// </remarks>
    /// <param name="entityId">Id of the entity the View represents</param>
    /// <param name="controllerId">Typecode of the view being removed</param>
    /// <returns>true if the operation succeeded, false in all other cases</returns>
    public bool SendViewScopeOut(ulong entityId, Enums.GSS.Controllers controllerId)
    {
        var messageData = new Memory<byte>([]);
        return SendPacketMemory(entityId, 6, controllerId, ref messageData);
    }

    /// <summary>
    ///     Send a GSS Controller Keyframe message
    /// </summary>
    /// <typeparam name="TController">The Aero Class of AeroGenTypes.Controller</typeparam>
    /// <param name="controller">The Aero Controller</param>
    /// <param name="entityId">Id of the entity the View represents</param>
    /// <param name="playerId">Player id (?) used in controller messages</param>
    /// <returns>true if the operation succeeded, false in all other cases</returns>
    /// <exception cref="ArgumentException">The passed controller does not have the AeroMessageIdAttribute or AeroAttribute, or it is not a AeroGenTypes.Controller</exception>
    public bool SendControllerKeyframe<TController>(TController controller, ulong entityId, ulong playerId)
        where TController : class, IAero
    {
        if (typeof(TController).GetCustomAttributes(typeof(AeroMessageIdAttribute), false).FirstOrDefault() is not AeroMessageIdAttribute aeroMsgAttr)
        {
            throw new ArgumentException($"The passed Aero controller is required to be annotated with {nameof(AeroMessageIdAttribute)} (Type: {typeof(TController).FullName})");
        }

        if (typeof(TController).GetCustomAttributes(typeof(AeroAttribute), false).FirstOrDefault() is not AeroAttribute aeroAttr)
        {
            throw new ArgumentException($"The passed Aero controller is required to be annotated with {nameof(AeroAttribute)} (Type: {typeof(TController).FullName})");
        }

        if (aeroAttr.AeroType != AeroGenTypes.Controller)
        {
            throw new ArgumentException($"The AeroType should be Controller to send Controller Keyframe. Received {aeroAttr.AeroType} instead. (Type: {typeof(TController).FullName})");
        }

        // Generate and send
        var typecode = (Enums.GSS.Controllers)aeroMsgAttr.ControllerId;
        controller.SerializeToMemory(out var packetMemory);
        var messageData = new Memory<byte>(new byte[8 + packetMemory.Length]);
        packetMemory.CopyTo(messageData[8..]);
        Serializer.WritePrimitive(playerId).CopyTo(messageData);
        return SendPacketMemory(entityId, 4, typecode, ref messageData);
    }

    /// <summary>
    ///     Send a GSS Controller Remove message
    /// </summary>
    /// <typeparam name="TController">The Aero Class of AeroGenTypes.Controller</typeparam>
    /// <param name="controller">The Aero Controller</param>
    /// <param name="entityId">Id of the entity the View represents</param>
    /// <param name="playerId">Player id (?) used in controller messages</param>
    /// <returns>true if the operation succeeded, false in all other cases</returns>
    /// <exception cref="ArgumentException">The passed controller does not have the AeroMessageIdAttribute or AeroAttribute, or it is not a AeroGenTypes.Controller</exception>
    public bool SendControllerRemove<TController>(TController controller, ulong entityId, ulong playerId)
        where TController : class, IAero
    {
        if (typeof(TController).GetCustomAttributes(typeof(AeroMessageIdAttribute), false).FirstOrDefault() is not AeroMessageIdAttribute aeroMsgAttr)
        {
            throw new ArgumentException($"The passed Aero controller is required to be annotated with {nameof(AeroMessageIdAttribute)} (Type: {typeof(TController).FullName})");
        }

        if (typeof(TController).GetCustomAttributes(typeof(AeroAttribute), false).FirstOrDefault() is not AeroAttribute aeroAttr)
        {
            throw new ArgumentException($"The passed Aero controller is required to be annotated with {nameof(AeroAttribute)} (Type: {typeof(TController).FullName})");
        }

        if (aeroAttr.AeroType != AeroGenTypes.Controller)
        {
            throw new ArgumentException($"The AeroType should be Controller to send Controller Keyframe. Received {aeroAttr.AeroType} instead. (Type: {typeof(TController).FullName})");
        }

        // Generate and send
        var controllerId = (Enums.GSS.Controllers)aeroMsgAttr.ControllerId;
        var messageData = new Memory<byte>(new byte[8]);
        Serializer.WritePrimitive(playerId).CopyTo(messageData);
        return SendPacketMemory(entityId, 5, controllerId, ref messageData);
    }

    /// <summary>
    ///     Send a Control, Matrix or Normal GSS Message
    /// </summary>
    /// <typeparam name="TNormal">The Aero class of AeroGenTypes.Normal</typeparam>
    /// <param name="message">The Aero message</param>
    /// <param name="entityId">For GSS Messages, Id of the entity the message is for</param>
    /// <param name="messageIdOverride">Optional way to override the message ID</param>
    /// <returns>true if the operation succeeded, false in all other cases</returns>
    /// <exception cref="ArgumentException">The passed message does not have the AeroMessageIdAttribute, AeroAttribute, or it is not a AeroGenTypes.Normal</exception>
    public bool SendMessage<TNormal>(TNormal message, ulong entityId = 0, byte messageIdOverride = 0)
        where TNormal : class, IAero
    {
        if (typeof(TNormal).GetCustomAttributes(typeof(AeroMessageIdAttribute), false).FirstOrDefault() is not AeroMessageIdAttribute aeroMessageIdAttribute)
        {
            throw new ArgumentException($"The passed package is required to be annotated with {nameof(AeroMessageIdAttribute)} (Type: {typeof(TNormal).FullName})");
        }

        if (typeof(TNormal).GetCustomAttributes(typeof(AeroAttribute), false).FirstOrDefault() is not AeroAttribute aeroAttr)
        {
            throw new ArgumentException($"The passed Aero controller is required to be annotated with {nameof(AeroAttribute)} (Type: {typeof(TNormal).FullName})");
        }

        if (aeroAttr.AeroType != AeroGenTypes.Normal)
        {
            throw new ArgumentException($"The AeroType should be Normal if it should be sent as a message. Received {aeroAttr.AeroType} instead. (Type: {typeof(TNormal).FullName})");
        }

        var type = aeroMessageIdAttribute.Typ;
        var messageId = (byte)aeroMessageIdAttribute.MessageId;
        if (messageIdOverride != 0)
        {
            messageId = messageIdOverride;
        }

        message.SerializeToMemory(out var packetMemory);

        switch (type)
        {
            case AeroMessageIdAttribute.MsgType.Matrix:
                return SendPacketMemoryMatrix(messageId, ref packetMemory);
            case AeroMessageIdAttribute.MsgType.GSS:
                {
                    var typecode = (Enums.GSS.Controllers)aeroMessageIdAttribute.ControllerId;
                    return SendPacketMemory(entityId, messageId, typecode, ref packetMemory);
                }

            case AeroMessageIdAttribute.MsgType.Control:
                return SendPacketMemoryMatrix(messageId, ref packetMemory); // Everything's gonna be just fine
            default:
                throw new ArgumentException("Message type not implemented");
        }
    }

    /// <summary>
    ///     Send serialized data of a gss channel packet to the client
    /// </summary>
    /// <param name="entityId">Id of the entity the packet is for</param>
    /// <param name="messageId">Message Id relative to the controllerId</param>
    /// <param name="controllerId">Typecode of the entity matching the view or controller</param>
    /// <param name="packetToSend">Memory buffer</param>
    /// <param name="msgEnumType">Optionally, the enum type containing the message id may be specified for enhanced verbose-level logging</param>
    /// <returns>true if the operation succeeded, false in all other cases</returns>
    /// <exception cref="InvalidOperationException">If <see cref="msgEnumType" /> is not null and does not contain an element with a value equal to <see cref="messageId" /> </exception>
    private bool SendPacketMemory(ulong entityId,
                                  byte messageId,
                                  Enums.GSS.Controllers controllerId,
                                  ref Memory<byte> packetToSend,
                                  Type? msgEnumType = null)
    {
        const int HeaderByteSize = 9;

        var serializedData = new Memory<byte>(new byte[HeaderByteSize + packetToSend.Length]);
        packetToSend.CopyTo(serializedData[HeaderByteSize..]);

        Serializer.WritePrimitive(entityId).CopyTo(serializedData);

        // Intentionally overwrite first byte of Entity ID
        Serializer.WritePrimitive((byte)controllerId).CopyTo(serializedData);
        Serializer.WritePrimitive(messageId).CopyTo(serializedData[8..]);

        if (msgEnumType == null)
        {
            _logger.Verbose("<-- {Channel}: Controller = {Controller} Entity = 0x{EntityId:X16} MsgID = 0x{MessageId:X2}",
                            Type,
                            controllerId,
                            entityId,
                            messageId);
        }
        else
        {
            _logger.Verbose("<-- {Channel}: Controller = {Controller} Entity = 0x{EntityId:X16} MsgID = {Message} (0x{MessageId:X2})",
                            Type,
                            controllerId,
                            entityId,
                            Enum.Parse(msgEnumType,
                                       Enum.GetName(msgEnumType, messageId) ?? throw new InvalidOperationException($"Message enum type {msgEnumType.FullName} has no element with a value of {messageId}")),
                            messageId);
        }

        return Send(serializedData);
    }

    /// <summary>
    ///     Send serialized data of a matrix channel packet to the client
    /// </summary>
    /// <param name="messageId">Id of the matrix message being sent</param>
    /// <param name="packetMemory">Memory buffer</param>
    /// <param name="msgEnumType">TODO: Optionally, the enum type containing the message id may be specified for enhanced verbose-level logging</param>
    /// <returns>true if the operation succeeded, false in all other cases</returns>
    private bool SendPacketMemoryMatrix(byte messageId, ref Memory<byte> packetMemory, Type? msgEnumType = null)
    {
        const int HeaderByteSize = 1;
        var serializedData = new Memory<byte>(new byte[HeaderByteSize + packetMemory.Length]);
        packetMemory.CopyTo(serializedData[HeaderByteSize..]);
        Serializer.WritePrimitive(messageId).CopyTo(serializedData);
        return Send(serializedData);
    }

    /// <summary>
    ///     Send again anything the client hasn't acked in time, and report anything that has run out
    ///     of attempts. A resend goes straight out rather than back through the outgoing queue,
    ///     because it already carries the sequence number and header it was built with.
    /// </summary>
    private void SendOverdue()
    {
        if (_retransmits == null || _retransmits.Pending == 0)
        {
            return;
        }

        foreach (var resend in _retransmits.Due(DateTime.UtcNow))
        {
            if (resend.GaveUp)
            {
                // Nothing above this layer knows a message was lost, so this line is the only
                // record that the client's copy of something is now permanently wrong.
                _logger.Warning("{Channel} SeqNum {SeqNum} went unacked through {Attempts} resends and has been dropped", Type, resend.SequenceNumber, resend.Attempt);
                continue;
            }

            _logger.Debug("<- {Channel} resending SeqNum {SeqNum}, attempt {Attempt}, {Pending} unacked", Type, resend.SequenceNumber, resend.Attempt, _retransmits.Pending);
            _client.Send(resend.Packet);
            LastActivity = DateTime.Now;
        }
    }

    /// <summary>
    ///     Send data to the client
    /// </summary>
    /// <param name="packetData">Memory buffer</param>
    /// <returns>true if the operation succeeded, false in all other cases</returns>
    private bool Send(Memory<byte> packetData)
    {
        var headerLength = 2;
        if (IsSequenced)
        {
            headerLength += 2;
        }

        // TODO: Send UGSS messages that are split over RGSS
        while (packetData.Length > 0)
        {
            var length = Math.Min(packetData.Length + headerLength, _maxPacketSize);

            var t = new Memory<byte>(new byte[length]);
            packetData[..(length - headerLength)].CopyTo(t[headerLength..]);

            ushort sequenceNumber = 0;
            if (IsSequenced)
            {
                sequenceNumber = CurrentSequenceNumber;
                if (IsReliable)
                {
                    _logger.Verbose("<- {Channel} SeqNum =  {SeqNum}", Type, CurrentSequenceNumber);
                }

                Serializer.WritePrimitive(Utils.SimpleFixEndianness(CurrentSequenceNumber)).CopyTo(t.Slice(2, 2));
                unchecked
                {
                    CurrentSequenceNumber++;
                }
            }

            var header = new GamePacketHeader(Type, 0, packetData.Length + headerLength > _maxPacketSize, (ushort)t.Length);
            var headerData = Serializer.WritePrimitive(Utils.SimpleFixEndianness(header.PacketHeader));
            headerData.CopyTo(t);

            // Tracked at the point the bytes are finished rather than when they leave, which on a
            // GSS channel is up to one network tick later. That makes the timeout slightly early,
            // never late, which is the harmless direction.
            _retransmits?.Track(sequenceNumber, t, DateTime.UtcNow);

            if (IsGSS)
            {
                _client.SequencedMessages.Enqueue(t);
            }
            else
            {
                _outgoingPackets.Enqueue(t);
            }

            packetData = packetData[(length - headerLength)..];
        }

        return true;
    }
}