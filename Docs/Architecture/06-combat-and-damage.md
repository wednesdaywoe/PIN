# Layer 6: Combat & Damage

This documents both how the loop works today and where it's known to be incomplete.

## The weapon-fire path

```
client: FireWeaponProjectile (aim direction, client time)
  └─ Controllers/Character/CombatController.FireWeaponProjectile
      ├─ NetworkPlayer.HandleFireWeaponProjectile → WeaponSim.OnFireWeaponProjectile
      └─ echoes WeaponProjectileFired back so the client sees its own tracer

WeaponSim.OnFireWeaponProjectile                Systems/WeaponSim/WeaponSim.cs
  ├─ CharacterEntity.GetActiveWeaponDetails()   resolves weapon template + attributes from the loadout
  ├─ SDBInterface.GetAmmo(weapon.AmmoId)
  ├─ CharacterEntity.GetProjectileOrigin(aim)   muzzle offset (1.62 standing / 1.08 crouched)
  ├─ GetCurrentSpreadPct(...)                   base + burst ramp + movement bonus
  └─ per round: PRNG.Spread(...) → ProjectileSim.FireProjectile(...)

ProjectileSim.FireProjectile                    Systems/ProjectileSim/ProjectileSim.cs
  ├─ PhysicsEngine.ProjectileRayCast(origin, direction, source, trace)   hitscan, 500m
  ├─ resolve hit body → entity; bail unless it's a different CharacterEntity
  ├─ HostilityRules.CanDamage(shooter, target)
  ├─ damage = DamageFalloff.DamageAt(distance) * hit.DamageMod (* HeadshotMult)
  └─ target.TakeDamage(amount, attacker, damageType, flags)
```

Projectiles are hitscan. There's no travel time, gravity, or projectile entity, even though `Ammo`
carries `ProjectileSpeed`, `Gravity`, bounce and homing parameters. `ProjectileSim` has a
commented-out `Tick` waiting on a real simulation.

### Spread

`WeaponSim` reproduces the client's spread model so server-side hit resolution matches what the
player sees. `PRNG.Spread` ([Systems/PRNG](../../UdpHosts/GameServer/Systems/PRNG)) is a
reimplementation of the client's deterministic spread RNG, seeded by `(time, weapon slot, round
index)`. That's why the client's fire time is passed all the way down instead of using server time.

State per entity in `WeaponSim.WeaponSimState`: accumulated spread time, last spread direction and
time, movement spread bonus. `Tick` (50ms) decays accumulated spread after `MsSpreadReturnDelay`
and ramps the movement bonus down.

Both `GetCurrentSpreadPct` and `ProcessWeaponSpread` carry the comment *"Consider this whole thing
a sham, needs further RE."* Treat the formulas as approximations, not ground truth.

Live debugging: `Preferences.DebugWeapon` makes `DebugWeaponSpread` push a JSON blob
(`WeaponSim.Spread`) to the client each tick with the resolved weapon, spread, and flags.

### Range decay

[DamageFalloff.cs](../../UdpHosts/GameServer/Systems/ProjectileSim/DamageFalloff.cs) resolves a
weapon and its ammo into one curve: full damage out to `DamageDecayRangefrac * weapon.Range`, a
linear fall to a floor at `weapon.Range`, flat beyond that. The floor is the higher of
`ammo.MinDamageFrac` and `weapon.MinDamage / weapon.DamagePerRound`. Both are read as fractions of a
full round so a weapon buff carries the floor up with it rather than decaying a buffed weapon down
to its unbuffed minimum.

Decay applies to the weapon's damage per round; `hit.DamageMod` and `HeadshotMult` scale what's
left. Ability projectiles skip it entirely, since their damage comes from the command and there's no
weapon template to take a `Range` from.

Like the faction stance encoding, the curve is a guess. The fields exist but the way the client
combines them hasn't been confirmed. All of the guess lives in `DamageFalloff.Resolve`, which
returns a disabled curve whenever the numbers don't describe a sensible falloff (no range, decay
starting at or past max range, a floor at or above full damage, `DamageDecay` unset). Being wrong
therefore leaves damage as it was before decay existed rather than quietly weakening every weapon.
`/dbg_weapon` prints the inputs, the resolved curve and samples along it, so it can be checked
against real client damage numbers without firing a shot.

## Applying damage

`CharacterEntity.TakeDamage(amount, attacker, damageType, damageFlags)`
([CharacterEntity.cs](../../UdpHosts/GameServer/Entities/Character/CharacterEntity.cs)):

1. Ignore if `amount <= 0` or the target isn't alive.
2. `SetCurrentHealth(CurrentHealth - amount)`.
3. Build a `DamageHitStruct` and send `DealtHitEvent` to the attacker (if player-controlled) and
   `TookHitEvent` to the target (if player-controlled).
4. If health hits zero, `Die(killer)`.

`Die` sets `Alive = false`, sets character state `Dead`, broadcasts `KilledEvent` via
`EntityMan.SendToScoped`, and for NPCs only schedules despawn after 30s via
`SetRemainingLifetime`. Player respawn runs through `NetworkPlayer.Respawn()`, which repositions at
the nearest uncaptured outpost, sends `ForcedMovement` + `Respawned`, and rewrites the base
controller state in two passes.

`TakeDamage` is the single funnel. Anything that wants to hurt something should call it rather than
touching health, so hit events, death and respawn all stay consistent.

## Ability damage

Abilities reach the same funnel through aptitude commands in
[Systems/Aptitude/Commands/Damage](../../UdpHosts/GameServer/Systems/Aptitude/Commands/Damage):

| Command | What it does |
|---------|--------------|
| `InflictDamageCommand` | Direct + splash damage to `context.Targets` |
| `FireProjectileCommand` | Fires an ability hitscan through `ProjectileSim.FireAbilityProjectile` |
| `SetWeaponDamageCommand` | Overrides or multiplies the initiator's weapon damage for the effect's lifetime |
| `SetWeaponDamageTypeCommand` | Overrides the damage type |
| `EnergyToDamageCommand` | Converts an energy register to damage |

`SetWeaponDamage*` are active commands: they stash the prior value in an `ICommandActiveContext`,
register in `context.Actives`, and restore it in `OnRemove`. That's the pattern to copy for any
modifier that must be undone when its effect ends.

`InflictDamageCommand` details worth knowing:

- Damage, splash range, and their register ops all go through `AbilitySystem.RegistryOp`.
- `Weapondamage` / `Weapondamagetype` / `UseWeaponRadius` pull from the initiator's equipped
  weapon and its ammo.
- Splash centres on `Context.InitPosition` when `Frominitiatorpos` is set, otherwise the first
  target, otherwise `Self`. Falloff is linear from `Pointblankrange` out to the splash range.
- A `HashSet` guards against a target taking both direct and splash damage from one execution.

## Hostility

[Systems/Hostility/HostilityRules.cs](../../UdpHosts/GameServer/Systems/Hostility/HostilityRules.cs)
is the single place that decides who may hurt whom. Every damage path calls
`HostilityRules.CanDamage(attacker, target)`; nothing else should re-derive the rule.

```
HostilityRules.GetStance(source, other)
  ├─ same HostilityInfo team id                → Friendly
  ├─ same HostilityInfo faction id             → Friendly
  ├─ either side has no faction flag           → Neutral
  └─ SDBUtils.GetFactionStance(a, b)           → dbcharacter::FactionRelations
