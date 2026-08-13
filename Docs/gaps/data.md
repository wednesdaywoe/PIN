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

<a id="data-6"></a>

### DATA-6 — Monster health and shields are hardcoded placeholders [ ] open

`dbcharacter::Monster` and `MonsterScaling` exist but aren't read.
[CharacterEntity.cs:342-347](../../UdpHosts/GameServer/Entities/Character/CharacterEntity.cs#L342-L347)
gives every monster the same placeholder max health/shields regardless of type or level.

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

### DATA-10 — NPC perception/threat tuning is invented, not sourced from `dbmonster` [ ] open

`dbmonster::Monster` carries `Behavior`/`BehaviorOffensive`/`BehaviorDefensive` name references but
no numeric perception radius or aggro field, so
[TargetSelection.cs](../../UdpHosts/GameServer/Systems/AI/TargetSelection.cs) picks its own
detection range (40m) and threat gain/decay/engage-threshold constants rather than reading them off
a def. Every monster currently notices, aggros and disengages identically regardless of type. Part
of [M2](../streams/m2-npc-combat.md); revisit if the named behaviors turn out to carry this data
somewhere PIN hasn't parsed yet.

The attack pass adds more invented numbers of the same kind, all in the same two files:

| Number | Where | What it decides |
|--------|-------|-----------------|
| 40m perception | `TargetSelection` | how close before an NPC notices you |
| 60m leash | `TargetSelection` | how far before it forgets you |
| 60 threat cap | `TargetSelection` | how long it stays interested after losing sight (6.25s) |
| 20/8/10 gain, decay, engage | `TargetSelection` | how fast it aggros and disengages |
| 250ms burst floor | `AttackWindow` | slowest cycle a template with no `MsPerBurst` may fire at |

The leash and the cap were added after [N4](../In-Game-Tests/NPC-Combat.md) ran, and are worth
separating from the rest: they are invented numbers, but they exist to fix a genuine defect rather
than to fill a hole in the data. Without them engagement was bounded by the weapon's reach instead of
perception — 180m against a 40m radius — and threat was uncapped, so an NPC that had watched a target
for a minute needed minutes of decay to let go. It never disengaged in practice.

None of these are confirmed against anything. N4 checks that disengagement behaves like a radius, not
that 60m is the radius Firefall used.

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
