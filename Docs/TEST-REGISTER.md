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

**NPC Combat is complete, 13 of 13, as of 2026-08-13 — the first stream in the queue to close.** A
monster now notices you, turns, runs the ground down, stops where its own weapon can reach, shoots,
tracks you while it fires, gives up and walks home. That is [M2](PROGRESS.md)'s exit condition seen
in game.

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
retail's own standoff distance for most of what [DATA-10](ISSUE-REGISTER.md) currently invents. PIN
can't reach that table yet; it is now the highest-value thing left in the SDB for M2.

Re-run from **N8**, the milestone's exit condition. **N9** is still the entry most likely to come
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

- [ ] [Inventory Delivery](In-Game-Tests/Inventory.md) — 0 of 2 passing. `createitem` shows a
  pickup toast and delivers nothing; the wire-format fix is unverified
  ([NET-18](ISSUE-REGISTER.md)). Gates every entry that spawns an item, all of P0–P6

## Combat

- [x] [NPC Combat](In-Game-Tests/NPC-Combat.md) — 13 of 13 passing. Monsters notice you, turn, close
  the ground, stop where their weapon can reach, shoot, hurt you, track you while firing, give up and
  walk home, and die cleanly mid-burst. It took four sittings and produced
  [DATA-11](ISSUE-REGISTER.md), [NET-22](ISSUE-REGISTER.md), a real AI defect (N4), one entry that was
  wrong about what the server can see (N3), and [CLIENT-3](ISSUE-REGISTER.md)
- [~] [Hostility](In-Game-Tests/Hostility.md) — 6 of 7 passing. H5 passed once NPCs could attack,
  confirming `CanDamage` with a monster as the attacker. Only H7 is left and it needs two clients
- [~] [Damage Decay](In-Game-Tests/Damage-Decay.md) — 4 of 5 passing. The curve's shape between
  its two confirmed anchor points is deliberately left unqueued
- [ ] [Deployables and Vehicles](In-Game-Tests/Deployables-And-Vehicles.md) — 0 of 7 passing, not
  yet run. [NET-14](ISSUE-REGISTER.md) (the discarded faction id) was found reading this code, not
  by running V6
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
