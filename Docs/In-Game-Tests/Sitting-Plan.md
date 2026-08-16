---
project: pin
kind: reference
title: One-Sitting Run Order
relates:
  - ../TEST-REGISTER.md
  - README.md
---

# One-Sitting Run Order

> **At the machine, open [sitting-plan.html](sitting-plan.html) instead of this file.** Same plan,
> with every command behind a copy button, the steps as ticks that survive a page reload, and a
> result box per entry that exports as markdown to paste back into the stream docs. This file is the
> readable version for the repo.

Everything in the queue that can be run **solo, in one unbroken session** — one server start at the
beginning, one login, no logout and no restart until the end. Written 2026-08-15 against the queue
as it stood that morning.

The entries themselves live in their own stream files and are not repeated here. This file only
answers "in what order, and what do I need to have done first". Follow the links for the pass and
fail conditions.

Blocks are ordered so that setup is shared, log counts stay clean, and the one thing that has ever
killed a session outright comes last.

| Block | Entries | Time | Runs where |
|-------|---------|------|-----------|
| 0 | setup and baselines | 10 min | station |
| 1 | [D7](Damage-Loop.md) Part A | 3 min | station shelf |
| 2 | [D7](Damage-Loop.md) Part B | 10 min | station shelf |
| 3 | [D7](Damage-Loop.md) Part C | 5 min | station shelf |
| 4 | [B4](Death-And-Respawn.md), [K3](Kill-Rewards.md), [D1](Damage-Loop.md), [B2](Death-And-Respawn.md) | 10 min | station shelf |
| 5 | [V1–V4](Deployables-And-Vehicles.md), [V6](Deployables-And-Vehicles.md), [V7](Deployables-And-Vehicles.md), [D3](Damage-Loop.md) | 20 min | station shelf |
| 6 | [G4](Resource-Payout.md), [G6](Resource-Payout.md), [B5](Death-And-Respawn.md), [K2](Kill-Rewards.md) | 45 min | deposit 1 |
| 7 | [E3](Environment.md), [E7](Environment.md), [E4](Environment.md), [E5](Environment.md), [K4](Kill-Rewards.md) first half | 25 min | melding walls |
| 8 | [K4](Kill-Rewards.md) second half, [G3](Resource-Payout.md) | 10 min | anywhere |
| 9 | prediction sweep and the Biotech question | 30 min | garage |

About 2h45 for all of it. Block 9 is last on purpose and can be dropped; see **Why this order**.

---

## Block 0 — setup and baselines

```
dotnet test PIN.sln
cd ~/Games/PIN && ./start-pin.sh
```

Confirm the build banner matches the server's own line before going to the client:

```
grep -a "Running build" ~/Games/PIN/logs/GameServer.log
```

Log in, then press `f7` in game. That turns on the client's own ability logging
([Client Logging](Client-Logging.md)); block 9 is unreadable without it and the last two sittings
both lost their client-side evidence to a session where it was off.

Take the baseline counts now. Several entries below are counts, and a count is only meaningful
against a number taken before it:

```
grep -ac "took .* damage from" ~/Games/PIN/logs/GameServer.log
grep -ac "paid .* of resource 10" ~/Games/PIN/logs/GameServer.log
grep -ac "rolled nothing" ~/Games/PIN/logs/GameServer.log
grep -ac "died to" ~/Games/PIN/logs/GameServer.log
```

Write the four numbers down.

---

## Block 1 — D7 Part A: do the grades resolve

The gate for blocks 2 and 3. Costs two commands and tells you whether the rest of the tiering work
is worth running.

```
npc 528
npc 1189
grep -a "graded" ~/Games/PIN/logs/GameServer.log | tail -5
```

