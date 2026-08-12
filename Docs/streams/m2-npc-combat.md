---
project: pin
kind: stream
title: "M2: NPCs That Fight Back"
relates:
  - ../PROGRESS.md
---

# M2: NPCs That Fight Back

[AIEngine.cs](../../UdpHosts/GameServer/AIEngine.cs) is an empty `Tick`. It's called first in the
shard loop and does nothing, which makes this the single largest gap between the server and a
game.

More of the pieces exist than expected. NPCs and players are the same `CharacterEntity`, so NPCs
already load a chassis and both weapon ids from `dbmonster`, already take damage through the same
funnel, and already replicate movement through `RefreshMovementView` whenever something moves
them. Nothing moves them, decides what they want, or pulls a trigger.

Split it three ways, in this order:

**Perception and target selection.** Pick a target from what's in scope.
`HostilityRules.GetStance` already answers who's a valid enemy and `PhysicsEngine.TargetRayCast`
answers line of sight, so this is a scoring loop plus a threat table, not new machinery.

**Locomotion.** The hard part, because movement is client-authoritative and an NPC has no client.
`SetPosition` replicates correctly, so the wire side is done; what's missing is anything that
decides where. There's no navmesh, and world collision only loads when `LoadMapsCollision` is on.
The first pass should be leashed direct steering with a downward raycast to clamp to ground, and
should accept that NPCs will walk into walls. Pathing is its own project and shouldn't block a
monster shooting back.

**Attacking.** Two paths already work server-side, and the choice between them matters. Abilities
activate from the server today, which is how `Thumper` drives its own state machine. Weapon fire
doesn't: `WeaponSim.OnFireWeaponProjectile` is driven by a client fire message and `PRNG.Spread`
seeds off the client's time, so an NPC needs either a synthesised seed or a direct call into
`ProjectileSim.FireProjectile`. Prefer abilities where `dbmonster` names one, since that's what the
data was built for.

Then wire death to something. `CharacterEntity.Die` broadcasts `KilledEvent` and schedules an NPC
despawn 30 seconds later, and that's the whole of it. Nothing else in the server learns that
anything died, which is what M5 and M7 both need.

| Work | Where |
|------|-------|
| Threat table, target selection, line of sight | new, under `Systems/AI`, called from `AIEngine.Tick` |
| Leashed steering and ground clamping | same, plus `CharacterEntity.SetPosition` |
| Server-side attack entry point | [WeaponSim](../../UdpHosts/GameServer/Systems/WeaponSim), [ProjectileSim](../../UdpHosts/GameServer/Systems/ProjectileSim), `AbilitySystem.HandleActivateAbility` |
| Death notification other systems can subscribe to | `CharacterEntity.Die`, [EventBus](../../UdpHosts/GameServer/Systems/SystemEvents) |
| Spawn groups worth fighting, replacing the hardcoded debug row of monsters | [EntityManager.cs](../../UdpHosts/GameServer/Systems/EntityManager/EntityManager.cs) around line 360 |

Exit: spawn a hostile monster with `npc`, walk into its range, and it turns, closes, shoots, and
kills you. Kill it instead and it dies, drops nothing yet, and despawns.

This is the biggest milestone on the list and the one most likely to split further once started.
Target selection and attacking are testable offline as pure functions; nothing about how the client
renders NPC movement is, so expect the locomotion pass to need several trips to the game machine.

H5 in [Hostility](../In-Game-Tests/Hostility.md) is permanently blocked until this lands — it needs
an NPC that actually decides to shoot.
