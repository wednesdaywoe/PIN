---
project: pin
kind: test-stream
title: "Environmental Damage (E1-E7)"
relates:
  - ../TEST-REGISTER.md
---

# Environmental Damage

Part of the [in-game test queue](README.md). Setup, admin commands and the type id tables:
[Session Setup](Session-Setup.md).

The first check of [HazardSim](../../UdpHosts/GameServer/Systems/Hazards/HazardSim.cs), which is the
first thing in the server that can hurt you without something else pulling a trigger. Until it landed
every `TakeDamage` call came from a projectile or an `InflictDamage` aptitude command, so deep water
and melding walls — both near-instant deaths in retail — did nothing, and a character read as
invulnerable to anyone who tried the obvious thing.

Two halves, and they are not equally trustworthy:

- **Drowning is read, not guessed.** The client sends its own submersion on every movement pose
  (`WaterLevelAndDesc`, decoded in `Submersion`), and the thresholds and rates come out of
  `dbvisualrecords::WaterDesc` and status effects 787 and 789. E1 and E2 confirm the reading reaches
  the server intact.
- **The melding is inferred.** Where the wall is comes from the same control points the client draws,
  but *which side of it is lethal* is read off the winding of those points, and the damage rate is
  invented ([DATA-12](../gaps/data.md#data-12)). E3 is the entry that matters: it is the only thing
  standing between this and a server that kills players in safe territory.

Run **E3 before E4**, and run E3 with `invuln` on.

Confirm the deployed build is newer than the hazard work before running any of these:

```
ls -la ~/Games/PIN/GameServer/GameServer.dll UdpHosts/GameServer/bin/Release/net10.0/GameServer.dll
```

Every entry wants the server log open:

```
tail -f ~/Games/PIN/logs/GameServer.log
```

`HazardSim` writes a Debug line whenever either reading changes state, and nothing while it holds:

```
grep -aE "water hazard|the melding at|Water description nibble" ~/Games/PIN/logs/GameServer.log | tail -20
```

Environmental damage reaches the existing damage log with no attacker, so it reads
`Fallback took 959 damage from null` — the `null` is the world, not a bug.

Two commands do most of the work here. Both are new:

| Usage | Aliases | Notes |
|-------|---------|-------|
| `hazard` | `env` | prints your water level, the nearest melding wall, its distance, and which side you're on |
| `invuln [on\|off]` | `god` | no argument toggles; applies to the current target if there is one, otherwise to you |

**E1, E2 and E6 passed on the first sitting, 2026-08-12.** The melding half (E3, E4, E7) has not run
yet, so nothing about the side convention is confirmed. E5 is half-evidenced — see the note there.

## [x] E1: The client's water level reaches the server

The reading everything about drowning rests on. Cheap, and worth doing first because it needs no
damage to land.

1. `invuln on`
2. Walk into any water and stop when it is roughly knee deep
3. `hazard`
4. Wade out until you are chest deep, then `hazard` again
5. `grep -a "Water description nibble" ~/Games/PIN/logs/GameServer.log | tail -5`

Pass: the level rises as you wade in, is 0 back on dry land, and the depth reads as a plausible
fraction of your height (5/15 is 0.33, which is where the client starts slowing you down, so the
number should cross 0.33 about when wading starts to feel heavy).

**Record the position you were standing in and the level at each step.** No water coordinates are
given in this entry because the server has none to give: `LoadMapsCollision` is `false` and no
water volumes are loaded from anywhere, so the only place water exists is the client's map files.
This entry is where the coordinates for E2 come from.

**Also record the description nibble.** It picks which `dbvisualrecords::WaterDesc` row applies and
it is an index into per-zone map data the server doesn't have, so every body of water is currently
read as row 10001 — drown at 0.735 of your height, die at 1.0. The 2016 capture only ever shows
nibble 0. If a different one turns up in play, that log line is what a real mapping would be built
from.

**Passed 2026-08-12, and it found the thing it was watching for.** The level tracks wading and the
whole 0–15 range is live; the readings in that session ran 9, 13 and 15. More usefully, **two
description nibbles turned up in zone 448, not one**:

```
[23:02:18 DBG] Water description nibble 0 seen for the first time; every nibble is read as WaterDesc 10001
[23:04:52 DBG] Water description nibble 2 seen for the first time; every nibble is read as WaterDesc 10001
```

So the zone has at least two water bodies with different descriptions, and the assumption in
[DATA-13](../gaps/data.md#data-13) is doing real work rather than covering a case that never
happens. If nibble 2 indexes the six rows in id order it would be row 10003, which starts drowning
at 0.0845 and killing at 0.328 — knee deep. That is a guess about the indexing and not a reason to
act, but it is the reason not to resolve the nibble on a hunch. Nobody drowned in the shallows,
which is the assumption behaving correctly, not evidence that the water was ordinary.

The position wasn't recorded, so E2 still has no fixed coordinates.

## [x] E2: Deep water drowns you

1. `invuln off`
2. Swim out to where you are in over your head, from the position E1 found
3. Stay there and watch the health bar
4. `grep -aE "water hazard|damage from null" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: `water hazard None -> Drowning` appears, damage starts landing at 2% of max health a second,
and it stops the moment you get your head out.

Two rates, both from retail:

- **Drowning** (in over the head, 0.735 of your height) is a flat 1% of max health every 500ms —
  effect 787. About 58 seconds through 3000 shields and 19192 health, so this is slow enough to swim
  out of on purpose, which is the point of it.
- **Dying** (fully submerged, level 15 of 15) is 2% every 500ms and the rate is multiplied by 1.05
  after every tick — effect 789. That accelerates to about 14 seconds.

**Passed 2026-08-12, and the compounding is visible in the log a tick at a time:**

```
[23:04:57 DBG] Fallback water hazard None -> Dying at depth 1.00 (level 15/15)
[23:05:00 DBG] Fallback took 514 damage from null, 389 of it on shields, 0 shields and 19067 health left
[23:05:00 DBG] Fallback took 540 damage from null, 0 of it on shields, 0 shields and 18527 health left
[23:05:01 DBG] Fallback took 567 damage from null, ...
                          ... 595, 625, 656, 689, 724, 760, 798, 838, 880 ...
[23:05:06 DBG] Fallback water hazard Dying -> None at depth 0.60 (level 9/15)
```

Every step is exactly ×1.05 of the last and they land 500ms apart, which is effect 789's curve
reproduced. Eighteen ticks took 3000 shields and 7800 health in about nine seconds; a couple more and
it would have been a kill. Both thresholds behaved: a later dive read `None -> Drowning` at level 13
and `Drowning -> Dying` at 15.

**Level 15 is reachable**, which the entry used to hedge on — `dying_percent` of 1.0 is a threshold
the client can actually reach, so the top of the scale needs no re-reading.

## [ ] E3: The melded side of a wall is the melded side

**The entry this whole stream exists for.** Melded ground is taken to be on the left of the directed
perimeter curve, which is inferred from how the control points are wound and not stated anywhere in
the data. Backwards, it would make the safe world lethal and the melding safe.

Checked offline by running `MeldingField` itself — not a model of it — over all 16 perimeters and all
24 outposts that `CustomDBInterface` loads for zone 448. Twenty-three read clear. The one that
doesn't is **outpost 30** (`outpost_name` 151665, faction 9, north of Hydro Core), 522m behind
`Hydro_Core_Melding_01`, which is plausible for that part of the map rather than obviously wrong —
worth walking to if the rest of this entry passes, because it is the one place the convention and
the map might genuinely disagree.

That is reassuring and is not the same as being right: outposts only prove where the melding *isn't*,
and 15 of the 16 walls have no outpost within 300m of them at all.

1. `invuln on` — do not skip this
2. `pflags` — the coordinates below are the wall's own altitude and are above the ground in places
3. For each pair, teleport to the safe point, run `hazard`, teleport to the melded point, run
   `hazard` again:

| Wall | Nearest outpost | Safe side | Melded side |
|------|-----------------|-----------|-------------|
| New Eden Melding 03 | 21, 128m away | `tp 129 -1920 605` | `tp 119 -2039 605` |
| New Eden Melding 09 | 40, 224m away | `tp -1967 1094 518` | `tp -2064 1165 518` |
| NewEden MeldingDefenceBase Melding | 34, 154m away | `tp 866 -1745 548` | `tp 922 -1851 548` |
| Hydro_Core_Melding_03 | 38, 207m away | `tp 266 1264 434` | `tp 330 1366 434` |

Every pair straddles its wall by 60m along the perpendicular, so both points are close enough that
the answer should be unambiguous on screen too.

Pass: at each safe point `hazard` names that wall at about 60m with `melded: False`, at each melded
point it names the same wall with `melded: True`, and **the melded point is the one that looks like
it is inside the melding from the camera**. That last clause is the actual test; the numbers only
confirm the server agrees with itself.

Fail: if the sides come out swapped for a wall, note which wall. The convention is one comparison in
`MeldingField.Locate`, so a consistent swap is a one-line fix; a swap on some walls and not others
means the perimeters are not consistently wound and each one needs its own answer.

## [ ] E4: Walking into the melding kills you

1. `invuln off`
2. `tp 129 -1920 605` — the safe side of New Eden Melding 03
3. Walk north-east across the wall towards `119 -2039 605` and stay there
4. `grep -aE "the melding at|damage from null" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: `entered the melding at New Eden Melding 03` appears as you cross, damage starts, and you die
in about twelve seconds — 5% of max health every 500ms through shields and health both.

That 5% is invented and is the only invented number in this stream
([DATA-12](../gaps/data.md#data-12)). Retail killed you faster than this. It is tuned to be
unmistakably lethal while still leaving time to turn around, on the grounds that a wall which kills
you before the log line renders is harder to test than one that doesn't. If it feels wrong in play,
say how it felt — that is the only evidence available, since no melding-wall effect survives in the
db to read a real rate from.

## [ ] E5: Leaving a hazard stops it, and the ramp resets

1. `invuln off`
2. `tp 129 -1920 605`, cross into the melding, take a few ticks of damage, walk back out
3. Let shields recharge, then do it again
4. `grep -aE "the melding at" ~/Games/PIN/logs/GameServer.log | tail -10`

Pass: `left the melding` appears, damage stops within half a second, shields recharge normally, and
the second crossing does the same damage per tick as the first rather than picking up where it left
off.

The same check applies to water, and matters more there because drowning's `Dying` stage compounds:
surface, breathe, go under again, and the first tick of the second dive should hurt exactly as much
as the first tick of the first one.

**Half-evidenced by the 2026-08-12 run and still open.** Surfacing stopped the damage inside one
tick — `Dying -> None at depth 0.60` with no further `damage from null` — but the second dive that
session happened with `invuln` on, so nothing was taken and the ramp reset was never observed. Redo
the second dive without it.

## [x] E6: `invuln` actually stops everything

Not just a convenience — it is what makes E3 runnable, so it has to be trustworthy.

1. `invuln on`
2. `tp 119 -2039 605` and stand in the melding for a minute
3. `npc 1196 5 0 0` and let it shoot you
4. `invuln off` and stay where you are

Pass: no health or shield movement at all while it is on, the `entered the melding` line still
appears (the reading keeps running, only the damage is suppressed), and everything resumes the
moment it is off.

**Passed 2026-08-12, against water rather than the melding.** `Fallback invulnerability ON` at
23:05:18, then a dive at 23:05:24 that logged `None -> Drowning` at level 13 and `Drowning -> Dying`
at 15 with not one `damage from null` line behind it. That is the clause that matters: the hazard
reading keeps running under `invuln`, only the damage stops, which is what makes E3 safe to run.

Two clauses of this entry are still unevidenced — the melding, because none of the melding entries
have run, and the NPC half, because the monster in that session was shot before `invuln` went on.
Neither changes the conclusion but neither has been seen.

## [ ] E7: NPCs are unaffected, on purpose

1. `tp 119 -2039 605` with `invuln on`
2. `npc 1196 0 0 0` and `npc 528 3 0 0` — a Chosen Fiend and a Melded Aranha, both standing in the
   melding with you
3. Wait a minute, then `grep -a "damage from null" ~/Games/PIN/logs/GameServer.log | tail -10`

Pass: neither monster takes any environmental damage. Nothing in the log names them.

This is deliberate, not an oversight: melded creatures live in the melding, and no NPC has a client
to report a water level, so `HazardSim` only ever looks at player-controlled characters. It is
recorded here so that a later session finding an Accord trooper wading unharmed through a river
knows it was a decision. What it costs is that an NPC can chase you into water it should drown in.
