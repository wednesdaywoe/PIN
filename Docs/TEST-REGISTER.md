---
project: pin
kind: register
title: Test Register
relates:
  - PROGRESS.md
  - ISSUE-REGISTER.md
---

# Test Register

Living list of testing streams, run against a real client. A stream's checkbox here is
**derived**: it reads `[x]` only when every item in its own detail doc has passed. Status markers
in the detail docs: `[ ]` not run, `[x]` passed, `[!]` failed (left in with what happened), `[-]`
skipped or not reproducible. A failing item that names a real defect gets filed to the
[Issue Register](ISSUE-REGISTER.md) and stays open here until that entry closes — see the
cross-references below. How to add an entry, and the full status-marker legend, is in
[In-Game-Tests/README.md](In-Game-Tests/README.md).

## Current frontier

**The 2026-08-14 evening sitting closed two milestones, three issue entries, and the queue's
biggest gate, in one pass.** [Persistence](In-Game-Tests/Persistence.md) went **7 of 7** and
closed [M6](PROGRESS.md): crystite and items survive a menu logout, a double relog with nothing
doubling, a killed client, and — beyond what any entry asked — a killed and restarted *server*;
the login returns you to the outpost you left from (Copacabana); and a deliberately corrupted save
was quarantined to a `.corrupt-<stamp>` file with a precise parse error, costing the character its
progress rather than its ability to log in. [Reliability Under Loss](In-Game-Tests/Reliability.md)
went **6 of 6** and closed [M8](PROGRESS.md) — the rollup line below has the numbers — and its L6
closed [NET-24](ISSUE-REGISTER.md): the haunted thumper really was one lost scope-out on a channel
that never resent. **The loop the project promises now closes end to end and survives both a bad
link and a logout.**

**Inventory went 2 of 3 and turned [NET-18](ISSUE-REGISTER.md) from a question into a defect with
a workaround**: the client declines PIN's partial item update, so a created item appears only
after `dbg_inventory resend` — and in the garage picker, not the inventory window, whose search
doesn't match created items. I3 killed the fullness theory with a measurement (246 → 247 items,
all Gear, no UI ceiling). The remaining defect is confined to the item arrays of one message; the
next comparison is capture message [9].

**The prediction sweep opened the same night and earned its keep immediately.**
[P0 passed](In-Game-Tests/Prediction-Sweep.md) — the cert-gated 86074 slotted on the Dreadnaught,
every frame reads 45, [NET-19](ISSUE-REGISTER.md) closed — and the first presses after it found a
class bug, [NET-26](ISSUE-REGISTER.md): Turret Mode (P2) and Frontline Medic (P6) both self-cancel
seconds after engaging while the server keeps the effect set, and the two effects share only one
duration element, the `requirecstate` check. Hover Mode (P3) was the control and held — its
required state, airborne, is one the client tracks itself — so the failing states are specifically
**server-owned**. Two more findings fell out on the way: [DATA-19](ISSUE-REGISTER.md), Ultimates
never recharge (blocks P1), and Hover's lift never engaging (a gameplay gap recorded in P3, likely
[DATA-5](ISSUE-REGISTER.md) territory). Offline next steps are written where they belong: name
effects 1184 and 10812–10815 and read the two `requirecstate` targets in the SDB (NET-26), and
diff the partial item message against the capture (NET-18).

**[M7](PROGRESS.md) was built on the morning of 2026-08-15 and closed the same morning —
[Thumper Defence](In-Game-Tests/Thumper-Defence.md) went 6 of 6 in one sitting, and with it the
milestone plan is finished.** Both offline unknowns resolved on the spot: the collision asset
loads (no fallback line), and the waves can absolutely kill the machine — the tester's first
defended cycle was **lost at 91%**, wave 4's three sappers erasing 1300 health in four seconds,
which is F5 passing on a cycle meant for F1. The re-run went 14 kills for 14 spawns and paid
`completion 1.00, defence 0.63 (1060/4000 health): 25 crystite`, and the tester's on-screen 43 is
the session ledger balanced to the unit (25 machine + 18 kills). F5 also passed unattended before
the tester was even in position: the zone's NPC-owned debug thumper now spawns its own waves and,
undefended, loses to them every session — expect its destruction in future logs where G5 used to
see a 0-participant completion. The sitting's product beyond the passes is the first balance data
the encounter ever produced: 49 per claw at ~1 hit/s/sapper means a near-perfect solo defence
still pays only ~63%, and the tester logged two more readings (Aranha melee reach long, grenade
AoE smaller than Aranha spacing). All tuning deferred, deliberately.

