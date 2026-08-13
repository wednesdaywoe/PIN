---
project: pin
kind: gap-detail
title: "Issue Register — Static Data (DATA)"
relates:
  - ../ISSUE-REGISTER.md
---

# Static Data (DATA)

Values the server reads that are invented, hardcoded as a stand-in, or unconfirmed against what
build 1962 actually shipped. Distinct from an unbuilt roadmap feature: everything here is already
live and running, just running on a number nobody's checked.

<a id="data-1"></a>

### DATA-1 — Battleframe shield pool kept non-zero on purpose [x] closed, with a guard

`dbitems::Battleframe` confirmed in M1 that build 1962 shipped **no live shields** — only 5 of 1676
rows carry a non-zero `base_shields`. [HardcodedCharacterData.cs](../../UdpHosts/GameServer/Data/HardcodedCharacterData.cs)
keeps the shield pool at 3000 anyway, because a character with zero shields is harder to observe
and debug against than one with some. The recharge rate and delay (150/sec, 10000ms) are the real
shipped values; only the pool size is the deliberate divergence. Revert is a one-line change
whenever fidelity wins out over observability. See [M1](../streams/m1-combat-models.md).

<a id="data-2"></a>

### DATA-2 — Thump node type hardcoded to `20` for every deposit [ ] open, scheduled as M4

