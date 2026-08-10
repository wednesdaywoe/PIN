# Layer 4: Entities & Replication

## The entity hierarchy

```
IEntity                     Entities/IEntity.cs        id, shard, position, orientation, hostility, scope/interaction queries
 └─ BaseEntity              Entities/BaseEntity.cs     + optional components
     └─ BaseAptitudeEntity  Entities/BaseAptitudeEntity.cs   + status effect slots (IAptitudeTarget)
         ├─ CharacterEntity       players and NPCs alike
         ├─ VehicleEntity
         ├─ DeployableEntity
         ├─ TurretEntity
         ├─ OutpostEntity
         ├─ ThumperEntity / CarryableEntity / MeldingEntity / AreaVisualDataEntity
```

`CharacterEntity` ([Entities/Character/CharacterEntity.cs](../../UdpHosts/GameServer/Entities/Character/CharacterEntity.cs),
~1900 lines) is the big one. `IsPlayerControlled` distinguishes a player from an NPC; both are the
same class, so anything you add for players applies to NPCs unless you guard it.

### Components

Optional, plain-property composition on `BaseEntity`. Not ECS, just nullable feature bags:

| Component | Effect when non-null |
|-----------|----------------------|
| `Collision` | Entity gets a physics body |
| `Interaction` | Entity is interactable; supplies type and duration |
| `Scoping` | Overrides scope range, or marks the entity global scope |
| `Encounter` | Ties the entity to an encounter |

Defaults live in the `BaseEntity` accessors (`GetScopeRange()` returns `100f` when `Scoping` is
null).

## Views and Controllers

Every replicated entity exposes Aero-generated state objects. For `CharacterEntity`:

- **Views** (to everyone who can see it): `Character_ObserverView`, `Character_EquipmentView`,
  `Character_CombatView`, `Character_MovementView`, `Character_TinyObjectView`
- **Controllers** (to the owning player only, and the receive side of client commands):
  `Character_BaseController`, `Character_CombatController`,
  `Character_MissionAndMarkerController`, `Character_LocalEffectsController`,
  `Character_SpectatorController`

The same field often exists in both a view and a controller and both must be written, which is why
the entity exposes setter methods rather than letting callers poke properties:

```csharp
public void SetCombatFlags(CombatFlagsData value)
{
    Character_CombatController.CombatFlagsProp = value;
    Character_CombatView.CombatFlagsProp = value;
}
```

Follow that convention. Setting one side only produces the classic symptom of "the owner sees it
but nobody else does", or the reverse.

## How state reaches the client

1. You assign an Aero `*Prop`. The generated code marks the field dirty. Nothing is sent yet.
2. `EntityManager.FlushChanges(entity)` walks the entity's views/controllers and calls
   `Channel.SendChanges`, which serialises only dirty fields.
3. `EntityManager.Tick` flushes every entity periodically. Code that needs immediate delivery
   calls `FlushChanges` itself. `BaseAptitudeEntity.AddEffect`/`ClearEffect` do this so effect
   changes are never coalesced.

Full state is sent instead of deltas in three cases: scope-in, a client `KeyframeRequest`, and
respawn-style resets that deliberately rewrite everything.

## Scoping

`EntityManager` ([Systems/EntityManager/EntityManager.cs](../../UdpHosts/GameServer/Systems/EntityManager/EntityManager.cs))
decides which entities each player knows about.

- On a timer, for every entity × every player: should it be scoped?
  - always, if it's the player's own character or what the character is attached to
  - always, if `IsGlobalScope()`
  - otherwise `Vector3.Distance(entity, player) <= entity.GetScopeRange()`
- Transitions queue a `ScopeInRequest` (drained at a limited rate, one per interval, to avoid
  bursting a client) or call `ScopeOut` immediately.
- `ScopeIn` sends view keyframes for the entity, plus controller keyframes and a batch of
  one-time messages (`CharacterLoaded`, `EliteLevels_InitAllFrames`, `ProgressionXPRefresh`, …)
  when the entity is the receiving player's own character.

`_scopedPlayersByEntity` is the authority on who currently sees what; `SendToScoped(entity, msg)`
is how a system broadcasts an event to exactly the right audience.

## Identity

- **Entity guid**: 64-bit, built by [GUIDService.cs](../../UdpHosts/GameServer/GUIDService.cs) from
  server id, time, a counter, and a type code in the low byte matching `Enums.GSS.Controllers`.
  Lookups mask it off: `entityId & 0xffffffffffffff00`.
- **Entity ref id**: 16-bit handle from `Shard.AssignNewRefId`, stored in `Shard.EntityRefMap`
  alongside the controller id, used where the protocol can't carry a full guid.
- **`AeroEntityId`**: the wire form (`EntityId { Backing, ControllerId }`).

## Adding an entity type

1. Subclass `BaseAptitudeEntity` (or `BaseEntity` if it can never hold status effects).
2. Add the Aero views/controllers it replicates and implement `SetStatusEffect` /
   `ClearStatusEffect` if aptitude-capable.
3. Add spawn logic in `EntityManager` (see `SpawnDeployable`, `SpawnOutpost` for the shape),
   attach components, then `Add(entity)`.
4. Handle it in `ScopeIn` / `ScopeOut`. These are explicit per-type `if (entity is …)` chains, so
   a new type that isn't added there will be invisible to clients.
5. If it should collide or be shootable, create a physics body; see
   [layer 7](07-physics-and-world.md).
