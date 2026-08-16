---
project: pin
kind: test-stream
title: "Damage to Deployables and Vehicles (V1-V7)"
relates:
  - ../TEST-REGISTER.md
---

# Damage to Deployables and Vehicles

Part of the [in-game test queue](README.md). Setup, admin commands and the deployable/vehicle type
id table: [Session Setup](Session-Setup.md).

Added in the current working tree:
[IDamageable](../../UdpHosts/GameServer/Systems/Combat/IDamageable.cs),
implemented by `CharacterEntity`, `DeployableEntity` and `VehicleEntity`. See
[layer 6](../Architecture/06-combat-and-damage.md).

Everything about the funnel is covered offline. What isn't, and what these are for, is whether the
health numbers read off the SDB are the right columns and whether the client accepts health updates
on entity types that have never sent one.

## Pick a type that can actually be hit

**Read this before V1–V4.** Health and a hitbox are two different columns, and most deployables have
one without the other. `start_hitpoints` decides whether damage is allowed to land;
`collision_id` decides whether a bullet ever arrives to land it. A row with health and no collision
looks fine in the log and is untouchable in game — shots pass straight through and nothing is ever
written about it.

The 2026-08-15 run lost the whole block to this. Type **517**, named by V3 below, ships with
`collision_id = 0`. It was never destructible and never could have been. The server says so at spawn:

```
[23:03:35 WRN] Deployable 517 info has no collision id, what do?
```

That warning goes to the log, not to the person who typed the command, so it reads in game as an
invulnerable deployable. It is not a bug in the damage code.

This is not a rare row. Of 3,902 deployable types, **1,017 have health and no collision at all**,
and of the 274 that are faction 2 — the ones you are allowed to shoot — only **101** have both
health and a hitbox whose pose file is actually on disk.

**Use type 1229 for V1 through V4.** 2,400 health, scale 1.35 so it is large enough to hit, faction
2, collision 189604 with a real pose file, turret 23, and death ability 35277 — the same death
ability 517 had, so it is the same kind of thing, only hittable. At 39 damage a round it takes 62
hits, which fits inside one Heavy Machine Gun clip and still leaves the health bar somewhere to
travel.

Type **356** is also sound on paper — 10,000 health, collision 189632, faction 2 — but 10,000 health
is 257 rounds, more than a clip, and the 2026-08-15 run never got it to spawn at all. Its pose file
`00189632.pose` was never requested in either log, so the command never reached the server. If you
use it, watch the client for the command's own reply.

## [ ] V1: Deployables have health at all (blocks V2-V4)

Verifies that `StartHitpoints` is the right column and that a deployable can be hit.

1. `deployable 1229` — 2400 health, faction 2, and a hitbox that loads
2. Shoot it
3. ```
   grep -aE "has no StartHitpoints|has no collision id|Deployable .* took" ~/Games/PIN/logs/GameServer.log | tail -20
   ```

- Pass: no `Deployable 356 has no StartHitpoints` line, and shooting prints
  `Deployable {Type} took {Amount} damage`.
- Fail: the warning appears for 356 too. `StartHitpoints` isn't the health column; check the record
  in [MinimalSDB](../../Tools/MinimalSDB) dump mode against `StandardHealth` and the scaling table
  before changing `SpawnDeployable`.

The warning appearing for `deployable 395` is correct behaviour, not a failure — it really does have
`StartHitpoints = 0`.

## [ ] V2: A deployable's health bar moves

1. `deployable 1229` — 2400 health, so the bar has somewhere to travel and one clip empties it
2. Shoot it and watch the client, not just the log

Pass: the bar drops. This is the first time anything has written `CurrentHealthPct` on a deployable,
so a bar that doesn't move means the client wants something else changed alongside it, or wants a
keyframe rather than a delta.

## [ ] V3: Destroying a deployable

