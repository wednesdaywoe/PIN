# Layer 1: Processes & Configuration

## The three processes

| Process | Project | Transport | Role |
|---------|---------|-----------|------|
| MatrixServer | [UdpHosts/MatrixServer](../../UdpHosts/MatrixServer) | UDP 25000 | Handshake and socket-id assignment only |
| GameServer | [UdpHosts/GameServer](../../UdpHosts/GameServer) | UDP 25001 | All in-zone simulation |
| WebHostManager | [WebHosts/WebHostManager](../../WebHosts/WebHostManager) | HTTP 4400-4411 / HTTPS 44300-44311 | Runs every HTTP API host in one process |

Optionally a fourth: **RIN** (external, [themeldingwars/RIN](https://github.com/themeldingwars/RIN)) provides
character persistence over gRPC. The GameServer degrades gracefully without it
([layer 9](09-webhosts-and-rin.md)).

`.bat` launchers at the repo root start each one against its `bin` output.

### MatrixServer

[MatrixServer.cs](../../UdpHosts/MatrixServer/MatrixServer.cs) is a four-message state machine over
fixed-size structs, not Aero messages:

- `POKE` → replies `HEHE` with a freshly generated socket id
- `KISS` → replies `HUGG` with the GameServer's port (hardcoded `25001`)
- `ABRT` → client abandoning the handshake

After `HUGG` the client connects to the GameServer directly and MatrixServer plays no further
part. Note the GameServer port is a literal in this file, so changing `GameServerSettings.Port`
alone won't move clients.

### GameServer startup

[Program.cs](../../UdpHosts/GameServer/Program.cs) → Autofac container → `GameServer.Run()`.

1. [GameServerModule.cs](../../UdpHosts/GameServer/GameServerModule.cs) registers settings, Serilog,
   and loads the client's `clientdb.sd2` via FauFau into a `StaticDB` instance.
2. [GameServer.cs](../../UdpHosts/GameServer/GameServer.cs) constructor: `SDBInterface.Init(sdb)`,
   `CustomDBInterface.Init()`, `GRPCService.Init(...)`. All static data is loaded before any
   client can connect, which is why `SDBInterface` is a static class everywhere else.
3. `Startup()`: `DataUtils.Init()`, controller `Factory.Init()`, then `NewShard(ct)` which
   constructs a `Shard` and starts its thread.
4. Each inbound packet's first 4 bytes are the socket id; unknown ids create a `NewNetworkPlayer`
   and migrate it into the least-populated shard (`_maxPlayersPerShard = 64`).

Shard ids are `serverId | (shardId << 8) | Controllers.GenericShard`. The low byte is a controller
type code, same as entity guids.

## Configuration

### GameServer

[GameServerSettings.cs](../../UdpHosts/GameServer/GameServerSettings.cs) is the typed surface;
values come from `App.config` (built from [App.Default.config](../../UdpHosts/GameServer/App.Default.config)),
then CLI options overlay a subset ([CliOptions.cs](../../UdpHosts/GameServer/CliOptions.cs), currently
only log level).

The settings that actually change behaviour day to day:

| Setting | Meaning |
|---------|---------|
| `StaticDBPath` | Path to the client's `system/db/clientdb.sd2`. Required; the server won't start without it |
| `ZoneId` | The single zone this process hosts (default `448`) |
| `AssetDBPath` | Client `system/assetdb`, source of pose/collision tagfiles |
| `MapsPath` / `AssetsPath` | PIN Maps/Asset data, used for world collision |
| `LoadMapsCollision` | Off by default; world geometry only collides when this is on |
| `LoadZoneEntities` | Spawn outposts/deployables/NPCs on first tick |
| `GrpcChannelAddress` | RIN address; empty disables the gRPC listener entirely |

Since the build output config is what runs, edit
`UdpHosts/GameServer/bin/<Config>/net10.0/GameServer.dll.config` for local paths, or
`App.config` for values you want to keep.

### WebHosts

[WebHosts/WebHostManager/config/appsettings.json](../../WebHosts/WebHostManager/config/appsettings.json),
with an `appsettings.Development.json` overlay selected by `ASPNETCORE_ENVIRONMENT`. `Program.cs`
holds the list of host types to start; each `WebServer` class declares its own ports.

## Dependency injection

Autofac, one module per UDP host (`GameServerModule`, `MatrixServerModule`). The container is
built once in `Main` and resolves exactly two things: the settings object and the server. Below
that point construction is manual, and `Shard` news up every system in its constructor. There's no
container inside the game loop and putting one there would be a large change, so a new system is
registered by adding a field to [Shard.cs](../../UdpHosts/GameServer/Shard.cs).

Static singletons that sidestep DI on purpose, because they're immutable after startup:
`SDBInterface`, `CustomDBInterface`, `GRPCService`, `Controllers.Factory`, `GuidService`,
`DataUtils`.
