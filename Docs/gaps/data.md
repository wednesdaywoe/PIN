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
