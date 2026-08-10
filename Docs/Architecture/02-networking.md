# Layer 2: Networking & Protocol

## Inbound path, end to end

```
UDP datagram
 └─ PacketServer.ListenThreadAsync        Lib/Shared.Udp/PacketServer.cs
     └─ IncomingPackets (BufferBlock)
         └─ GameServer.HandlePacket        UdpHosts/GameServer/GameServer.cs
             │  first 4 bytes = socket id → NetworkPlayer (creates + migrates into a shard if new)
             └─ NetworkClient.HandlePacket  UdpHosts/GameServer/NetworkClient.cs
                 │  splits the datagram into multiple 2-byte-headered game packets
                 └─ Channel.HandlePacket    UdpHosts/GameServer/Channel.cs   (queued, not processed)
                     └─ Channel.Process     runs on the shard's network tick
                         └─ PacketAvailable event
                             ├─ Control_PacketAvailable   acks, time sync, close
                             ├─ Matrix_PacketAvailable    login, zone ack, keyframe requests
                             └─ GSS_PacketAvailable       → controller dispatch
```

Two things about that diagram matter in practice:

- **Receiving and processing are decoupled**: `HandlePacket` only enqueues. Nothing is
  interpreted until `Channel.Process` runs on the shard's network tick (20 Hz), so no handler
  ever runs on the socket thread.
- **One datagram carries many messages**: `NetworkClient.HandlePacket` walks the buffer,
  reading a `GamePacketHeader` and slicing off `header.Length` bytes each iteration.

## The game packet header

[Packets/GamePacketHeader.cs](../../UdpHosts/GameServer/Packets/GamePacketHeader.cs), two big-endian
bytes packed as:

| Bits | Field |
|------|-------|
| 15-14 | Channel (0 Control, 1 Matrix, 2 ReliableGss, 3 UnreliableGss) |
| 13-12 | Resend count |
| 11 | Split flag |
| 10-0 | Length, including the header itself |

Length is 11 bits, so a single game packet can't exceed 2047 bytes; the MTU budget in
`Channel` is stricter still (`PacketServer.MTU` 1400, minus 80 for IP/UDP, minus 4 for the socket
id).

## The four channels

[Channel.GetChannels](../../UdpHosts/GameServer/Channel.cs) fixes the properties of each:

| Channel | Sequenced | Reliable | GSS | Carries |
|---------|-----------|----------|-----|---------|
| Control | no | no | no | Acks, time sync, MTU probe, close |
| Matrix | yes | yes | no | Login, EnterZone/ExitZone, keyframe requests |
| ReliableGss | yes | yes | yes | Entity state, events, most gameplay |
| UnreliableGss | yes | no | yes | High-frequency, droppable state (movement echoes, debug) |

Sequenced channels prefix a `ushort` sequence number. Reliable channels ack inbound sequence
numbers back over Control (`SendAck`).

### Reliability caveats

Outbound reliability isn't implemented. `Control_PacketAvailable` logs client acks and discards
them (`// TODO: Track reliable packets`), and there's no retransmit queue. "Reliable" currently
means "we ack what the client sends", not "we resend what the client missed". Inbound resends are
detected via `ResendCount` and XOR-decoded with `{0xFF, 0xAA, 0xCC}`, flagged with its own TODO
about whether that path is correct.

Split messages are reassembled by buffering into `_incomingSplitMessagePackets` until a packet
arrives without the split flag; the sorted dictionary is keyed by sequence number so
out-of-order fragments still join correctly.

## GSS dispatch

`GSS_PacketAvailable` reads a fixed prefix:

```
[1] controller id   [7] entity id (shifted << 8 to restore the full guid)   [1] message id
```

Then [Controllers/Factory.cs](../../UdpHosts/GameServer/Controllers/Factory.cs) resolves the
controller id to a singleton handler instance, discovered by reflection over `[ControllerID]`, and
[Controllers/Base.cs](../../UdpHosts/GameServer/Controllers/Base.cs) reflects again over
`[MessageID]` methods to find the handler.

So adding a client → server message is:

```csharp
[MessageID((byte)Commands.SomeCommand)]
public void SomeCommand(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
{
    var query = packet.Unpack<SomeCommandMessage>();   // Aero type
    ...
}
```

Handlers are invoked through `MethodInfo.Invoke` inside a try/catch that logs
`TargetInvocationException` and swallows it. An exception in a handler won't crash the server and
won't surface as a failed action. It appears only as an error log line, so when a client action
silently does nothing, check the log before assuming the message never arrived.

Existing controllers: `Character/BaseController`, `Character/CombatController`,
`Character/MissionAndMarkerController`, `Character/SpectatorController`, `Vehicle/BaseController`,
`Vehicle/CombatController`, `Turret/BaseController`, and `GenericShard`.

## Outbound: messages, views, controllers

[Channel.cs](../../UdpHosts/GameServer/Channel.cs) offers five send shapes:

| Method | Use |
|--------|-----|
| `SendMessage(msg, entityId)` | A one-off Aero message/event |
| `SendChanges(viewOrController, entityId)` | Only the fields Aero's change tracking marked dirty |
| `SendViewKeyframe(view, entityId)` | Full state, on scope-in or on client request |
| `SendControllerKeyframe(controller, entityId, playerId)` | Full state of an owned controller |
| `SendViewScopeOut` / `SendControllerRemove` | Tear-down |

`SendChanges` is the workhorse: mutate the Aero property (`SomethingProp = value`), and the
generated change tracking records it. Nothing goes out until something calls `SendChanges`,
usually `EntityManager.FlushChanges` on its periodic sweep. See
[layer 4](04-entities-and-replication.md).

## Matrix channel milestones

The login flow is driven from `Matrix_PacketAvailable`:

- `Login` → `NetworkPlayer.Login(characterGuid)`: builds the `CharacterEntity`, fetches remote
  character data over gRPC, applies a loadout, creates the physics body, sends
  `WelcomeToTheMatrix`, then `EnterZone`.
- `EnterZoneAck` → initialises the character controllers and adds the entity to `EntityManager`.
- `KeyframeRequest` → the client asking for full state on specific entities it hasn't resolved.
- `ExitZoneAck` → removes the entity.
