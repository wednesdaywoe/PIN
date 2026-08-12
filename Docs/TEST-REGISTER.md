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

- [~] [Hostility](In-Game-Tests/Hostility.md) — 5 of 7 passing. H5 is permanently blocked until
  [M2](PROGRESS.md) gives NPCs something to decide with
- [~] [Damage Decay](In-Game-Tests/Damage-Decay.md) — 4 of 5 passing. The curve's shape between
  its two confirmed anchor points is deliberately left unqueued
- [ ] [Deployables and Vehicles](In-Game-Tests/Deployables-And-Vehicles.md) — 0 of 7 passing, not
  yet run. [NET-14](ISSUE-REGISTER.md) (the discarded faction id) was found reading this code, not
  by running V6
- [~] [Damage Loop](In-Game-Tests/Damage-Loop.md) — 1 of 4 passing, carried over from before the
  transport work paused the queue

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
