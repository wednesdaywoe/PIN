---
project: pin
kind: test-stream
title: "Damage Loop (D1-D4, D6-D7)"
relates:
  - ../TEST-REGISTER.md
---

# Damage Loop

Part of the [in-game test queue](README.md). Setup, admin commands and the monster type id table:
[Session Setup](Session-Setup.md).

These landed in commits `9768cf4`, `31f0ff3`, `4a8075e` but predate the pause, so their in-game
state is unknown. Prune whatever you've already seen working.

D5 was the fourth entry in this batch; it grew into an investigation of its own and lives in
[Charge Camera](Charge-Camera.md).

## [ ] D1: Weapon damage loop end to end

1. `npc 1196`, kill it
2. Let a hostile source kill you, or shoot yourself off a height with `float` off
3. Move after respawning

Pass: projectile hits damage characters, death fires, respawn puts the player at the nearest
uncaptured outpost and movement input is accepted again afterwards.

## [x] D2: Headshot and crit

1. `npc 1196`
2. Land one body shot and one headshot on it
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -10`

Pass: `hit.DamageMod` and `weapon.HeadshotMult` visibly change the number; the `Critical` damage
flag reaches the client on headshots and crit materials. With the 46-damage assault rifle the
expected pair is 39 body and 58 head.

## [ ] D3: `InflictDamage` splash falloff

1. `npc 1196 0 0 0`, `npc 1196 3 0 0`, `npc 1196 6 0 0` — three targets at increasing distance from
   one splash centre
2. Fire the splash ability centred on the first
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: damage scales down linearly from `Pointblankrange` to the splash range, and the target caught
by both the direct hit and the splash is only damaged once.

## [ ] D4: `SetWeaponDamage` restores on effect end

1. `npc 1196`, shoot it once, note the damage
2. Apply the ability that overrides weapon damage, shoot again
3. `listeffects` to confirm it's active, then wait for it to expire
4. Shoot a third time
5. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -10`

Pass: the third number matches the first, so the `OnRemove` restore path in the active-command
pattern actually ran.

## [x] D6: A basic creature dies in about two seconds

Written 2026-08-15, and it exists to falsify a number rather than to confirm a feature works.

**Passed the same day on the health question, which is what it was for. Three kills, 31 hits each,
no variance at all.** The counts came back 144 → 175 → 206 → 237, and the last kill's tail reads:

```
13:53:20 DBG CharacterEntity (2235630902493516800) took 39 damage ... 264 health left
13:53:20 DBG ... 225 health left      13:53:20 DBG ... 108 health left
13:53:20 DBG ... 186 health left      13:53:20 DBG ...  69 health left
13:53:20 DBG ... 147 health left      13:53:20 DBG ...  30 health left
                                      13:53:20 DBG ...   0 health left
```

Clean steps of 39 landing exactly on 0 from 1200. The pool is the value the code says it is, the
creature dies, and 31 × 39 = 1209 against 1200 is the expected one-round overkill.

**Time to kill was about 3.1 seconds**, not the 2.0 predicted — and that is the video's "a few
seconds" more squarely than the prediction was. The number stands.