**Everything below this line predates the 2026-08-14 evening sitting.**

**The queue is paused, and for once not because something is blocked.** [M6](PROGRESS.md) and
[M8](PROGRESS.md) were both built on a machine with no client installed, so nothing here can run
until the work reaches one. Two things follow. Everything the next sitting should carry is written
down below rather than remembered, and [Capture Replay](In-Game-Tests/Capture-Replay.md) is the only
stream that still answers anything today.

**That stream just paid for itself, and this is the first time it has.** M8's whole implementation
was calibrated off the 2016 capture instead of off a guess — the retransmit timeout, the resend count
the header carries, whether a resend is the same bytes, whether an ack is cumulative — and reading it
closed [NET-3](ISSUE-REGISTER.md) outright and found two live faults nobody would have reproduced,
because neither happens on a link that doesn't drop packets. The open wire questions
([NET-10](ISSUE-REGISTER.md), [NET-12](ISSUE-REGISTER.md), [NET-17](ISSUE-REGISTER.md)) are the same
shape and are still the cheapest work in the register.

**[Reliability Under Loss](In-Game-Tests/Reliability.md) is new and unrun, L1 through L6.** It is the
only stream in the queue that has to break the network deliberately, with a `tc netem` rule on `lo`
that the entry spells out and that **survives until it is deleted** — an entry run the next day
against a forgotten qdisc measures the wrong thing. **L2 is the one to read first when something
looks wrong**: it counts resend attempts, and a spread across 1, 2 and 3 means the client is
rejecting PIN's resends rather than the network being bad, which is the one thing in M8 that no
offline test can settle.

**[Persistence](In-Game-Tests/Persistence.md) is new and unrun, C1 through C7**, written the same day
M6's code landed rather than after it. **C1 is the milestone's exit condition** and the rest are
diagnosis for when it fails. Two of them are worth carrying into the sitting deliberately. **C3**
relogs twice rather than once, because resources are restored by adding them and a restore that ran
twice would multiply rather than fail, which one relog cannot show. And **C5** is the one closest to
how sessions actually end here: most never send `RequestLogout` at all, so the autosave is the real
save and the logout path is the tidy exception.

**[Thump Placement](In-Game-Tests/Thump-Placement.md) closed [M4](PROGRESS.md) on 2026-08-14 — 6 of
6.** The whole stream ran in one long sitting. The map names the zone's resources (S1), a ground
report reads what is under your feet (S2), and the payouts came out as the milestone predicted:

| | Where | Paid |
|---|-------|------|
| S3 | rich crystite vein, near center | **48 crystite** |
| | the same vein, 59% out to the rim | 33 crystite |
| S5 | the poor crystite trace, 72% out | **8 crystite** |
| S4 | barren ground | 1 unit of sifted earth |

**Where you thump now matters, and it is measurable.** S6 closed it: a deposit placed by walking to
a spot and typing `deposit add` survived a restart with its id, position and radius intact, and was
thumped again afterwards — so new zone content does not need this repo.

Two defects came out of the sitting rather than out of a code read, which is what the queue is for:
[NET-24](ISSUE-REGISTER.md), a finished thumper the client never removes from the world, and
[DATA-18](ISSUE-REGISTER.md), the hardcoded ability that sends it away.

