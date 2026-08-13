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
  ├─ TryResolveHit(...)                         shared with FireAbilityProjectile
  │   ├─ PhysicsEngine.ProjectileRayCast(origin, direction, source, trace)   hitscan, 500m
  │   ├─ resolve hit body → entity; bail unless it's a different, living IDamageable
  │   └─ HostilityRules.CanDamage(shooter, target)
  ├─ damage = DamageFalloff.DamageAt(distance) * hit.DamageMod (* HeadshotMult)
  └─ target.TakeDamage(new DamageInfo { ... })
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
a sham, needs further RE."* Treat the formulas as approximations, not ground truth. `PRNG.Spread`
itself is on firmer ground: `SpreadTests.ReproducesACapturedClientShot` runs a shot captured from the
real client through it and matches the direction the client produced to within 1e-5, so the RNG is
right even where the spread percentage feeding it isn't.

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
left, and the single rounding to an integer happens after all of it — confirmed in game, since
rounding the body number before multiplying gives headshots that are a point or two off what the
client shows. Ability projectiles skip decay entirely, since their damage comes from the command and
there's no weapon template to take a `Range` from.

Confirmed in game on 2026-08-11 (test queue R1/R2). The BioTech Needler Shotgun resolves to full
damage out to 7m falling to 17.5 at 70m, which is `Range × DamageDecayRangefrac` and
`DamagePerRound × MinDamageFrac` exactly, and 80 logged shots land on the interpolation between
them. The reading that mattered was `DamageDecayRangefrac` as *where decay starts* — the other one
would have had a shotgun holding full damage for 63 of its 70 metres.

The client never gets a chance to disagree: the floating damage number is `DealtHit.DamageValue`,
which is the amount the server already applied. What that leaves unverified is not the anchors but
the shape between them — `Resolve` interpolates linearly and the live server may not have. See R4
for why that isn't queued.

`DamageFalloff.Resolve` returns a disabled curve whenever the numbers don't describe a sensible falloff (no range, decay
starting at or past max range, a floor at or above full damage, `DamageDecay` unset), so a weapon
it can't read keeps the damage it had before decay existed rather than being quietly weakened.
`dbg_weapon` prints the inputs, the resolved curve and samples along it; with `--loglevel debug`
every shot past the full damage range also logs the curve it was evaluated against.

`DamageFalloffTests` covers the shape and every path into a disabled curve, so what's left for the
game is only whether this is the right shape.

## Applying damage

Anything that wants to hurt something builds a
[DamageInfo](../../UdpHosts/GameServer/Systems/Combat/DamageInfo.cs) and hands it to an
[IDamageable](../../UdpHosts/GameServer/Systems/Combat/IDamageable.cs). Callers cover what the
attacker's side knows about, meaning range decay, hit location and splash falloff, and stop there.
Mitigation lives in `TakeDamage` and nowhere else, which is what stops shields and the resistance
tables from having to be written once per call site.

`DamageInfo` carries the pre-mitigation amount as a float, the attacker, the damage type and the
response flags. `DamageInfo.Points` is that amount as whole points; every implementation rounds
through it so a hit worth 0.4 is ignored by all of them or by none.

`IDamageable` is what makes an entity type shootable. Three types implement it:

| Type | Vitals | Death |
|------|--------|-------|
| `CharacterEntity` | Health and shields, `Alive` plus a `CharacterState` the client animates | `Die`, then respawn or a 30s corpse |
| `DeployableEntity` | Health from `Deployable.StartHitpoints`, replicated as a percentage | `Destroy`: runs `DeathAbilityid`, takes its turret with it, removed after 2s |
| `VehicleEntity` | Health from `VehicleInfo.MaxHitPoints`, replicated as points | `Destroy`: ejects occupants, runs `DeathAbility`, removed after 2s |

Anything else in a raycast's way is scenery as far as damage is concerned, and a shot that lands on
it is a miss. Turrets are the notable absence: `Turret_ObserverView` carries no health field at all,
so the client doesn't model them as damageable either, and they have no collision body to be shot in
the first place. A turret dies with the deployable or vehicle it's bolted to.

The shared parts are small on purpose. `DamageEvents` builds the `DamageHitStruct` and sends the
attacker its damage number, which is the same for every target type; `Vitals.HealthPercent` is the
percentage conversion the health bars want. Everything else, meaning what a health pool is and what
dying means, belongs to the implementation.