**But it ran with the wrong weapon, and the entry as first written could not have told.** The
observed rate of fire was a flat **10 rounds a second** — 10 in each of two complete seconds, across
all three kills — which is 100 ms a round. That is the assault rifle's 105 ms, not the Heavy MG's
65 ms. Confirming it: `equipitem` logs at Information on success and **there is no such line in the
log**, only `13:50:59 INF createitem 85968 as item, item type Weapon, x1`. The equip bailed out at
its not-carrying guard, which is [NET-18](../gaps/network.md#net-18) — a created item is not in the
server's inventory list until it is resent.

Two authoring mistakes, both worth keeping:

1. **The damage figure cannot distinguish the two weapons.** 39 is the Heavy MG's full resolved
   damage *and* the assault rifle's body-shot damage (46 × the ×0.85 step from
   [D2](#-d2-headshot-and-crit)). The rate of fire is the only discriminator and it was written in
   as a secondary check. An entry that picks between two hypotheses must key on a figure the two
   do not share.
2. **`dbg_inventory` was written as conditional advice rather than as a step.** The project already
   knew created items need a resend before they can be equipped. Advice inside a fail-branch does not
   get read before the run.

### The re-run, same day — chaingun confirmed, ×0.85 question closed

The corrected steps were run and the equip took this time:

```
14:12:56 INF equipitem 85968 guid 1F069FCE00040BFD into Primary of loadout 20030
```

Three more kills after it, at **31 hits each again**, counts 237 → 268 → 299 → 330. Six kills
across two weapons and not one of them varied.

**Every hit landed 39 — all 93 of them, with no other value in the set.** The Heavy MG's full
resolved damage, with no reduction. So the ×0.85 step
[D2](#-d2-headshot-and-crit) recorded on the rifle is **not a blanket rule and not a weapon
property**, and reading the code says why:
[ProjectileSim.cs](../../UdpHosts/GameServer/Systems/ProjectileSim/ProjectileSim.cs#L49) resolves
`damage = decayed * hit.DamageMod`, and `hit.DamageMod` arrives **on the client's hit message**. It
is a per-hit value the client supplies, most likely hit location. The rifle's 39-from-46 was one
client-sent modifier on those particular shots, not a discount applied to a weapon. Question closed.

### Left open by this entry: hits per second cap at 10 on both weapons

| Weapon | ms/burst | Shots/sec it should manage | Best second observed |
|--------|----------|----------------------------|----------------------|
| Assault rifle | 105 | 9.5 | **10** |
| Heavy MG | 65 | 15.4 | **10** |

The rifle matches its data. The Heavy MG lands a third fewer hits than its rate of fire allows, and
the two kills ran to the same ~3 seconds despite the chaingun firing 58% more rounds on paper.

**This cannot be settled from the current logging, and the innocent explanation is the likely one.**
Only *hits* are logged — a round that misses writes nothing — and the target is a melee creature
closing on the player, so 10 hits from 15.4 shots is about 65% accuracy against a moving target,
which is unremarkable. The other reading is that the server is dropping fire messages, and that
would be a 35% damage shortfall on every automatic weapon in the game.

Distinguishing them needs a shots-fired counter next to the hits, which does not exist. Worth adding
before anyone tunes automatic weapons; not worth blocking on now.

`MonsterMaxHealth` moved from 500 to 1200 that day ([DATA-6](../gaps/data.md#data-6)). The 500 came
from a tester's beta-video reading that a small Aranha died to "a few seconds of sustained chaingun
fire", and that reading had never been divided by a rate of fire. The Heavy Machine Gun lands **39 a
round every 65 ms** — `Rounds/burst` is 1 — which is **600 damage a second**, so 500 health was
0.83 seconds and not a few. 1200 is two seconds, the low end of a 1200–1800 band.

**Count hits in the log rather than timing it.** A stopwatch measures the tester's reflexes; the hit
count measures the server.

The prediction is a **range, not a number, and which end it lands on is itself a result.**
[D2](#-d2-headshot-and-crit) above records that a rifle resolving to 46 in
[Weapons.md](../Wiki/Reference/Weapons.md) lands **39** on a body shot — an unexplained ×0.85 step
applied at hit time, after item resolution. Whether the Heavy MG takes the same step is unknown:

| If a body shot lands | Per hit | Hits to kill 1200 | Held fire at 65 ms apart |
|----------------------|---------|-------------------|--------------------------|
| the full resolved 39 | 39 | **31** | 2.0 s |
| 39 with D2's ×0.85 | 33 | **36** | 2.4 s |

Either is a pass for the health number. **Read the per-hit figure in the log first**, then judge the
count against the matching row — do not compare a count against the wrong row and call it a failure.
If the log says 33, that pins the ×0.85 step onto a second weapon and is worth writing up
separately; if it says 39, the step is specific to the rifle and D2's note needs revisiting.

Monster **528** (Melded Aranha) is the subject because it is the creature the video anchor is about,
and it carries `difficulty_cost` 20 — near the bottom of the shipped threat ladder, so it is exactly
the kind of thing that should die fast. It is a melee attacker with 5m reach, hence `invuln`.

1. `invuln on` — this entry is about your damage, not theirs, and 528 closes to melee.
2. `createitem 85968` — the Heavy Machine Gun, 39 a round, 250-round clip, 80m range.
3. `dbg_inventory` — **required, not optional.** A created item is absent from the server's
   inventory list until it is resent, and `equipitem` will silently refuse it
   ([NET-18](../gaps/network.md#net-18)). Skipping this is what made the first run of this entry
   measure the wrong gun.
4. `equipitem 85968 Primary`, then **confirm it took**:
   `grep -a "equipitem" ~/Games/PIN/logs/GameServer.log | tail -1` must show
   `equipitem 85968 guid ... into Primary`. No line means it did not equip and the run is void.
   Run `equipitem` with no arguments to list slot names if `Primary` is rejected.
5. `npc 528` to put one at your feet, then back off a few metres so every round connects.
6. Hold fire until it dies. Do not stop and restart — a gap in fire makes the count unreadable.
7. `grep -ac "took .* damage from" ~/Games/PIN/logs/GameServer.log` before and after, and subtract.
   The damage line is at **Debug** level, so confirm Debug logging is on first by checking any
   `DBG` line appears in the log at all.
8. **Read the rate of fire, which is the step that identifies the weapon:**
   `grep -a "took .* damage from" ~/Games/PIN/logs/GameServer.log | tail -31 | awk '{print $1}' | uniq -c`
   — about **15 a second** is the Heavy MG (65 ms). About **10 a second** is the assault rifle
   (105 ms) and means the equip did not take.

Pass: the per-hit figure reads **39 or 33**, the count matches that row of the table above, and the
creature dies. Record both numbers, not just the count.

Fail, hits log a per-hit figure that is neither 39 nor 33: the health number is not the problem and
this entry is not the one to read. A wrong per-hit figure is weapon resolution, so go to
[DATA-11](../gaps/data.md#data-11) — that bug stripped a rifle's damage once already and the melee
weapon in [DATA-20](../gaps/data.md#data-20) is the only live confirmation it stays fixed.

Fail, the count matches but it reads as far too long in the hand: the arithmetic is right and the
target is wrong. Record how it felt — this is the one place a tester's judgement outranks the
number, and it is why 1200 was taken from the bottom of the band rather than the middle. Note that
[N16](NPC-Combat.md) called 2500 a bullet sponge, so there is a known ceiling somewhere below that.

Fail, far more hits than either row predicts: something is adding health after spawn, or the
creature is not 528. Check the spawn line named the type you asked for.

**Whatever this returns, it does not close [DATA-6](../gaps/data.md#data-6).** One flat pool for
every creature in the game is the entry, and 1200 is still one flat pool. What a pass buys is
confidence in the *band*, which is what the `difficulty_cost` → level work will be anchored against.

## [x] D7: Creatures are no longer all the same size

**Passed 2026-08-15, 22:31–22:44, all three parts.** Part A: both grade lines exact. Part B: **32**
hits for 528 and **72** for 1189, and the pair repeated a second time on fresh spawns — four kills,
four counts, no drift, every round 39. Part C: **49** a hit from 528 against **112** from 1189
(predicted 49 and 113; a point of rounding on the subject, none on the anchor). 528 held its ×1.00
anchor on all six spawns, so the difference is the grade and nothing else.

The Part C grep did not match at first. That was the pattern, not the run — see step 4 below.

One thing the numbers say that the entry did not anticipate: at 112 a hit against the player's
19192 health, **1189 needs about 170 uninterrupted hits to kill you** — nearly four minutes of
standing still. It is not dangerous, it is slow. Any entry that needs a death by monster should
expect to wait, or be killed by something else. See [B4](Death-And-Respawn.md).

Written 2026-08-15, after the `difficulty_cost` → level work landed. This is the entry that checks
the thing D6 explicitly could not close: **one flat pool for every creature in the game**.

Every NPC in PIN used to have identical health and identical damage, because the shipped power curve
`dbcharacter::MonsterScaling` is keyed by a creature's *level* and levels were server content that
never shipped. `Monster.difficulty_cost` — a threat grade 906 of the 3109 creature types carry — now
supplies that missing key. A grade picks the nearest row of the curve, and the row gives health and
damage together.

### Why this pair of creatures and no other

**528 and 1189 carry the same weapon, `20046`.** They also share an empty behaviour script, the same
health regen, and hostile factions. The *only* thing that differs between them and matters here is
the grade: 20 against 45. So any difference this test sees is the tier and nothing else — which is
exactly the control D6 lacked when it could not tell a Heavy MG from an assault rifle.

528 is the anchor, and that is deliberate. Its multiplier is **exactly** ×1.00 by construction, so it
should behave the way it did in D6. **If 528 moves, the cause is not tiering.**

| Creature | Grade | Level | Health | Damage scalar | HMG hits to kill, at 39 a round |
|----------|-------|-------|--------|---------------|----------------------------------|
| **528** (anchor/control) | 20 | 13 | 1224 | ×1.00 | **32** |
| **1189** (subject) | 45 | 18 | 2801 | ×2.29 | **72** |

528 reads 32 rather than D6's 31 because 1200 was the anchor *target* and level 13 ships at 1224 —
the curve is quantised and 1224 is the nearest real row. A one-hit drift is the expected cost of
landing on shipped rows instead of invented ones, and it is the whole reason 528 is still readable
as a control.

### Part A — the grades resolve (no combat, do this first)

Costs nothing and tells you whether the rest of the test is worth running.

1. `npc 528`
2. `npc 1189`
3. `grep -a "graded" ~/Games/PIN/logs/GameServer.log | tail -5`

Pass: two lines reading

```
Monster 528 graded 20 -> level 13, 1224 health, damage x1.00
Monster 1189 graded 45 -> level 18, 2801 health, damage x2.29
```

Fail, both lines say `(ungraded, fell back to the anchor)`: the curve did not load. The server reads
the **full** `clientdb.sd2`, not the pruned one, so check the `Opening SDB from` line points at the
Firefall install and not at `clientdb_minimal.sd2`.

Fail, no `graded` lines at all: the damage line is at **Debug** level and so is this one. Confirm any
`DBG` line appears in the log before concluding anything.

**If Part A fails, stop.** Parts B and C cannot mean anything if the grade never resolved.

### Part B — health differs, by hit count

Same method as [D6](#-d6-a-basic-creature-dies-in-about-two-seconds), because counting hits in the
log measures the server while a stopwatch measures the tester's reflexes.

1. `invuln on` — this part is about your damage, not theirs.
2. `createitem 85968` — the Heavy Machine Gun, 39 a round, 250-round clip, 80m range.
3. `dbg_inventory` — **required, not optional.** A created item is absent from the server's inventory
   list until it is resent ([NET-18](../gaps/network.md#net-18)).
4. `equipitem 85968 Primary`, then confirm it took:
   `grep -a "equipitem" ~/Games/PIN/logs/GameServer.log | tail -1` must show
   `equipitem 85968 guid ... into Primary`. No line means the run is void.
5. `grep -ac "took .* damage from" ~/Games/PIN/logs/GameServer.log` — the before count.
6. `npc 528`, back off a few metres, hold fire until it dies. Take the count again and subtract.
7. `npc 1189`, same. Take the count again and subtract.

Pass: **32 for 528 and 72 for 1189.** The 250-round clip covers both without a reload.

Confirm the per-hit figure is 39 while you are here:
`grep -a "took .* damage from" ~/Games/PIN/logs/GameServer.log | tail -5`

Fail, both counts come back the same: the grade is resolving (Part A said so) but the health is not
reaching the entity. That is the `SetMaxHealth` call in `CharacterEntity`, not `MonsterTier`.

Fail, 1189 takes about 72 hits but 528 takes 31 rather than 32: harmless, and worth writing down
rather than treating as a failure. It means a round landed slightly above 39 somewhere, and
[D6](#-d6-a-basic-creature-dies-in-about-two-seconds) already found that per-hit damage carries a
client-sent modifier.

### Part C — damage differs, with the weapon held constant

This is the half [DATA-20](../gaps/data.md#data-20) is about, and the shared weapon is what makes it
readable.

1. `invuln off` — **required for this part**, and it is the step that makes 1189 dangerous.
2. `npc 528`, let it hit you several times, then kill it.
3. `npc 1189`, let it hit you several times, then kill it.
4. Count the two figures rather than reading them off a tail — the hits interleave with your own,
   and a monster shooting once every 1280ms fills 20 lines fast:

   ```
   grep -a "Fallback took .* damage from" ~/Games/PIN/logs/GameServer.log | sed 's/.*took \([0-9]*\) damage.*/\1/' | sort -n | uniq -c
   ```

   `Fallback` is the player's placeholder name (`HardcodedCharacterData.MaleFallbackData.Name`),
   not an error. These are the hits *you* took. **Match the name exactly — the line is
   `Fallback took`, with nothing between.** A pattern of `Fallback .* took` matches nothing at all,
   which is how the 2026-08-15 run briefly read as "no damage logged" while the log held 63 hits.

   To see the raw lines instead:
   `grep -a "Fallback took .* damage from" ~/Games/PIN/logs/GameServer.log | tail -20`

Weapon 20046 is the melee weapon [DATA-20](../gaps/data.md#data-20) already measured: template
`damage_per_round` 225 × modifier 0.22 = 49.5, observed as **49**. So the predicted pair is concrete
rather than a ratio:

| Creature | Damage scalar | 49.5 × scalar | Rounds to |
|----------|---------------|---------------|-----------|
| **528** | ×1.00 | 49.5 | **49** or 50 |
| **1189** | ×2.29 | 113.4 | **113** |

Pass: 528 lands about 49 and 1189 lands about 113. Both carry weapon 20046, so there is no second
explanation for a difference.

Damage is applied as `(int)MathF.Round(Amount)`, which is why 49.5 can read as either 49 or 50 — D6
saw 49. A one-point wobble at the anchor is rounding, not a finding.

Take several hits from each rather than one. Range decay applies to what monsters shoot too, so a
single pair of numbers taken at different distances proves less than a handful taken at contact.

Fail, the two numbers are equal: `ScalingDamageMultiplier` is not reaching
`GetEffectiveWeaponDamage`. Note that an ability using `SetWeaponDamage` deliberately does *not*
clobber it — the two multipliers are separate fields for that reason — so an active effect is not the
explanation.

Fail, 1189 kills you outright: record it and say so. ×2.29 on a grade-45 creature is the *low* end of
the ladder; grade 300 minibosses resolve to ×14.16, and if the low end already reads as lethal then
the anchor is wrong rather than the mapping. This is the failure mode most worth catching early, and
a tester's judgement outranks the arithmetic here.

### What this closes and what it does not

A pass closes the health half of [DATA-6](../gaps/data.md#data-6) for the **906 graded creature
types** and the damage half of [DATA-20](../gaps/data.md#data-20).

It does not close either entry outright. **2203 of 3109 creature types carry no grade at all** and
still share one flat pool, and `EliteWanderer` is among them — so an ungraded creature is unrated,
not harmless, and the fallback is a known gap rather than an answer. Grade 0 is also why the log
line says `(ungraded, fell back to the anchor)` out loud instead of quietly.
