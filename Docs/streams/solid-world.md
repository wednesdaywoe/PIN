---
project: pin
kind: stream
title: "Solid World: what the terrain merge changes"
relates:
  - ../PROGRESS.md
  - world-authoring.md
  - ../gaps/data.md
  - ../ISSUE-REGISTER.md
---

# Solid World

**Scoping pass, 2026-08-16, written the day the collision merge landed.** One casual session has
been played against it and nothing looked wrong, which is worth exactly what a casual session is
worth: no deliberate check has been run on any of the systems below. Everything about PIN's own
code was read here. Everything about in-game behaviour is a prediction, and is marked as one — the
whole point of the first sitting is that a solid world can break as much as it fixes.

Not on any milestone yet. Whether this becomes M9 is the first decision at the bottom.

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
| [Sightline.cs](../../UdpHosts/GameServer/Systems/AI/Sightline.cs) | Cover has to be a spawned object, because a hill is not there to block anything ([N3](../In-Game-Tests/NPC-Combat.md)) | Real cover. Breaking line of sight by stepping behind a rock |
| [Submersion.cs](../../UdpHosts/GameServer/Systems/Hazards/Submersion.cs) | Drowning depth is whatever the client says it is, packed into one byte of every movement pose | A server-side reading, if the water layers get consumed — see below |
| [world-authoring.md](world-authoring.md) | Placing anything by coordinate is blind, which is how three Chosen ended up anchored under the terrain and shooting a player who never saw them | Placement that can be checked before it is committed |

## The first thing to check is whether NPCs can still shoot at all

This is the risk that outranks the rest, and it is one line of code.

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

Neither is on the critical path. Both are cheap once the first sitting says the statics behave.

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

1. **One sitting, before any code.** Does an NPC still shoot? Does an NPC stand on the ground?
   Does a shot stop at a wall? Does anything fall through the floor? Written as test entries first,
   because the method notes are right that an under-specified step gets run wrong.
2. **Fix whatever that breaks.** Most likely candidate is the sightline ray needing to start and
   end at chest height rather than at the feet, which is a small change with a real risk of
   over-correcting into shooting through low walls.
3. **Retire the workarounds, one at a time, each with its own check.** Steering's borrowed height
   first, since it is the one with a comment that names the assumption directly.
4. **Consume the water and movement-blocker layers**, if 1 through 3 come out clean.
5. **Correct the documentation.** Eleven files across `Docs/` currently state as fact that the
   server holds no terrain — the Architecture guide, PROGRESS, the Test Register, four test streams
   and `world-authoring.md` itself — and they will be read as true by whoever comes next.

## Exit

An NPC chases you up a hill, stands on it, loses sight of you when you step behind a rock, and your
shot at it stops in the rock rather than passing through. Placement by coordinate can be checked
against the ground before it is committed.

## Decisions this needs

- **Is this M9, or is it maintenance?** The milestone plan is finished and PROGRESS.md says so in
  as many words. Adding a ninth milestone is a statement that the slice is not the end of the plan,
  which may be exactly right, but it is the user's call rather than a bookkeeping detail.
- **Does the 13 GB cache stay outside the repo?** It is generated, machine-specific and enormous.
  Nothing currently documents how to rebuild it, and the next machine will need to.
- **Is server-side water worth having** when the client's own reading is confirmed and working.

## What can't be done from here

Only the first sitting, and it does not wait on anything — the client is on this machine, the
server is running with terrain loaded right now, and the checks need one player and no special
world state.
