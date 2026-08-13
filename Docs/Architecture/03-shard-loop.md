# Layer 3: Shard & Tick Loop

[Shard.cs](../../UdpHosts/GameServer/Shard.cs) is the single most useful file in the project to
read first. A shard is one zone instance: it owns every gameplay system, the entity table, the
connected clients, and the loop that drives them.

## Threads

Per process ([PacketServer.cs](../../Lib/Shared.Udp/PacketServer.cs)):

- **ListenThread**: socket receive → `IncomingPackets` buffer
- **ServerRunThread**: drains `IncomingPackets`, routes to the owning client
- **SendThread**: drains `OutgoingPackets` → socket

Per shard: one **RunThread**. Gameplay is single-threaded per shard, which is why systems can
use plain `Dictionary` internally. The collections shared with other threads (`Entities`,
`Clients`, `Encounters`, `EntityRefMap`) are `ConcurrentDictionary`.

## The loop

`Shard.RunThread` spins without a sleep, just `Thread.Yield()`, so the loop runs as fast as the
core allows and the rate constants are minimum intervals rather than a fixed timestep.

```
loop:
  if (elapsed since last net tick >= 1/20s)  NetworkTick()   → every client: drain + Channel.Process
  Tick():
      AI.Tick()             perception, target selection, NPC weapon fire, >= 50ms apart
      Physics.Tick()        accumulator, steps Bepu at 50ms
      EntityMan.Tick()      zone spawn, scope in/out, change flush, lifetimes
      EncounterMan.Tick()
      Abilities.Tick()      duration/update chains, >= 20ms apart
      WeaponSim.Tick()      spread recovery, >= 50ms apart
      ShieldSim.Tick()      shield recharge, >= 100ms apart
      Hazards.Tick()        drowning and melding damage, >= 500ms apart
      EventBus.Flush()
```

Ordering is meaningful: physics moves bodies before the entity manager decides what is in scope,
and `EventBus.Flush()` is last so events raised anywhere in the tick are delivered at a single
consistent point.

`_gameTickRate` (1/60) is passed into the `Shard` constructor but isn't currently used to pace the
loop. `ShouldNetworkTick` only gates the network tick.

Each system does its own rate limiting with the same pattern:

```csharp
if (currentTime > _lastUpdate + _updateIntervalMs) { _lastUpdate = currentTime; ... }
```

Intervals in use: abilities 20ms, AI 50ms, weapon sim 50ms, physics 50ms, shields 100ms, hazards
500ms, and the scope check / scope-in / change flush / lifetime check in `EntityManager`.

Two of those intervals aren't arbitrary. The AI's 50ms is also the finest grain an NPC's rate of
fire can be paced at, and the hazard sim's 500ms is retail's own `update_frequency` for the drowning
status effects whose damage rates it reproduces.

## Clocks, three of them, don't mix them up

| Property | Type | Value |
|----------|------|-------|
| `CurrentTimeLong` | `ulong` | Unix milliseconds (`DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`) |
| `CurrentTime` | `uint` | Low 32 bits of the above |
| `CurrentShortTime` | `ushort` | Low 16 bits of the above |

`CurrentShortTime` wraps roughly every 65 seconds. Several protocol fields are 16-bit times and
the client compares them, so wrap-around is a real source of bugs. See
`BaseAptitudeEntity.NextStatusEffectChangeTime` in
[Entities/BaseAptitudeEntity.cs](../../UdpHosts/GameServer/Entities/BaseAptitudeEntity.cs), which
keeps per-entity status-effect change times strictly increasing: the client dedupes on that field
and would otherwise drop a follow-up effect applied in the same millisecond.

Separately, `delta` passed to `Tick` is measured from a `Stopwatch`, while `currentTime` is wall
clock. Don't derive one from the other.

## Shard membership

- `GameServer.GetNextShard` assigns the least-populated shard under 64 players; a new shard is
  created when all are full. In practice one shard runs, hosting `Settings.ZoneId`.
- `MigrateIn` calls `player.Init(shard)` and registers the client; `MigrateOut` removes the
  character entity and the client.
- `AssignNewRefId` hands out 16-bit entity refs (`EntityRefMap`) that the protocol uses in place
  of full 64-bit guids in some messages. Zero and `0xffff` are reserved.
- `GetNextGuid(type)` → [GUIDService.cs](../../UdpHosts/GameServer/GUIDService.cs), which packs
  server id, time, counter, and the controller type code into the low byte.

## EventBus

[Systems/SystemEvents](../../UdpHosts/GameServer/Systems/SystemEvents) is a simple deferred
publish/subscribe. Publishing queues; `Flush()` at the end of the tick delivers. Use it when a
system needs to react to another without a hard reference (physics → gameplay is the existing
case).
