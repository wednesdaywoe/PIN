---
project: pin
kind: stream
title: "Solid World: what the terrain merge changes"
satisfied: 2026-08-19
relates:
  - ../PROGRESS.md
  - world-authoring.md
  - ../gaps/data.md
  - ../ISSUE-REGISTER.md
---

# Solid World

**Scoped 2026-08-16, the day the collision merge landed. Run 2026-08-17 —
[Solid World](../../Game Testing/solid-world.html) went 5 of 7, and the results are folded in below
rather than left as predictions.** The short version: the merge is sound, the world behaves, and
every defect the sitting found belongs to PIN's own code rather than to the terrain.
**Follow-up run 2026-08-19 — 3 of 3, closing [DATA-23](../gaps/data.md#data-23).** The ground query
holds from both sides (a chasing creature stands on the hill; a wave ring spawns on the slope's
surface, every member killable) and `probe` answers all of its controls, though the night's hunt
found no collision-less object for [DATA-22](../gaps/data.md#data-22).

**Closed satisfied 2026-08-19** (decision 2026-08-19, user-chosen): this does not become M9. The
vertical slice is done and proven, so the next work opens as a new stream with new goals —
[crafting](crafting.md), per the [charter](../Restoration.md)'s "thumping first, crafting second" —
rather than as a ninth milestone on a finished plan. Leftovers are dispatched at the bottom.

## What the sitting settled

| Prediction, written 2026-08-16 | What happened, 2026-08-17 |
|---|---|
| Creatures may stop shooting entirely | **Wrong.** 675 damage lines on the player; ordinary ground does not block a sightline |
| A rise should break line of sight | **Right.** Four holds for `NoLineOfSight` at 12–15m against a 180m rifle, resuming when the tester stepped out |
| One world hit in six dies at the muzzle | **Did not recur.** 391 world hits, none under a metre, against 15 of 86 the previous day |
| Sappers may walk to the machine and do nothing | **Nearly right, and small.** Three single-cycle blips, each resumed immediately |
| Steering's borrowed height is wrong now there is a surface to be wrong about | **Right, and visible.** A creature chasing the tester uphill rose alongside the slope instead of onto it |

Two things the sitting produced that no entry asked for. **Wave members spawn inside hillsides**,
and the tester found it only because the ground now stops bullets and made them unkillable — an old
placement bug that terrain converted into a defect. And a Chosen shot the tester through a rock
formation, with a tester hypothesis attached: that terrain and scenery behave differently. Chasing
that offline produced [DATA-22](../ISSUE-REGISTER.md) — **252 objects in zone 448 convert to nothing
at load**, all of them the one shape format scenery uses, while the ground's mesh format fails not
once.

Both findings are now issue entries: [DATA-22](../gaps/data.md#data-22) for the missing objects,
[DATA-23](../gaps/data.md#data-23) for the ground query nobody has built.

## What landed

Commit `c58a177` (Xsear, merged into `arclight` as `1cc76e5`) replaced the old
`.pinzone.json` loader with a direct reader for the client's own map files, and added
[Tools/CollisionGenerator](../../Tools/CollisionGenerator/) to bake the parsed geometry into a
cache so the cost is paid once rather than every boot. The server now waits for the zone to finish
loading before it starts simulating or accepts a connection.

**It is already switched on and already running.** From the deployment's own config and log, this
machine, 2026-08-16 20:09:

| | |
|---|---|
| `LoadMapsCollision` | `true` in `~/Games/PIN/GameServer/GameServer.dll.config` |
| `MapsPath` | the Flatpak Steam client's `system/maps` |
| `CachePath` | `~/Games/PIN/collision-cache`, 13 GB across 281 baked chunks |
| Zone 448 (New Eden) | 93 chunks, **1,364,781 statics**, loaded from cache in **6 seconds** |
| Errors or warnings during load | none |
| Cost | ~8.6 GB resident with New Eden loaded, and the server no longer accepts a connection until the zone is up |

So the expensive half of this stream is done and paid for. What remains is finding out what a
world with ground in it does to systems written on the assumption that there is none.

## The ground was load-bearing by its absence

Four places in the server are shaped around having no terrain. Each is a comment in the source
saying so, which makes them easy to find and easy to be wrong about — a comment is not a
measurement.

| Site | What it does today | What terrain should allow |
|---|---|---|
| [Steering.cs](../../UdpHosts/GameServer/Systems/AI/Steering.cs) | An NPC has no idea where the floor is, so it borrows the height of whoever it is chasing and is capped at a 45° climb to stop it levitating | A downward raycast. The NPC stands on the ground it is actually on, and `MaxSlope` stops being a substitute for terrain |
| [Sightline.cs](../../UdpHosts/GameServer/Systems/AI/Sightline.cs) | Cover has to be a spawned object, because a hill is not there to block anything ([N3](../../Game Testing/NPC-Combat.html)) | Real cover. Breaking line of sight by stepping behind a rock |
| [Submersion.cs](../../UdpHosts/GameServer/Systems/Hazards/Submersion.cs) | Drowning depth is whatever the client says it is, packed into one byte of every movement pose | A server-side reading, if the water layers get consumed — see below |
| [world-authoring.md](world-authoring.md) | Placing anything by coordinate is blind, which is how three Chosen ended up anchored under the terrain and shooting a player who never saw them | Placement that can be checked before it is committed |

## The first thing to check was whether NPCs can still shoot at all — they can

**Answered 2026-08-17: X3 passed on 675 damage lines.** The section is kept because the reasoning is
still live for anything that aims from foot height, and because X6's three blips are this failure
happening rarely rather than never.

This was the risk that outranked the rest, and it is one line of code.

[`Sightline.IsClear`](../../UdpHosts/GameServer/Systems/AI/Sightline.cs) asks the physics engine what
the shot hits first and accepts the shot only if that is the target:

```csharp
var (hit, _, hitEntityId) = shard.Physics.TargetRayCast(shot.Origin, shot.Direction, shooter, shot.Range);
return !hit || hitEntityId == target.EntityId;
```

Until today the ray had nothing to hit but entities, so the check was nearly free. As of this
build it can hit 1.4 million pieces of ground. **`TargetRayCast` excludes the shooter's own body
and nothing else** — not the ground the shooter is standing on, not the step it is walking down.
An entity's position is at its feet, and the shot is aimed at the target's feet plus an aim height,
so a ray between two NPCs on slightly uneven ground can clip the ridge between them.

If that happens the failure is quiet and total: every NPC in the game stops firing and nothing
logs an error, because "no line of sight" is a normal answer. It is also the easiest thing in the
world to confirm or rule out — spawn one creature on open flat ground and one across a dip, and
see which one shoots.

The same ray backs `ProjectileSim` and the `target` admin command, so a player's own shots are on
the same path. That half is more likely to be an improvement than a regression, since a shot
stopping at a wall is the behaviour everyone expects, but "shots now stop at the ground under a
crouching enemy" is the failure mode to watch for.

## Two layers are parsed and thrown away

`ChunkProcessor` walks each chunk and adds only `ChunkStaticGeometryCollisionLayer` at LOD 3 to the
simulation. Two sibling layers are fully parsed into objects and then consumed by nobody in the
game server, confirmed by grep:

- **`ChunkWaterCollisionLayer`** — water as geometry. This is the missing half of `Submersion`:
  the server could know where the surface is instead of trusting the client's byte. Whether that is
  worth doing is a real question, not an obvious yes, because the client's reading is confirmed
  against the 2016 capture and works.
- **`ChunkMovementBlockerCollisionLayer`** — the invisible walls that keep a player inside the
  playable area. Nothing enforces them today. This is one of the few items on
  [public-server-hardening.md](public-server-hardening.md) that would become nearly free.

Neither is on the critical path. Both are cheap, and the sitting has now said the statics behave.
The 2026-08-17 rebuild counted them in one chunk: 11 water layers and 11 movement-blocker layers in
`1_0243_0917`, tiny next to the 26.8MB of static geometry beside them.

## The bigger prize is placement data, and it is a research question

[DATA-15](../gaps/data.md) records that retail's spawn placements were server content and are gone,
and [world-authoring.md](world-authoring.md) is the appendix written around that loss. The new
parser reads several layers out of the zone and chunk files that are not collision at all:

- `ZonePathLayer` — an id, then a list of steps, each with a position, an orientation and a blob of
  action bytes. That is the shape of a patrol route.
- `ZonePropEncounterNameRegistryLayer` and `ChunkPropEncounterNameRegistryLayer` — arrays of plain
  ASCII names, per zone and per chunk.
- `ZoneSubzoneRegionsLayer`, `ZoneTransferBoundsLayer`, `ZonePropDoodadLayer` (still raw bytes).

**This does not mean the placements survived.** Names are not positions, and an encounter name
registry may be nothing more than a string table the client uses for labels. But paths with
positions and per-step actions are world content by any reading, and the question "what is actually
in these layers for zone 448" is answerable offline, today, with no client and no session. It is
the cheapest research in the register and it could move DATA-15, which has been closed as
unrecoverable since the beginning.

Worth doing as a separate short pass rather than folding into this one, because the answer decides
whether world authoring is a tool-building problem or a data-reading problem.

## Order of work

Revised 2026-08-17 against what the sitting found. Step 1 is done; the old step 2 turned out to be
unnecessary and its slot now belongs to the ground query, which the sitting promoted from a
consequence of this slice to the point of it.

1. ~~**One sitting, before any code.**~~ Done 2026-08-17, 5 of 7, results above.
2. ~~**A ground query, and its two callers.**~~ Built 2026-08-17 as
   `PhysicsEngine.TryGetGroundHeight`, verified in game 2026-08-19 by
   [SOLID-WORLD-8 and SOLID-WORLD-9](../../Game Testing/solid-world.html), both passed. That is
   [DATA-23](../gaps/data.md#data-23) whole, both symptoms, closed.
3. ~~**An admin command that reports what a ray hits**~~ — built 2026-08-17 as `probe`, verified in
   game 2026-08-19 by [SOLID-WORLD-10](../../Game Testing/solid-world.html): world answered with a
   static id and distance, a creature answered by name, the sky answered nothing, and the ground
   line tracked the caller's footing.
4. **The failed hulls** — lives in the register as [DATA-22](../gaps/data.md#data-22), which is its
   home now that this stream is closed. The real fix is finding why
   `ConvexHullHelper.ComputeHull` returns an empty hull for 235 objects.
5. **Consume the water and movement-blocker layers** — evicted on closure to the deferred backlog
   in [PROGRESS.md](../PROGRESS.md). Still cheap, still not urgent.
6. **Correct the documentation** — evicted on closure to the same backlog: files across `Docs/`
   still state as fact that the server holds no terrain.

## Exit

An NPC chases you up a hill and **stands on it**, loses sight of you when you step behind a rock,
and your shot at it stops in the rock rather than passing through. Placement by coordinate can be
checked against the ground before it is committed.

Three of those four are true as of 2026-08-19 — the standing-on-it half passed in game. What
remains is the checked-placement half: the query exists and `probe` reads it back, but nothing in
the authoring path calls it yet before committing a coordinate.

## Decisions this needed — resolved or dispatched on closure

- **Is this M9, or is it maintenance?** Resolved (decision 2026-08-19, user-chosen): neither. The
  slice is done and proven; new work opens as a new stream with new goals rather than as a ninth
  milestone. Rejected: M9 (extends a plan that declared itself finished); silent maintenance
  (understates work that retired the authoring discipline three milestones were built around).
- **Does the 13 GB cache stay outside the repo?** Still open, moved to the deferred backlog in
  [PROGRESS.md](../PROGRESS.md) along with its loose end — the deployed cache loads 1,364,926
  shapes where a fresh rebuild produces 1,364,781, a difference of 145.
- **Is server-side water worth having?** Still open, moved to the same backlog with the
  water/movement-blocker item it belongs to.

## What can't be done from here

Nothing in the list above. Steps 2, 3 and 4 are all server-side, the client is on this machine, and
each of them wants its own short sitting afterwards rather than a shared one.
