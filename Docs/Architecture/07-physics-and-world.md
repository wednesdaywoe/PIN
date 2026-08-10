# Layer 7: Physics & World

[Systems and loaders under UdpHosts/GameServer/Physics](../../UdpHosts/GameServer/Physics), built on
**BepuPhysics2** (vendored submodule at [Lib/BepuPhysics2](../../Lib/BepuPhysics2)).

## What the physics engine is actually for

Not character movement. Movement is client-authoritative and relayed
([Systems/MovementRelay](../../UdpHosts/GameServer/Systems/MovementRelay)); the server copies the
client's reported position into the entity and pushes the kinematic body to match. Physics exists
so the server can answer "what did this ray hit?", which covers hit registration, targeting and
line of sight.

Consequences worth knowing:

- Every entity body is kinematic (`BodyDescription.CreateKinematic`). Nothing is dynamically
  simulated; bodies are teleported by `UpdateEntity`.
- Body orientation is stored inverted (`Quaternion.Inverse(entity.Orientation)`) to match the
  client's convention.
- The simulation steps at a fixed 50ms via an accumulator in `PhysicsEngine.Tick`, independent of
  the shard loop rate.

## Body lifecycle

| Call | When |
|------|------|
| `CreateKineticEntity(CharacterEntity)` | On login, after the loadout is applied so the pose asset is known |
| `CreateKineticEntity(BaseEntity)` | For entities with a `Collision` component, using `HitboxCollisionId` |
| `UpdateEntity(entity)` | Whenever position/orientation changed and the body must follow |
| `RemoveEntity(entity)` | On despawn |

Three parallel maps keep the association: `_entityIdToBody`, `_bodyToEntityId`,
`_entityIdToAssetKey`. Shapes are cached per `AssetCompoundKey` (asset id + offset + scale) so
identical characters share one compound shape.

## Raycasts

```csharp
ProjectileHitResult? ProjectileRayCast(Vector3 origin, Vector3 direction, CharacterEntity source, uint trace)
(bool, Vector3, ulong) TargetRayCast(Vector3 origin, Vector3 direction, CharacterEntity source, float maxRange = 500f)
```

`RayHitHandler` skips the source's own body (`AvoidSourceBody`). Max range for projectiles is 500m
and speed is a fixed 500, used only for the debug visualisation since the trace itself is
instantaneous.

The valuable part of a hit is the child index: for a compound pose shape it identifies which body
part was struck. `ProjectileRayCast` looks it up in the pose data to produce
[ProjectileHitResult](../../UdpHosts/GameServer/Physics/ProjectileHitResult.cs):

- `Headshot`: from `poseShapeData.ShapeFlags.Headshot`
- `Crit`: from the SDB physics material's `IsCritHit`
- `DamageMod`: per-shape damage multiplier

A hit is only attributed to an entity when the collidable is kinematic and present in
`_bodyToEntityId`; world geometry hits return `null`. Note that a physics material id of 0
resolves to `null` with no fallback (flagged TODO in the source).

## Asset loading

| Loader | Reads | Purpose |
|--------|-------|---------|
| `ZoneLoader` | PIN Maps data (`MapsPath`) | World collision geometry, only when `LoadMapsCollision` is on |
| `SimulationCache` | `.bepu` cache next to the maps data | Serialised simulation; `LoadZone` tries the cache first and writes it after a fresh load, because parsing raw zone collision is slow |
| `TagfileLoader` | Client `assetdb` tagfiles | Collision/asset definitions |
| `PoseLoader` | `.pose` files | Per-bone hitbox compounds, hit flags, damage modifiers, materials |
| `IniLoader` | ini-style asset metadata | Supporting data |

`LoadZone` raises `OnZoneLoaded`, which is what lets `EntityManager` defer zone entity spawning
until collision is available.

Robustness workarounds already in place (recent commits): a tagfile that yields no shapes no
longer trips an assert, and a deployable with no collision specified no longer crashes.

## Debug pipe

[PhysicsEngine.Debug.cs](../../UdpHosts/GameServer/Physics/PhysicsEngine.Debug.cs) plus
[Lib/Shared.Debug/Proto](../../Lib/Shared.Debug/Proto) stream protobuf messages (body creation,
tick updates, projectile spawn/impact/timeout) to an external viewer. Enable via the
`isDebugPipeClient` constructor flag. `DebugProjectileHitCallbacks` is the projectile-specific
side, constructed by `Shard` and handed to `PhysicsEngine`.

## Movement relay

[MovementRelay.cs](../../UdpHosts/GameServer/Systems/MovementRelay/MovementRelay.cs) handles
`MovementInput` for characters and vehicles: it validates minimally, updates the entity's
position/velocity/aim state, and forwards to observers. Because it's client-authoritative, this is
the layer where any future anti-cheat or reconciliation would go. `CharacterEntity.Alive` gates
movement input acceptance so inputs are ignored until respawn completes.
