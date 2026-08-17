---
project: pin
kind: stream
title: "Appendix: Authoring World Content"
relates:
  - ../PROGRESS.md
  - ../gaps/data.md
---

# Appendix: Authoring World Content

PIN has to place things in the world itself. Retail's placements were server content and are gone
([DATA-15](../gaps/data.md#data-15)), and this isn't confined to monsters: [M4](m4-thump-placement.md)
needs a per-zone resource map and [M7](m7-encounter-combat.md) needs encounter spawn points, both of
which are the same problem wearing different names.

Two attempts at zone 448's spawn groups failed on 2026-08-13, both on placement rather than on code.
The second is the instructive one: three Chosen anchored under the terrain, shooting a player who
never saw them. That is what authoring blind looks like, and it will keep happening.

## The problem is not editing, it is not knowing where the ground is

Calling this "we need a map editor" gets the shape slightly wrong, and the difference decides what to
build. An editor that can't answer "what is the ground height at X,Y" is as blind as a text file, so
the tool isn't the missing piece. The missing piece is terrain.

`LoadMapsCollision` is off and `MapsPath` is empty, so the physics world holds entity colliders and
nothing else. Every server-side raycast in PIN sees an empty world with a few objects floating in it,
which [N3](../In-Game-Tests/NPC-Combat.md) found the hard way. There is no ground to query, no cover
to break line of sight, and nothing for a spawn to stand on.

Three consequences already have entries: NPC ground clamping borrows the target's footing because
there is nothing to raycast against, spawn anchors are inferred from shipped object positions
([DATA-15](../gaps/data.md#data-15)), and shots pass through scenery.

## What already exists, which is more than expected

The consumer side of terrain is built and proven.

| Piece | State |
|-------|-------|
| [ZoneLoader](../../Lib/Shared.Collision/ZoneLoading/ZoneLoader.cs) | 160 lines, reads `{zoneId}.pinzone.json` plus `chunks/*.pinchunk.json`, adds statics per chunk at its origin |
| [TagfileLoader](../../Lib/Shared.Collision/Tagfile/) | 972 lines, turns Havok `Hkp*` shapes (box, sphere, capsule, cylinder, extended mesh) into Bepu statics |
| `SimulationCache` | 392 lines, caches a built simulation so the cost is paid once |
| Settings | `MapsPath`, `LoadMapsCollision`, `AssetDBPath` all plumbed through `GameServerModule` |

None of it is speculative. `TagfileLoader.LoadRigidBody` is the same path that loads a deployable's
collision out of the assetdb today, which is what made N3 pass against the Battleframe Station's
collision id. Havok shapes reaching Bepu correctly is a solved, tested problem here.

**What is missing is the extractor.** Nothing in this repo produces `.pinzone.json`. The client ships
`system/maps/{zone}.zone` and a `.worldDir` listing `*_opt.worldMap` chunk files, and FauFau's
readers for both are header-only stubs: `Zone` returns magic, version, timestamp and name;
`WorldMap` returns magic, version and a list of positions that look like chunk origins. Neither
reaches the ENWF collision layers (`Cg`, `Cg2`, `Cg3`) the loader wants.

So the terrain job is one job: read the client's world chunks and emit the JSON the existing loader
already consumes. Everything downstream of that is done.

## Three tiers, and only one of them is worth starting now

**Tier 1, in-game placement. Built 2026-08-13.** Use the running client as the editor, because it is
already a correct 3D view of the map with a character standing on real terrain. Your own footing is
exact ground truth, which is the one measurement this server can trust.

[SpawnGroupServerCommand](../../UdpHosts/GameServer/Systems/Admin/Commands/SpawnGroupServerCommand.cs):

| Command | Does |
|---------|------|
| `spawngroup list` | every group in the zone, each monster's position and how far away it is |
| `spawngroup new <name>` | an empty group |
| `spawngroup add <monsterId> [groupId]` | places a monster where you are standing |
| `spawngroup drop <groupId> <index>` | removes one placement |
| `spawngroup remove <groupId>` | removes a group |
| `spawngroup delay <groupId> <seconds>` | sets the respawn delay |
| `spawngroup reload` | despawns and respawns everything from the file |

Every edit saves and respawns immediately, so the loop is walk, place, look, adjust, with no restart.
`add` refuses in two situations, and both are the same objection: you have to have *walked* here.

- **Airborne.** The point of placing from a player is that the position is known-good ground, and
  mid-air is known-good air.
- **Standing where you were put.** After a `tp` or a respawn, `CharacterEntity.PlacedPosition` holds
  the destination until you walk off it, and `add` refuses with *"You were put here rather than
  walking here, which is not proof of ground."*

The second one was learned the hard way on the first session that used this tool. **The client
reports grounded inside terrain exactly as it does on top of it** — [N16](../In-Game-Tests/NPC-Combat.md)
teleported to a coordinate nobody had stood on, landed about 8m under the basin floor, fought a
whole engagement from in there, and the destination went into the footing record looking like every
other measurement. Everything here rests on a player's footing being real, so the one thing that can
produce a fake one has to be refused rather than trusted. `MovementRelay` applies the same rule and
records no footing until you leave the spot you were placed on.

It writes to the deploy directory's copy of `spawn_group.json`. Copy it back into the repo when a
session produces something worth keeping:

```
cp ~/Games/PIN/GameServer/StaticDB/CustomData/spawn_group.json \
   ~/Github/PIN/UdpHosts/GameServer/StaticDB/CustomData/
```

This is also what killed anchor-and-radius. A group used to be a centre point, a radius and a count,
with members scattered over the disc; that only works if the ground is flat, and around zone 448's
valley the terrain moves 12m vertically inside 9m horizontally, so members landed in hillsides. Now
every monster carries the position someone stood on to place it.

Limits worth being honest about: one point at a time, no overview, and you have to physically walk
everywhere you want to place something. For the handful of spawn groups and resource nodes a
vertical slice needs, that is fine.

**Tier 2, finish the terrain pipeline. The real answer, and its own project.** Write the extractor.
The payoff is not mainly authoring: it is a downward raycast for NPC ground clamping instead of the
target-footing hack, real cover for every shot in the game, snap-to-ground for anything placed, and
the precondition for pathing. It would close [DATA-15](../gaps/data.md#data-15) outright rather than
managing it.

The unknown is entirely in the chunk format. The Havok end is done, the Bepu end is done, and the
question is how much work sits between a `.worldMap` file and the tagfile objects
`TagfileLoader.ProcessObject` already accepts.

**Tier 3, a standalone 3D editor. No.** It only makes sense after tier 2, and after tier 2 the case
for it is weak, because tier 1 plus real terrain covers the same ground with a fraction of the work
and uses a renderer that is already correct.

## Recommendation

Tier 1 is done. Tier 2 as a scheduled stream once the slice closes, on the strength of what it fixes
rather than what it authors. Tier 3 not at all.

The thing to avoid is building an editor on top of no terrain, which is the version of this that
looks like progress and leaves the actual defect in place.