**A restart no longer destroys a sitting's log.** S2 passed on 2026-08-14 and its transcript did
not survive: every start truncated `logs/`, and the restart S6 asks for took the evidence of
everything before it. Outgoing logs now move to `~/Games/PIN/logs/previous/<server>-<stamp>.log`,
ten kept per server, so a mid-sitting restart costs nothing. The same restart also reinstalls, which
is a second trap S6 walks into — [Session Setup](In-Game-Tests/Session-Setup.md#restarting-mid-sitting)
has both.

**[Kill Rewards](In-Game-Tests/Kill-Rewards.md) closed [M5](PROGRESS.md) on 2026-08-13, on its
second attempt.** A Gaia creature paid 4 crystite and the tester saw it arrive. Two melded resources
were paid alongside it off the second loot table, which nobody had predicted — 77343/77344/77345
carry the `Resource` flag, so a kill can pay something even when the crystite roll misses.

**K1's first attempt failed in the most ordinary way available: the wrong table.** 21 kills rolled
crystite ten times and paid it zero times, because the resource-or-item check asked
`dbitems::ResourceItem` — a table that exists, has 111 rows, and lists gatherable materials from id
75537 up rather than the contents of the resource pane. Crystite is id 10 and is not in it. The check
is now the `Resource` flag on `RootItem`, which `createitem` has always used and
[G1](In-Game-Tests/Resource-Payout.md) confirmed.

**What made it a one-pass diagnosis was a log line written for a different entry**, and that is the
part worth carrying forward. K3 asked for rolled-but-unpaid items to be named in the log, on the
argument that "nothing arrived" and "nothing was owed" have to be distinguishable — the same argument
the thumper's participant count settled. The output was `rolled item 10 x2, not paid`, and item 10 is
crystite, so the log stated the bug without anyone reproducing anything. **Neither of K1's own
failure branches applied.** The server was not quietly right while the screen was wrong, and the log
was not silent; it was fluent and wrong, which is the failure mode a test entry is worst at
anticipating.

That session also promoted [DATA-16](ISSUE-REGISTER.md)'s basis-points guess to confirmed, on a table
reached from monster 528 rather than somewhere distant.

**K2 is open and the two sessions disagree.** 48% then 8% against an expected 25%, on 21 and 12
kills. Both are noise at that size, so nothing has been measured yet — it wants 40 kills in one
post-fix sitting. Carry the rate in regardless: table 25 is reached a quarter of the time, so
**three kills in four pay nothing and that is correct**.

**[Resource Payout](In-Game-Tests/Resource-Payout.md) went 3 of 5 on the day it was written,
2026-08-13, and closed [M3](PROGRESS.md).** A thumper called down at a player's feet ran its full
cycle and paid 200 crystite, logged as `paying 200 of resource 10 to 1 participant(s)`. Every one of
the three passed first attempt, which is not this queue's usual ratio and is worth being slightly
suspicious of rather than pleased about.

The milestone closed on a calldown made by an admin command, deliberately: the retail route needs a
beacon item and that is [I1](In-Game-Tests/Inventory.md)'s problem, not M3's. **G3 is the entry that
reopens it** when items are deliverable, so it stays in the queue rather than being marked skipped.

The stream was split at the delivery boundary because a payout that arrives and is never drawn looks
exactly like one that never happened, and **G1 — the half worth being nervous about — went first**:
0 crystite to 200 to 400 to 600 on three `createitem 10 200` calls, the number climbing in an
inventory already on screen. **That result reaches further than its own entry.** It is the first
confirmation anywhere that this client accepts a partial `InventoryUpdate` and merges it, which
takes the delivery mechanism off [I1](In-Game-Tests/Inventory.md)'s suspect list.

**G5 passed without being run**, which is the ideal outcome for a regression check on an unattended
timer: the zone's NPC-owned debug thumper completed during G2's session and paid
`to 0 participant(s)` without taking the shard down. Both thumpers finished inside the same twenty
seconds and the only thing telling them apart in the log is the participant count.

Two entries are left and neither blocks the milestone. **G3** asks whether the client offers a
thumper calldown at all without a beacon item, which is I1's problem; the `thumper` admin command
exists so G2 didn't have to wait for the answer, and G2 used it. **G4** expects PIN to pay full
yield for a thumper cut short, which is the flat grant M3 promised rather than a defect. G2 half
answers it already — the tester collected at `COMPLETED` rather than waiting out its 120 seconds,
and was paid in full twelve seconds later.

**What none of it exercised is the aptitude command.** `Thumper.OnSuccess` reads the grant straight
out of `CustomDBInterface`, so M3's first cut — the uncommented `case` in `Factory.LoadCommand` — is
still code that has never run.

**NPC Combat is closed, 16 of 16, on 2026-08-13 — and with it [M2](PROGRESS.md).** A monster notices
you, turns, runs the ground down, stops where its own weapon can reach, shoots, tracks you while it
fires, gives up and walks home. Thirteen of them are standing in zone 448 when you log in, they come
back ninety seconds after they die, and on the closing run the basin pack killed a player. Nothing
in that sentence needs an admin command.

**N14 took four attempts and failed on content every time, never on the spawn code.** The first
groups used the three monsters the test queue had already proven, because all three were known
hostile to the player. They are also in three mutually hostile factions: the zone fought itself out
in twenty seconds, killed Aero, and left a player logging in to find a few survivors and empty
ground. The second put three Chosen under the terrain, where they shot a player who never saw them.
The third scattered groups over a radius across ground that moves 12m vertically inside 9m
horizontally, and put monsters inside walls.

**The fourth passed, and then N16 found a fourth way to be underground — this time the tester.**
That entry's own `tp` target was a coordinate nobody had stood on, so the fight was conducted from
about 8m inside a hillside, and the teleport destination went into the footing record as if it were
ground. The client reports grounded inside terrain exactly as it does on top of it, which makes a
placement tool that trusts the player's footing only as good as the footing being genuine. Closed
the same way as the others: `CharacterEntity.PlacedPosition` marks a position a character was *put*
at, no footing is recorded until it walks off that spot, and `spawngroup add` refuses outright until
then.

**Two findings came out of the closing run and neither belongs to M2.** A player who dies has no way
back — the server kills them correctly and the client never asks to respawn
([NET-23](ISSUE-REGISTER.md)) — and the first balance reading ever taken against real content says
the monsters are bullet sponges, which is [DATA-6](ISSUE-REGISTER.md)'s flat health pool for every
creature in the game rather than anything about the pack. The incoming half of that reading is
roughly retail already. **The pool has since been dropped from 2500 to 500** (2026-08-13), so any
NPC Combat entry re-run from here is being run against a different fight than the one recorded
above — kills take about 13 rifle shots now rather than 64.

None of those is the sort of thing a test entry catches twice, so each became a server-side guard
rather than a note. `SpawnGroupSim` audits every standing NPC pair at startup for hostile
neighbours, and N14 step 4 greps for it. `MovementRelay` logs a grounded player's position once a
second, which is the only terrain measurement this server ever receives. And `spawngroup add` places
a monster where the tester is standing, which removes the guess entirely: the queue stopped writing
coordinates into a file and started walking to them.

The second failure is also the clearest example yet of the stream's own method. From the screen it
was "there are no enemies"; from the log it was three NPCs closing to 9m and holding fire for
`NoLineOfSight`. Being shot by something you cannot see now has its own named signature in N14.

What every one of the first thirteen entries had in common is that the tester spawned the monster.
[M2](PROGRESS.md)'s exit condition is a monster worth fighting, not a monster spawned by an admin
command, so the spawn groups landing added **N14 to N16**. N14 is the exit condition and the only
entry in the queue that forbids the `npc` command.

It took two sittings that day. The first was called on one defect: monsters teleported, having
neither walked nor slid. The cause was replication, not steering — `Character_MovementView` is
deliberately not flushed to scoped clients and an NPC had nothing sending a pose on its behalf, so a
client held its scope-in position until a checksum mismatch corrected it in one jump
([NET-22](ISSUE-REGISTER.md)). Nothing that run produced was markable.

The shape of that failure is the lesson worth keeping: **the server log could not have shown it.**
Every line the AI writes describes the server's own copy, which was stepping 30cm every 50ms exactly
as `SteeringTests` says. A defect that lives in what was *not* sent is invisible to a log of what
was decided, and this stream's whole method — write down what to grep — had nothing to offer.

It also explained something that had been shrugged off as roughness since combat testing started:
NPCs aiming seconds behind a moving player, firing where you were and then snapping. Aim goes
through the same unflushed view. It read as slow AI because the *burst* travels on the combat view,
which is flushed — so the client was told to draw the shot on time and drew it along a stale aim.
**N13** was written for that half, because nothing about watching a monster walk correctly would
prompt anyone to check whether it aims correctly, and N1–N7 all passed while it was broken.

Two findings came out of the closing sitting as gaps rather than failures, both from watching a
melee Aranha. It renders about 90° off the player it is attacking, and a side-by-side against a
Chosen in the same spot pinned that on the creature model rather than on anything the server sends
([CLIENT-3](ISSUE-REGISTER.md)) — cosmetic, since shots are aimed from live positions. And chasing
"4m looks too far for a claw" turned up `combatDist=` inside the shipped behaviour strings, which is
retail's own standoff distance for most of what [DATA-10](ISSUE-REGISTER.md) currently invents. The
research pass that came out of it settled the question: those strings are on `dbcharacter::Monster`
and 2100 of its 3109 rows carry them, and the instance table behind the rest was never in the client
db to be found. It also turned up `perceptionDist`, which caps at 25m against PIN's 40m.

Superseded by the sitting that closed N8–N13; kept because the reasoning still applies. Re-run from
**N8**, the milestone's exit condition. **N9** is still the entry most likely to come
back with something: the movement state that drives the run animation (`0x2004`) is derived from two
enums, has never been seen on the wire, and sits on a packing [NET-12](ISSUE-REGISTER.md) already
flags — and now finally gets drawn. **N12** looks at whether an NPC stays on the ground, which it has
no way of knowing: the server holds no terrain, so an NPC borrows its target's footing and refuses
slopes too steep to be ground.

**Environmental damage: the water half passes, the melding half hasn't run.** E1, E2 and E6 went
green on 2026-08-12. Drowning reproduces effect 789's compounding curve tick for tick in the log, and
`invuln` suppresses damage while the hazard reading keeps running, which is what makes the melding
entries safe to attempt. Between them they closed the "my character is invulnerable" reading from
[H5](In-Game-Tests/Hostility.md).

E1 also found what it was watching for: **zone 448 uses at least two water descriptions**, not one,
so [DATA-13](ISSUE-REGISTER.md)'s assumption is load-bearing rather than theoretical.

**E3 is what's left and it gates E4.** Where a melding wall is comes from the control points the
client draws, but which side of it is lethal is inferred from their winding, and the damage rate is
invented ([DATA-12](ISSUE-REGISTER.md)). A backwards reading would make the safe world lethal. Run it
with `invuln` on.

The same session produced [DATA-14](ISSUE-REGISTER.md) off a side question about why a spawned
monster wasn't hurting anyone: 1196 really does 8 dps, and the ids that hit harder are mostly ones
whose damage PIN inflates because it doesn't model reloading.

**The standing-still half of NPC Combat is complete, 7 of 7, and it paid for itself several times
over.** N1–N7 ran on
2026-08-12 and four of them failed first time. N2 and N6 failing *differently* — one monster with no
range, another doing no damage — exposed [DATA-11](ISSUE-REGISTER.md), a zero multiplier read
literally that had stripped the range off 269 weapons and the damage off 220 in every weapon the
server had ever resolved, player weapons included. N4 caught NPCs that never disengaged. N3 caught
the entry itself being wrong: with `LoadMapsCollision` off there is no world geometry server-side, so
every raycast PIN makes sees an empty world with a few entities in it. A crash in the tail of the
same session log closed [NET-21](ISSUE-REGISTER.md). None of that was visible from the screen.

Everything below this line predates these sessions and is unchanged.

**Transport is clear; the queue is otherwise untouched since the M1 client sessions.** The
2026-08-11/12 sessions were entirely about T7–T9 — closing the world-entry freeze — so
Transport-And-Lifecycle is the only stream that moved. Damage-Decay and Hostility passed most of
their items during the M1 push and are otherwise stable. Everything downstream of Inventory's I1–I2
(all of Prediction-Sweep's P0–P6, most of the combat items) is still blocked on the same two things
it's always been blocked on: `createitem` actually delivering, and P0 confirming the certificate
fix. Nothing has been filed to the issue register as a new failure this session — the two open
prediction fixes ([NET-19](ISSUE-REGISTER.md)) were already tracked before this pass.

---

## Read first

- [ ] [Session Setup](In-Game-Tests/Session-Setup.md) — reference, not a pass/fail stream: build,
  deploy, admin commands, and the type-id tables the rest of the queue spawns with
- [x] [Transport and Lifecycle](In-Game-Tests/Transport-And-Lifecycle.md) — 9 of 9 passing. T1–T3
  get a client into the world over HTTP-only; T4–T9 close the world-entry freeze, ending in
  [CLIENT-1](ISSUE-REGISTER.md)'s launch-option workaround

## Answerable without a client

- [ ] [Capture Replay](In-Game-Tests/Capture-Replay.md) — reference, not a pass/fail stream: reads
  wire-format and data questions off a 2016 capture instead of a game-machine trip

## Inventory

- [~] [Inventory Delivery](In-Game-Tests/Inventory.md) — **2 of 3, run 2026-08-14.** I1 landed on
  its own middle branch, confirmed twice: a created item is real, equippable and usable, but only
  appears after `dbg_inventory resend` — the client declines PIN's partial item update
  ([NET-18](ISSUE-REGISTER.md) re-scoped to exactly that, still open). I2 passed: the created
  20003 was hand-equipped and the flags round-tripped. I3 killed the fullness theory: 246 → 247
  items, all in Gear, no capacity ceiling in the UI (only a weight meter at 0/255). **The gate on
  P0–P6 is open in practice**: `createitem`, then `resend`, then look in the garage picker — the
  inventory window's search doesn't find created items

## Combat

- [x] [NPC Combat](In-Game-Tests/NPC-Combat.md) — 16 of 16 passing, closed 2026-08-13. Monsters
  notice you, turn, close the ground, stop where their weapon can reach, shoot, hurt you, track you
  while firing, give up and walk home, and die cleanly mid-burst — and thirteen of them are standing
  in the zone before you type anything, respawn where they fell, and can kill you. It took five
  sittings and produced [DATA-11](ISSUE-REGISTER.md), [NET-22](ISSUE-REGISTER.md),
  [NET-23](ISSUE-REGISTER.md), a real AI defect (N4), one entry that was wrong about what the server
  can see (N3), one entry that fought its own fight from inside a hillside (N16), and
  [CLIENT-3](ISSUE-REGISTER.md)
- [~] [Hostility](In-Game-Tests/Hostility.md) — 6 of 7 passing. H5 passed once NPCs could attack,
  confirming `CanDamage` with a monster as the attacker. Only H7 is left and it needs two clients
- [~] [Damage Decay](In-Game-Tests/Damage-Decay.md) — 4 of 5 passing. The curve's shape between
  its two confirmed anchor points is deliberately left unqueued
- [ ] [Deployables and Vehicles](In-Game-Tests/Deployables-And-Vehicles.md) — 0 of 7 passing, not
  yet run. [NET-14](ISSUE-REGISTER.md) (the discarded faction id) was found reading this code, not
  by running V6

## Resources

- [~] [Kill Rewards](In-Game-Tests/Kill-Rewards.md) — 1 of 5 passing, written and run 2026-08-13.
  **K1 passed on the second attempt and closed [M5](PROGRESS.md)**: a Gaia creature paid 4 crystite
  and the tester saw it arrive. The first attempt failed inside PIN — 21 kills rolled crystite ten
  times and paid it zero times, because the resource-or-item check consulted `dbitems::ResourceItem`,
  the gatherable-materials list, which does not contain crystite. That session also confirmed
  [DATA-16](ISSUE-REGISTER.md)'s basis-points guess on a table that is on the kill path. **K2 is
  still open and the two sessions disagree** — 48% then 8% against an expected 25%, on 21 and 12
  kills, which is noise at both ends; it wants 40 kills in one post-fix sitting
- [~] [Resource Payout](In-Game-Tests/Resource-Payout.md) — 3 of 5 passing, all on 2026-08-13.
  **G2 is [M3](PROGRESS.md)'s exit condition and it passed**: a thumper ran its cycle and paid 200
  crystite to one participant. G1 proved a resource count climbs on screen as partial updates
  arrive, which also proves the client merges a partial `InventoryUpdate` at all; G5 passed
  unattended. Left: G3, whether the client can call a thumper down without the admin command — "no"
  is a valid answer for as long as [I1](In-Game-Tests/Inventory.md) is open — and G4, what a
  thumper cut short pays. **M4 replaced the payout model after these passed** — a re-run of G2's
  steps now pays what the ground holds, not a flat 200; the record stands as history and G4 was
  rewritten for the gradient before ever running
- [x] [Thump Placement](In-Game-Tests/Thump-Placement.md) — **6 of 6, closed 2026-08-14**,
  [M4](PROGRESS.md)'s check. S1 on 2026-08-13; S2–S6 the following day in one sitting. Both protocol
  unknowns the stream carried are settled: `ResourceLocationInfo`'s field order was confirmed twice,
  and the ground-report request nothing in the UI sends turns out to come from the client's own code
  behind an equipped Scan Hammer. The payout table is in the frontier note above. Two footnotes
  worth keeping: S2's first pass was recorded on the tester's word because a restart truncated the
  log (fixed the same morning — outgoing logs now rotate into `logs/previous/`), and S6 failed once
  for a reason outside itself, because a plain `./start-pin.sh` reinstalls the repo's copy of the
  very file the `deposit` command writes. **S6 needs `--no-build`**
- [x] [Thumper Defence](In-Game-Tests/Thumper-Defence.md) — **6 of 6, closed 2026-08-15, written
  and run the same morning the code landed.** [M7](PROGRESS.md)'s check. Three cycles instead of
  the planned two, because the first defended attempt was lost at 91% — which passed F5 on a cycle
  meant for F1 and proved the waves lethal beyond argument. The re-run paid 25 crystite at
  defence 0.63, ledger balanced to the unit against the tester's screen. Collision asset loads;
  claws are 49 a hit at ~1/s per sapper; a flawless solo defence keeps ~63% of the yield. The
  NPC-owned debug thumper now loses its own cycle unattended every session, a changed zone
  behaviour recorded in F5. Tuning readings (claw rate vs pool vs payout curve, melee reach,
  grenade AoE vs spacing) recorded and deferred
- [~] [Damage Loop](In-Game-Tests/Damage-Loop.md) — 2 of 5 passing. D1–D4 carried over from before
  the transport work paused the queue. **D6 added and passed 2026-08-15**: `MonsterMaxHealth` was
  raised 500 → 1200 that day after the beta-video reading behind the 500 was re-converted with a
  real rate of fire, and three kills came back at 31 hits each with no variance, health stepping by
  39 onto exactly 0. Time to kill 3.1 seconds, which reads the video's "a few seconds" more directly
  than the 2.0 predicted. **The entry measured the wrong gun and still passed** — the equip had
  silently failed on NET-18, so the rifle fired rather than the Heavy MG, and the health result held
  because it is read off the health column rather than off the hit count. **Re-run the same day with
  the equip confirmed: 31 again, six kills across two weapons with no variance anywhere.** It also
  closed D2's open ×0.85 — all 93 Heavy MG hits landed a full 39, and `ProjectileSim` shows the
  modifier arrives on the *client's* hit message, so it is per-hit (hit location) rather than a
  weapon or server rule. Two authoring lessons recorded in the entry: a test choosing between two
  hypotheses must key on a figure they do not share (39 is both the Heavy MG's damage and the
  rifle's body-shot damage), and a known prerequisite belongs in the steps rather than in a
  fail-branch. Left open, deliberately not chased: hits/sec caps at 10 on both weapons where the
  Heavy MG's data allows 15.4 — only hits are logged, so misses and dropped shots are
  indistinguishable without a shots-fired counter
- [~] [Environmental Damage](In-Game-Tests/Environment.md) — 3 of 7 passing. Drowning works and
  matches retail's own curve; `invuln` suppresses damage without stopping the hazard reading. The
  three melding entries are untouched, and E3 — which side of a perimeter is the lethal one — has to
  run before E4. E5 is half-evidenced: the damage stopped on surfacing, but the second dive of that
  session ran with `invuln` on, so the ramp reset was never seen

## Death and respawn

- [~] [Death and Respawn](In-Game-Tests/Death-And-Respawn.md) — **2 of 5, written and run
  2026-08-15, the day the code landed, and the exit condition is one of the two.** The check on
  [NET-23](ISSUE-REGISTER.md). **A player died and got back up by both routes** — B1's give-up key
  (down 11:17:02, respawned 11:17:05) and B3's 30-second fallback (down 11:03:51, respawned
  11:04:22) — **and both sessions ended in menu logouts instead of the reconnect every previous
  death forced.** B1's pass also retires most of B2's risk: the tap-out gate reads the same clock
  value the countdown does, so a prompt appearing on time means the client converts absolute shard
  times correctly. Left: the countdown's visible reading (B2), monsters still dying rather than
  bleeding out (B4), and dying mid-thumper (B5). **B1 was first recorded as unattempted from a bad
  grep** — the handler had no log line, so a search for `RequestRespawn` could only ever return
  nothing; it logs `tapped out` now. Run the stream with `invuln` off

## Persistence

- [x] [Persistence](In-Game-Tests/Persistence.md) — **7 of 7, closed 2026-08-14 in one sitting.**
  [M6](PROGRESS.md)'s exit condition (C1) passed: 37 crystite survived a menu logout. So did
  everything around it: the save file reads correctly (C2), nothing doubles across a double relog
  (C3), the login returns you to the outpost you left from (C4, Copacabana), a killed client keeps
  its last-earned crystite (C5), and a created item survives relogs (C6). Beyond what any entry
  asked, the character also survived a full **server** kill and restart intact. C7 closed the
  stream: a deliberately corrupted save cost the character its progress, not its ability to log
  in — quarantined to a `.corrupt-<stamp>` file with a precise parse error in the log, fresh save
  created, login clean

## Session stability

- [x] [Reliability Under Loss](In-Game-Tests/Reliability.md) — **6 of 6, closed 2026-08-14, the
  day the stream first ran.** L1, [M8](PROGRESS.md)'s exit condition, passed on the tester's
  verdict: ten minutes at 5% induced loss was "indistinguishable from any other session", over
  ~2,550 server resends (94% attempt 1, zero attempt 3), ~100 client resends correctly
  deduplicated, nothing abandoned, and a queue peaking at 82 unacked. L6 closed
  [NET-24](ISSUE-REGISTER.md): 2 stale keyframe requests against a baseline of 648, model gone
  from the ground. [NET-1](ISSUE-REGISTER.md) closed with the stream

## Client prediction

- [~] [Charge Camera](In-Game-Tests/Charge-Camera.md) — 4 of 8 passing (the failures are dead-end
  attempts on the way to D5h's fix, not open bugs). Read this before trusting any prediction-shaped
  result; [NET-15](ISSUE-REGISTER.md) and [NET-16](ISSUE-REGISTER.md) are what it left open
- [ ] [Client Logging](In-Game-Tests/Client-Logging.md) — reference, not a pass/fail stream: how
  to make the client log its own ability engine, the tooling behind D5h and everything below it
- [ ] [Prediction Sweep](In-Game-Tests/Prediction-Sweep.md) — 0 of 23 passing. The 47 other effects
  shaped like the one D5h fixed (27 player-reachable), entirely blocked on P0 confirming the
  certificate fix ([NET-19](ISSUE-REGISTER.md), rolled up as [NET-20](ISSUE-REGISTER.md))

## Battleframe constraints

- [x] [Constraints Transport](In-Game-Tests/Constraints-Transport.md) — **1 of 1, closed
  2026-08-14, same day it was written.** Unknown JSON keys in the `garage_slots` web response
  survive into Lua in all three shapes (scalar, object, array), so route 2 of
  [Battleframe Constraints](streams/battleframe-constraints.md) can carry capacity numbers on the
  existing payload. A JSON `null` drops its key entirely rather than arriving as `nil` — send a
  value or send nothing. The probes were removed after the run

## Known to need the client, not yet scheduled

Not tests so much as things that can't be checked offline at all, kept here so they're not mistaken
for done:

- Whether any new netfield shape or message layout is accepted by the client
- Client prediction agreement on spread, movement and effect timing
- Anything about what the client renders
