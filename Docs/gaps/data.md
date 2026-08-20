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

### DATA-1 — Battleframe shield pool [x] closed 2026-08-15 by reverting it to retail's 0

`dbitems::Battleframe` confirmed in M1 that build 1962 shipped **no live shields** — only 5 of 1676
rows carry a non-zero `base_shields`, and the 2016 capture agrees, reading 0 on every
`Character_BaseController` message for the player.
[HardcodedCharacterData.cs](../../UdpHosts/GameServer/Data/HardcodedCharacterData.cs) kept the pool
at 3000 anyway, on the argument that a character with zero shields is harder to observe and debug
against than one with some. The recharge rate and delay (150/sec, 10000ms) are the real shipped
values and are unchanged; only the pool size was ever the divergence.
See [M1](../streams/m1-combat-models.md).

**The observability argument was backwards, and a tester walked into it on 2026-08-15.** The 1962
client has no shield display at all. `ShieldBar` survives as a texture region in `skins/skin.xml`
with **no reference anywhere else in the UI**, and both `HUD/HealthBar` and `HUD/Vitals` bind
`ON_HEALTH_CHANGED` and `ON_TOOK_HIT` with nothing for shields — Red 5 deleted the readout when they
deleted the stat, the same way they disabled the heat-blob overlay
([Client UI Source](../Client-UI-Source.md)). So the pool was 3000 hit points the player could not
see, and what it actually bought was a window at the start of every fight where hits land, health
does not move, and nothing on screen explains why.