[ResourceNodeBeaconCalldownCommand.cs:23](../../UdpHosts/GameServer/Systems/Aptitude/Commands/Calldown/ResourceNodeBeaconCalldownCommand.cs#L23)
resolves `nodeType` to the literal `20` at both call sites, with a TODO next to it. Every deposit
in the world is currently the same deposit. Fixing this is the headline of
[M4](../streams/m4-thump-placement.md); recorded here too because it's live, wrong output today,
not just an unbuilt feature.

<a id="data-3"></a>

### DATA-3 — `Battleframe.base_health` doesn't match observed live health [ ] open

The SDB column reads roughly 1000; a live capture shows the real value was 19192
([Capture Replay](../In-Game-Tests/Capture-Replay.md)). Something scales base health before it
reaches the wire, and PIN doesn't do it — no scaling logic exists yet, so any health derived
straight from this column is off by roughly 19x.

<a id="data-4"></a>

### DATA-4 — Splash and ability-projectile range falloff are unmodeled [ ] open

M1 confirmed the *weapon* range-decay curve against a real capture
([DamageFalloff.cs](../../UdpHosts/GameServer/Systems/ProjectileSim/DamageFalloff.cs)). Two
neighbors never got the same treatment: ability splash damage falls off on a curve that's a guess,
not derived from client data
([SplashFalloff.cs:7](../../UdpHosts/GameServer/Systems/Combat/SplashFalloff.cs#L7)), and
`ProjectileSim.FireAbilityProjectile` applies zero range falloff at all — full damage at any
range.

<a id="data-5"></a>

### DATA-5 — Nearly all server-side aptitude command defs are empty stubs [ ] open, large scope

3797 of 3812 `env=server` `aptgss_` command defs parse, execute and remove nothing; 15 have been
hand-reconstructed from captures so far
([05-aptitude-abilities.md:140-147](../Architecture/05-aptitude-abilities.md)). This is the data
side of the "~197 aptitude stubs" line in [Deferred](../PROGRESS.md#deferred) — recorded here as
the size of the unknown, not as work to schedule.

**What is missing is the parameters, not the scripts** (measured 2026-08-13, while establishing that
retail's spawn placements are unrecoverable for [DATA-15](#data-15)). Two things make that precise.

`apt::CommandType` names every command the engine has, 218 of them, split 168 server and 50 client,
and each row carries the def table that holds its parameters. Hashing those names against the file
says which shipped: **161 named def tables are referenced by the data and absent from the db**, and
they are exactly the `aptgss::` set. That is the same fact as the stub count above, arrived at from
the data rather than from PIN's own loader, which is worth having because it rules out the reading
that PIN simply hasn't mapped them.

`apt::BaseCommandDef` is the other half, and it did ship: **143,498 command steps**, each naming a
command type and the next step in its chain. **18,025 of those steps call a server-side command
whose def table is absent.** So retail's scripts survive as structure with every leaf blank. The
chain says a step activates a spawn table; the row naming which table, holding which monsters,
is gone.

For the population question specifically, the calls break down as:

| Calls | Command | What the step did |
|-------|---------|-------------------|
| 884 | `agsEncounterSignalCommandDef` | drove encounter state |
| 399 | `SpawnLootCommandDef` | dropped loot |
| 385 | `agsTargetByNPCCommandDef` | picked an NPC to act on |
| 356 | `agsDeployableSpawnCommandDef` | placed an object |
| 297 | `agsNPCSpawnCommandDef` | spawned a monster |
| 252 | `agsActivateSpawnTableCommandDef` | switched a spawn table on |
| 59 | `agsEncounterSpawnCommandDef` | spawned an encounter |
| 56 | `agsNPCBehaviorChangeCommandDef` | swapped an NPC's behaviour mid-fight |
| 15 | `agsUpdateSpawnTableCommandDef` | retuned a live spawn table |
| 3 | `agsCreateSpawnPointCommandDef` | made a spawn point at runtime |

Two conclusions worth carrying. Firefall populated a zone by **running scripts**, not by reading a
placement list, which is why no spawn table turns up anywhere and why looking for one was the wrong
search. And the surviving skeleton is usable on its own terms: it says how a retail encounter was
shaped, how many spawn steps it took and in what order, without saying what any of them spawned.
[M7](../streams/m7-encounter-combat.md) is where that stops being trivia.

<a id="data-6"></a>

### DATA-6 — Monster health and shields are hardcoded placeholders [ ] open

`dbcharacter::Monster` and `MonsterScaling` exist but aren't read.
[CharacterEntity.cs:342-347](../../UdpHosts/GameServer/Entities/Character/CharacterEntity.cs#L342-L347)
gives every monster the same placeholder max health/shields regardless of type or level.

**This is what "bullet sponge" means, and [N16](../In-Game-Tests/NPC-Combat.md) is the first entry
that felt it.** 2500 health against a player rifle landing 39 a shot is about 64 shots to kill a
level 1 Chosen Fiend, and it is the same 64 shots for every monster in the game. The other half of
that session's balance reading is not this entry: the player's 19192 is
[DATA-3](#data-3)'s live-observed number and correct, and the Chosen's 1 damage a shot resolves to
about the 8 dps [DATA-14](#data-14) measured for monster 1196, so the incoming side is roughly
retail. It is the outgoing side — one flat health pool for every creature — that makes a fight
against a low-level monster take as long as a fight against anything else.

**The retail numbers shipped, and the TODO in the code names the wrong table.** Checked
2026-08-13 against `prod-1962` after a beta video showed a Dreadnaught killing monster 528 with a
few seconds of chaingun fire:

| Table | Rows | What it holds |
|-------|------|---------------|
| `dbcharacter::MonsterScaling` | 80 | `level`, `health`, `damage` — health 100 at level 1 rising to 153726 at level 80 |
| `dbencounterdata::ScalingTableEntry` | 155 | `health_scale`/`damage_scale` per `player_count`, six tables — party-size scaling in percent |
| `dbcharacter::Monster.difficulty_cost` | 906 of 3109 non-zero | 28 distinct values, 1..1000 — the only shipped per-monster toughness rating |

`MonsterScaling` is keyed by **level**, not by `ScalingTableId`, so
`CharacterEntity.cs`'s "derive from `monsterInfo.ScalingTableId`" points at the wrong join.
`ScalingTableId` (55 monsters, 5 values) resolves to `ScalingTableEntry`, which is the group-size
multiplier applied *after* the base — one player is always 100%.

So the base health formula is `MonsterScaling[level].health`, and **the missing input is level, not
the table.** A monster's level was set by whatever spawned it, which was server content
([DATA-15](#data-15)). The shipped substitute is `difficulty_cost`: monster 528 sits at 20, in the
lowest populated band (only four rated monsters in the whole db are below it), and monster 1196
sits at 35, which matches how the two actually play. It is an encounter budget rather than a level,
so it orders monsters correctly but does not convert to one.

Against that curve PIN's original flat 2500 was a level 17–18 monster, which is why 528 read two or
three tiers above what it is. Level 8 is 477 health and level 10 is 745 — the band where a few
seconds of sustained fire kills something.

**The interim lever has been pulled: `MonsterMaxHealth` is 500 as of 2026-08-13** (user-chosen),
which lands the flat pool at about level 8 on the shipped curve, or roughly 13 rifle shots against
N16's 39-a-shot reading instead of 64. It is still one number for every creature in the game, so
what it fixes is the slog and not the entry. Deliberately above `MonsterScaling`'s own level-1
figure of 100: level is the input PIN doesn't have, and 100 across the board would make everything
trivial for exactly the reason 2500 made everything a slog. **Nothing in game has confirmed the new
feel yet** — the first read on it will come from whoever runs
[K1](../In-Game-Tests/Kill-Rewards.md), which needs a lot of kills.

The proposed fix keeps the shipped curve and puts the one missing number where PIN already authors
world content: an optional per-member `level` in `spawn_group.json`, defaulting per group, set in
game by a `spawngroup level` subcommand alongside `add` and `delay`. That closes this entry using
only shipped data plus PIN's own placements, and it does not reintroduce player levels, which the
[restoration charter](../PROGRESS.md) rules out. The interim lever was
`HardcodedCharacterData.MonsterMaxHealth` and it has now been moved, so what is left here is the
per-monster path rather than the number.

<a id="data-7"></a>

### DATA-7 — Character level comes from `HardcodedCharacterData`, not real progression [ ] open

Every read of `Level`/`EffectiveLevel` — `EntityManager.cs:1186`,
`CharacterEntity.cs:1692-1693`, `BaseController.cs:449-450` — resolves through the hardcoded
character stand-in rather than tracked XP/progression state. Blocks anything that should scale
with a real level.

<a id="data-8"></a>

### DATA-8 — A scatter of aptitude commands read a hardcoded constant instead of the def parameter [ ] open

Three separate commands each hardcode one number that the def is supposed to supply:

- Muzzle-offset origin is one constant per stance, doesn't vary by character or frame
  ([CharacterEntity.cs:1355](../../UdpHosts/GameServer/Entities/Character/CharacterEntity.cs#L1355)).
- `CombatController.SendAbilityActivated` hardcodes `GlobalCooldown` instead of reading it off
  `InstantActivationCommand`'s def
  ([CombatController.cs:311-313](../../UdpHosts/GameServer/Controllers/Character/CombatController.cs#L311-L313)).
- `ForcePushCommand` push force is a constant, not read from ability params
  ([ForcePushCommand.cs:18](../../UdpHosts/GameServer/Systems/Aptitude/Commands/Impact/ForcePushCommand.cs#L18)).

Low severity individually; grouped because the pattern (and the fix) is the same for all three —
plumb the def value through instead of the placeholder.

<a id="data-9"></a>

### DATA-9 — Two web endpoints return invented or stub data [~] needs confirmation

`CharactersController.cs:36-38`'s `CharacterData` endpoint returns `Key`/`Namespace`/`Value`
fields the code itself flags with "ToDo: What are these?". `garage_slots` and
`character_sheet.json` return a fixed frame (Firecat / Rhino) regardless of the actual character
([Prediction-Sweep.md:171-172](../In-Game-Tests/Prediction-Sweep.md)) — currently the leading
suspect if [NET-19](network.md#net-19)'s P0 verification doesn't hold.

<a id="data-10"></a>

### DATA-10 — NPC perception/threat tuning is invented; retail's own numbers ship for a third of the table [ ] open

`dbmonster::Monster` carries `Behavior`/`BehaviorOffensive`/`BehaviorDefensive` name references but
no numeric perception radius or aggro field, so
[TargetSelection.cs](../../UdpHosts/GameServer/Systems/AI/TargetSelection.cs) picks its own
detection range (40m) and threat gain/decay/engage-threshold constants rather than reading them off
a def. Every monster currently notices, aggros and disengages identically regardless of type. Part
of [M2](../streams/m2-npc-combat.md).

The attack and locomotion passes add more invented numbers of the same kind:

| Number | Where | What it decides |
|--------|-------|-----------------|
| 40m perception | `TargetSelection` | how close before an NPC notices you |
| 60m leash | `TargetSelection` | how far before it forgets you |
| 60 threat cap | `TargetSelection` | how long it stays interested after losing sight (6.25s) |
| 20/8/10 gain, decay, engage | `TargetSelection` | how fast it aggros and disengages |
| 250ms burst floor | `AttackWindow` | slowest cycle a template with no `MsPerBurst` may fire at |
| 12m standoff, 3m band | `NpcMovement` | how close it walks in, and how far you get before it follows |
| 0.8 of reach | `NpcMovement` | how far back a short-ranged monster settles instead |
| 50m chase leash | `NpcMovement` | how far from where it spawned it will chase before going home |
| 45° max slope | `Steering` | steepest ground it believes is ground rather than a wall |

**The named behaviors do carry it, and PIN cannot reach them yet.** Chased down on 2026-08-13 after
a melee Aranha was seen standing further off than its claw wanted. `Monster.BehaviorOffensive` is a
string with its parameters inline, and among them is exactly the number the table above invents:

```
Arch_FullbodyMelee_Attack(combatDist=4,am1Id=82621,am1Facing=true,am1MaxDist=15,am1NavToDist=5.0,...)
Arch_MoveThenFire_Attack(am1Id=86132,...,combatWalk=true,combatDist=30)
Arch_MedRangedHumanoid_Attack(triggerPullTime=1500,fireRestDuration=1000)
```

So retail set standoff per behavior — 4m for a full-body melee, 30m for move-then-fire — along with
`am1MaxDist`, `am1NavToDist`, facing and turn radius. That is most of this table, shipped.

#### The research pass, 2026-08-13: there is no instance table to find

The entry above assumed the strings were reachable for every monster once someone identified the
table behind `BehaviorInstanceId`. That was the wrong shape of problem. Searching the string content
of all 575 tables (`MinimalSDB find combatDist`, and again for `Arch_`) returns
`dbcharacter::Monster` and nothing else, on three columns: `behavior`, `behavior_offensive`,
`behavior_defensive`. No other table in the file holds a behavior name, and no integer column
anywhere holds a hash of one, which is what a name-keyed instance table would look like once its own
column names were stripped. The header says this db was built with the `Client` flag. Behaviour
trees ran on the server, so the instance rows went to a database PIN doesn't have and can't get.

Two counts in the paragraph above were also wrong, and the corrected ones change what the work is:

| Shape | Rows |
|-------|------|
| carries at least one behaviour string | 2100 |
| carries only a `BehaviorInstanceId` | 695 |
| carries both | 62 |
| carries neither | 314 |

So two thirds of the table shipped its parameters inline. What it did not ship is a way to reach the
other third, and the three monsters the test queue uses (1196, 528, 2342) are all in that third,
along with 281. That is the whole reason this looked unreachable: every monster anyone had spawned
by hand was one of the ones with nothing to read.

Parsing the strings that did ship yields, per monster:

| Parameter | Monsters | Values | The constant it would replace |
|-----------|----------|--------|-------------------------------|
| `combatDist` | 108 | 0.5 to 50, 22 distinct | 12m standoff |
| `preferredMinCombatDist` | 42 | 3.5 to 120, 7 distinct | 12m standoff |
| `perceptionDist` | 67 | 4, 5, 6, 8, 10, 15, 25 | 40m perception |
| `maxDistFromSpawn` | 13 | 30 and 45 | 50m chase leash |
| `am1MaxDist` / `am1MinDist` | 23 / 14 | 5 to 150 / 0 to 10 | nothing yet; ability ranges |
| `wideTurnRadius` | 8 | 3, 5, 10 | nothing yet; NPCs turn instantly |
| `triggerPullTime` | 301 | 100 to 10000 | nothing yet; see [DATA-14](#data-14) |
| `fireRestDuration` | 251 | 0 to 9000 | nothing yet; see [DATA-14](#data-14) |

150 monsters carry a standoff distance of one kind or the other.

The perception row is the finding worth acting on independently of the rest. Retail's widest
`perceptionDist` in the whole file is 25m and its most common values are 10 and 15, against PIN's
flat 40m. Every NPC in the game currently notices you from at least 1.6 times as far as the furthest-
seeing monster retail shipped, and from four times as far as the most common one. `maxDistFromSpawn`
says the same about the chase leash, at 30 to 45 against PIN's 50.

None of that is a lookup PIN can just wire up, because a parser has to exist first and 959 rows would
still fall through it. What the numbers do settle is what the fallbacks should be: a default taken
from the distribution of what shipped is defensible in a way that 40m is not.

**Perception stays at 40m for now** (decision 2026-08-13, user-chosen). Narrowing it was on the table
as soon as the 25m ceiling turned up, and it was declined until [N14 to
N16](../In-Game-Tests/NPC-Combat.md) have run: [N4](../In-Game-Tests/NPC-Combat.md) measured
disengagement against the current radius, and changing the number underneath a spawn-group test makes
a failure ambiguous between the content and the tuning. The cost is recorded under
[DATA-15](#data-15): at 40m no hostile group fits on zone 448's starting shelf at all, so an invented
constant is currently choosing where the level's monsters can stand.

`triggerPullTime` and `fireRestDuration` are the two most widely populated parameters in the whole
set, and they describe exactly the pause [DATA-14](#data-14) says is missing from NPC cadence. That
is a second lead out of the same strings and it covers 301 monsters, not 150.

The leash and the cap were added after [N4](../In-Game-Tests/NPC-Combat.md) ran, and are worth
separating from the rest: they are invented numbers, but they exist to fix a genuine defect rather
than to fill a hole in the data. Without them engagement was bounded by the weapon's reach instead of
perception — 180m against a 40m radius — and threat was uncapped, so an NPC that had watched a target
for a minute needed minutes of decay to let go. It never disengaged in practice.

Movement **speed** is deliberately not on this list, because it turned out not to need inventing —
see [MoveSpeed](../../UdpHosts/GameServer/Systems/AI/MoveSpeed.cs). It is not on `dbmonster` where
you would look for it: that record's `FastSpeed` is an override which 3095 of its 3109 rows leave at
-1. It is on the monster's `ChassisId`, which is a `dbitems::Battleframe` — the same
record a player's frame speed comes from — and that resolves a real run speed for 2971 monsters.
Only 124 need a stand-in, and even that is the table's own most common value (6 m/s) rather than a
guess. What this does mean is that reading -1 as a speed would have every one of them walking
backwards, which is [DATA-11](#data-11) again with a different sentinel.

None of the invented numbers are confirmed against anything, and [N1 to
N13](../In-Game-Tests/NPC-Combat.md) passing doesn't change that. Those entries check that
disengagement behaves like a radius, that a monster stops short and that it walks home. They can't
check that 60m, 12m and 50m are the numbers Firefall used, and the table above now says two of the
three aren't.

<a id="data-11"></a>

### DATA-11 — A zero multiplier in `WeaponTemplateModifiers` was read literally [x] fixed 2026-08-12

A weapon item's numbers are its template's, adjusted by its own `dbitems::WeaponTemplateModifiers`
row as `(base + modifier) * multiplier`. The data populates *one* of those two columns per stat and
leaves the other at `0` — the format zero-fills, so a `0` multiplier means "this row doesn't set
one". PIN multiplied by it.

Three real rows, which is what made the pattern legible:

| Weapon | column pair | value / mult | PIN resolved | should be |
|--------|-------------|--------------|--------------|-----------|
| Chosen Grunt Rifle 85953 | `range` | 80 / **0** | **0** | 180 |
| Accord Famas 76108 | `damage_per_round` | 35 / **0** | **0** | 65 |
| Accord Guard Rifle 137167 | `damage_per_round` | 0 / 5 | 230 | 230 |

Across the table that zeroed the range of 269 weapons and the damage of 220, player and NPC alike —
66 of 191 NPC weapons had no range and 54 no damage. `WeaponTemplateModifier` now treats a zero
multiplier as absent, in all six overloads: the first fix only covered the `float` one, which left
every int-typed stat (`DamagePerRound` among them) still reading zero.

Two things are worth taking from how this was found. It had been live in every weapon the server
ever resolved and no test caught it, because the resolution was only ever exercised through a player
firing a weapon that happened to have a non-zero multiplier. It took an NPC — which picks its weapon
from `dbmonster` rather than from what a player chose to equip — to land on the broken rows. And it
presented as two unrelated bugs: [N2](../In-Game-Tests/NPC-Combat.md) looked like broken AI (a
monster that aims and never fires, because its rifle's reach was 0) while N6 looked like broken
damage routing (a monster firing with full muzzle VFX into a target that never lost health, because
its rounds did 0). One zero, two symptoms, neither of them where the bug was.

`Tests/GameServer.Tests/Weapons/WeaponTemplateModifierTests.cs` pins the rule using these rows.
Regenerate [the weapon reference](../Wiki/Reference/Weapons.md) with `Tools/SdbDocs` after touching
resolution — it renders through `GetDetailedWeaponInfo`, so it shows what the server would send, and
it is where the 269 and 220 counts came from.

<a id="data-12"></a>

### DATA-12 — Melding wall damage rate is invented [ ] open

[HazardSim](../../UdpHosts/GameServer/Systems/Hazards/HazardSim.cs) takes 5% of max health every
500ms off anyone standing in the melding, which kills in about twelve seconds through a full shield
and health pool. Nothing chose that number but the need for one.

Drowning, sitting right next to it in the same file, is the contrast: effects 787 and 789 survive
intact in `clientdb.sd2` with their `update_frequency` of 500ms and their damage as a register of
`max_health * 0.01`, so those rates are read rather than picked. The melding has no equivalent. Of
the 20 status effects that inflict damage type 29 (`dbcharacter::DamageType` names it "Melding"),
every one whose chain could be walked is a melded creature's attack — a PBAE target selector with a
radius, applied by something that hit you. Nothing in the db applies damage for being in a place.
The reasonable reading is that retail drove the wall from zone logic on the server, which never
shipped to a client and so isn't in a client db.

Retail killed faster than twelve seconds. The rate is deliberately survivable so that
[E4](../In-Game-Tests/Environment.md) can be watched happening rather than inferred from a corpse;
tightening it is one constant.

<a id="data-13"></a>

### DATA-13 — The water description nibble can't be resolved, so all water is row 10001 [ ] open

`WaterLevelAndDesc` arrives from the client packed `ddddllll`. The low nibble is submersion and is
fully understood — see [Submersion](../../UdpHosts/GameServer/Systems/Hazards/Submersion.cs), where
the reading is confirmed against the 2016 capture. The high nibble picks which of the six
`dbvisualrecords::WaterDesc` rows applies, and it is an index into a per-zone list the map holds and
the server does not, so it can't be turned into a row id here.

Every body of water is therefore read as row 10001: movement restricted at 0.33 of your height,
drowning at 0.735, dying at 1.0. Two of the six rows carry exactly that profile, so it is the
standard water rather than an arbitrary pick, and the capture only ever shows nibble 0.

The six rows, read back through `SDBInterface.GetWaterDesc`:

| Row | restricted | drowning | dying | drown effect | dying effect |
|-----|-----------|----------|-------|--------------|--------------|
| 10001 | 0.33 | 0.7353 | 1.0 | 787 | 789 |
| 10002 | 0.0496 | 0.9522 | 1.0 | 4525 | 4525 |
| 10003 | 0 | 0.0845 | 0.328 | 4722 | 9752 |
| 10008 | 0.0654 | 0.9575 | 1.0 | 4525 | 4525 |
| 10110 | 0.33 | 0.73 | 1.0 | 787 | 789 |
| 10111 | 0.0496 | 0.9575 | 1.0 | 12112 | 12112 |

The failure this avoids is worse than the one it accepts. Row 10003 starts drowning you at 0.0845
and killing you at 0.328 — presumably something caustic — so a wrong resolution would kill people
standing in a river. `Submersion.Against` treats an unresolvable row as harmless for the same
reason. What it costs is that if 448 has any non-standard water, it currently behaves like a lake.

`HazardSim` logs each distinct nibble the first time it sees one, which is what a real mapping would
have to be built from; [E1](../In-Game-Tests/Environment.md) is the entry that collects them.

<a id="data-14"></a>

### DATA-14 — No ammo or reload model, so small-clip NPC weapons fire at an inflated rate [ ] open

[AttackWindow](../../UdpHosts/GameServer/Systems/AI/AttackWindow.cs) paces an NPC from `MsPerBurst`,
`MsBurstDuration` and `RoundsPerBurst` and stops there. `BaseClipSize`, `ReloadTime` and
`ReloadPenalty` are resolved onto the same `WeaponTemplateResult` and read by nothing, so an NPC
never runs a clip dry and never pauses to reload.

"An NPC fires forever" was recorded when the attack pass landed and read as a fairness problem. It
isn't only that. For any weapon whose cycle is shorter than its reload, the missing pause is most of
the weapon's real cadence, and dropping it multiplies its damage output:

| Monster | Weapon | dmg/round | clip | reload | PIN | retail, with the reload |
|---------|--------|-----------|------|--------|-----|-------------------------|
| 281 Chosen Sniper | 77049 NPC Charge Sniper Rifle | 500 | 1 | 1000ms | 4 shots/sec, 2000 dps | ~1 shot/sec, ~450 dps |
| 1375 Chosen Gatling Gunner | 87382 Heavy Laser MG | 20 | 500 | 2000ms | 80 dps | ~80 dps, barely affected |

The error scales with how small the clip is, so it is invisible on the sustained-fire weapons the
test queue has used so far — 1196 carries 54 rounds, 2342 carries 9600 — and enormous on the
one-shot ones. That is the trap: the ids most tempting to reach for when a fight needs to be shorter
are exactly the ids whose numbers are made up.

Nothing needs this to close the slice, and the fix is a clip counter plus a reload gate in
`NpcCombat`, not new data. Recorded because the numbers are live and wrong today, and because
[Session Setup](../In-Game-Tests/Session-Setup.md) now recommends monsters by dps, which is a
recommendation this defect can poison.

<a id="data-15"></a>

### DATA-15 — Spawn group placements are PIN's own content, not retail's [ ] open, by necessity

[spawn_group.json](../../UdpHosts/GameServer/StaticDB/CustomData/spawn_group.json) says where zone
448's standing monsters are, what they are and how fast they come back. Every one of those decisions
is PIN's. Retail kept placements in server-side spawn tables, and the two server-side aptitude
commands that drove them, `ActivateSpawnTableCommandDef` and `UpdateSpawnTableCommandDef`, are both
in the empty-stub pile [DATA-5](#data-5) counts.

It is worth being clear about how thoroughly this one is unrecoverable, because the neighbouring
files are not. `deployable.json`, `melding.json` and `outpost.json` are all recovered content:
melding perimeters carry the same names the client's own `system/maps/448.zone` uses, because the
client has to draw them. Nothing has to draw a spawner. The zone file holds terrain, melding
perimeters, cinematic camera paths and dropship scripting, and no monster placements at all, which
is the same wall [DATA-10](#data-10) hit from the other side.

So the groups are authored, and the [restoration charter](../Restoration.md) is what makes that
acceptable rather than a shortfall: the goal is the loop working on 1962, not a survey of where
Red 5 put things.

What is still worth writing down is which parts are guesses and which aren't:

- **Positions are measured now, not inferred. It took three failures to get there.** The server holds
  no terrain to sample, so nothing offline can answer "is this spot on the ground", and the first two
  cuts answered it from shipped object positions instead. An object's position is not a ground
  height: objects sit wherever they were placed, including inside structures and below the surface.
  A lone object at Z 465.52 put three Chosen 26m under the shelf above them, shooting a player who
  never saw them.

  Requiring corroboration, three objects within 25m agreeing on a height, survived one more run and
  then failed too. It rules out a badly placed object but not a slope, and around zone 448's valley
  the ground moves **12m vertically inside 9m horizontally**. Members scattered over a 12m radius
  landed in the hillside, which is the "some were inside walls" reading from the third sitting. That
  killed anchor-and-radius as a model: a radius is a bet that the ground is flat, and this server
  cannot check the bet.

  Every monster now carries the position a player stood on to place it, written by
  [`spawngroup add`](../../UdpHosts/GameServer/Systems/Admin/Commands/SpawnGroupServerCommand.cs).
  Zone 448's current content is 13 monsters on walked footings, twelve on the Z 401 basin floor and
  one on the shelf above it. See [Authoring World Content](../streams/world-authoring.md), including
  the part worth knowing for later: the consumer side of terrain collision is already built and
  proven, and only the extractor from the client's world chunks is missing.

  The measurement behind all of it is
  [MovementRelay.RecordGroundSample](../../UdpHosts/GameServer/Systems/MovementRelay/MovementRelay.cs),
  which logs a grounded player's position when they have both moved 3m and let a second pass. A
  player's pose is the only ground truth about terrain that reaches this server, because the client
  computed it against the real thing:

  ```
  grep -a "Ground sample" ~/Games/PIN/logs/GameServer.log
  ```

  **A fourth way to get this wrong showed up on the run that closed the milestone, and it is the one
  that undermines the method rather than a placement.** The client reports grounded inside terrain
  exactly as it does on top of it. [N16](../In-Game-Tests/NPC-Combat.md) teleported to a coordinate
  nobody had stood on, landed about 8m under the basin floor, and its destination entered the footing
  record indistinguishable from a real measurement. Placement is only trustworthy if the footing was
  walked to, so `CharacterEntity.PlacedPosition` marks any position a character was *put* at,
  `RecordGroundSample` writes nothing until it leaves that spot, and `spawngroup add` refuses until
  then.
- **Which monsters are chosen is constrained, not free**, and the first cut got it wrong. 1196, 528
  and 2342 are the three [N1 to N13](../In-Game-Tests/NPC-Combat.md) ran against, so all three
  resolve a working weapon and read as hostile to faction 1, and that is the only relationship
  anyone checked before shipping them 25m apart. They are also in three mutually hostile factions:
  1196 is chosen (2), 528 is melding (6), 2342 is gaea (7), and gaea is hostile to everything while
  chosen and melding are friendly only with each other. The zone fought itself out in the first
  twenty seconds of the 2026-08-13 sitting. The content now uses chosen and melded only, and
  `SpawnGroupSim` audits every standing pair at startup rather than trusting anyone to remember.
- **Respawn delays (90 to 120s) and pack sizes (3 to 6) are invented outright.** Nothing anywhere
  suggests what retail used.
- **Where the groups can go is decided by [DATA-10](#data-10)'s perception number, not by design.**
  Anything hostile within 40m of Aero kills it, and the only ground near the station anything has
  been seen standing on is a shelf about 45m across with Aero on it. No hostile group fits there at
  all, which is why zone 448 currently has nothing to fight within 120m of where a player logs in.
  Retail's widest shipped `perceptionDist` is 25m and its most common are 10 and 15, at which the
  shelf holds a group comfortably.

The delay is also measured from the corpse despawning rather than from the kill, which adds
`CharacterEntity.Die`'s fixed 30s to all of them. That is an implementation choice in
[SpawnGroupSim](../../UdpHosts/GameServer/Systems/Spawning/SpawnGroupSim.cs), made to keep spawning
off `CharacterDiedEvent` while M5 and M7 are the ones that want it, and it is worth remembering
before anyone tunes the numbers by feel.

<a id="data-16"></a>

### DATA-16 — `roll_mode` is unmapped, so PIN's loot-drop rates are inferred [ ] open

`dbitems::LootTable.roll_mode` decides how a table's entries combine, carries six distinct values
across 2472 rows, and nothing documents any of them. The client is the only thing that ever
interpreted the column, and it interpreted it in compiled code.

[LootRoller](../../UdpHosts/GameServer/Systems/Loot/LootRoller.cs) implements two readings, both
inferred from the arithmetic of the tables themselves rather than from anything that shipped:

- **Mode 2 rolls every entry independently.** "CORE NPC Kill Loot 2 (Small)" carries two subtables at
  100 and one at 8. Nothing but three separate chances makes sense of that.
- **Everything else picks at most one entry, weighted.** "Small Crystite Drops (2-20)" is mode 0 and
  its four rows are 80/10/5/5, summing to exactly 100 — a distribution over one result. Where weights
  sum below 100 the remainder is nothing, which is how mode 3's "Creature Kill - Small Equipment
  Drop" (5/4/3/2/1) stays rare.

Modes 1, 4 and 5 fall into the weighted branch untested, because nothing reachable from a zone 448
monster uses one.

The second guess is the probability scale. Every table on the kill path uses small percentages, and
the reading is percent. The column reaches 10000 in `LootTableSubTableDist` and 9638 in
`LootTableItemDist`, so at least one table somewhere is in basis points and will roll at a hundredth
of its intended rate here.

**What this costs is drop rates, not correctness.** A wrong reading pays too often or too rarely; it
does not pay the wrong thing, because quantities and item ids come off the rows directly.
[LootRollerTests](../../Tests/GameServer.Tests/Loot/LootRollerTests.cs) pins the reading so a change
to it is deliberate, and [K2](../In-Game-Tests/Kill-Rewards.md) — twenty kills counted against an
expected quarter — is the only measurement available in game. Unlike [DATA-10](#data-10), there is no
shipped string to recover the answer from: the semantics were never data.
