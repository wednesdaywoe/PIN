---
project: pin
kind: stream
title: "M2: NPCs That Fight Back"
relates:
  - ../PROGRESS.md
---

# M2: NPCs That Fight Back

[AIEngine.cs](../../UdpHosts/GameServer/AIEngine.cs) is an empty `Tick`. It's called first in the
shard loop and does nothing, which makes this the single largest gap between the server and a
game.

More of the pieces exist than expected. NPCs and players are the same `CharacterEntity`, so NPCs
already load a chassis and both weapon ids from `dbmonster`, already take damage through the same
funnel, and already replicate movement through `RefreshMovementView` whenever something moves
them. Nothing moves them, decides what they want, or pulls a trigger.

Split it three ways, in this order:

**Perception and target selection.** Pick a target from what's in scope.
`HostilityRules.GetStance` already answers who's a valid enemy and `PhysicsEngine.TargetRayCast`
answers line of sight, so this is a scoring loop plus a threat table, not new machinery.

**Locomotion.** The hard part, because movement is client-authoritative and an NPC has no client.
`SetPosition` replicates correctly, so the wire side is done; what's missing is anything that
decides where. There's no navmesh, and world collision only loads when `LoadMapsCollision` is on.
The first pass should be leashed direct steering with a downward raycast to clamp to ground, and
should accept that NPCs will walk into walls. Pathing is its own project and shouldn't block a
monster shooting back. — *the raycast has nothing to hit; see below for what replaced it.*

**Attacking.** Two paths already work server-side, and the choice between them matters. Abilities
activate from the server today, which is how `Thumper` drives its own state machine. Weapon fire
doesn't: `WeaponSim.OnFireWeaponProjectile` is driven by a client fire message and `PRNG.Spread`
seeds off the client's time, so an NPC needs either a synthesised seed or a direct call into
`ProjectileSim.FireProjectile`. Prefer abilities where `dbmonster` names one, since that's what the
data was built for. — *`dbmonster` names none; see below.*

Then wire death to something. `CharacterEntity.Die` broadcasts `KilledEvent` and schedules an NPC
despawn 30 seconds later, and that's the whole of it. Nothing else in the server learns that
anything died, which is what M5 and M7 both need.

| Work | Where |
|------|-------|
| Threat table, target selection, line of sight | new, under `Systems/AI`, called from `AIEngine.Tick` |
| Leashed steering and ground clamping | same, plus `CharacterEntity.SetPosition` |
| Server-side attack entry point | [WeaponSim](../../UdpHosts/GameServer/Systems/WeaponSim), [ProjectileSim](../../UdpHosts/GameServer/Systems/ProjectileSim), `AbilitySystem.HandleActivateAbility` |
| Death notification other systems can subscribe to | `CharacterEntity.Die`, [EventBus](../../UdpHosts/GameServer/Systems/SystemEvents) |
| Spawn groups worth fighting, replacing the hardcoded debug row of monsters | new, under [Systems/Spawning](../../UdpHosts/GameServer/Systems/Spawning/), loaded from `EntityManager.SpawnZoneEntities` |

Exit: spawn a hostile monster with `npc`, walk into its range, and it turns, closes, shoots, and
kills you. Kill it instead and it dies, drops nothing yet, and despawns.

This is the biggest milestone on the list and the one most likely to split further once started.
Target selection and attacking are testable offline as pure functions; nothing about how the client
renders NPC movement is, so expect the locomotion pass to need several trips to the game machine.

H5 in [Hostility](../In-Game-Tests/Hostility.html) is permanently blocked until this lands — it needs
an NPC that actually decides to shoot. — *unblocked by the attack pass; run it with
[N2](../In-Game-Tests/NPC-Combat.html).*

## What's landed

**Perception and target selection — confirmed in game.** New
[Systems/AI](../../UdpHosts/GameServer/Systems/AI/) holds a per-NPC [ThreatTable.cs](../../UdpHosts/GameServer/Systems/AI/ThreatTable.cs)
(decaying threat scores, unit-tested in isolation — see `Tests/GameServer.Tests/AI/ThreatTableTests.cs`)
and [TargetSelection.cs](../../UdpHosts/GameServer/Systems/AI/TargetSelection.cs), which scores
every hostile `CharacterEntity` in range and with line of sight (`HostilityRules.AreHostile` +
`Physics.TargetRayCast`, the same idiom the `target` admin command already uses) into that table
each tick and picks the highest-scoring live target once it crosses an engage threshold.
[AIEngine.cs](../../UdpHosts/GameServer/AIEngine.cs) now takes the shard (matching every other
system) and drives this for every non-player-controlled, alive `CharacterEntity`, exposing the
result as `AIEngine.CurrentTargetOf(entityId)` for locomotion and attacking to read once they land.