**Type changed 2026-08-15.** This used to say `deployable 517`, which has no hitbox and therefore
cannot be shot at all — see [Pick a type that can actually be hit](#pick-a-type-that-can-actually-be-hit).
1229 carries the same turret family and the same death ability 35277, so it tests the same thing.

1. `deployable 1229` — 2400 health and turret 23, so it dies inside one clip and has a turret to lose
2. Shoot it until it dies
3. ```
   grep -a "Deployable .* took" ~/Games/PIN/logs/GameServer.log | tail -10
   ```
   `Destroy` itself logs nothing, so the last line reaching `0 of 2400 health left` is the signal.

Pass: the death ability fires, the deployable disappears about 2s later, its turret goes with it, and
shots stop registering against it as soon as it dies. Watch for a turret left floating, which means
the `Remove(Turret)` in `Destroy` didn't take.

## [ ] V4: A destroyed deployable doesn't leave an invisible wall

1. Do V3, then wait for the model to disappear
2. `npc 1196 <coords past where it stood>` so there's something to hit behind it
3. Shoot through where it was

Pass: shots hit whatever is behind it. This is what the `Physics.RemoveEntity` call in
`EntityManager.OnRemovedEntity` is for, and the same check applies to an NPC corpse after its 30s
despawn, which had the same problem before this change.

## [ ] V5: Vehicle damage and wrecking

Needs a second person, or an occupied vehicle you can shoot from outside, to see the ejection.

1. `vehicle 116` — one of the two debug vehicles, spawned under NPC 2312's ownership
2. Have the second player get in
3. Shoot it until it's destroyed
4. `grep -a "Vehicle .* took" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: health drops on the client, occupants are ejected on destruction with camera and movement
control returned to them, the death ability runs, and the vehicle disappears. Ejection is the risky
part; a player left attached to a removed entity is the failure to watch for.

## [ ] V6: Friendly deployables are still safe

Verifies that `SpawnDeployable` hands out the right faction, since `HostilityRules` itself is
covered offline.

1. `deployable 348` — faction 1, the same as you
2. `target` with no argument while aiming at it, then `hostility`
3. Shoot it

Pass: `hostility` reports `Friendly` with `can damage: False`, and no `Deployable ... took` line
appears in the log.

Read `SpawnDeployable` before trusting a result here. It computes a `factionId` from owner faction,
then `overrideFactionId`, then the SDB default, and then assigns `deployableInfo.DefaultFaction`
directly instead of the value it just worked out. `useOwnerFaction` and `overrideFactionId`
therefore do nothing, and a record whose `DefaultFaction` is 0 gets faction 0 rather than the
intended fallback of 1. For `deployable 348`, whose default is already 1, the result happens to be
right — so this entry passing says nothing about the two paths that are broken. A player-placed
deployable is the case that would actually fail.

## [ ] V7: Splash reaches deployables and vehicles

1. `deployable 211 0 0 0` and `vehicle 116 3 0 0` — substitute your own coordinates, close enough
   that one splash covers both
2. Fire a splash ability into the gap between them
3. ```
   grep -aE "Deployable .* took|Vehicle .* took" ~/Games/PIN/logs/GameServer.log | tail -20
   ```

Pass: both take splash damage, and neither takes it twice from one execution.

**It has to be an ability, not a weapon.** No weapon splashes in PIN — the shot traces one ray and
damages exactly what it struck, and a shipped blast radius on the ammunition is never read
([DATA-21](../ISSUE-REGISTER.md)). The 2026-08-16 run found this by firing a Grenade Launcher
between two touching enemies and killing neither: the grenade hit the ground, and the ground has no
health. That result is real and recorded, but it is not V7 — V7 needs the ability path, which is
where splash is actually implemented.

If you don't know which of your abilities splashes, find out rather than guessing: `npc 1196 0 0 0`
and `npc 1196 2 0 0`, fire each ability at the gap in turn, and grep for an `ExecutionId` that
produced two `damage from` lines. One execution damaging two entities is the splash. A weapon will
never produce that line, however large its blast looks on screen.