That window is 45 seconds long against the Basin Mouth pack, because most NPC gunfire lands for 1
point (see [DATA-20](#data-20)). The tester read it as a broken `invuln` command, toggled the
command three times inside the shield window, filed it as a bug, and only found out otherwise from
the server log: shields 3000 → 0 with health flat at 19192 across all three toggles.

**Set to 0 on 2026-08-15** (user-chosen), which is retail's own value and the only one this client
can render honestly. Fidelity and observability turned out to point the same way, so the trade the
entry was built on no longer exists. If per-frame shields are ever restored from real data, the
readout has to be authored in Lua first or the same trap comes straight back.

<a id="data-2"></a>

### DATA-2 — Thump node type hardcoded to `20` for every deposit [x] closed 2026-08-14

Was: `nodeType` resolved to the literal `20` at both call sites, so every deposit in the world was
the same deposit. M4 replaced the literal with a lookup —
[ResourceMapSim.ResolveNodeType](../../UdpHosts/GameServer/Systems/Resources/ResourceMapSim.cs)
answers with the deposit under the calldown position, out of the per-zone
[resource_deposit.json](../../UdpHosts/GameServer/StaticDB/CustomData/resource_deposit.json), and
`20` survives only as what barren ground legitimately is ("Default, Thumper Sifted Earth", whose
shipped yield is one unit of sifted earth). All three callers resolve: the real calldown, the
`thumper` admin command, and the zone 448 debug thumper.

Closed on 2026-08-14 by [S3–S5](../../Game Testing/Thump-Placement.html). Four node types resolved from
position in a single sitting (242, 241, 233, and 20 where no deposit covers the ground), and the
payouts came apart with them: 48 crystite near the rich vein's center against 33 from the same vein
59% of the way out, 8 from the poor trace, and one unit of sifted earth from barren ground. Two runs
minutes apart on deposit 1 differing in nothing but position is the part that closes this, because
it rules out the reading where a lookup happens to work once.

<a id="data-3"></a>

### DATA-3 — `Battleframe.base_health` doesn't match observed live health [ ] open

The SDB column reads roughly 1000; a live capture shows the real value was 19192
([Capture Replay](../../Game Testing/Capture-Replay.html)). Something scales base health before it
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

The chains these steps sit in were walked on 2026-08-14 by [Tools/ChainWalk](../../Tools/ChainWalk/)
for M7: they are one-shot verbs, not wave scripts, and the reading is in
[streams/m7-encounter-combat.md](../streams/m7-encounter-combat.md).

Two conclusions worth carrying. Firefall populated a zone by **running scripts**, not by reading a
placement list, which is why no spawn table turns up anywhere and why looking for one was the wrong
search. And the surviving skeleton is usable on its own terms: it says how a retail encounter was
shaped, how many spawn steps it took and in what order, without saying what any of them spawned.
[M7](../streams/m7-encounter-combat.md) is where that stops being trivia.

<a id="data-6"></a>

### DATA-6 — Monster health and shields are hardcoded placeholders [~] 906 of 3109 creature types tiered 2026-08-15, 2203 still flat

`dbcharacter::Monster` and `MonsterScaling` exist but aren't read.
[CharacterEntity.cs:342-347](../../UdpHosts/GameServer/Entities/Character/CharacterEntity.cs#L342-L347)
gives every monster the same placeholder max health/shields regardless of type or level.

**This is what "bullet sponge" means, and [N16](../../Game Testing/NPC-Combat.html) is the first entry
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
[K1](../../Game Testing/Kill-Rewards.html), which needs a lot of kills.

The proposed fix keeps the shipped curve and puts the one missing number where PIN already authors
world content: an optional per-member `level` in `spawn_group.json`, defaulting per group, set in
game by a `spawngroup level` subcommand alongside `add` and `delay`. That closes this entry using
only shipped data plus PIN's own placements, and it does not reintroduce player levels, which the
[restoration charter](../PROGRESS.md) rules out. The interim lever was
`HardcodedCharacterData.MonsterMaxHealth` and it has now been moved, so what is left here is the
per-monster path rather than the number.

#### `difficulty_cost` is a threat tier, not just a budget — read 2026-08-15

The entry above called it "an encounter budget rather than a level", which understated it. Reading
the ladder against `Monster.behavior` — a **string** column, so the creature's own AI script names
itself in plain text — shows the rating tracks the design tier the way a hand-assigned tier would:

| Rating | Types | Behaviour scripts found in the band |
|--------|-------|-------------------------------------|
| 20–30 | 120 | unnamed wanderers, `StockMelee` |
| 50–100 | 424 | `EliteWanderer`, `ThresherSpitter`, `ThresherTailWhipper` |
| 120 | 13 | `SiegebreakerWithCharge`, `Brontodon` |
| 150–300 | 75 | every `*MiniBoss` script: `GiantAranha`, `LandShark`, `CrystalAranha`, `RaiderBaron` |
| 400 / 1000 | 2 | one type each, the top of the scale |

**`GiantAranhaMiniBoss` appears at 150 and again at 300, and `LandSharkMiniBoss` at both as well.**
Red 5 shipped the same creature behaviour as separate monster types at separate ratings, which is
exactly the "a worker aranha is weak, a sieger is dangerous" structure the
[thumper reconstruction](../thumper-encounter-reconstruction.md#5-composition) tiers by hand. So the
column orders 906 creature types against each other, for free, and the only decision left is where
to anchor the band — two numbers instead of 906.

Caveat on coverage: **2,203 of 3,109 types rate 0**, and that set includes `EliteWanderer` and
`RaiderBaronMiniBoss` entries that plainly are not harmless. A 0 most likely means "not placed by
the budgeted spawner" (mission and story NPCs) rather than "no threat", so a cost-driven scheme
needs a fallback for the unrated two thirds rather than reading 0 as a level.

#### The curve's shape, and where the player sits on it

`MonsterScaling` is not one geometric run but **four segments**, each a fixed step per level:

| Levels | Step per level |
|--------|----------------|
| 1–10 | ×1.25 |
| 11–20 | ×1.18 |
| 21–~35 | ×1.10 |
| ~40–80 | ×1.05 |

Growth decelerates deliberately, so a few levels are worth much more at the bottom than the top —
which is what makes the low band the useful one for tiering ordinary fauna.

The player's 19192 ([DATA-3](#data-3)) is **level 38** on this curve, not the 45 that
`HardcodedCharacterData.Level` stamps. The two are independent numbers from different tables and the
disagreement is minor, but any argument of the form "the player is level 45, so monsters should be
too" is reading the stamp rather than the health.

#### The 500 does not survive its own source — checked 2026-08-15

The flat 500 came from a tester's beta-video reading: monster 528 took "just a few seconds of
sustained fire with the chaingun or assault rifle", and a burst weapon like the Tigerclaw's plasma
cannon "would probably 1-shot" it. **The conversion from that observation to 500 was never done with
a fire rate**, and doing it moves the answer a long way.

Resolved 1962 figures, via `Weapons.md` and `Weapon-Templates.md`. `Rounds/burst` is 1 on every one
of these, so damage per second is simply `damage_per_round ÷ (ms_per_burst ÷ 1000)`:

| Weapon | Damage/round | ms/burst | DPS | Time to kill 500 |
|--------|--------------|----------|-----|------------------|
| Heavy MG (the chaingun), tpl 32/12115 | 39 | 65 | **600** | **0.83 s** |
| Assault rifle, tpl 12113 | 46 | 105 | **438** | **1.14 s** |

**500 health is under one second of chaingun fire.** "A few seconds" is 1,200 at two seconds and
1,800 at three — level **13 to 16** on the shipped curve, two and a half to four times the current
figure. The tester's *feel* target and PIN's current number are not close.

The one-shot half of the observation **cannot be checked as posed**, because the weapon it names
does not behave that way in 1962:

| Plasma weapon | Damage/round | ms/burst | Chargeup | One-shots up to |
|---------------|--------------|----------|----------|-----------------|
| `Plasma Cannon - Current` (12129) — the mainline player line, 262 items, tiers A4→A32 spanning levels 1–50 | 100 | 600 | **4,000 ms** | 100 |
| `Plasma Cannon` (14) — the older template | 400 | 700 | 0 | 400 |

The mainline plasma cannon does **100 a shot behind a four-second charge**. It one-shots almost
nothing. What does one-shot in that range is a Tigerclaw *ability* — `Pulsar` reads
`Base Damage 155 to 17763`, `Hellfire` 209 to 13322 dps — so a remembered one-shot is most likely an
ability rather than the primary weapon, and it constrains nothing.

**Era caveat, and why it does not rescue the 500.** The video is beta-era and these weapon numbers
are 1962, which is exactly the mismatch the
[thumper reconstruction](../thumper-encounter-reconstruction.md#0-why-this-one-is-hard) warns about.
But PIN *ships* 1962 weapons, so converting a feel target through 1962 rates is the right arithmetic
for PIN regardless of what the beta's own numbers were. The target is "a few seconds to kill with
the weapon the player is actually holding", and 500 does not deliver it.

One anchor does survive intact, because it is an absolute number rather than a converted one:
**shell-less hissers and skivers at 200 HP**, from the 0.6 patch notes recording a 700 → 200 change
`[notes]`. That is level **4** (195) on the curve, within 3%. It anchors the floor of the weakest
tier; it says nothing about where a rated-20 melded aranha sits.

**The two surviving anchors are in different units, which is why they look further apart than they
are.** The 200 is a 0.6-era figure measured against 0.6-era weapons; the 1,200–1,800 is a 1962-era
figure because it was converted through 1962 rates. They cannot be compared directly, and for PIN's
purposes — PIN ships 1962 weapons — the 1962-unit number is the one that governs. The 200 is
therefore useful as evidence that the curve is the right instrument, and not usable as a value to
set.

**Changed 2026-08-15 (user-approved): `MonsterMaxHealth` 500 → 1200.** Two seconds of held Heavy MG
fire, the conservative end of the band. The band's top is 1,800 and the choice of its bottom is
deliberate: [N16](../../Game Testing/NPC-Combat.html) reported a sponge at 2500, so the known failure
direction is from above, and 500 → 1200 is already a 2.4× move. **Confirmed in game the same day by
[D6](../../Game Testing/Damage-Loop.html#-d6-a-basic-creature-dies-in-about-two-seconds).** Three kills,
31 hits each with no variance, health stepping down by 39 to land exactly on 0 from 1200. Time to
kill was **about 3.1 seconds** rather than the predicted 2.0, because the run measured the assault
rifle at 10 rounds a second and not the Heavy MG at 15 — the equip had silently failed on
[NET-18](network.md#net-18). That makes the outcome *better* evidence than the prediction was: 3.1
seconds is the video's "a few seconds" read directly, and the same pool under the Heavy MG would be
about 2. Both sit inside the band, so **1200 holds** and nothing needs moving.

**The re-run with the Heavy MG actually equipped gave the same 31 hits again** — six kills across
two weapons, no variance in any of them — so 1200 is confirmed independently of which gun fired.

It also closed the ×0.85 question. All 93 hits of the Heavy MG run landed a full **39**, with no
other value in the set, so the 46-into-39 that
[D2](../../Game Testing/Damage-Loop.html#-d2-headshot-and-crit) saw on the rifle is neither a blanket
rule nor a weapon property: `ProjectileSim` resolves `damage = decayed * hit.DamageMod` and
`hit.DamageMod` arrives on the **client's** hit message, so it is a per-hit value (most likely hit
location) rather than anything the server or the item carries.

One thing the run raised and could not settle: hits per second top out at **10 on both weapons**,
which matches the rifle's 105 ms exactly and falls a third short of the Heavy MG's 65 ms. Only hits
are logged, so a miss is indistinguishable from a dropped shot, and 65% accuracy against a closing
melee target is the unremarkable explanation. The alternative is a 35% damage shortfall on every
automatic weapon. A shots-fired counter beside the hit log would separate them; nothing depends on
it until automatic weapons get tuned.

Deliberately *not* read off `MonsterScaling` even though 1200 sits near its level 13 row. Taking a
value from a level-keyed curve would imply a per-creature level that does not exist yet, and the
point of this entry is that the level is the missing input. It stays a flat constant until
`difficulty_cost` → level replaces the whole line.

#### One table checked and ruled out

`dbcharacter::MonsterAttributeRange` looks like the answer — it is per-monster, per-attribute, and
carries `Base` and **`PerLevel`** columns. It is not. 167 rows covering 81 of 3,109 types,
**`per_level` is 0.0 in every single row**, and its `attribute_id` values (1143, 1144, 1582, 1953,
1954, 2005) are not in `AptitudeStat`'s 1–25 range, so none of them is health or damage. Recorded so
the next reader does not spend the lookup.

#### Built 2026-08-15: `difficulty_cost` → level → the shipped curve

[`MonsterTier`](../../UdpHosts/GameServer/Systems/Combat/MonsterTier.cs) supplies the join this entry
spent three research passes looking for, and it is a small file because both halves shipped. The one
invented step is the conversion itself.

**Health is taken as proportional to grade.** The justification is what the column is called: a
*cost* graded across 906 creature types is what an encounter budget spends, so a creature's grade is
what the designers priced it at, and a price tracks power. The constant of proportionality is not
chosen — it falls out of the one anchor that was measured in game:

```
HealthPerGradePoint = AnchorHealth / AnchorGrade = 1200 / 20 = 60
```

`AnchorGrade` 20 is monster 528's shipped grade; `AnchorHealth` 1200 is what
[D6](../../Game Testing/Damage-Loop.html#-d6-a-basic-creature-dies-in-about-two-seconds) confirmed for it
across six kills. Moving the anchor moves the whole ladder and keeps its shape, which is why it is
expressed as a quotient rather than as a table anyone can edit row by row.

**The grade does not become health directly — it picks the nearest row of `MonsterScaling`.** That
detour buys three things a plain multiply does not. Every creature lands on a level Red 5 actually
shipped rather than on an interpolated invention. `damage` arrives from the same row as `health`,
which is what lets one lookup close two entries. And the quantisation is the curve's own: fine at
the bottom where ordinary creatures live, coarse at the top where bosses do.

Where the shipped grades land:

| Grade | Types | Level | Health | Damage scalar | What sits here |
|-------|-------|-------|--------|---------------|----------------|
| 20 | 33 | 13 | 1,224 | ×1.00 | the anchor; monster 528 |
| 35 | 36 | 16 | 2,011 | ×1.64 | monster 1196 |
| 50 | 150 | 18 | 2,801 | ×2.29 | the single most common grade in the game |
| 100 | 117 | 25 | 6,280 | ×5.13 | |
| 150 | 40 | 29 | 9,195 | ×7.51 | where the `*MiniBoss` behaviour scripts start |
| 300 | 35 | 37 | 17,334 | ×14.16 | `GiantAranhaMiniBoss`, `LandSharkMiniBoss` at their larger grade |
| 1000 | 1 | 61 | 60,834 | ×49.70 | one row; three times the player's pool |

The top of that ladder is a sanity check in itself. A grade-300 miniboss lands at 17,334 against the
player's 19,192 — a real fight that is still winnable — and nothing between 20 and 300 has to be
argued for separately, because the shipped grades already order them.

**Levels stay internal.** Nothing sends `MonsterLevel` to the client, and the 1962 client has no
creature-level readout to send it to. This does for creatures exactly what PIN already does for
players: carry a level as a scalar and never show it, which is what the
[charter](../../README.md)'s leveless goal actually requires.

**What is still open, and it is the majority.** 2,203 of 3,109 creature types carry **no grade at
all** and still share one flat pool — `MonsterMaxHealth`, now defined as `MonsterTier.AnchorHealth`.
They fall back to the anchor row, which is byte-for-byte the behaviour PIN had before this change, so
nothing regresses. But grade 0 is *unrated*, not harmless: `EliteWanderer` and `AggressiveWanderer`
both sit there. Until those get a source, this entry stays open. The log line names the fallback out
loud rather than letting it pass as a resolved tier.

Two divergences from the earlier proposal in this entry, both deliberate. The per-placement `level`
in `spawn_group.json` is **not** needed for graded creatures — the grade already carries the
ordering, and a hand-authored level per placement would be 906 decisions where the shipped data has
none. It may still be the answer for the ungraded 2,203, where there is nothing to read. And the
`interim lever` framing is gone: `MonsterMaxHealth` is no longer a tuning knob for the game's feel,
it is the anchor that positions a ladder, and the two should not be confused when someone next wants
fights to be shorter.

Verified against the real `clientdb.sd2` on the running server the same day — 528 resolved to level
13/1,224/×1.00, 1196 to level 16/2,011/×1.64, and ungraded monster 356 fell back and said so.
Thirteen unit tests pin the mapping in
[`MonsterTierTests`](../../Tests/GameServer.Tests/Combat/MonsterTierTests.cs), including the two
promises that keep a bad grade from breaking a spawn: monotonicity across every grade the game
actually uses, and a missing curve costing tiering rather than throwing. The in-game check is
[D7](../../Game Testing/Damage-Loop.html#-d7-creatures-are-no-longer-all-the-same-size), which isolates
the tier by using two creatures that share a weapon.

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
([Prediction-Sweep.md:171-172](../../Game Testing/Prediction-Sweep.html)) — currently the leading
suspect if [NET-19](network.md#net-19)'s P0 verification doesn't hold.

<a id="data-10"></a>

### DATA-10 — NPC perception/threat tuning is invented; retail's own numbers ship for a third of the table [ ] open

`dbmonster::Monster` carries `Behavior`/`BehaviorOffensive`/`BehaviorDefensive` name references but
no numeric perception radius or aggro field, so
[TargetSelection.cs](../../UdpHosts/GameServer/Systems/AI/TargetSelection.cs) picks its own
detection range (25m, narrowed from 40m) and threat gain/decay/engage-threshold constants rather
than reading them off a def. Every monster currently notices, aggros and disengages identically regardless of type. Part
of [M2](../streams/m2-npc-combat.md).

The attack and locomotion passes add more invented numbers of the same kind:

| Number | Where | What it decides |
|--------|-------|-----------------|
| 25m perception (was 40m) | `TargetSelection` | how close before an NPC notices you |
| 37.5m leash (was 60m) | `TargetSelection` | how far before it forgets you |
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
| `perceptionDist` | 67 | 4, 5, 6, 8, 10, 15, 25 | 25m perception, narrowed to this ceiling |
| `maxDistFromSpawn` | 13 | 30 and 45 | 50m chase leash |
| `am1MaxDist` / `am1MinDist` | 23 / 14 | 5 to 150 / 0 to 10 | nothing yet; ability ranges |
| `wideTurnRadius` | 8 | 3, 5, 10 | nothing yet; NPCs turn instantly |
| `triggerPullTime` | 301 | 100 to 10000 | nothing yet; see [DATA-14](#data-14) |
| `fireRestDuration` | 251 | 0 to 9000 | nothing yet; see [DATA-14](#data-14) |

150 monsters carry a standoff distance of one kind or the other.

The perception row is the finding worth acting on independently of the rest. Retail's widest
`perceptionDist` in the whole file is 25m and its most common values are 10 and 15, against PIN's
then-flat 40m — every NPC noticing you from at least 1.6 times as far as the furthest-seeing monster
retail shipped, and four times as far as the most common one. PIN now sits at that 25m ceiling. `maxDistFromSpawn`
says the same about the chase leash, at 30 to 45 against PIN's 50.

None of that is a lookup PIN can just wire up, because a parser has to exist first and 959 rows would
still fall through it. What the numbers do settle is what the fallbacks should be: a default taken
from the distribution of what shipped is defensible in a way that 40m is not.

**Perception stayed at 40m through M2 and was narrowed to 25m on 2026-08-13** (both decisions
user-chosen). Holding it was the right call while it lasted: [N4](../../Game Testing/NPC-Combat.html)
measured disengagement against the 40m radius, and moving the number underneath a spawn-group test
would have made a failure ambiguous between the content and the tuning. N14 to N16 have now passed,
so that reason expired.

25m is retail's widest rather than its most common, which is the smallest change that buys the thing
worth buying — a group that fits on zone 448's starting shelf. Staying at the top of the shipped
range also keeps NPCs as alert as anything retail had, which matters because PIN gives them no other
detection cue: no hearing, no reacting to gunfire, nothing but distance and line of sight. The leash
moved with it, 60m to 37.5m, holding the 1.5x ratio that stops a target on the boundary being
acquired and dropped repeatedly. `ChaseLeash` stayed at 50m — it bounds how far a fight wanders from
where it started, which is a question about the zone rather than about eyesight.

**Narrowing the radius does not by itself put anything near the station.** Zone 448's thirteen
monsters are where they were placed, in the basin, 120m out. What changed is that a group *may* now
stand on the shelf, and putting one there is a walk with `spawngroup add` like every other placement
([DATA-15](#data-15)) — there is no server-side terrain to place one from a desk.

Still one number for every monster in the game. Reading it per monster would not help the zone as it
stands: 1196, 528 and 2342 are all in the 695-row third that carries only a `BehaviorInstanceId`.

`triggerPullTime` and `fireRestDuration` are the two most widely populated parameters in the whole
set, and they describe exactly the pause [DATA-14](#data-14) says is missing from NPC cadence. That
is a second lead out of the same strings and it covers 301 monsters, not 150.

The leash and the cap were added after [N4](../../Game Testing/NPC-Combat.html) ran, and are worth
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
N13](../../Game Testing/NPC-Combat.html) passing doesn't change that. Those entries check that
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
presented as two unrelated bugs: [N2](../../Game Testing/NPC-Combat.html) looked like broken AI (a
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
[E4](../../Game Testing/Environment.html) can be watched happening rather than inferred from a corpse;
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
have to be built from; [E1](../../Game Testing/Environment.html) is the entry that collects them.

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
[Session Setup](../../Game Testing/Session-Setup.html) now recommends monsters by dps, which is a
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
  exactly as it does on top of it. [N16](../../Game Testing/NPC-Combat.html) teleported to a coordinate
  nobody had stood on, landed about 8m under the basin floor, and its destination entered the footing
  record indistinguishable from a real measurement. Placement is only trustworthy if the footing was
  walked to, so `CharacterEntity.PlacedPosition` marks any position a character was *put* at,
  `RecordGroundSample` writes nothing until it leaves that spot, and `spawngroup add` refuses until
  then.
- **Which monsters are chosen is constrained, not free**, and the first cut got it wrong. 1196, 528
  and 2342 are the three [N1 to N13](../../Game Testing/NPC-Combat.html) ran against, so all three
  resolve a working weapon and read as hostile to faction 1, and that is the only relationship
  anyone checked before shipping them 25m apart. They are also in three mutually hostile factions:
  1196 is chosen (2), 528 is melding (6), 2342 is gaea (7), and gaea is hostile to everything while
  chosen and melding are friendly only with each other. The zone fought itself out in the first
  twenty seconds of the 2026-08-13 sitting. The content now uses chosen and melded only, and
  `SpawnGroupSim` audits every standing pair at startup rather than trusting anyone to remember.
- **Respawn delays (90 to 120s) and pack sizes (3 to 6) are invented outright.** Nothing anywhere
  suggests what retail used.
- **Where the groups can go is decided by [DATA-10](#data-10)'s perception number, not by design.**
  Anything hostile within perception of Aero kills it, and the only ground near the station anything
  has been seen standing on is a shelf about 45m across with Aero on it. At the original 40m no
  hostile group fitted there at all, which is why zone 448 has nothing to fight within 120m of where
  a player logs in. **Perception is now 25m**, at which the shelf holds a group comfortably — but the
  thirteen existing monsters have not moved, so putting one there is still a walk with
  `spawngroup add`.

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

The second guess is the probability scale, and **K1's first run promoted it from theoretical to
confirmed.** It is not some distant table: "Melded Loot Common" (5463) is reached from monster 528's
own `loot_table2_id` and its rows are 5000/1000/100. A percentage cannot exceed 100, so that table
is out of 10000, and reading it as percent made a rarity gradient into something that dropped on
every kill.

`LootRoller.ScaleOf` now decides per table — 10000 if any of the table's own rows exceeds 100,
otherwise 100 — which keeps a table internally consistent and gets both known cases right: table 25
(80/10/5/5) stays a percentage distribution summing to exactly 100, and 5463 (6100 of 10000) becomes
61% something and 39% nothing. That is still an inference. Nothing in the file marks the scale.

**What this costs is drop rates, not correctness.** A wrong reading pays too often or too rarely; it
does not pay the wrong thing, because quantities and item ids come off the rows directly.
[LootRollerTests](../../Tests/GameServer.Tests/Loot/LootRollerTests.cs) pins the reading so a change
to it is deliberate, and [K2](../../Game Testing/Kill-Rewards.html) — twenty kills counted against an
expected quarter — is the only measurement available in game. Unlike [DATA-10](#data-10), there is no
shipped string to recover the answer from: the semantics were never data.

<a id="data-17"></a>

### DATA-17 — The zone's richest deposit sat inside a no-thumping zone [x] closed 2026-08-14

Deposit 1 was written into
[resource_deposit.json](../../UdpHosts/GameServer/StaticDB/CustomData/resource_deposit.json) by
hand, on a coordinate a tester had walked over, which is the standard PIN adopted after
[DATA-15](#data-15). Walked ground turned out not to be enough. Its center was 14.7m from outpost
17's, and the client refuses a thumper anywhere near a base without asking the server: every scan
taken on the station shelf on 2026-08-13 came back `NOTHUMPINGZONE` before a position ever reached
the GameServer. Ten scans that evening produced seven refusals and three `OK`s, which is what proved
the refusals were about where the tester stood rather than about the scan protocol.

The fix was to move it, on 2026-08-14, to 199.8, 315.7 — one of the three coordinates the client
itself had answered `OK` at. So it is confirmed thumpable rather than merely far from a base, which
is a stronger property than any of the other deposits have. Renamed Basin Head Crystite to match.

Two things keep this from being a one-off correction. **The other three hand-written deposits have
never been scanned** and could each have the same problem, since none of their centers has been
tested against the client's own no-thumping rule. And the failure was invisible: a scan inside a
no-thumping zone and a scan over barren ground looked identical from the server, because neither
produces a reading. A barren result now logs the nearest deposit and its distance, so finding out
which one you are looking at costs a single press of G. That guard is what closes the entry rather
than the move itself.

The three accepted coordinates were all 78–182m from any deposit, so `barren` was the correct
answer at each of them. Nothing about the sitting suggested the sampler was wrong; the deposits were
simply in places the client would not let anyone drill.

<a id="data-18"></a>

### DATA-18 — A thumper's state-change animations are hardcoded ability ids [ ] open

A thumper announces each stage of its life by firing an ability, and the ability is what carries
the animation and the sound. Four fire over a full cycle, and **three of the four are literals in
PIN's source**, read from nothing:

| Stage | PIN fires | Read from |
|-------|-----------|-----------|
| landing ends | `LandedAbility` | the beacon's def (`landed_ability`, 111 on every row) |
| warm-up ends | `34579` | a literal in [Thumper.cs](../../UdpHosts/GameServer/Systems/Encounters/Encounters/Thumper.cs) |
| thumping ends | `34215` | a literal |
| a completed thumper departs | `34216` | a literal |
| **cut short by a player** | `CompletedAbility` | the beacon's def (`completed_ability`, 123 on every row) |

The last row is the odd one and it is what surfaced the entry. `Thumper.OnInteraction` has two
branches: press E while the thumper is still `THUMPING` and it fires the beacon's own
`CompletedAbility` before jumping to `LEAVING`; wait the cycle out and press E during `COMPLETED`
and it only brings the countdown forward, leaving the hardcoded `34216` to fire on the next tick.
**Two different abilities send a thumper away depending on when you send it.**

A tester reported the difference on 2026-08-14 without knowing the code split existed: a thumper
collected early "played the entire animation with SFX", where every thumper sent away the ordinary
way had "shot upward with 100% momentum instantly and silently". The suspicion was that the
manually placed deposit under it mattered; it does not, since a deposit carries only a node type and
a yield gradient and touches neither the state machine nor any ability. The timing is the whole
difference.

That makes the shipped `completed_ability` the strong candidate for what retail played on
extraction, and PIN's `34216` a stand-in that renders as an instant silent launch. The fix is to
read the def in both branches, but the reading is worth confirming side by side first —
[G4](../../Game Testing/Resource-Payout.html) takes the early path deliberately and now asks for the
observation. Purely cosmetic: the payout, the participants and the completion event are all
resolved in `OnSuccess`, which neither branch changes.

`aptfs::ResourceNodeBeaconCalldownCommandDef` also carries a `death_ability` (33978 on most rows,
0 on two) that nothing in PIN reads at all, so a destroyed thumper has no departure of any kind.

<a id="data-19"></a>

### DATA-19 — No ultimate-charge model [ ] open, found 2026-08-14

Found by [Prediction-Sweep P1](../../Game Testing/Prediction-Sweep.html) rather than by reading:
module 141814 — the second owner of the camera-lock effect, ability 41232 — turns out to be an
**Ultimate**. Slotting it empties the ultimate charge meter, the meter is supposed to refill
through combat, and in PIN it never does, so the ability can never be pressed. P1 is blocked on
this, and so is any other sweep candidate that turns out to sit on an Ultimate.

Retail charged the meter through combat activity. Whatever carries that gain — a stat the server
ticks, a per-hit grant, a message the client waits for — PIN never sends it, so the client's meter
sits where slotting left it. Same family as [DATA-7](#data-7)'s hardcoded progression: a
regeneration the server is supposed to drive and doesn't. Untouched so far by every ability test in
the queue because nothing before P1 ever equipped an Ultimate.

Open questions for whoever picks it up: where the charge lives (a character stat? an item stat?),
whether the 2016 capture's session ever gains charge (worth a `CaptureReplay` look before
guessing), and whether `ability <id>` server-side bypasses the meter — if it does, the sweep can
test an Ultimate's *chain* today even though the keypress route stays blocked, at the cost of not
testing prediction.

<a id="data-20"></a>

### DATA-20 — Monster damage is never scaled, so most NPC gunfire lands for 1 point [~] tiered creatures scale 2026-08-15, the unit problem survives

A whole pack shooting a player does almost nothing. Measured over five minutes in the Basin Mouth
pack with `invuln` off: **8,977 hits of 1 damage against 265 of 49**, and a player taking 4 minutes
40 seconds to die from 19192 health ([DATA-3](#data-3)).

**The resolution code is correct — this was checked before it was blamed.** Both weapons in that
fight read out of the db exactly as PIN computed them:

| Weapon | Template `damage_per_round` | Modifier mult | PIN sends | Observed |
|--------|------------------------------|---------------|-----------|----------|
| 20046 (melee, range 2.6m) | 225 | 0.22 | 49.5 → **49** | 49 |
| 85953 (rifle, range 100m, clip 30) | **1** | 1.0 | **1** | 1 |

So the 49s are right to the unit, which also re-confirms [DATA-11](#data-11)'s modifier fix on a
live path. And the rifle's 1 is what build 1962 actually shipped in
`dbitems::WeaponTemplates`. Nothing is being annihilated.

**The missing piece is level scaling, which makes this [DATA-6](#data-6)'s twin rather than
DATA-11's.** `dbcharacter::MonsterScaling` ships whole — 80 rows — and carries a **`damage` column
beside `health`**, keyed by level, at a flat 2:1 ratio the entire way up:

| Level | Damage | Health |
|-------|--------|--------|
| 1 | 50 | 100 |
| 5 | 122 | 244 |
| 10 | 373 | 745 |
| 20 | 1,950 | 3,900 |
| 45 | 13,934 | 27,869 |
| 80 | 76,863 | 153,726 |

Retail scaled a monster's damage by its level the same way it scaled its health, and an NPC weapon
template's `damage_per_round` is a unit value that scaling multiplied. PIN has no level for a
monster — level was server content, the same wall DATA-6 hit — so it applies no scaling to either
side and reads the raw 1.

DATA-6 was half-answered on 2026-08-13 by dropping monster health to a flat 500, roughly level 8 on
this curve. **The damage half never got the same treatment**, which is why monsters stopped being
sponges while the player quietly became one. Whatever number is chosen here should be chosen against
the same level DATA-6's 500 implies, or the two halves describe different creatures.

**Not a regression.** Rotated logs from 2026-08-14, two builds earlier, carry the same split (253
hits of 1 against 18 of 49).

Open question before anything is applied: whether `MonsterScaling.damage` is damage per round, per
burst, or a budget the weapon divides up. Health is plainly absolute, and the 2:1 ratio suggests a
paired design figure rather than a per-round number, so applying it as a flat multiplier on
`damage_per_round` is a guess that wants stating out loud rather than assuming.

**The 2:1 holds on all 80 rows without one exception**, which sharpens that question rather than
answering it. Numbers that exact are derived from each other, not tuned independently — someone
wrote `damage = health / 2` in a spreadsheet. A per-shot damage figure would not survive that
treatment, because per-shot damage has to interact with fire rate and clip size, and those vary
enormously across the weapon templates. So the reading this entry now favours is that
`MonsterScaling.damage` is a **per-creature damage budget** that its weapon spends, not a per-round
value — but the arithmetic that would prove it has not been done.

The top end argues the same way. Taking the [DATA-6](#data-6) tier read above and mapping
`difficulty_cost` onto levels, a rating-300 mini-boss lands somewhere around level 20–30. Its
`damage` column there is 1,950–5,057 against a player's 19192 — survivable per hit, brutal as a
one-second budget, absurd if a 30-round clip delivers it per round. Any candidate reading can be
sanity-checked against that bracket before it is built.

**A cost-to-level mapping is what closes both halves at once.** Letting the grade pick the row gives
both `health` and `damage` from the same creature, which is the property this entry exists to
restore. The alternative, another flat damage number beside the flat health, repeats the mistake that
created this entry: it fixes the symptom for one creature and leaves every other creature describing
a different animal.

#### Built 2026-08-15, and it sidesteps the unit question rather than answering it

That mapping exists now —
[`MonsterTier`](../../UdpHosts/GameServer/Systems/Combat/MonsterTier.cs), written up in full under
[DATA-6](#data-6). A graded creature's damage is scaled by `ScalingDamageMultiplier`, applied in
`CharacterEntity.GetEffectiveWeaponDamage`, which is the single funnel every damage path already goes
through — `ProjectileSim`, `FireProjectileCommand` and `InflictDamageCommand` all resolve there.

**The multiplier is a ratio against the anchor, not the `damage` column's absolute value, and that is
what dodges the open question above.** This entry could not decide whether `MonsterScaling.damage` is
per round, per burst, or a budget. A *ratio* of two rows does not care: whatever unit the column is
in, it cancels. So the shipped 2:1 curve is used for the one thing it unambiguously encodes — how
much more dangerous level 18 is than level 13 — and not for the thing it does not.

```
ScalingDamageMultiplier = curve[creature.level].damage / curve[anchor.level].damage
```

Because `damage = health / 2` on all 80 rows, that ratio is identical to the health ratio. A
grade-300 miniboss is ~14× the anchor creature in both, which is what "larger ones are individually
more dangerous" has to mean if it is to mean anything mechanical.

The anchor grade comes out at exactly **×1.00** by construction. That is not a rounding convenience —
it is what makes monster 528 a usable control, and it is why
[D7](../../Game Testing/Damage-Loop.html#-d7-creatures-are-no-longer-all-the-same-size) can isolate the
tier: 528 (grade 20) and 1189 (grade 45) carry the **same weapon, 20046**, the same empty behaviour
script and the same regen, so the only thing that can differ between them is the scalar.

**Deliberately a separate field from `WeaponDamageMultiplier`.** That one is owned by the
`SetWeaponDamage` aptitude command, which saves the prior value and restores it on effect end — a
tier written there would be destroyed by the first ability that touched it. Players stay at 1.

**What this does not fix, and the arithmetic is worth doing before anyone celebrates.** The rifle
template that started this entry ships `damage_per_round` **1**, and its owner — monster 1196, grade
35 — now scales by ×1.64. `DamageInfo.Points` is `(int)MathF.Round(Amount)`, so those 8,977 hits of
1 become hits of **2**. A genuine doubling, and still 2 against a 19,192 health pool.

That is the real shape of the problem this entry names. A unit-valued template multiplied by a tier
scalar **quantises brutally at the bottom**: every grade from 20 through 45 rounds a 1 to either 1 or
2, so the shipped grade ladder — which separates those creatures cleanly on health — collapses to two
distinct damage values for them. Only from about grade 65 (×3.19) does a 1-damage weapon start
resolving to distinguishable numbers.

Whether that is a bug or the honest consequence of a correct model turns entirely on the open
question above — whether retail scaled a unit `damage_per_round` multiplicatively at all, or whether
`MonsterScaling.damage` is a budget the weapon divides and a 1 in the template means something other
than one point. **That question is now the only thing keeping this entry from closing**, and it got
sharper rather than vaguer: it is no longer "what unit is the column in" but "does a template value
of 1 mean one point, given that scaling it cannot produce a meaningful spread". The 2,203 ungraded
creature types are DATA-6's remainder, not this one's.

<a id="data-22"></a>

### DATA-22 — 252 objects in New Eden convert to nothing, so you can shoot through them [ ] open, found 2026-08-17

**A tester was shot through a rock formation by a Chosen Fiend that should have had no line of
sight, on the first deliberate sitting against terrain collision
([X4](../../Game Testing/solid-world.html), 2026-08-17). The tester's own reading was that world
terrain and environment meshes behave differently, and that reading is right in substance — but not
because they are two systems. They are two shape formats with two different success rates.**

The ground ships as a mesh. `TagfileLoader` converts meshes without a single failure across the
whole zone. Individual objects — rocks, boulders, scenery — ship instead as `hkpConvexVerticesShape`,
a shape given by its corner points, and that is the format that breaks.

Measured by rebuilding zone 448's collision from the client's own map files with
[CollisionGenerator](../../Tools/CollisionGenerator/) on 2026-08-17:

| | |
|---|---|
| Chunks processed | 93 |
| Shapes built | 1,364,781 |
| `Produced a hull with 0 points` | **235** |
| `IsHullFaceValid reports false` | **17** |
| Failures that were not a convex hull | **0** |

**What a failed object becomes is the part worth knowing.** All three failure paths in
[`ProcessShape`](../../Lib/Shared.Collision/Tagfile/TagfileLoader.cs) return
`new StaticDescription(RigidPose.Identity, PlaceholderBox)` — a 1m box at the chunk's local origin,
which `ZoneLoader` then offsets to the chunk's world origin at **Z 0**. The basin floor of zone 448
is at Z 401, so every one of those placeholders is roughly 400m underground. There is no phantom
wall anywhere in the playable world, which is worth stating plainly because it is the failure mode
this would otherwise look like. The cost is entirely the *absence*: the real object has no collision
at any height, so a shot passes through a rock that is visibly there.

252 objects against 1,364,781 shapes is 0.02% and the zone is not full of holes. But these are whole
objects rather than fragments of one, and a player standing behind one of them has cover that the
server does not know about — which is exactly the shape of a bug that gets reported as "the AI
cheats".

**One theory was checked and killed before this one was reached.** The chunk files store the world
at five levels of detail and `ChunkProcessor` reads only level 3, which looks like an obvious place
to be losing small scenery. Parsing a raw `.gtchunk` directly says otherwise: level 3 is the only
level that carries a collision layer at all — 26.8MB of it in `1_0243_0917`, against zero in levels
0, 1, 2 and 4. The loader is reading everything there is to read.

**Two more layers in that same file are parsed into objects and consumed by nobody**, confirmed by
grep across the game server: `ChunkWaterCollisionLayer`, which is the half
[Submersion](../../UdpHosts/GameServer/Systems/Hazards/Submersion.cs) is missing, and
`ChunkMovementBlockerCollisionLayer`, the invisible bounds nothing enforces. Neither is scenery and
neither explains this entry; they are recorded here because the same rebuild is what proved it.

**The instrument for finding these in game exists as of 2026-08-17**, which it did not when this
entry was opened: `probe` casts a ray where you are aiming and says whether it met the world, an
entity or nothing. Aiming squarely at something solid and being told nothing is this entry, standing
in front of one instance of it. [X10](../../Game Testing/solid-world.html) is the sweep and has not
run. Until then the only census is the offline rebuild above, which counts the failures without
saying which object or where.

Closing this means finding out why `ConvexHullHelper.ComputeHull` yields an empty hull for these
inputs. The cheap mitigation, which is not the fix, is to place the fallback box at the object's own
vertex centroid instead of at the origin, so a broken rock is solid and wrong rather than absent.
The 17 `IsHullFaceValid` rejections may be a separate cause: that check sums signed tetrahedron
volumes about the **origin** rather than about the shape's own centroid, which loses precision for
geometry far from it.

<a id="data-23"></a>

### DATA-23 — Nothing asks the world where the ground is [x] built 2026-08-17, verified in game 2026-08-19

**Terrain has been loaded since 2026-08-16 and no code queries it. Every height in the server is
still borrowed or assumed, and the first sitting on real ground produced two symptoms of the same
missing capability.**

**A creature chasing a player uphill walks into the air.** Observed 2026-08-17: it tracks the slope
correctly from level ground, then rises alongside the hill rather than onto it, holding a height
that belongs to ground it has not reached yet.
[Steering](../../UdpHosts/GameServer/Systems/AI/Steering.cs) says why in its own comment — with no
terrain to raycast against, a destination carries the *player's* Z, because a player standing on the
ground is a ground measurement. The creature's horizontal position lags behind the player's, so its
height is always the height of somewhere further up the hill. `MaxSlope` bounds how wrong it gets;
it cannot make it right.

**Wave members spawn inside hillsides, and terrain turned that from cosmetic into a defect.** A
thumper places its sappers on a 20m ring at the machine's own height. On a slope, an arc of that
ring is underground. Until this week a buried sapper was still shootable, because bullets passed
through the ground too; now the ground stops them and the attacker cannot be killed while it damages
the machine. The tester's report on 2026-08-17 — *"they spawned inside the ground which prevented my
attacks from hitting them"* — reads as a terrain regression and is really an old placement bug that
terrain made visible. It is [DATA-15](#data-15)'s problem inside the encounter rather than in
authored content: `spawngroup add` refuses a footing nobody walked to, and nothing protects a
runtime spawn the same way.

Both symptoms close with one capability that is now possible for the first time: a downward raycast
against the loaded statics, exposed off `PhysicsEngine` as a ground query, called from `Steering`
for the vertical half of a step and from the wave ring at spawn time. `TargetRayCast` is not it —
it needs a source `CharacterEntity` to exclude and reports an entity id rather than a surface.

The same query is what
[Authoring World Content](../streams/world-authoring.md) has been blocked on since 2026-08-13. Its
whole method — walk there, place it at your feet, never type a coordinate — exists because
`MovementRelay.RecordGroundSample` was the only ground truth reaching this server. It stops being
the only one the moment this lands, which is the larger reason to do it before anything else in
[the terrain slice](../streams/solid-world.md).

**Built the same day, and verified in game 2026-08-19 — both symptoms gone.**
`PhysicsEngine.TryGetGroundHeight` casts down from 30m above the asked-for point, 200m of reach,
statics only — a creature standing on another creature is not standing on the ground. It starts
above rather than at the point because the point is usually a guess, and a guess that landed inside
a hillside would find nothing at all with the surface behind the ray. `Steering.TryStep` takes it as
an optional probe and lands each step on the ground under where the step lands; the thumper's wave
ring drops each of its points onto the ground under it.
[SOLID-WORLD-8 and SOLID-WORLD-9](../../Game Testing/solid-world.html) ran 2026-08-19 and both
passed: the creature that rose alongside the slope on 2026-08-17 kept its feet on the surface up
the slope, across its face and down a drop, with 284 ground samples in the log doing the work; and
the wave ring on a slope spawned its members between heights 401.0 and 407.3 across 20m — the
slope's own shape — instead of one flat arc inside the hill, with every member killable and the
defended cycle paying out. The feared regression, a creature frozen by the slope limit on walkable
ground, did not appear.

**One property of the implementation is worth knowing before reading a failure.** The slope limit is
measured between two pieces of *ground*, never against the height the creature is carrying. Measuring
from its own Z reads a creature that spawned inside a hillside — or one left in the air by a
pre-terrain chase — as having a wall in front of it, and freezes it there permanently. That was a
real defect in the first cut, caught by a test rather than by a session. The probe costs two
downward rays per moving creature per tick, which buys correctness in exactly the cases this entry
was opened for.

A third consumer was named here and is now built. No admin command could ask what a ray hits:
`target` casts one but reports "Failed to find target" when the ray strikes the world, which is
indistinguishable from hitting nothing, so "does this rock have collision?" could not be answered in
game and [DATA-22](#data-22) had to be measured offline. `probe` answers it — the ray's verdict,
the distance, and the ground height under the caller, which is the same query this entry is about
made visible from inside the game.