Both destruction paths run the death ability the SDB names rather than inventing an effect. Whatever
Firefall wanted a wrecked deployable to do lives in that ability.

`CharacterEntity.TakeDamage(DamageInfo)`
([CharacterEntity.cs](../../UdpHosts/GameServer/Entities/Character/CharacterEntity.cs)) is the most
involved of the three:

1. Ignore if the target isn't alive or the amount rounds away to nothing.
2. Shields absorb up to whatever is left of them and the rest carries into health.
3. Restart the shield recharge delay, whether the shield took any of it or not.
4. Build a `DamageHitStruct` and send `DealtHitEvent` to the attacker (if player-controlled) and
   `TookHitEvent` to the target (if player-controlled). Both report the whole hit rather than the
   part that reached health.
5. If health hits zero, `Die(killer)`.

`Die` sets `Alive = false`, sets character state `Dead`, broadcasts `KilledEvent` via
`EntityMan.SendToScoped`, and for NPCs only schedules despawn after 30s via
`SetRemainingLifetime`. Player respawn runs through `NetworkPlayer.Respawn()`, which repositions at
the nearest uncaptured outpost, sends `ForcedMovement` + `Respawned`, and rewrites the base
controller state in two passes.

`TakeDamage` is the single funnel. Anything that wants to hurt something should call it rather than
touching health, so hit events, death and respawn all stay consistent. Nothing outside an entity's own
`TakeDamage` should ever look at what kind of thing it is damaging; both damage paths resolve their
target to an `IDamageable` and stop there.

### Shields

[ShieldSim](../../UdpHosts/GameServer/Systems/Combat/ShieldSim.cs) refills shields on a 100ms tick,
once `ShieldRechargeDelayMs` has gone by without a hit landing. At a low rate a tick is worth a
fraction of a point, so `CharacterEntity.RechargeShields` carries the remainder between ticks instead
of truncating it away and never regenerating anything. That carry is
[ShieldRecharge](../../UdpHosts/GameServer/Systems/Combat/ShieldRecharge.cs), covered by
`ShieldRechargeTests`.

Max shields and both recharge numbers are placeholders in `HardcodedCharacterData`, next to the
hardcoded max health. `dbitems::Battleframe` carries `base_shields` and the
recharge pair per frame, but whether those columns shipped with live values hasn't been confirmed;
[MinimalSDB](../../Tools/MinimalSDB) in `dump` mode answers that before any of it gets tuned. Monsters
are given zero shields, so the guess only lands on players and NPC damage behaves exactly as it did.

`MaxShields` and `CurrentShields` only exist on `BaseController`, which means a player sees their own
shield bar and nobody else's. Health has a percentage on `ObserverView`; shields have no equivalent.

### Invulnerability

`CharacterEntity.Invulnerable` drops every incoming hit at the top of `TakeDamage`. Nothing in the
data sets it and nothing clears it: it exists for the `invuln` admin command, so a hazard can be
walked into and back out of. Retail had the notion for real — `RequireDamageResponseCommandDef`
carries a `NotInvulnerable` flag — but PIN doesn't model damage responses, so this sits in front of
all of them rather than being one.

## Environmental damage

[HazardSim](../../UdpHosts/GameServer/Systems/Hazards/HazardSim.cs) is the only damage source that
isn't something pulling a trigger, and the only caller of `TakeDamage` that passes a null attacker.
It runs on a 500ms tick — retail's `update_frequency` for the effects it is standing in for — and
looks at player-controlled characters only.

