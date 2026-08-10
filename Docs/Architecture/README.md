# PIN Architecture Guide

A layered map for navigating the codebase.

For the original high-level overview (protocol diagrams, external references) see
[../README.md](../README.md).

## The layers

| #   | Layer                                                      | Read it to…                                                                      |
| --- | ---------------------------------------------------------- | -------------------------------------------------------------------------------- |
| 1   | [Processes & Configuration](01-processes-and-config.md)    | Know which executables exist, what ports they own, how settings and DI are wired |
| 2   | [Networking & Protocol](02-networking.md)                  | Trace a packet from the socket to a handler, or send a message to a client       |
| 3   | [Shard & Tick Loop](03-shard-loop.md)                      | Understand threads, tick order, and the several different clocks                 |
| 4   | [Entities & Replication](04-entities-and-replication.md)   | Add an entity type, change a replicated field, or debug scoping/keyframes        |
| 5   | [Aptitude (Abilities & Effects)](05-aptitude-abilities.md) | Implement an ability command, or work out why an effect didn't apply             |
| 6   | [Combat & Damage](06-combat-and-damage.md)                 | Work on hit registration, damage numbers, death and respawn                      |
| 7   | [Physics & World](07-physics-and-world.md)                 | Touch collision, raycasts, zone loading, or pose/hitbox data                     |
| 8   | [Static Data (SDB)](08-static-data.md)                     | Look up game data, add an SDB record, or add custom JSON data                    |
| 9   | [Web Hosts & RIN](09-webhosts-and-rin.md)                  | Work on HTTP APIs, character persistence, or the gRPC link                       |
| 10  | [Cookbook](10-cookbook.md)                                 | "Where do I add X?" recipes, plus what can be verified without the game          |

## The short version

Firefall's client talks to three processes:

- **MatrixServer** (UDP 25000): handshake only. Assigns a socket id, points the client at the
  GameServer, then gets out of the way.
- **GameServer** (UDP 25001): everything that happens in a zone.
- **WebHostManager** (HTTP 4400-4411): hosts every HTTP API the client calls, in one process.

Inside the GameServer the shape is:

```
UDP socket
  └─ PacketServer            Lib/Shared.Udp/PacketServer.cs      listen / send threads
      └─ GameServer          UdpHosts/GameServer/GameServer.cs   client map, shard assignment
          └─ Shard           UdpHosts/GameServer/Shard.cs        the game loop; owns every system
              ├─ NetworkPlayer per connected client, 4 Channels
              ├─ EntityManager   entity lifetime, scoping, replication
              ├─ AbilitySystem   chains, commands, status effects
              ├─ PhysicsEngine   Bepu simulation, raycasts
              ├─ WeaponSim / ProjectileSim
              ├─ MovementRelay, EncounterManager, ChatService, AdminService, AIEngine
              └─ EventBus        deferred intra-shard events
```

A shard is a zone instance plus its game loop. Everything gameplay-related hangs off it, so
`IShard` is the context object passed almost everywhere.

## Conventions worth knowing before you read code

- **Aero** ([Lib/AeroMessages](../../Lib/AeroMessages)) is a source generator that produces the
  wire types (`AeroMessages.GSS.V66.*`). Anything named `*Data`, `*View`, `*Controller`, or
  living under `AeroMessages` is generated protocol shape. It's a submodule, so regenerate it
  there rather than in place.
- **Views vs Controllers**: a *View* is entity state replicated to observers. A *Controller* is
  state replicated to the entity's own player, and also the target of client → server messages.
  Both are Aero types with change tracking; see [layer 4](04-entities-and-replication.md).
- **`Todo` folders are deliberate**: `Systems/Aptitude/Commands/**/Todo/` holds ~197 command
  stubs that parse their SDB definition and `return true` without doing anything, so an
  unimplemented step doesn't abort the chain. Promoting one out of `Todo/` is the standard unit
  of ability work.
- **StyleCop and .NET analyzers** are on for everything except the vendored submodules
  (see [Directory.Build.props](../../Directory.Build.props)). Warnings aren't errors, but the
  build is expected to stay at its current warning count.
- **Guid type codes**: the low byte of an entity guid is its controller id. `entityId & 0xFF`
  tells you what kind of entity it is, and the server routinely masks it off
  (`& 0xffffffffffffff00`) to get the lookup key. See
  [GUIDService.cs](../../UdpHosts/GameServer/GUIDService.cs) and
  [Lib/Core.Data/EntityGuid.cs](../../Lib/Core.Data/EntityGuid.cs).

## Building

```
git clone --recurse-submodules …      # AeroMessages, BepuPhysics2, Bitter are submodules
dotnet build PIN.sln
dotnet test PIN.sln                   # Tests/GameServer.Tests
```

Builds clean on macOS and Linux with the .NET 10 SDK and no game installed. Running it needs
`clientdb.sd2` from the client, so compile-checking, the tests and code review stay viable offline;
see [the Cookbook](10-cookbook.md#what-can-be-verified-without-the-game).
