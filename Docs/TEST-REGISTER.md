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

## Blocks most of the queue

- [ ] [Inventory Delivery](In-Game-Tests/Inventory.md) — 0 of 3 passing. `createitem` shows a
  pickup toast and delivers nothing; the wire-format fix is unverified
  ([NET-18](ISSUE-REGISTER.md)). Gates every entry that spawns an item, all of P0–P6.
  **I3 is new and should be run first**: every other line of investigation asks whether the item
  was built right, and none asks whether the client had room for it — PIN hands out all 20
  battleframes and their modules at login, which no retail character carried

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
- [~] [Damage Loop](In-Game-Tests/Damage-Loop.md) — 1 of 4 passing, carried over from before the
  transport work paused the queue
- [~] [Environmental Damage](In-Game-Tests/Environment.md) — 3 of 7 passing. Drowning works and
  matches retail's own curve; `invuln` suppresses damage without stopping the hazard reading. The
  three melding entries are untouched, and E3 — which side of a perimeter is the lethal one — has to
  run before E4. E5 is half-evidenced: the damage stopped on surfacing, but the second dive of that
  session ran with `invuln` on, so the ramp reset was never seen

## Client prediction

- [~] [Charge Camera](In-Game-Tests/Charge-Camera.md) — 4 of 8 passing (the failures are dead-end
  attempts on the way to D5h's fix, not open bugs). Read this before trusting any prediction-shaped
  result; [NET-15](ISSUE-REGISTER.md) and [NET-16](ISSUE-REGISTER.md) are what it left open
- [ ] [Client Logging](In-Game-Tests/Client-Logging.md) — reference, not a pass/fail stream: how
  to make the client log its own ability engine, the tooling behind D5h and everything below it
- [ ] [Prediction Sweep](In-Game-Tests/Prediction-Sweep.md) — 0 of 23 passing. The 47 other effects
  shaped like the one D5h fixed (27 player-reachable), entirely blocked on P0 confirming the
  certificate fix ([NET-19](ISSUE-REGISTER.md), rolled up as [NET-20](ISSUE-REGISTER.md))

## Known to need the client, not yet scheduled

Not tests so much as things that can't be checked offline at all, kept here so they're not mistaken
for done:

- Whether any new netfield shape or message layout is accepted by the client
- Client prediction agreement on spread, movement and effect timing
- Anything about what the client renders
