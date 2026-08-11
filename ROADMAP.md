# Roadmap

Where the server is going, in dependency order. The destination here is a vertical slice, just one zone that plays.

For what the code does today, read [Architecture Guide](Docs/Architecture/README.md). This
document only covers what's missing and what order to fix it in. Checks that need a running client
go in [In-Game Tests](Docs/In-Game-Tests.md) as work lands.

## The destination

A player logs into zone 448, gets noticed by NPCs that close and shoot back, kills them for XP and
loot, scans the ground for a deposit worth working, calls down a thumper and defends it through
extraction for the resources it pulls up, logs out, and comes back to the same character. One
player, one zone, one loop that closes.

That's the slice, every system it touches is one Firefall needs anyway, so nothing is throwaway.

## Not on the path

These are all real work and some of them are more interesting than what's below. They're excluded
because the slice closes without them.

| Out of scope | Why it can wait |
|--------------|-----------------|
| PvP | Every player character is faction 1, so `HostilityRules` already forbids it. Nothing in the loop needs it |
| Other zones | The server hosts one `ZoneId` per process. A second zone is configuration, not code, until zone transitions matter |
| The remaining ~197 aptitude stubs | Implement the ones the slice's abilities actually hit, when they turn out to be hit. Working the folder alphabetically is unbounded |
| Projectile travel time, gravity, bounce | Hitscan resolves hits correctly. Travel time changes feel, not whether the loop closes |
| Damage type resistance tables | Loaded but unused. Damage lands without them; they make it correct |
| Turret damageability | `Turret_ObserverView` has no health field, so the client doesn't model turrets as shootable either. Fixing it means protocol work for a small payoff |
| Army, mail, market, chat beyond what exists | Social systems. None of them gate a session |
| Crafting and blueprints | Spending resources needs `RequireResource` and `RequireResourceFromTarget`, both stubs, plus `Blueprint_Resources` which nothing reads. Gathering is the loop; crafting is a second one |

## Dependency order

```
M1 confirm the combat models
 └─> M2 NPCs that fight back
      ├─> M3 resources come out of the ground
      │    ├─> M4 where you thump matters
      │    └─> M7 an encounter that plays
      └─> M5 killing something pays

M6 persistence across sessions   wants M3 and M5 landed, so there's something worth saving
M8 session stability             independent, but "playable" isn't honest without it
```

M3 and M5 don't depend on each other. M3 doesn't strictly depend on M2 either, since a thumper pays
out fine with nothing attacking it, but it's sequenced after because a payout is more interesting
once something is trying to stop you.

M3 and M4 are the same feature cut in half on purpose. M3 makes a thumper pay anything at all and is
a day's work; M4 makes what it pays depend on where you put it, and is the only milestone here whose
data has to be invented rather than recovered.

---

## M1: Confirm the combat models

Three combat numbers are guesses that a real client answers in an afternoon: the faction stance
encoding, the range decay curve, and whether `Battleframe` shipped live shield values. All three
are already queued in [In-Game Tests](Docs/In-Game-Tests.md), and all three degrade safely when
wrong.

It goes first because everything below tunes against these. Building AI that fights the player is
harder to judge when you can't tell whether a fight feels wrong because the AI is bad or because
shields are invented.

| Work | Where |
|------|-------|
| Answer H1, fix `ToStance` for the values logged, delete the cross-faction guard | [SDBUtils](UdpHosts/GameServer/StaticDB/SDBUtils.cs), test queue H1-H6 |
| Check the decay curve against real client damage numbers with `dbg_weapon` | [DamageFalloff.cs](UdpHosts/GameServer/Systems/ProjectileSim/DamageFalloff.cs) |
| Dump `dbitems::Battleframe` and find out whether `base_shields` and the recharge pair hold live values | [Tools/MinimalSDB](Tools/MinimalSDB) in `dump` mode |
| Replace the placeholders with whatever the dump says, or record that they're invented on purpose | [HardcodedCharacterData.cs](UdpHosts/GameServer/Data/HardcodedCharacterData.cs) |

Exit: no combat number in the server is an unconfirmed guess, or the ones that remain are written
down as deliberate.

Size is small and it's nearly all client time rather than code. The risk is that a stance encoding
that isn't a signed scale means rewriting `ToStance` against whatever the log shows, which is still
one method.

---

## M2: NPCs that fight back

[AIEngine.cs](UdpHosts/GameServer/AIEngine.cs) is an empty `Tick`. It's called first in the shard
loop and does nothing, which makes this the single largest gap between the server and a game.

