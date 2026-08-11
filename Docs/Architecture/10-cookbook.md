# Layer 10: Cookbook

Task-shaped recipes, plus what can be done without the game running.

## Where do I add…?

### …a handler for a message the client sends

[Layer 2](02-networking.md). Find or add the controller under
[Controllers/](../../UdpHosts/GameServer/Controllers), add a method with `[MessageID((byte)Commands.X)]`,
`packet.Unpack<X>()`, done. Controllers are discovered by reflection; no registration needed.

If the message id is unrecognised the server logs `Unrecognized MsgID for GSS Packet` with a hex
dump. That log line is how you find out an id exists in the first place.

### …a new replicated field on an entity

[Layer 4](04-entities-and-replication.md). The field itself is Aero-generated, so it must exist in
the [AeroMessages](../../Lib/AeroMessages) submodule first. On our side, add a `Set*` method on the
entity that writes every view and controller carrying that field, and let the periodic
`FlushChanges` deliver it (or call `FlushChanges` directly if it must not be coalesced).

### …an ability command

[Layer 5](05-aptitude-abilities.md). Usually: pick a stub from a `Todo/` folder, implement
`Execute`, uncomment its case in
[Systems/Aptitude/Factory.cs](../../UdpHosts/GameServer/Systems/Aptitude/Factory.cs), move the file
up a folder and fix the namespace.

If the command modifies state that must be undone when its effect ends, register in
`context.Actives` with an `ICommandActiveContext` holding the prior value and restore it in
`OnRemove`. `SetWeaponDamageCommand` is the model.

### …an admin/debug command

[Systems/Admin/Commands](../../UdpHosts/GameServer/Systems/Admin/Commands). Subclass `ServerCommand`,
add `[ServerCommand(description, usage, "name", "alias")]`, override `Execute`. `AdminService`
discovers it by reflection at construction; `help` lists it automatically. `context.Target` is
whatever the player last picked with `target`, which makes these the cheapest way to test a system
on a specific entity.

Commands are typed into the in-game Admin chat channel as a bare word, with no leading slash: the
client eats `/`-prefixed input as its own commands, so `AdminService` never sees it. That is also
why the server's own chat commands use a `\` prefix instead.

### …a new entity type

[Layer 4](04-entities-and-replication.md), and remember to extend
`EntityManager.ScopeIn`/`ScopeOut`. Those are explicit per-type chains, so a type missing from them
is invisible to clients.

### …a lookup into game data

[Layer 8](08-static-data.md). Record → `StaticDBLoader.Load*` → field + `Init` line + accessor in
`SDBInterface`. Composite joins belong in `SDBUtils`.

### …a test

[Tests/GameServer.Tests](../../Tests/GameServer.Tests), which is xUnit and needs no clientdb. It
reaches anything reachable without a `Shard`, so the practical question is usually whether the logic
you want to cover can be lifted out of an entity or a tick loop first. `SplashFalloff` and
`ShieldRecharge` were both pulled out of their callers for exactly that reason, and both are small
enough to show what a worthwhile extraction looks like.

## Debugging playbook

| Symptom | First thing to check |
|---------|---------------------|
| Client action does nothing | Controller dispatch swallows handler exceptions; look for `HandlePacket Caught` in the log |
| Ability does nothing | Filter logs by the activation's `ExecutionId`; `Chain.Execute` logs each command it runs |
| Effect applies but client ignores it | Status-effect change-time collision; see `NextStatusEffectChangeTime` in [BaseAptitudeEntity.cs](../../UdpHosts/GameServer/Entities/BaseAptitudeEntity.cs) |
| Owner sees a change, others don't (or vice versa) | A `Set*` that writes the controller but not the view, or the reverse |
| Entity invisible to clients | Missing from `ScopeIn`, or `Scoping`/scope range wrong |
| Shots pass through things | Body never created, or the hit collidable isn't kinematic / isn't in `_bodyToEntityId` |
| Shots register but deal no damage | Hostility; run `hostility` on the target, see [layer 6](06-combat-and-damage.md) |
| Damage is lower than expected | Range decay; `dbg_weapon` prints the resolved curve and samples it |
| Null reference deep in a command | An SDB `Get*` returned `null` for a missing id |
| Client requests an endpoint we lack | Watch the `WebHost.CatchAll` log |

Useful switches: `Preferences.DebugWeapon` for live spread telemetry, `DebugProjectileHitCallbacks`
for projectile traces, log level via `--loglevel` or `App.config`.

## What can be verified without the game

The solution builds and analysers run on macOS/Linux with no Firefall installation. Only *running*
the server needs `clientdb.sd2`; compiling doesn't.

Anything that lands without a client behind it goes in the queue at
[Docs/In-Game-Tests](../In-Game-Tests/README.md) rather than being assumed working.

```
git submodule update --init --recursive     # AeroMessages, BepuPhysics2, Bitter
dotnet build PIN.sln                        # ~30s clean, expect warnings, 0 errors
dotnet build PIN.sln -c Release             # what CI runs
dotnet test PIN.sln                         # Tests/GameServer.Tests, no clientdb needed
```

CI ([.github/workflows](../../.github/workflows)) builds Debug and Release and runs the tests on
Linux, Windows and macOS against .NET 10 and .NET 11.

That means the following is fully checkable offline:

- Compilation, StyleCop and .NET analyser warnings on any change
- Anything expressible as a pure function over inputs: spread and PRNG maths, `RegistryOp`,
  splash falloff, damage composition, `SDBUtils` joins, guid packing, packet header encode/decode
- Reading and reasoning about protocol/data flow, which is most of the work in this codebase

[Tests/GameServer.Tests](../../Tests/GameServer.Tests) covers the first slice of that second bullet.
Worth knowing what it does and doesn't buy: a test can say the range decay curve does what
`DamageFalloff.Resolve` means it to do, but only the client can say whether that's the curve Firefall
shipped. Where a model is a guess, the test pins the guess and the in-game entry checks it.

And the following genuinely needs the game:

- Whether the client accepts a netfield shape or a message layout
- Client-side prediction agreement (spread, movement, effect timing)
- Anything about what the client renders or how it feels