Full entry and its expected output:
[D7](Damage-Loop.md#-d7-creatures-are-no-longer-all-the-same-size).

**If Part A fails, skip blocks 2 and 3 and go straight to block 4.** Nothing else in the sitting
depends on it.

```
rment
```

---

## Block 2 — D7 Part B: health differs

```
invuln on
createitem 85968
dbg_inventory
equipitem 85968 Primary
grep -a "equipitem" ~/Games/PIN/logs/GameServer.log | tail -1
```

The `dbg_inventory` line is a step, not advice. A created item is not in the server's inventory
list until it is resent, and D6 lost a whole run to skipping it
([NET-18](../gaps/network.md#net-18)). If no `equipitem ... into Primary` line appears, the run is
void — fix that before shooting anything.

Then Part B as written: kill 528 and 1189 with hit counts either side.

---

## Block 3 — D7 Part C: damage differs

```
invuln off
```

Then Part C as written. **This is the part where 1189 can kill you.** If it does, that is a
recordable result, not a mishap — say so and note how fast.

```
rment
```

---

## Block 4 — the cheap combat leftovers

Four entries off one or two kills and one death.

1. [B4](Death-And-Respawn.md): `npc 528`, kill it, watch the corpse for 30 seconds,
   `grep -a "died to" ~/Games/PIN/logs/GameServer.log`
2. [K3](Kill-Rewards.md): `grep -a "not paid — item drops are unbuilt" ~/Games/PIN/logs/GameServer.log`
   then `dbg_inventory`. The half nobody has checked is the bag, so look at it
3. [D1](Damage-Loop.md) and [B2](Death-And-Respawn.md), on the same death: `invuln off`, let a
   monster kill you, **watch the bleedout timer for a few seconds before pressing anything** (that
   is all B2 is), then tap out on Reload and move

B2 only needs one glance. If you miss it here, block 6 and block 7 both kill you again.

```
rment
```

---

## Block 5 — deployables

No travel, no setup, and the only block where nothing can kill you. Run
[V1–V4, V6, V7](Deployables-And-Vehicles.md) in that order; V1 gates V2–V4.

V7 tells you how to find which of your abilities splashes. **Do that discovery step, because it is
also what [D3](Damage-Loop.md) needs** — D3 has sat unrun purely for want of knowing which ability
to press. Once you know it, D3 is three `npc 1196` spawns and one shot.

V5 is skipped: it needs a second person in the vehicle.

```
rment
```

---

## Block 6 — the thumper block

The longest block and the one that pays for the sitting: three unrun entries, and it hands
[K2](Kill-Rewards.md) its measurement for free.

**Re-read the baseline counts here** — write down `died to`, `paid .* of resource 10` and
`rolled nothing` again before the first thumper. Every wave member is monster **528**, which is
exactly the type K2 asks for, and three defended cycles stand up roughly 40 of them. That is K2's
sample in a block you were running anyway.

Walk to within a few metres of `199.8, 315.7` — Basin Head Crystite, deposit 1, about 67m from the
Battleframe Station. Do not `tp` there if you can walk; a teleport destination is a position nobody
has stood on.

1. **[G4](Resource-Payout.md)** — `thumper`, let it reach `THUMPING`, wait about 2½ minutes, then
   interact to cut it short. Note whether the departure had an animation and a sound; that single
   observation is what [DATA-18](../ISSUE-REGISTER.md) has been waiting on
2. **[G6](Resource-Payout.md)** — `thumper` again, let the whole cycle run, collect at `COMPLETED`,
   then **watch the spot for a full minute**. This is the check on the thumper that used to stand
   on screen forever
3. **[B5](Death-And-Respawn.md)** — `thumper` a third time, `invuln off`, and let wave 2 or 3 kill
   you. Respawn, walk back, finish the defence

Then take the three counts again. The difference is K2.

Each cycle is about 5½ minutes of clock plus the fighting. Do not run `rment` while a thumper is
standing — it removes the encounter along with everything else.

---

## Block 7 — the melding trip

Four entries at the same four walls, and the trip also gives [K4](Kill-Rewards.md) its first half.

1. `invuln on`, then [E3](Environment.md) — the four wall pairs, `hazard` at each of the eight
   points. **E3 gates the rest of the melding entries**: if the safe and melded sides come out
   swapped, stop and record which wall
2. [E7](Environment.md) — spawn two monsters in the melding beside you and wait a minute
3. `invuln off`, then [E4](Environment.md) — walk in and die to it. That death is also K4 step 1;
   take K4's grep straight afterwards
4. [E5](Environment.md) — cross, take a few ticks, walk out, let shields recharge, cross again.
   The second crossing must hurt the same as the first. Do the water half too, and **do it with
   `invuln off`** — the 2026-08-12 run left this open by diving the second time with it on

Dying to the melding respawns you at the nearest outpost, which is nowhere near the station. That
is fine; `tp` back afterwards.

---

## Block 8 — the two loose ends

- [K4](Kill-Rewards.md) second half: `npc 528` next to a Chosen group and let them fight. Nothing
  should be paid to anyone
- [G3](Resource-Payout.md): look for a thumper beacon in the calldown interface. **"Nothing is
  offered" is the result**, not a failure — mark it `[-]` and say so

---

## Block 9 — prediction sweep, and the Biotech question

Last, and see below for why.

**Start with the one press that changes an open issue.** Frontline Medic I (ability 34606, module
`createitem 75579`) self-cancelled on 2026-08-14 and that is one of the two legs
[NET-26](../ISSUE-REGISTER.md) stands on. Its effect requires battleframe archtype **13**, the
Biotech family — Biotech (chassis 75774), Dragonfly (76335) or Recluse (76336). The sweep has been
run on a Dreadnaught, which is archtype 8.

So: switch to a Biotech-family frame, slot 75579, and press it.

- **It holds** — the client was right to cancel it on a Dreadnaught, that press was never evidence,
  and NET-26 shrinks to Turret Mode alone
- **It cancels anyway** — the frame requirement is not the explanation and NET-26 keeps both legs

Either answer is worth the sitting. Then work down [P6](Prediction-Sweep.md)'s runnable rows as
time allows — 1875, 2562, 1651, and the five-effect ability 187 — using the sweep's shared
procedure. Each is `createitem`, `dbg_inventory`, slot in the garage, one keypress.

---

## Why this order

**The garage goes last because it is the one thing that has ever ended a session.** The battleframe
station is the open case in [the freeze notes](../gaps/client.md), and block 9 is the only block
that has to open it. Everything above it is finished and recorded before you take that risk. If
block 9 freezes the client, you lose block 9 and nothing else.

**D7 leads because it is the newest code and the cheapest gate.** Four unverified changes from the
last two days ride on it: the creature grading, the health figure of 1200, the shield pool going to
zero, and a one-line inventory fix. Part A costs two commands and says whether the other two parts
mean anything.

**Counts come before the things they count.** K2 needs about 40 kills of one type counted in a
single session, and the thumper block stands up exactly that. Take the numbers immediately before
block 6 and immediately after, rather than greping the whole log at the end, where D7's kills and
block 4's are mixed in.

**`rment` between blocks, never inside one.** It removes every entity except players, which cleans
up spawned monsters and deployables — and would also remove a standing thumper mid-cycle.

## Not in this sitting

| Entry | Why |
|-------|-----|
| [K5](Kill-Rewards.md), [H7](Hostility.md), [V5](Deployables-And-Vehicles.md) | need a second client |
| [P1](Prediction-Sweep.md) | ability 41232 is an Ultimate and its charge meter never refills ([DATA-19](../ISSUE-REGISTER.md)) |
| [D4](Damage-Loop.md), [R5](Damage-Decay.md) | both need an ability that changes weapon damage, and nobody has identified which one that is. Offline work, not sitting work |
| [D5c](Charge-Camera.md) | diagnoses a camera that no longer gets stuck — D5 closed at D5h |
| C1–C7 [Persistence](Persistence.md), L1–L6 [Reliability](Reliability.md) | already passed, and both need the restarts this sitting is avoiding |

## A 90-minute cut

If the sitting is short, run blocks 0–4 and block 6, and stop. That is D7 whole, four cheap combat
entries, three thumper entries and K2 — the newest code checked, and the block with the most unrun
entries in it.