More of the pieces exist than expected. NPCs and players are the same `CharacterEntity`, so
NPCs already load a chassis and both weapon ids from `dbmonster`, already take damage through the
same funnel, and already replicate movement through `RefreshMovementView` whenever something moves
them. Nothing moves them, decides what they want, or pulls a trigger.

Split it three ways, in this order:

**Perception and target selection.** Pick a target from what's in scope. `HostilityRules.GetStance`
already answers who's a valid enemy and `PhysicsEngine.TargetRayCast` answers line of sight, so
this is a scoring loop plus a threat table, not new machinery.

**Locomotion.** The hard part, because movement is client-authoritative and an NPC has no client.
`SetPosition` replicates correctly, so the wire side is done; what's missing is anything that
decides where. There's no navmesh, and world collision only loads when `LoadMapsCollision` is on.
The first pass should be leashed direct steering with a downward raycast to clamp to ground, and
should accept that NPCs will walk into walls. Pathing is its own project and shouldn't block a
monster shooting back.

**Attacking.** Two paths already work server-side, and the choice between them matters. Abilities
activate from the server today, which is how `Thumper` drives its own state machine. Weapon fire
doesn't: `WeaponSim.OnFireWeaponProjectile` is driven by a client fire message and `PRNG.Spread`
seeds off the client's time, so an NPC needs either a synthesised seed or a direct call into
`ProjectileSim.FireProjectile`. Prefer abilities where `dbmonster` names one, since that's what the
data was built for.

Then wire death to something. `CharacterEntity.Die` broadcasts `KilledEvent` and schedules an NPC
despawn 30 seconds later, and that's the whole of it. Nothing else in the server learns that
anything died, which is what M5 and M7 both need.

| Work | Where |
|------|-------|
| Threat table, target selection, line of sight | new, under `Systems/AI`, called from `AIEngine.Tick` |
| Leashed steering and ground clamping | same, plus `CharacterEntity.SetPosition` |
| Server-side attack entry point | [WeaponSim](UdpHosts/GameServer/Systems/WeaponSim), [ProjectileSim](UdpHosts/GameServer/Systems/ProjectileSim), `AbilitySystem.HandleActivateAbility` |
| Death notification other systems can subscribe to | `CharacterEntity.Die`, [EventBus](UdpHosts/GameServer/Systems/SystemEvents) |
| Spawn groups worth fighting, replacing the hardcoded debug row of monsters | [EntityManager.cs](UdpHosts/GameServer/Systems/EntityManager/EntityManager.cs) around line 360 |

Exit: spawn a hostile monster with `npc`, walk into its range, and it turns, closes, shoots, and
kills you. Kill it instead and it dies, drops nothing yet, and despawns.

This is the biggest milestone on the list and the one most likely to split further once started.
Target selection and attacking are testable offline as pure functions; nothing about how the client
renders NPC movement is, so expect the locomotion pass to need several trips to the game machine.

---

## M3: Resources come out of the ground

The thumper collects nothing. `SetProgress` interpolates 0 to 1 between two timestamps, completion
fires its abilities, and the entity is removed. No resource ever changes hands.

The ledger to pay into already works. `CharacterInventory` keeps resources keyed by SDB type id
with add, consume and query, pushes partial `InventoryUpdate` messages once `EnablePartialUpdates`
is set at login, and includes them in the full sync. So unlike item loot in M5, none of this needs
protocol work. What's missing is a connected path and the data to send through it.

Two cuts:

1. `ModifyOwnerResourcesCommand` is implemented, its def JSON loads, and `CustomDBInterface` has the
   accessor. Only its `case` in `Factory.LoadCommand` is commented out, so no chain can construct
   it. That one is a single line.
2. Of the 145 defs in `aptgss_ModifyOwnerResourcesCommandDef.json`, exactly one carries real values
   (id 50329, 200 crystite). The rest are id-and-comment shells, so what each grant does still has
   to be recovered from the client's ability data.

That buys a flat grant per beacon, the same pile every time. What a particular patch of ground
actually holds is M4, and deliberately not this milestone.

Today the only thing that puts resources in the ledger is
`HardcodedCharacterData.FallbackInventoryResources`, a fixed list every character gets at login.
Moving one resource because of something the player did is the whole milestone.

