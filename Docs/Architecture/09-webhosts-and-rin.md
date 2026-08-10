# Layer 9: Web Hosts & RIN

Firefall's client makes a lot of ordinary HTTP calls alongside the UDP connection: login,
character list, inventory, market, assets. PIN answers them all from one process,
[WebHostManager](../../WebHosts/WebHostManager), which starts every host in parallel.

## The hosts

| Project | HTTP | HTTPS | Purpose |
|---------|------|-------|---------|
| [WebHost.OperatorApi](../../WebHosts/WebHost.OperatorApi) | 4400 | 44300 | First contact; API versions and capabilities. This is the `OperatorHost` in `firefall.ini` |
| [WebHost.WebAsset](../../WebHosts/WebHost.WebAsset) | 4401 | 44301 | Asset streaming, icons, textures, audio |
| [WebHost.ClientApi](../../WebHosts/WebHost.ClientApi) | 4402 | 44302 | The big one: accounts, characters, armies, mail, oracle, zones |
| [WebHost.InGameApi](../../WebHosts/WebHost.InGameApi) | 4403 | 44303 | Bulkier in-game data |
| WebAccount | 4404 | 44304 | (catch-all) |
| Frontend | 4405 | 44305 | (catch-all) |
| [WebHost.Store](../../WebHosts/WebHost.Store) | 4406 | 44306 | RedBean store |
| [WebHost.Chat](../../WebHosts/WebHost.Chat) | 4407 | 44307 | Chat channels |
| [WebHost.Replay](../../WebHosts/WebHost.Replay) | 4408 | 44308 | Replay actions |
| Web | 4409 | 44309 | (catch-all) |
| [WebHost.Market](../../WebHosts/WebHost.Market) | 4410 | 44310 | Marketplace |
| RedHanded | 4411 | 44311 | (catch-all) |

[WebHost.CatchAll](../../WebHosts/WebHost.CatchAll) backs every unimplemented endpoint and logs
what was requested. Watching its log is the fastest way to find out what the client wants next.

HTTPS uses the ASP.NET development certificate, hence `dotnet dev-certs https --trust` in setup.

## Structure

Each host is an ASP.NET Core app deriving from `BaseWebServer`
([Lib/Shared.Web](../../Lib/Shared.Web)) and overriding two hooks:

```csharp
public class WebServer : BaseWebServer
{
    protected override void ConfigureChildServices(IServiceCollection services) { ... }
    protected override void ConfigureChild(IApplicationBuilder app, IWebHostEnvironment env) { ... }
}
```

`WebHost.ClientApi` is organised by feature folder (`Characters/`, `Armies/`, `Login/`, `Mail/`,
`Oracle/`, `Zones/`, …), each holding controllers, `Models/`, and a repository. Other hosts are
flatter, with a `Controllers/` folder.

To add an endpoint: find the right host, add the controller action and its response model, and
match the client's expected JSON exactly. The OpenAPI specs in
[themeldingwars/Documentation](https://github.com/themeldingwars/Documentation/tree/master/Networking)
are the reference.

## RIN over gRPC

[RIN](https://github.com/themeldingwars/RIN) is the separate persistence/management service. The
GameServer talks to it through [GRPC/GRPCService.cs](../../UdpHosts/GameServer/GRPC/GRPCService.cs),
with the contract in [GameServerAPI.proto](../../UdpHosts/GameServer/GRPC/GameServerAPI.proto).

Two directions:

- **Request/response**: `GetCharacterAndBattleframeVisualsAsync(characterId)` during login,
  `SaveCharacterSessionDataAsync(...)` for zone/outpost/playtime.
- **Duplex stream**: `ListenAsync` consumes `Event` messages (army applications, character
  updates) and dispatches to
  [GRPC/EventHandlers](../../UdpHosts/GameServer/GRPC/EventHandlers). `GameServer.ListenGrpcAsync`
  retries every 30s if the stream drops.

RIN is optional. `NetworkPlayer.Login` wraps the character fetch in a try/catch and falls back to
[HardcodedCharacterData.FallbackData](../../UdpHosts/GameServer/Data/HardcodedCharacterData.cs)
with a hardcoded chassis, so the server is fully usable without it. Setting `GrpcChannelAddress` to
an empty string skips the listener entirely.

That fallback path is why a lot of character state is still hardcoded. Inventory comes from
`CharacterInventory.LoadHardcodedInventory()`, and max health, level and permissions come from
`HardcodedCharacterData`. Anything that should eventually persist lives there.