```

`CanDamage` protects friendlies only. Neutrals are fair game, matching the client's own
friendly/hostile/neither split in `PersonalFactionStanceData`. A null attacker is environmental
damage and always allowed.

Because every player character is faction 1, players still can't damage each other. That now falls
out of the faction rule instead of an explicit player-vs-player check.

The stance encoding is an assumption. `FactionRelations.HostilityStance` is a signed byte and
`SDBUtils.ToStance` reads it as a scale centred on neutral, negative hostile and positive friendly.
That hasn't been checked against a real `clientdb.sd2`. `BuildFactionStances` logs the distinct
stance and `Faction.DefaultStance` values it finds at startup, and if none are negative it disables
cross-faction hostility and warns, so a wrong guess degrades to same-faction protection only rather
than silently blocking damage that should land. Confirm against the log, fix `ToStance`, delete the
guard.

In game, `/hostility` (alias `/stance`) prints both sides' faction and team, the stance each way,
and whether damage is allowed.

## Current gaps

These are known-missing rather than accidental, and are the natural next pieces of work:

| Gap | Where |
|-----|-------|
| Faction stance encoding unconfirmed, and reputation-derived stance (`GetFactionReputations`) is unused | `SDBUtils.ToStance` |
| Range decay curve unconfirmed, and ability projectiles have no falloff at all | `DamageFalloff.Resolve`, `ProjectileSim.FireAbilityProjectile` |
| Shields are never consumed: `TakeDamage` only subtracts health, though `SetCurrentShields` exists and max shields is currently forced to 0 | `CharacterEntity.TakeDamage` |
| Damage type vs. `DamageResponse` resistance tables are loaded but unused in the damage calculation | `SDBInterface.GetDamageResponse*` |
| Only `CharacterEntity` can take damage; deployables, turrets and vehicles cannot | `InflictDamageCommand.ApplyDamage`, `ProjectileSim` |
| `Usedmgdealt` has nowhere to read from; dealt damage isn't recorded on the context | `InflictDamageCommand` |
| 13 `ModifyDamageBy*` commands are stubs in `Todo/` | `Commands/Damage/Todo/` |
| Deaths aren't wired into AI or encounters; NPC corpses just despawn on a timer | `CharacterEntity.Die` |
| `FireProjectile` and `FireAbilityProjectile` duplicate hit resolution and flag logic | `ProjectileSim` |
| The aptitude hostility commands are all stubs, so nothing can change an entity's faction at runtime | `Commands/Hostility/Todo/`, `Commands/Target/Todo/TargetHostilesCommand` and friends |

## Useful debug hooks

- `/target`, `/cleartarget`, `/spawncharacter`, `/effect`, `/listeffects`, `/removeeffect`,
  `/setcombatflags`, `/dbg_weapon`, `/hostility`, all in
  [Systems/Admin/Commands](../../UdpHosts/GameServer/Systems/Admin/Commands)
- `DebugProjectileHitCallbacks` streams projectile spawn/impact/timeout to the debug pipe
- Every ability log line carries an `ExecutionId`; filter on it to see one activation end to end