Detection range and the threat gain/decay/engage numbers are invented — `dbmonster` has no
perception field to read them from — tracked as [DATA-10](../gaps/data.md#data-10), where a research
pass on 2026-08-13 established that retail's numbers ship inline on two thirds of the monster table,
that the instance rows behind the rest never reached the client, and that PIN's then-40m perception
was wider than any `perceptionDist` in the file. It has since been narrowed to that file's 25m
ceiling. The aim geometry
and line-of-sight check it shares with the attack pass now live in
[Sightline.cs](../../UdpHosts/GameServer/Systems/AI/Sightline.cs), because the two have to agree:
selection deliberately holds a target through a moment of broken sight, so the shot is what has to
look again.

**Attacking — confirmed in game.** An NPC with a target now faces it and shoots it.
[NpcCombat.cs](../../UdpHosts/GameServer/Systems/AI/NpcCombat.cs) runs after target selection each
tick: it aims, sets body yaw, re-checks range and line of sight, and pushes rounds through the same
`WeaponSim.OnFireWeaponProjectile` a client fire message lands in, so an NPC's shots spread, fall off
with range, crit and respect `CanDamage` on exactly the player's code. The `PRNG.Spread` seeding
problem turned out not to be one — the seed is just a timestamp, and `Shard.CurrentTime` is as good a
one as the client's.

Abilities were meant to be the preferred path here. They can't be: `dbcharacter::Monster` has no
ability column at all, only `Weapon1Id`/`Weapon2Id` and the *names* of behaviour trees PIN doesn't
load (`Behavior`, `BehaviorOffensive`, `BehaviorDefensive`). Weapons are what the data actually
offers, and `LoadMonster` already slots both of them.

Two things the server had never needed an opinion about, because a player's client decides them:

- **Burst cadence.** [AttackWindow.cs](../../UdpHosts/GameServer/Systems/AI/AttackWindow.cs) resolves
  `MsPerBurst`, `MsBurstDuration` and `RoundsPerBurst` into a range and a rhythm. Pure and unit-tested,
  and **N5 confirmed in game on 2026-08-12** that the rhythm reads as a weapon firing. The tests pin
  the reading; N5 pins that the reading is plausible. Whether it matches Firefall's exact cadence is
  still a capture question.
- **Which way a body faces.** `Facing.Towards` builds a yaw quaternion from the aim direction. That
  the stored orientation is the *inverse* of the world rotation was never in doubt (both
  `GetProjectileOrigin` and the physics engine invert it); that local forward is +X was a guess, and
  **N1 confirmed it in game on 2026-08-12**. Settled, and the only place in the server that turns a
  direction into an orientation.

Ammo, clips and reloading aren't modelled — an NPC fires forever. Nothing paces sub-shots faster than
the AI tick, so a burst can't stack several rounds onto one timestamp and fire them all down the same
line.

That turns out to be more than a fairness problem, recorded later as
[DATA-14](../gaps/data.md#data-14): for a weapon whose cycle is shorter than its reload, the missing
pause *is* most of its real cadence. Monster 281's sniper carries a one-round clip and a second-long
reload, so PIN fires it four times a second and it does 2000 dps where retail's was about 450. The
error is invisible on the sustained-fire weapons the test queue has used and enormous on the one-shot
ones, which are exactly the ids someone reaches for when a fight is taking too long.

Also found by building this, and the more consequential bug of the two:
[DATA-11](../gaps/data.md#data-11). Weapon resolution multiplied by a `WeaponTemplateModifiers`
multiplier of 0 instead of reading it as "unset", which zeroed the range of 269 weapons and the
damage of 220 — player and NPC alike. It had been live in every weapon the server ever resolved. No
player ever tripped it, because a player only ever fires what they chose to equip; it took an NPC
picking its weapon out of `dbmonster` to land on the broken rows. The first two NPC test entries
failed in two different ways because of it, neither of them looking like a data bug: N2's monster
aimed and never fired (rifle with 0 reach), N6's fired with full muzzle VFX into a target that never
lost health (rounds worth 0 damage).

Fixed along the way: `AIEngine.Tick` was reading the shard's `deltaTime` as seconds when it's the raw
loop delta in milliseconds, on a loop that spins as fast as the thread allows. Threat gain and decay
were therefore roughly a thousand times too fast and every visible hostile crossed the engage
threshold on its first tick, while the raycasts ran thousands of times a second per NPC. It now
follows the same shape as `ShieldSim` and `WeaponSim`: its own 50ms clock, elapsed seconds derived
from the wall clock. That interval is also the finest grain an NPC's rate of fire can be paced at.

**Locomotion — code complete, not yet seen in game.** An NPC with a target now walks to it.
[NpcMovement.cs](../../UdpHosts/GameServer/Systems/AI/NpcMovement.cs) runs between target selection
and the attack pass each tick, so the range and line-of-sight checks are made from where the NPC
ends the tick rather than where it started it. It steers straight at the target, stops 12m short —
or at 80% of its weapon's reach, whichever is nearer, so a Melded Aranha's 5m claw closes to 4m
instead of standing uselessly at 12 — and walks home again when it loses the target or chases more
than 50m from where it spawned. The steering itself is
[Steering.cs](../../UdpHosts/GameServer/Systems/AI/Steering.cs), pure and unit-tested.

Two things it isn't: there is no pathing and no navmesh, so it walks into whatever is on the line,
which the milestone accepted up front; and several NPCs on one target all converge on the same spot
and stand in each other, because nothing separates them.

**Speed came out of the data, which was not where the data was.** `dbmonster` has `NormalSpeed` and
`FastSpeed` columns and 3095 of its 3109 rows hold -1 in the one that matters — they are an
override, and almost nothing overrides. The real speed is on the monster's `ChassisId`, a
`dbitems::Battleframe`, the same record a player's frame speed comes out of; that resolves for 2971
monsters, and the 124 left over take the table's own most common value rather than an invented one.
A Chosen Fiend runs at 6 m/s, a Melded Aranha at 11. Reading the -1 literally would have given every
monster in the game a negative speed, which is [DATA-11](../gaps/data.md#data-11)'s lesson arriving a
second time with a different sentinel: in this database an out-of-band number means "unset" far more
often than it means anything.

**The ground is the part with no good answer.** The milestone doc planned "a downward raycast to
clamp to ground", and there is nothing to raycast against: `LoadMapsCollision` is off and `MapsPath`
is empty, so the physics world holds entity colliders and no terrain at all — the thing
[N3](../In-Game-Tests/NPC-Combat.html) found the hard way. What the server does have is where a player
is standing, which is a ground height measured at that spot by the only participant that owns the
terrain. So an NPC takes its target's height as ground and climbs toward it, with two rules keeping
that from turning into a flying monster: the height is only believed while the target is *not*
airborne (`IsAirborne`, which the client reports on every pose message), and no destination is
walked to up a slope steeper than 45° measured over the whole remaining approach. The second rule
is what refuses a player standing on a roof 10m up and 3m away. Both are guesses about what looks
right, and N12 is where they get looked at.

Also new, and small: `CharacterEntity.SetMovementState` sets the state a client animates from and
keeps `MovementStateContainer` in step with it, so a running NPC also reads as moving to `WeaponSim`
and takes the same spread penalty a running player does. The running value itself (`0x2004`) is
derived from the `Movestate` and `MovementFlags` enums and has never been seen on the wire — on top
of a packing [NET-12](../gaps/network.md#net-12) already lists as unconfirmed — so N9 exists to look
at an NPC's legs.

**Death notification — code complete, not yet seen in game.** `CharacterEntity.Die` now enqueues a
`CharacterDiedEvent` (new in [Events.cs](../../UdpHosts/GameServer/Systems/SystemEvents/Events.cs))
onto the `EventBus`, flushed once per tick like everything else that goes through it, rather than
dispatched inline from inside whatever damage call happened to kill the character. `EventBus` had to
be added to `IShard` for this — it previously only reached `ChatService`, which gets the concrete
`EventBus` handed to it directly in `Shard`'s constructor, and `CharacterEntity` only ever holds the
`IShard` interface. Nothing subscribes yet; M5 (kill XP) and M7 (encounter death routing) are what
turn this from a broadcast with no listener into something that matters.

## What the client sessions found

[NPC Combat](../In-Game-Tests/NPC-Combat.html) N1–N7, all passing as of 2026-08-12. An NPC notices you,
turns to face you, respects cover, opens fire, damages you, disengages when you leave, and dies
mid-burst without leaving a corpse stuck firing. Perception, target selection and the attack pass are
confirmed against a real client rather than code-complete.

Four of the seven failed first time and only one was the AI's fault. The single largest find had
nothing to do with M2 at all — [DATA-11](../gaps/data.md#data-11), below — and a crash in the tail of
one session log closed [NET-21](../gaps/network.md#net-21). The other two:

**N4 found a real defect, now fixed and re-run green.** Engagement was bounded by the weapon's reach rather than
by perception — a Chosen Grunt Rifle carries 180m against what was then a 40m perception radius —
and threat had no ceiling, so a target that stood in front of an NPC for a minute banked several
hundred points and took minutes of decay to fall under the engage threshold. The two compounded into
an NPC that engaged at 40m and then shot you until you left draw distance. `TargetSelection` now
forgets anything past the leash and caps threat at 60, which decays back under the threshold in 6.25
seconds. N4 was measured at 40m perception / 60m leash; both have since narrowed to 25m and 37.5m,
which is the same ratio and the same behaviour at shorter range. The
leash is deliberately wider than perception so a target loitering on the boundary doesn't get dropped
and re-acquired every tick.

**N3 couldn't run and the entry was at fault; it passes now.** `LoadMapsCollision` is `false`, so the server holds no
terrain and no buildings, only entity colliders — there is no cover to break line of sight with, and
the shots that appeared to pass through scenery were passing through nothing. Re-run against a
deployable with a real collision id (395, the Battleframe Station) instead of world geometry, and it
passes. Worth remembering well beyond this test: every server-side raycast in PIN, including every
shot a player fires, currently sees an empty world with a few entities floating in it.

**Spawn groups — built, and rebuilt twice by what client sittings found.** Zone 448 comes with
thirteen monsters in three groups, described in
[spawn_group.json](../../UdpHosts/GameServer/StaticDB/CustomData/spawn_group.json) and kept
populated by [SpawnGroupSim](../../UdpHosts/GameServer/Systems/Spawning/SpawnGroupSim.cs), which
loads alongside the other world content in `EntityManager.SpawnZoneEntities` and runs its own 1s
clock. A group is a name, a respawn delay and a list of monsters each carrying its own position; a
member's place counts as empty once its entity is gone from the shard, so a kill costs the corpse's
fixed 30 seconds plus the delay. That keeps spawning off `CharacterDiedEvent`, which M5 and M7 are
the ones that want. The dead faction-test row in `TempSpawnTestEntities` is gone with it; it only
ever ran with a literal flipped in source, and `npc <id> <x> <y> <z>` does the same job without a
rebuild.

Retail's placements aren't recoverable, and it's worth being precise about that because the
neighbouring content files are. `melding.json` matches perimeter names in the client's own
`system/maps/448.zone`, because the client has to draw a melding wall. Nothing has to draw a
spawner: the zone file holds terrain, melding perimeters and cinematic paths, and no monster
placements at all. Retail's spawn tables were server-side, which is also why
`ActivateSpawnTableCommandDef` and `UpdateSpawnTableCommandDef` are two of
[DATA-5](../gaps/data.md#data-5)'s empty stubs. So the three groups are PIN's own content
([DATA-15](../gaps/data.md#data-15)), which the [restoration charter](../Restoration.md) allows.

**The first cut of the content was wrong in a way worth keeping written down.** It put three packs
15 to 25m apart on the starting shelf, using the three monsters the test queue had already proven:
Chosen Fiend, Melded Aranha and Aranha. All three are hostile to the player, which is the only
relationship anyone thought to check. They are also in three mutually hostile factions, and gaea
(the Aranha) is hostile to everything that exists. Within a second of the zone loading, every pack
opened fire on every other pack; Aero, the friendly test NPC, was dead 17 seconds in; and the
Valley Floor group settled into killing its own Chosen on a loop for the rest of the session. A
player logging in found a few survivors near the station and empty ground everywhere else, which is
exactly what it looked like from the outside and nothing like what the log said had been spawned.

Two things came out of that. The content now uses chosen and melded only, which are friendly with
each other and hostile to the player, placed where nothing hostile stands within perception of Aero.
And `SpawnGroupSim` now audits every standing NPC pair once at startup and warns on any hostile pair
inside `TargetSelection.PerceptionRange`, because the relationship that broke this is invisible in
the content file and nobody would think to look for it twice.

Where the groups can go turned out to be decided by [DATA-10](../gaps/data.md#data-10) rather than
by design. At the 40m perception radius these groups were placed under, nothing hostile fits on the
starting shelf at all while Aero is standing on it, since the shelf is about 45m across. An invented
constant was choosing the level layout.

Perception has since been narrowed to retail's own 25m ceiling, which makes the shelf usable. The
thirteen placements here were made under the wider radius and are unaffected — they were the
conservative case — so the shelf stays empty until somebody walks up and runs `spawngroup add`.

**The next two cuts were both wrong about height, and between them they ended offline authoring.**
With the factions sorted, three Chosen anchored at Z 465.52 shot a player who never saw them: the
log had them closing to 9m and then holding fire for `NoLineOfSight`, which is what standing under
the terrain looks like from the server's side. That anchor came from a single shipped object, on the
assumption that an object sits on the ground. Objects sit wherever they were placed.

Requiring corroboration, three objects within 25m agreeing on a height, ruled out a badly placed
object and not a slope. Around the valley the ground moves 12m vertically inside 9m horizontally, so
scattering a group over a radius put members in the hillside. That is the third sitting's "some were
inside walls", and it killed anchor-and-radius as a model: a radius is a bet that the ground is
flat, and this server cannot check the bet.

Two things replaced it.
[MovementRelay.RecordGroundSample](../../UdpHosts/GameServer/Systems/MovementRelay/MovementRelay.cs)
logs a grounded player's position once a second, because a player's pose is the only terrain
measurement that ever reaches this server and nothing wrote it down. And
[SpawnGroupServerCommand](../../UdpHosts/GameServer/Systems/Admin/Commands/SpawnGroupServerCommand.cs)
turns the running game into the editor: `spawngroup add <monsterId>` places a monster where you are
standing, saves the file and respawns the group, so the loop is walk, place, look, adjust. Every
monster now carries its own position rather than an offset from a group anchor, and zone 448's
thirteen are all footings from a walk — twelve from the walk that replaced the anchors, and the
thirteenth placed in game with the command itself. `SpawnScatter` and its tests are gone with the
model that needed them.

The wider version of that argument, including why a 3D map editor is the wrong next thing and what
loading real terrain would fix, is in [Authoring World Content](world-authoring.md).

What remains offline is a test that reads the shipped JSON back through the loader, because a
misspelt key deserialises to a default rather than failing, the server writes this file now, and
either way it would otherwise cost a client session to find. It also checks that no two monsters
were placed on the same spot, which is one keystroke away when you place by standing still.

## Closed, 2026-08-13

**[N14 to N16](../In-Game-Tests/NPC-Combat.html) all passed on the fourth attempt at the content and
the first one that failed at nothing.** Thirteen monsters were standing in zone 448 at login, the
hostile-neighbour audit was clean, North Flats refilled all three of its slots at ninety seconds to
the digit and at the recorded position rather than a drifted one, and Basin Mouth killed the tester.
N14 was the milestone's exit condition and the only entry in the stream that forbade the `npc`
command, so M2 closes here.

The heights stopped being the risk they had been for the previous two attempts, because every
placement stands on a footing a player walked over. **The tester's own position turned out to be the
remaining hole.** N16's `tp` target was a coordinate nobody had stood on, so that fight was run from
about 8m inside a hillside — and the teleport destination was written into the footing record as
ground, because the client reports grounded inside terrain exactly as it does on top of it. A tool
that trusts the player's footing is only as good as the footing being genuine, so
`CharacterEntity.PlacedPosition` now marks any position a character was *put* at rather than walked
to, `MovementRelay` records no footing until it leaves that spot, and `spawngroup add` refuses until
then. The same pass fixed the sampler logging one footing three hundred times while a tester stood
still: the interval and the spacing were ANDed rather than both required.

Two findings from the closing run belong to other milestones and are written up under the entries
that found them. Death is a one-way door — the server kills a player correctly, every attacker drops
target in the same second, and the client never sends `RequestRespawn`, so the session ends with a
reconnect ([NET-23](../gaps/network.md#net-23)). And the first balance reading taken against real
content resolves to [DATA-6](../gaps/data.md#data-6): one flat 2500 health pool for every creature
in the game is what "bullet sponge" means, while the incoming side of the fight is already roughly
retail.

What remains open in this milestone is the piece that has nothing to show: `CharacterEntity.Die`
publishes `CharacterDiedEvent` and nothing subscribes. M5 and M7 are what give it a listener.

The ordering across the whole milestone was deliberate. Perception had no in-game exit condition of
its own, and attacking gave it one without needing anything to move, which is why N1–N7 could check
perception, facing, line of sight, disengagement and firing in a single sitting. Locomotion is the
piece expected to need several trips to the game machine, and it was worth starting with a target
selection already seen picking the right thing — the thing it can't be worth debugging alongside.