| Hazard | Reading | Rate |
|--------|---------|------|
| Drowning | client's `WaterLevelAndDesc` past `dbvisualrecords::WaterDesc.drowning_percent` | 1% of max health a tick, flat (effect 787) |
| Fully submerged | same, past `dying_percent` | 2% a tick, multiplied by 1.05 after each one (effect 789) |
| Melding | position against the perimeter the client is drawing | 5% a tick, invented ([DATA-12](../gaps/data.md#data-12)) |

The water level is the client's reading, not the server's — with `LoadMapsCollision` off there is no
terrain and no water volume here to measure against, so the one byte the client sends on every
movement pose is the whole of it. [Submersion](../../UdpHosts/GameServer/Systems/Hazards/Submersion.cs)
unpacks it; [DATA-13](../gaps/data.md#data-13) covers the half of that byte that can't be resolved.

The melding reads the same control points the client draws the wall from, tessellated as a Hermite
curve by [MeldingField](../../UdpHosts/GameServer/Systems/Hazards/MeldingField.cs), so the two agree
even while a `MeldingRepulsor` is pushing a control point around. Melded ground is taken to be to the
left of the directed curve, which is inferred rather than read — [E3](../In-Game-Tests/Environment.md)
is what confirms it.

Applying retail's status effects instead of the damage directly would be the faithful version. It
isn't what happens, because those chains run through registers, conditional branches and stat
modifiers whose coverage in PIN's aptitude engine is unknown, and a half-executing chain is
indistinguishable from no damage at all.

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
  target, otherwise `Self`. Falloff is linear from `Pointblankrange` out to the splash range and
  lives in `SplashFalloff`, which is another guess and covered by `SplashFalloffTests`.
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

`FactionRelations.HostilityStance` is a signed byte and `SDBUtils.ToStance` reads it as a scale
centred on neutral, negative hostile and positive friendly. That was an assumption until 2026-08-11,
when a faction 1 versus faction 2 pair resolved to Hostile in game (test queue H1/H2) — which can
only happen if a negative read through. The fallback that forced every cross-faction pair to Neutral
while the encoding was in doubt is gone. `BuildFactionStances` still logs the distinct stance and
`Faction.DefaultStance` values it finds, and still warns when none are negative; that warning now
means the stances that follow are suspect rather than suppressed, since it can only fire on a db
that isn't the one the mapping was confirmed against.

In game, `hostility` (alias `stance`) prints both sides' faction and team, the stance each way,
and whether damage is allowed.

`HostilityRulesTests` covers every path that answers before the faction table is consulted, which
includes same-team, same-faction and the missing-flag fallbacks. Anything involving two different
factions goes through the table and so needs a real clientdb.

## Current gaps

These are known-missing rather than accidental, and are the natural next pieces of work:

| Gap | Where |
|-----|-------|
| Reputation-derived stance (`GetFactionReputations`) is unused. The stance encoding itself is confirmed | `SDBUtils.GetFactionStance` |
| Ability projectiles have no falloff at all. The weapon curve itself is confirmed against the server, but not yet against the client's own expectation | `DamageFalloff.Resolve`, `ProjectileSim.FireAbilityProjectile` |
| Shield pool is a deliberate divergence: retail shipped `Battleframe.BaseShields` as 0 on 1671 of 1676 frames, so PIN keeping one is a choice. The recharge pair is the shipped 150/sec and 10000ms | `HardcodedCharacterData`, `CharacterEntity.InitFields` |
| Damage type vs. `DamageResponse` resistance tables are loaded but unused in the damage calculation | `SDBInterface.GetDamageResponse*` |
| Deployable and vehicle health numbers are a first read of the SDB (`StartHitpoints`, `MaxHitPoints`) and neither destruction path has been seen in game | `DeployableEntity.Destroy`, `VehicleEntity.Destroy` |
| Turrets have neither health nor a collision body, so they can't be shot; they die with their parent | `TurretEntity` |
| `Usedmgdealt` has nowhere to read from; dealt damage isn't recorded on the context | `InflictDamageCommand` |
| 13 `ModifyDamageBy*` commands are stubs in `Todo/` | `Commands/Damage/Todo/` |
| Deaths aren't wired into AI or encounters; NPC corpses just despawn on a timer | `CharacterEntity.Die` |
| The aptitude hostility commands are all stubs, so nothing can change an entity's faction at runtime | `Commands/Hostility/Todo/`, `Commands/Target/Todo/TargetHostilesCommand` and friends |

## Useful debug hooks

- `target`, `cleartarget`, `npc`, `applyeffect`, `listeffects`, `removeeffect`, `cflags`,
  `dbg_weapon`, `hostility`, all in
  [Systems/Admin/Commands](../../UdpHosts/GameServer/Systems/Admin/Commands), and typed into the
  Admin chat channel without a leading slash. [In-Game Tests](../In-Game-Tests/Session-Setup.md)
  lists the monster type ids worth spawning and the faction each one carries.
- `DebugProjectileHitCallbacks` streams projectile spawn/impact/timeout to the debug pipe
- Every ability log line carries an `ExecutionId`; filter on it to see one activation end to end