| Work | Where |
|------|-------|
| Uncomment the `ModifyOwnerResources` case | [Factory.cs:344](UdpHosts/GameServer/Systems/Aptitude/Factory.cs#L344-L345) |
| Pay out on thumper completion, hardcoded per beacon | [Thumper.cs](UdpHosts/GameServer/Systems/Encounters/Encounters/Thumper.cs), its `CompletedAbility` chain |
| Fill in the grant defs that matter | `StaticDB/CustomData/Todo/aptgss_ModifyOwnerResourcesCommandDef.json` |

Exit: call down a thumper, let it finish, and come away holding more crystite than you started with,
updated in the UI without a relog.

Small, and the cheapest reward on the list, which is most of why it's this early. Nothing in it is
unknown, which is exactly why the unknowns were pushed into M4 instead of being allowed to hold this
up.

---

## M4: Where you thump matters

Thumping isn't a decision yet. `nodeType` is the literal `20` at both call sites, with the TODO next
to it saying as much, so every deposit in the world is the same deposit and the ground you pick
means nothing. This milestone is the scan, the map behind it, and a payout that comes from the spot
rather than a constant.

The protocol survives. Four messages describe the whole mechanic and none of them carry real data
today:

- `FoundResourceAreas` (2:138) is the density overlay, an array of `{Center, Unk4, NodeTypeId}`.
  Nothing sends it. `Unk4` is unidentified, radius or richness being the obvious guesses.
- `GeographicalReportRequest` (2:184) is the ground reading, and it arrives carrying the client's own
  verdict on the spot: `OK`, `NOTHUMPINGZONE` or `INVALIDSURFACE`. No handler exists.
- `GeographicalReportResponse` (2:139) answers with a `ScanId`, a position, a `Valid` byte and a
  composition array. This one the server does send, but only from `MapOpened` and hardcoded to
  `Valid = 0`, which the client renders as an empty report.
- `ResourceNodeCompletedEvent` (2:137) closes the loop with the same `ScanId`, a quantity and the
  composition actually extracted. Nothing sends it.

The yield model survives too, and it's better than expected.
[ResourceNodeTypeResource](UdpHosts/GameServer/StaticDB/Records/dbzonemetadata/ResourceNodeTypeResource.cs)
is keyed by node type and carries `CenterLow`/`CenterHigh`, `EdgeLow`/`EdgeHigh` and
`ItemQualityLow`/`ItemQualityHigh` per item. A deposit was never uniform: quantity and quality both
fall off from the middle to the rim, sampled from a range. That's what the overlay was drawing and
why standing in the right place paid. Neither it nor `ResourceNodeType` is loaded.

What doesn't survive is where anything is. The client only ever learned deposit positions by
scanning, so `clientdb.sd2` never carried them and there's nothing to import. The distribution has to
be generated, which makes this the one milestone with no ground truth to check against and the one
place where a design decision is genuinely ours. A seeded per-zone scatter is indistinguishable from
the original as far as the client is concerned, and the SDB gradient gives each blob its falloff for
free.

Placement isn't checked server-side either. `MaxSlope`, `MaxRange`, `OutdoorsHeight` and
`GroundAlignment` all sit unused on the calldown def because the client validates the spot and
reports its own verdict. Fine for one player, and it belongs with the rest of the trust problem in
the appendix.

| Work | Where |
|------|-------|
| A per-zone resource map that answers "what's near here" | new; `CustomDBInterface` is where per-zone data already lives, [ZoneLoader](UdpHosts/GameServer/Physics/ZoneLoader/ZoneLoader.cs) knows the extent |
| Load the node type and yield tables | [StaticDBLoader.cs](UdpHosts/GameServer/StaticDB/Loaders/StaticDBLoader.cs), [SDBInterface.cs](UdpHosts/GameServer/StaticDB/SDBInterface.cs) |
| Wire the scan command and send `FoundResourceAreas` | [Factory.cs:481](UdpHosts/GameServer/Systems/Aptitude/Factory.cs#L481-L482), [ResourceNodeScanDefCommand.cs](UdpHosts/GameServer/Systems/Aptitude/Commands/Other/Todo/ResourceNodeScanDefCommand.cs) |
| Handle `GeographicalReportRequest`, stop hardcoding `Valid = 0` | [BaseController.cs](UdpHosts/GameServer/Controllers/Character/BaseController.cs), its `MapOpened` handler |
| Resolve node type from position instead of `20` | [ResourceNodeBeaconCalldownCommand.cs:23](UdpHosts/GameServer/Systems/Aptitude/Commands/Calldown/ResourceNodeBeaconCalldownCommand.cs#L23) |
| Sample the gradient where the thumper landed and pay that out | the M3 grant path, plus `ResourceNodeCompletedEvent` |

Exit: scan, see deposits that aren't all alike, thump a rich one and a poor one, and come away with
visibly different piles.

Medium size and the least predictable milestone on the list. Everything else is reconnecting parts
built to fit each other; this one has a hole where the original data was. The scan def is the second
unknown, since its record has only `Id` mapped and all five entries in its JSON are shells, though
the comments name two of them: "Scan for resource nodes near the player" and "Scans for thumper
nodes in a 600 meter radius".

---

## M5: Killing something pays

Nothing rewards a kill. XP exists on the wire only as a `ProgressionXPRefresh` sent once at
scope-in, and there's no loot at all.

The XP half is mostly known shape: award on the death event from M2, hold it on the character, push
the existing message. Item loot is the part that needs protocol work first, because what the client
expects when something drops isn't established anywhere in the codebase, and the resource path from
M3 doesn't answer it. `AddLootTable`, `SpawnLoot` and `RequireLootStore` all have def JSONs sitting
unused in `CustomData/Todo/`, which is the place to start looking.

| Work | Where |
|------|-------|
| Award XP on kill, track it on the character, push progression updates | `CharacterEntity`, [EntityManager ScopeIn](UdpHosts/GameServer/Systems/EntityManager/EntityManager.cs) |
| Work out how the client wants a drop represented, then spawn it | protocol RE first; the unused loot defs in `StaticDB/CustomData/Todo/` and [CatchAll](WebHosts/WebHost.CatchAll) logs are the leads |
| Pick up a drop and put it in the bag | [CharacterInventory.cs](UdpHosts/GameServer/Data/CharacterInventory.cs), `api/v3/characters/{id}/inventories/bag` |

Exit: kill an NPC, watch XP go up, pick up what it dropped, see it in the bag.

The XP side is small. Item loot is unknown until the protocol question is answered, and that answer
could make it anywhere from a day to a milestone of its own.

---

## M6: Persistence across sessions

Everything a session produces is lost when it ends. Inventory comes from
`CharacterInventory.LoadHardcodedInventory`, health and level from `HardcodedCharacterData`, and
RIN only receives zone, outpost and playtime through `SaveCharacterSessionDataAsync`.

The decision to make first is whether RIN owns this. RIN is optional today and the server is fully
usable without it, which is a property worth keeping. Extending the gRPC contract means the
fallback path gets further from the real one, so either the fallback grows a local store or
"no RIN" stops meaning "no persistence". Make that call explicitly rather than by accident, because
every field added afterwards inherits it.

| Work | Where |
|------|-------|
| Decide RIN-owned vs local store, and what happens with no RIN | [GameServerAPI.proto](UdpHosts/GameServer/GRPC/GameServerAPI.proto), [layer 9](Docs/Architecture/09-webhosts-and-rin.md) |
| Persist XP, level, inventory and the resource ledger | `HardcodedCharacterData`, `CharacterInventory`, [GRPCService.cs](UdpHosts/GameServer/GRPC/GRPCService.cs) |
| Save on a timer and on logout, not just on zone change | `NetworkPlayer`, `Shard.MigrateOut` |

Exit: thump some crystite, kill things for XP and loot, log out, log back in, and all of it is still
there.

Medium size, low technical risk, one real architectural decision at the front.

---

## M7: An encounter that plays

The encounter framework is real and three encounters use it, but none of them involve combat.
`Thumper` is a timed state machine that advances on interactions and fires abilities at each
transition. Nothing spawns to attack it and nothing checks whether the players are still alive.

With M2 done this becomes wiring rather than new systems: spawn waves on the state transitions
that already exist, subscribe to the death event, and let the encounter fail as well as succeed.

| Work | Where |
|------|-------|
| Spawn waves keyed to encounter state | [Thumper.cs](UdpHosts/GameServer/Systems/Encounters/Encounters/Thumper.cs), [EncounterManager.cs](UdpHosts/GameServer/Systems/Encounters/EncounterManager.cs) |
| Route deaths back to the owning encounter | `EncounterComponent`, the M2 death event |
| A failure path, not just `OnSuccess` | [BaseEncounter.cs](UdpHosts/GameServer/Systems/Encounters/BaseEncounter.cs) |
| Scale the payout to how well the defence went, on top of whatever M4 says the ground holds | as above |

Exit: call down a thumper, defend it against waves that get harder, and either extract with the
resources or lose them.

Small once M2 and M3 exist, and almost entirely blocked on them. Worth resisting the urge to start
here, since an encounter with no combat in it is what already exists.

---

## M8: Session stability

Outbound reliability isn't implemented. `Control_PacketAvailable` logs client acks and discards
them, and there's no retransmit queue, so "reliable" currently means the server acks what the
client sends rather than resending what the client missed.

On a LAN with no loss this is invisible. Over the internet a dropped entity state message means the
client's copy of that entity is permanently wrong, which shows up as things that are invisible,
immortal, or standing somewhere they aren't. That's not a bug anyone can debug from the symptom, so
it needs to land before the slice gets shown to anyone playing over a real connection.

It's listed last because it blocks nothing above it, not because it's optional.

| Work | Where |
|------|-------|
| Track sent reliable messages, retransmit on missing ack | [Channel.cs](UdpHosts/GameServer/Channel.cs), `NetworkClient.Control_PacketAvailable` |
| Confirm the inbound resend path, which carries its own TODO about whether it's correct | same, [layer 2](Docs/Architecture/02-networking.md) |
| Test under induced packet loss | needs a client and a way to drop packets |

Exit: play a session with a few percent artificial loss and nothing desyncs.

Medium size and self-contained, but the failure it fixes is only reproducible with a real client on
a lossy link, which makes verification the expensive part.

---

## Keeping this current

A milestone is done when its exit criterion has been seen in game, not when the code compiles.
Move the check into [In-Game Tests](Docs/In-Game-Tests.md) as the work lands and record the result
there. When a milestone turns out to be two milestones, split it here rather than quietly widening
it, and when something on the "not on the path" list becomes necessary, move it up with the reason
it changed.

---

# Appendix: what a public server would need

None of this is scheduled and none of it should start before the slice closes. It's written down
because everything above assumes one trusted player on localhost, and it's easy to lose track of
how much the server gets away with because of that. A server that twenty strangers can connect to
is a different project from the one in the milestones, sharing maybe half its work.

## There is no identity

`AccountsController.Login` returns the same hardcoded account id to anyone who asks, and
[NetworkClient.cs:223](UdpHosts/GameServer/NetworkClient.cs#L223) takes the character guid off the
wire and believes it. Any client can claim to be any character. What's needed is real accounts, a
session token minted at web login, and the game server checking that a socket's claimed character
belongs to that session before `Player.Login` runs.

This is the biggest item here and the only one with no existing code to build from. Everything
else on this list is hardening something that already works.

## Nothing the client sends is checked

Movement is client-authoritative, weapon fire starts with a client message, and `PRNG.Spread`
seeds off the client's clock. That's fine when the client is you. With strangers it needs speed
and teleport bounds, fire rate ceilings, ammo accounting, and a server-side spread seed. Full
server authority over movement is a rewrite and shouldn't be the first move; bounds-checking what
arrives catches most of it for a fraction of the work.

## Two of these are already milestones

M8 is the one that blocks internet play outright, and its reasoning doesn't change here, it just
stops being optional. M6 does change: a public server can't have "no RIN" as a supported mode, so
the decision at the front of that milestone gets made for you, along with backups and a migration
story that a single-player server never needed.

## Shape

| Today | What a public server needs |
|-------|----------------------------|
| One process hosts one `ZoneId`, and MatrixServer answers `KISS` with a hardcoded `25001` | Something that routes a client to the right process for the right zone. Matrix can't currently say anything else |
| `_maxPlayersPerShard = 64`, gameplay single-threaded per shard | The cap is probably fine. It's never been tested above one, and scope-in batching, change flush, and the 16-bit `EntityRefMap` are where it would show |
| `Shard.RunThread` spins on `Thread.Yield()` with no sleep | A paced timestep. Every shard pegs a core right now whether or not anyone is in it, so idle zones cost the same as busy ones |
| `CurrentShortTime` wraps every ~65 seconds | Already a known source of bugs at one player. More entities and more players is more chances to land on it |
| ASP.NET dev certs and `localhost` in `firefall.ini` | Real certs, real DNS, and finding out how much the client actually validates |

## Operations

Twelve HTTP/HTTPS port pairs and two UDP listeners facing the internet, with no rate limiting
anywhere and UDP being what it is. `IsBanned` is hardcoded false and permissions come from
`HardcodedCharacterData`, so bans, reports, chat moderation and deciding who's allowed to run
commands are all greenfield. The Seq container in [compose.yaml](docker/compose.yaml) would have
to become real monitoring, and the `.bat` launchers a deploy.

## Distribution

Every player needs Firefall installed, a patched `FirefallClient.exe` and a hand-edited ini.
Assets stream from WebAsset, which costs nothing against a local install and becomes a bandwidth
bill plus a rights question against a public one, since they aren't ours to hand out. Requiring
players to bring their own install is what the README already does and where this usually lands.

## Rough order

Reliability, then identity, then persistence, then validating client input, then topology and ops.
The first and third are M8 and M6 and can't move. Identity is the one to think about early, since
"which character is this socket" reaches into login, persistence and every command that trusts a
caller.
