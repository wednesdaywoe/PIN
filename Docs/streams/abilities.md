---
project: pin
kind: stream
title: "Abilities: modules that do what their data says"
id-prefix: ABIL
thesis: >
  An equipped ability module runs its shipped chain on the server and the effect a
  player sees matches it: targets are picked the way the client picks them, damage,
  heals, cooldowns and crowd control land, thrown and placed things act where they
  are, and ultimates are earned rather than free.
satisfied-when: ABIL1..ABIL9 all [x]
relates:
  - ../PROGRESS.md
  - ../ISSUE-REGISTER.md
  - ../gaps/data.md#data-5
  - crafting.md
---

# Abilities

**Opened retroactively 2026-10-05**, to cover work done 2026-10-02 to 2026-10-05 on the `arclight`
branch without a stream doc. This replaces the [Deferred](../PROGRESS.md#deferred) line "the remaining
~197 aptitude stubs — implement the ones the slice's abilities actually hit". That line said to work
from what play reached, not alphabetically, and that is how it went: each run tried a handful of
real modules, and the commands they stopped on got built.

## How it was done

Two tools changed what "implement a stub" means here.

- **[Tools/SDBQuery](../../Tools/SDBQuery/)** (`2ef3f43`) loads `clientdb.sd2` through the server's
  own loader and prints an ability's chain, the effects it applies, its ammo and tiny objects, and the
  phase each step runs in. `unimpl` ranks the command types the Factory doesn't create by how many
  abilities use them, and it can be limited to a list such as the 284 equippable abilities.
- **[Tools/ClientRE](../../Tools/ClientRE/)** (`f915364`) is headless Ghidra over
  `FirefallClient.exe`. Most commands also run in the client for prediction, so their logic can be
  read from the decompiled `apt::*Command` classes instead of guessed. Targeting, filters, cooldowns,
  `ActiveInitiation`, `HealDamage`, `ApplyImpulse`, `RequireItemAttribute` and
  `UpdateWaitAndFireOnce` were all built that way.

Commands that only run on the server shipped with empty defs ([DATA-5](../gaps/data.md#data-5)),
so nothing in the client says what they did. Those were rebuilt from what the surrounding data
needs. Where a value had to be made up, it is listed under **Guesses** below.

## Items

- [x] **ABIL1** — **Targeting matches the client.** The target stack, `PushTargets`/`PopTargets`/
  `PeekTargets`, `TargetTrim` with Chomp, the target filters, `TargetHostiles`/`TargetFriendlies`
  (with `FactionStances` read from the SDB), and `TargetConeAE`, which melee, the use key, Healing
  Wave and about 350 other abilities use and which found nobody before. Each copied context now gets
  its own target lists. In game: CONE-1..5, AREA-1..4.
- [x] **ABIL2** — **Cooldowns are the client's.** `CooldownSet` with local, category and global
  cooldowns, `InflictCooldown` with precool, `TimeCooldown`. `AbilityActivated` goes out only when
  the chain succeeds, with the real cooldowns instead of a fixed 300 ms. Effects file their cooldown
  under the ability and time it from when the effect starts. In game: 4 of 4 on 2026-10-02 (Charge,
  35366); Poison Trail TRAIL-2.
- [x] **ABIL3** — **Heals.** `HealDamage` and `LoadRegisterFromBonus`. In game: 3 of 3 on 2026-10-02.
  Ability 31217 healed a friendly from 100 to 1200, stopped at max health, skipped the user, and
  scaled its cooldown to 49,980 ms.
- [x] **ABIL4** — **Thrown abilities fly and land.** Ammo with a `ProjectileSpeed` follows an arc and
  lands after its flight time. The ammo's impact ability runs where it lands, and `TargetPBAE` honours
  `UseInitPos` (523 commands). `ReplenishableDuration` was rebuilt so effects end (446 abilities). An
  effect's tick no longer splashes. `DetonateProjectiles` bursts a projectile in mid-air, and a
  projectile that hits nothing bursts when its time runs out. In game: AREA-1..4, DET-1, DET-2, DET-5.
  **Not run: DET-3**, whether a mid-air burst poisons what's near it.
- [x] **ABIL5** — **Crowd control.** `ForcePush` throws NPCs along an arc that stops at walls and
  lands on the ground. `ApplyImpulse` launches the way the client does. `CombatFlags` holds stuns and
  roots for as long as the effect lasts. Knocked-down NPCs report the `Knockdown` movement state.
  `RopePull` drags NPCs toward the puller. In game: CONE-2, CONE-4, CONE-5, STUN-1..4, IMP-1, IMP-3,
  IMP-4, PULL-1. The knockdown fall looks rough, which is [CLIENT-5](../ISSUE-REGISTER.md) and
  accepted for now.
- [x] **ABIL6** — **Modules read their stats.** Equipping a module now recalculates the loadout's stat
  totals, which were 0 for anything equipped in game. Added: the register stack,
  `LoadRegisterFromStat`, `RequireItemAttribute`, and `BattleFrameDuration` (Crater locked itself out
  forever before this; IMP-2 failed, then IMP-4 passed).
- [x] **ABIL7** — **The ultimate meter.** Before this it was hardcoded full. It now fills in combat
  over 120 s, gains a bonus from damage dealt (capped at 5 per hit), and is scaled by Charge Speed.
  It gates ultimates and empties when one is used, starts empty at login, and survives death. An
  ultimate's own damage earns no meter. In game: ULT-1..4, the last re-test passed 2026-10-05. This
  closes [DATA-19](../ISSUE-REGISTER.md).
- [x] **ABIL8** — **Things left in the world act on their own.** Tiny objects (`TinyObjectEntity`)
  run the status effect their shipped `dbcharacter::TinyObject` row names. Placed objects count as
  living, fire from themselves and get their owner's module stats. Timed waits are scheduled so they
  fire on time. Built on this: Creeping Death's and Poison Trail's clouds, Fuel Air Bomb's blast and
  fire patch, Fungal Bloom's fungus with its spore mines and clouds, Assassinate's teleport. In game:
  CLOUD-1, CLOUD-2, TRAIL-1, TRAIL-2, WAIT-1..3.
- [~] **ABIL9** — **Tiny objects are drawn.** Every character now has a `TinyObjectView` with 32
  slots, filled as objects appear and emptied as they go (`3a3585e`). Built, not yet run:
  VIS-1..4 is the current [test run](../../Game%20Testing/test-run.html).

## Guesses

These were made up because nothing shipped to read them from. They work in game, but no real data
backs them:

- **Ultimate meter rates.** 120 s from empty to full in combat, a 10 s combat window, 10 points per
  target's worth of health dealt with at most 5 per hit. All of it is tuning
  ([DATA-29](../gaps/data.md#data-29)).
- **The tiny object id map.** `TinyObjectCreate` steps are mapped to tiny object rows by hand
  (1631446 → 387, 1281349 → 386, 1523899 → 440 + 110, and others). An unmapped step still creates
  nothing. 569 of 575 `TinyObjectCreate` defs are empty ([DATA-28](../gaps/data.md#data-28)).
- **Fuel Air Bomb's 2-second fuse** isn't stored anywhere the server can read, so the blast happens
  on landing.
- **Knockback gravity** was picked to feel right.
- **Spread** on a placed object's shot is read as a cone's full width in degrees.
- **`ApplyImpulse` without `Alongvelocity`** uses the user's aim, or away from the impact.
- **`ReplenishableDuration`'s def** was rebuilt as seconds from `InitTime`, 10 s when unusable,
  capped at 120.
- **`TargetByNPC`** defs shipped as bare ids, so it was read off Gravity Pull and Absorption Bomb.
- **Distances:** Teleport stops 1.5 m short of where it landed, RopePull drops a pulled NPC 2.5–5 m
  in front, and a placed object fires from just above itself.
- **Projectile edge cases:** a projectile that outlives its lifetime bursts where it is, the throw's
  effect is held for the whole flight, and a trail's period ability ignores gravity.
- **`BattleFrameDuration`:** what `Notchanged = 0` means is unknown.

## Deferred

- **A player's own self-cast combat-flag effects** are left to the client. 303 of them have no
  duration step the server builds, so they would never end if the server held them.
- **Area and cone targeting respects walls** ([DATA-30](../gaps/data.md#data-30)): built
  2026-10-05 from the client's query flags, not yet run in game.
- **Prediction-Sweep P1** was blocked on the ultimate meter and can now run.
- **The remaining unimplemented command types.** Next: run `SDBQuery unimpl` over the equippable
  set and work down from the top. As before, go by what the commonly used modules hit, not by count.
