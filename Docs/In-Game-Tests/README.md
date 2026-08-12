# In-Game Test Queue

A running log of checks for me to run with a real client, written down as work
lands so nothing accumulates unverified. Work through it at the game machine, record the result
inline, and delete entries once they've passed and the behaviour is covered elsewhere.

Status markers: `[ ]` not run, `[x]` passed, `[!]` failed (leave it in with what happened),
`[-]` skipped or not reproducible.

Adding an entry: state the task, the steps in order, and every command in full with its arguments
filled in — real type ids, coordinates, item ids, grep lines. These get run on the game machine
away from the source, so a step that says "spawn a monster" instead of `npc 1196` costs a trip back
here, and an under-specified step gets run wrong. If a check needs a specific world state (two
clients, a particular NPC), say so; that's usually what makes an entry sit unrun.

One file per feature, and entry ids stay unique across the whole queue — a `D5h` or an `H1` in a
commit message or a code comment still names exactly one entry. Put a new entry in the file whose
feature it exercises; start a new file when a feature arrives that none of these cover.

## Read first

- [Session Setup](Session-Setup.md) — build, deploy and start the servers, the greps worth running
  after every session, the admin command table, and the monster, deployable and vehicle type ids
  the rest of the queue spawns with.
- [Transport and Lifecycle](Transport-And-Lifecycle.md) — **T1–T3**. HTTP-only URLs, world entry
  without the Wine TLS freeze, and a clean shutdown. A client that can't reach the world can't run
  anything else, so these go before everything below.

## Answerable without a client

- [Capture Replay](Capture-Replay.md) — some of the queue is a question about what the *real*
  server sent, not about what PIN does with it. Those can be read off a 2016 recording instead of
  waiting for a trip to the game machine.

## Blocks most of the queue

- [Inventory Delivery](Inventory.md) — **I1–I2**. `createitem` shows a pickup toast and delivers
  nothing, which gates every entry that has to spawn an item — all of P0–P6. The R series escaped it
  by using the Biotech frame's default weapon instead.

## Combat

- [Hostility](Hostility.md) — **H1–H7**. Faction stances, who may damage whom, and splash
  respecting faction.
- [Damage Decay](Damage-Decay.md) — **R1–R5**. Range-based falloff: whether the SDB columns
  resolve to a sane curve and whether the client agrees with it.
- [Deployables and Vehicles](Deployables-And-Vehicles.md) — **V1–V7**. Health on entity types
  that have never sent a health update, destruction, ejection and splash.
- [Damage Loop](Damage-Loop.md) — **D1–D4**. The end-to-end weapon loop carried over from the
  damage work: hits, headshots, splash falloff and effect restore.

## Client prediction

- [Charge Camera](Charge-Camera.md) — **D5, D5a–D5h**. Eight attempts at the stuck vertical
  mouselook, ending in the fix: PIN never wrote `LocalEffectsController`, the owner-private array
  the client binds its own predictions to. Read this before trusting any prediction-shaped result.
- [Client Logging](Client-Logging.md) — **D5g**. How to make the client log its own ability
  engine. This is what turned D5 from guesswork into a reading, and every entry in the sweep
  below depends on it.
- [Prediction Sweep](Prediction-Sweep.md) — **P0–P6**. The 47 other effects shaped like the one
  D5h fixed, plus the certificate and frame-level gates that block half of them from being
  slotted at all.

## Known to need the client, not yet scheduled

Not tests so much as things that can't be checked offline at all, kept here so they're not mistaken
for done:

- Whether any new netfield shape or message layout is accepted by the client
- Client prediction agreement on spread, movement and effect timing
- Anything about what the client renders
