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

## [ ] V1: Deployables have health at all (blocks V2-V4)

Verifies that `StartHitpoints` is the right column and that a deployable can be hit.

1. `deployable 356` — 10000 health, faction 2, so you're allowed to shoot it
2. Shoot it
3. ```
   grep -aE "has no StartHitpoints|Deployable .* took" ~/Games/PIN/logs/GameServer.log | tail -20
   ```

- Pass: no `Deployable 356 has no StartHitpoints` line, and shooting prints
  `Deployable {Type} took {Amount} damage`.
- Fail: the warning appears for 356 too. `StartHitpoints` isn't the health column; check the record
  in [MinimalSDB](../../Tools/MinimalSDB) dump mode against `StandardHealth` and the scaling table
  before changing `SpawnDeployable`.

The warning appearing for `deployable 395` is correct behaviour, not a failure — it really does have
`StartHitpoints = 0`.

## [ ] V2: A deployable's health bar moves

1. `deployable 356` — 10000 health, so the bar has somewhere to travel
2. Shoot it and watch the client, not just the log

Pass: the bar drops. This is the first time anything has written `CurrentHealthPct` on a deployable,
so a bar that doesn't move means the client wants something else changed alongside it, or wants a
keyframe rather than a delta.

## [ ] V3: Destroying a deployable

1. `deployable 517` — 100 health and turret 17, so it dies in a burst and has a turret to lose
2. Shoot it until it dies
3. ```
   grep -a "Deployable .* took" ~/Games/PIN/logs/GameServer.log | tail -10
   ```
   `Destroy` itself logs nothing, so the last line reaching `0 of 100 health left` is the signal.

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

If you don't know which of your abilities splashes, find out rather than guessing: `npc 1196 0 0 0`
and `npc 1196 2 0 0`, fire each ability at the gap in turn, and grep for an `ExecutionId` that
produced two `damage from` lines. One execution damaging two entities is the splash.
