# In-Game Test Queue

A running log of checks for me to run with a real client, written down as work
lands so nothing accumulates unverified. Work through it at the game machine, record the result
inline, and delete entries once they've passed and the behaviour is covered elsewhere.

Status markers: `[ ]` not run, `[x]` passed, `[!]` failed (leave it in with what happened),
`[-]` skipped or not reproducible.

Adding an entry: state the task, the steps in order, and every command in full with its arguments
filled in — real type ids, coordinates, item ids, grep lines. These get run on the game machine
away from the source, so a step that says "spawn a monster" instead of `npc 1196` costs a trip back
here, and an under-specified step gets run wrong. If a check needs a specific world state (two
clients, a particular NPC), say so; that's usually what makes an entry sit unrun.

## Session setup

```
dotnet build PIN.sln                     # 0 errors expected
dotnet test PIN.sln                      # all green before leaving the desk
WebHostManager                           # ports 4400-4411 / 44300-44311
MatrixServer                             # UDP 25000
GameServer                               # UDP 25001
```

Deploying a new build, from the repo root, with the GameServer stopped:

```
OPENSSL_ENABLE_SHA1_SIGNATURES=1 dotnet build UdpHosts/GameServer/GameServer.csproj -c Release
cp UdpHosts/GameServer/bin/Release/net10.0/GameServer.{dll,pdb} ~/Games/PIN/GameServer/
cd ~/Games/PIN && ./use-build.sh          # reports which tagged build is live
```

`use-build.sh good` swaps back to the `c659335` build from before the Damage/Effect work and
`use-build.sh current` swaps forward again; both refuse to run while the server is up. Copying a
fresh `GameServer.dll` in by hand leaves it matching neither tag, so `use-build.sh` with no argument
prints `unknown` until the new build is also copied over `GameServer.dll.current`.

Remember to watch the GameServer log throughout. Two things are silent failures worth grepping for
after any session:

```
grep -a "HandlePacket Caught" ~/Games/PIN/logs/GameServer.log    # a handler threw and was swallowed
grep -a "Unrecognized MsgID"  ~/Games/PIN/logs/GameServer.log    # the client sent something we don't handle
```

### Admin commands

Admin commands are typed into the in-game Admin chat channel as a bare word with no leading slash:
the client keeps `/`-prefixed input for itself, so anything starting with `/` never reaches
`AdminService`. `help` prints the full list with each command's aliases and usage.

| Usage | Aliases | Notes |
|-------|---------|-------|
| `help` | `listcmd`, `listcmds`, `cmdlist`, `cmds` | prints every command with its aliases |
| `npc <characterTypeId> [<x> <y> <z>]` | `character`, `monster`, `spawn_npc`, `spawn_character`, `spawn_monster` | no coords = at your feet |
| `deployable <deployableTypeId> [<x> <y> <z>]` | `spawn_deployable` | |
| `vehicle <vehicleTypeId> [<x> <y> <z>]` | `spawn_vehicle` | |
| `carryable <carryableTypeId> [<x> <y> <z>]` | `spawn_carryable` | |
| `target [entityId/name]` | — | no argument ray-casts from your aim; `target me` or `target self` targets you |
| `clear` | `cleartarget`, `targetclear`, `untarget`, `removetarget`, `remtarget`, `deletetarget`, `deltarget` | clears the command target |
| `hostility` | `stance` | stance both ways against the current target |
| `dbg_weapon` | — | weapon template, ammo and the resolved decay curve |
| `ability <abilityId>` | `useability`, `activateability`, `apt_ability` | runs the chain server-side, so the client never predicts it |
| `applyeffect <effectId>` | `apply_effect`, `apt_apply` | |
| `removeeffect <effectId>` | `remove_effect`, `apt_remove`, `apt_clear`, `apt_cancel` | |
| `listeffects` | `list_effects`, `apt_status`, `apt_list` | |
| `cancelfm <commandId>` | — | cancel a ForcedMovement by command id |
| `createitem <typeId>` | `create_item`, `giveitem`, `give_item` | second argument is quantity, resources only |
| `tp <x> <y> <z>` | `teleport` | z is up |
| `pflags` | `pflag`, `float` | toggles `cheat_float`; this is how you get to a measured distance |
| `cflags [value]` | `cflag` | sets CombatFlags |
| `rment` | `killall` | removes every entity except player characters — use between tests |
| `emote <id>` | — | remote views only |
| `say <message>` | — | server-wide chat |
| `rarmy` | `reloadarmy` | reload Army UI |
| `dbgattach <unk2> <unk3>` | — | AttachedTo debugging |

### Monster type ids

The debug row in `EntityManager.TempSpawnTestEntities`, with the faction each id carries in
`dbcharacter::Monster` and how a player sees it. Players are faction 1, and the stance comes from
`dbcharacter::FactionRelations`; ids whose faction has no relation row with 1 fall back to faction
1's own default stance, which is 0, so they read Neutral. Neutral is still damageable in both
directions — `HostilityRules.CanDamage` only protects Friendly — so a Neutral monster is a poor
choice for a hostility check and a fine one for a damage check.

| Type id | Monster | Faction | Seen by a player |
|---------|---------|---------|------------------|
| 1196 | Chosen Fiend | 2, chosen | Hostile |
| 528 | Melded Aranha | 6, melding | Hostile |
| 2342 | Aranha | 7, gaea | Hostile |
| 1304 | Black Hills Bandit | 22, Black Hills Bandits | Hostile |
| 290 | Accord Assault | 1, accord | Friendly, same faction as the player |
| 356 | Aero | 1, accord | Friendly, same faction as the player |
| 2407 | Tanken Saboteur | 17, Tanken | Neutral, no relation row |
| 2312 | unnamed, owns the debug vehicles | 17, Tanken | Neutral, no relation row |

Any of the four Hostile ids work for a check that needs a hostile target. For a wider pick, the
factions hostile to players are 2 chosen, 5 monster, 6 melding, 7 gaea, 8 bandit, 22 Black Hills
Bandits, 42 Aranha, 45 Rebels, 46 Reapers and 47 Ophanim. `dbcharacter::Monster` has 3109 rows and
1731 of them are accord, so the hostile ones concentrate in gaea (431), chosen (270) and bandit
(249).

### Deployable and vehicle type ids

Read out of `dbcharacter::Deployable`, chosen for having health, a visual, a death ability and a
faction that makes them damageable or not. Faction 2 is chosen, so a player can shoot it; faction 1
is accord, so a player cannot.

| Command | What it is | Health | Turret | Faction |
|---------|-----------|--------|--------|---------|
| `deployable 517` | small, hostile, dies fast — the destruction check | 100 | 17 | 2 |
| `deployable 356` | hostile, survives a magazine — the health bar check | 10000 | 9 | 2 |
| `deployable 211` | hostile, no turret | 6000 | none | 2 |
| `deployable 348` | accord, must NOT be damageable | 1400 | 7 | 1 |
| `deployable 395` | Battleframe Station, the one already spawned in zone 448 | **0** | none | 1 |
| `vehicle 116` / `vehicle 201` | the two debug vehicles from `TempSpawnTestEntities` | — | — | owner 2312 |

`deployable 395` has `StartHitpoints = 0` in the retail SDB, so it is genuinely undamageable and
will legitimately print the `has no StartHitpoints` line. Don't test with it — it's the deployable
you'll trip over first, because it spawns itself in the Coral Forest. 3036 of the 3902 deployable
rows do have health.

---

## Hostility rules

Added in the current working tree: [HostilityRules](../UdpHosts/GameServer/Systems/Hostility/HostilityRules.cs),
`SDBUtils.GetFactionStance`, `hostility`. See [layer 6](Architecture/06-combat-and-damage.md).

### [ ] H1: Confirm the faction stance encoding (blocks H2-H6)

The highest-value check here; everything else in this section depends on it. Verifies that the
server's loader reads `hostility_stance` as the signed scale `SDBUtils.ToStance` assumes.

1. Start the GameServer against a real `clientdb.sd2`.
2. Fire one shot at anything, which is what first builds the stance table.
3. Back at the source machine:

```
grep -a -A3 "faction stance pairs" ~/Games/PIN/logs/GameServer.log
```

Record the `Stance values in use` and `Faction default stances in use` lists.

- Pass: the stance list contains negative values → `SDBUtils.ToStance` reads the column correctly
  as a signed scale, nothing to change.
- Fail: the follow-up warning `Faction stances hold no negative values` appears → the column is an
  ordinal enum. Cross-faction hostility is disabled until `ToStance` is rewritten for the values
  logged. Bring the two lists back and the mapping can be fixed in one method.

Expect a pass: reading `dbcharacter::FactionRelations` straight out of the retail `clientdb.sd2`
gives `hostility_stance` values across all 89 rows of -2, -1, 0, 1 and 2, and `dbcharacter::Faction`
carries `default_stance` of -1, 0 and 1. Both are signed scales, which is what `ToStance` assumes.
What the run still has to confirm is that the server's loader reads the same column the same way.

### [x] H2: `hostility` against a hostile NPC

Verifies that a cross-faction pair resolves to Hostile and is damageable.

1. `npc 1196` — spawns a Chosen Fiend, faction 2, at your feet
2. Aim at it, or `target` with no argument to ray-cast onto it
3. `hostility`

Pass: both faction ids print and differ, stance is `Hostile` (or `Neutral` if H1 failed) and
`can damage: True`.

### [x] H3: `hostility` against yourself

Verifies that the same-faction short circuit reports Friendly and blocks damage both ways.

1. `target me`
2. `hostility`
3. `clear` when done, so later checks don't inherit the target

Pass: same faction both sides, stance `Friendly`, `can damage: False` in both directions.

### [x] H4: Player damage to NPCs still lands

The regression H1 guards against.

1. `npc 1196`
2. Shoot it to death, landing at least one headshot
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: health drops, headshots read higher than body shots, the kill registers, and the corpse
despawns about 30s later. If shots register but health never moves, H1 failed and the stance mapping
is inverted.

### [-] H5: NPC damage to the player still lands

1. `npc 1196`
2. Stand in front of it and wait

Pass: player health drops and the hit shows client-side.

Not runnable yet, and won't be until M2. [AIEngine](../UdpHosts/GameServer/AIEngine.cs) is ticked
every frame from `Shard.Tick` with an empty body, so nothing picks a target or pulls a trigger on an
NPC's behalf. A spawned monster stands still and soaks fire. Confirmed 2026-08-10. Re-run this the
moment an NPC can attack, since it's the only check that exercises `CanDamage` with a monster as the
attacker rather than the target.

### [x] H6: Same-faction NPCs no longer hurt each other

Verifies that splash respects faction, which is what changed.

1. `npc 1196 0 0 0`, `npc 1196 2 0 0`, `npc 290 4 0 0` — two chosen and one accord, spawned two
   metres apart. Substitute your own coordinates; any three points in a line work.
2. Fire a splash ability into the middle of the group
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: the two faction-2 NPCs take splash and the faction-1 one doesn't. Previously every character in
radius took damage.

### [ ] H7: Player versus player is still blocked

Needs two clients.

1. Both players log in
2. `target <the other player's entity id>` then `hostility`, to see the pair before shooting
3. Shoot the other player

Pass: no damage, and `hostility` prints faction 1 on both sides with `can damage: False`. This used
to come from an `IsPlayerControlled` check and now comes from both characters being faction 1, so
it's worth confirming the replacement actually holds.

---

## Range based damage decay

Added in the current working tree: [DamageFalloff](../UdpHosts/GameServer/Systems/ProjectileSim/DamageFalloff.cs),
wired into `ProjectileSim.FireProjectile`, printed by `dbg_weapon`. See
[layer 6](Architecture/06-combat-and-damage.md).

The curve maths is already covered offline by `DamageFalloffTests` in
[Tests/GameServer.Tests](../Tests/GameServer.Tests), so these checks are only about whether the model
matches the client and whether the SDB values make it fire at all.

Reading `dbitems::Ammo` and `dbitems::WeaponTemplates` out of the retail `clientdb.sd2` answers the
"does it fire at all" half without a client. `Resolve` enables decay for 120 of the 318 weapon
templates. The 198 it disables break down as 156 whose ammo has `DamageDecay = 0`, 20 with no ammo
row, 19 where the minimum works out to a full round or more, and 3 with non-positive damage per
round, so more than half of any weapon you pick at random is expected to print
`Range decay: disabled` and that on its own means nothing. **Pick a weapon that decays before
concluding anything**, and note that no template is disabled by `DamageDecayRangefrac` alone.

Weapons to test with, and the exact ids to conjure them:

| Command | Weapon | Range | Damage | Full damage to | Floor |
|---------|--------|-------|--------|----------------|-------|
| `createitem 20003` | PvE Shotgun, template 1, level 1 | 50m | 40 | **5m** | 2 at 50m |
| `createitem 20005` | Bolt-action Sniper, template 3, level 1 | 215m | 375 | 193.5m | 123.8 at 215m |
| whatever you spawn with | Assault rifle, template 4 family | 150m | 46 | 105m | 15.2 at 150m |

`createitem` puts the weapon in your inventory; equipping it is still a client-side loadout action.

### [ ] R1: Read the resolved curve for a few weapons (blocks R2-R4)

Verifies that the server resolves a sane curve from the SDB columns and prints it.

1. `createitem 20003` and `createitem 20005`, then equip each in turn
2. `dbg_weapon` with each of the shotgun, the sniper and your default rifle equipped
3. Record `DamageDecay`, `DamageDecayRangefrac`, `MinDamageFrac`, `MinDamage`, `Range` and the
   sampled curve for each — `dbg_weapon` prints samples at 0, 25, 50, 75, 100 and 125% of max range

- Pass: `DamageDecay` is non-zero on at least some weapons and the samples describe a falloff that
  looks like the weapon (a shotgun dropping off hard and early, a sniper barely at all).
- Fail: every weapon prints `Range decay: disabled`. The gating on `DamageDecay` or the sanity
  checks in `DamageFalloff.Resolve` are reading the columns wrong. Bring the printed inputs back and
  the model can be re-derived from them.

The anchoring worry this entry was written for is already settled offline. Across the whole `Ammo`
table `DamageDecayRangefrac` looks like it clusters at 1.0, but 908 of those 1.0 rows are ammo that
doesn't decay at all and never had the column touched. Restricted to the 308 rows with
`DamageDecay` non-zero it spreads properly — 34 at 0.1, 137 at 0.7, 45 at 0.9 — and the value tracks
the weapon class: shotgun ammo at 0.1, rifle at 0.7, sniper at 0.9. That is the "decay starts here"
reading. The other reading has a shotgun holding full damage for 90% of its range and a sniper for
10%, which is backwards, so `Resolve` does not need inverting. What R1 is still for is confirming
the server's loader reads the same columns and that the printed curve matches.

`MinDamageFrac` has 24 rows above 1.0 (values of 2, 2.2, 8 and 10). Those clamp to a full round and
disable decay, which is where the 19 disabled-by-minimum templates come from. It's the one column
whose units look wrong, so if a weapon that ought to decay prints `disabled`, check this first.

### [ ] R2: Damage actually drops with distance

Verifies that the resolved curve is applied to a real shot, at the distances the curve claims.

Use the shotgun. Decay starts at 5m there, so all three distances fit in a space you can pace out;
an assault rifle needs a target more than 105m away before a single number moves.

1. `createitem 20003`, equip it, then `dbg_weapon` and note `Range decay: full to Xm`
2. `npc 1196 0 0 0` — substitute a flat spot you can back away from in a straight line
3. `float` to toggle `cheat_float` on if you need height or distance the terrain won't give you
4. Shoot it from contact range, from just past X, and from as far as it stays hittable
5. `float` again to turn it off
6. Back at the source machine, pair each shot's distance with its damage:

```
grep -aE "HitHandler|damage from" ~/Games/PIN/logs/GameServer.log | tail -40
```

The `T` value in each `HitHandler` line is that shot's distance in metres, and the `took N damage`
line right after it is what landed. That pairing is the measurement — no pacing needed.

Pass: the numbers fall off past X and match the `dbg_weapon` samples at those distances. The
`Damage falloff for {Weapon} at {Distance}m` lines need `--loglevel debug` and only appear once a
shot lands past the full damage range, so their absence is only meaningful if `T` exceeded X.

Attempted 2026-08-10, inconclusive rather than failed. The session landed 122 hits, the longest at
83m, all with a 46-damage assault rifle whose decay doesn't begin until 105m. Damage came out at a
flat 39 body / 58 head throughout and the log has no `Damage falloff` line, which is what a correct
implementation does inside the full damage range. Nothing about the model was exercised.

### [x] R3: Point blank damage is unchanged

Verifies that decay doesn't touch anything inside the full damage range.

1. `npc 1196`
2. Shoot it at contact range with each weapon you have
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: identical damage to before this change. If close-range numbers moved, `FullDamageRange`
resolved to roughly 0 and the anchoring in R1 is wrong.

Passed 2026-08-10: an assault rifle held 46 base, 39 on a body hit and 58 on a headshot, unchanged
from 0m out to 83m, which is the whole of its full damage band up to where the shots stopped.

### [ ] R4: Server numbers agree with the client

The real test of the model, since the client computes its own expectation from the same SDB columns.

1. Do R2 again with the client's floating damage numbers visible
2. Note the client's number and the distance for one shot past the full damage range
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -5` for the same shot

Pass: they agree. A mismatch here would confirm or kill the curve outright.

### [ ] R5: Buffed weapon damage still decays proportionally

Verifies that both minimums are read as fractions of a round rather than absolute points.

1. Do R2 once unbuffed and write down the long-range number
2. Apply an ability that raises weapon damage, then fire at the same distance before it expires
3. `grep -a "Damage falloff" ~/Games/PIN/logs/GameServer.log | tail -5` to see both `Base` and
   `MinDamage` move together

Pass: long-range damage rises with the buff rather than decaying back to the unbuffed floor.
`Resolve` reads both minimums as fractions of a full round specifically so this holds.

---

## Damage to deployables and vehicles

Added in the current working tree: [IDamageable](../UdpHosts/GameServer/Systems/Combat/IDamageable.cs),
implemented by `CharacterEntity`, `DeployableEntity` and `VehicleEntity`. See
[layer 6](Architecture/06-combat-and-damage.md).

Everything about the funnel is covered offline. What isn't, and what these are for, is whether the
health numbers read off the SDB are the right columns and whether the client accepts health updates
on entity types that have never sent one.

### [ ] V1: Deployables have health at all (blocks V2-V4)

Verifies that `StartHitpoints` is the right column and that a deployable can be hit.

1. `deployable 356` — 10000 health, faction 2, so you're allowed to shoot it
2. Shoot it
3. ```
   grep -aE "has no StartHitpoints|Deployable .* took" ~/Games/PIN/logs/GameServer.log | tail -20
   ```

- Pass: no `Deployable 356 has no StartHitpoints` line, and shooting prints
  `Deployable {Type} took {Amount} damage`.
- Fail: the warning appears for 356 too. `StartHitpoints` isn't the health column; check the record
  in [MinimalSDB](../Tools/MinimalSDB) dump mode against `StandardHealth` and the scaling table before
  changing `SpawnDeployable`.

The warning appearing for `deployable 395` is correct behaviour, not a failure — it really does have
`StartHitpoints = 0`.

### [ ] V2: A deployable's health bar moves

1. `deployable 356` — 10000 health, so the bar has somewhere to travel
2. Shoot it and watch the client, not just the log

Pass: the bar drops. This is the first time anything has written `CurrentHealthPct` on a deployable,
so a bar that doesn't move means the client wants something else changed alongside it, or wants a
keyframe rather than a delta.

### [ ] V3: Destroying a deployable

1. `deployable 517` — 100 health and turret 17, so it dies in a burst and has a turret to lose
2. Shoot it until it dies
3. ```
   grep -a "Deployable .* took" ~/Games/PIN/logs/GameServer.log | tail -10
   ```
   `Destroy` itself logs nothing, so the last line reaching `0 of 100 health left` is the signal.

Pass: the death ability fires, the deployable disappears about 2s later, its turret goes with it, and
shots stop registering against it as soon as it dies. Watch for a turret left floating, which means
the `Remove(Turret)` in `Destroy` didn't take.

### [ ] V4: A destroyed deployable doesn't leave an invisible wall

1. Do V3, then wait for the model to disappear
2. `npc 1196 <coords past where it stood>` so there's something to hit behind it
3. Shoot through where it was

Pass: shots hit whatever is behind it. This is what the `Physics.RemoveEntity` call in
`EntityManager.OnRemovedEntity` is for, and the same check applies to an NPC corpse after its 30s
despawn, which had the same problem before this change.

### [ ] V5: Vehicle damage and wrecking

Needs a second person, or an occupied vehicle you can shoot from outside, to see the ejection.

1. `vehicle 116` — one of the two debug vehicles, spawned under NPC 2312's ownership
2. Have the second player get in
3. Shoot it until it's destroyed
4. `grep -a "Vehicle .* took" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: health drops on the client, occupants are ejected on destruction with camera and movement
control returned to them, the death ability runs, and the vehicle disappears. Ejection is the risky
part; a player left attached to a removed entity is the failure to watch for.

### [ ] V6: Friendly deployables are still safe

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

### [ ] V7: Splash reaches deployables and vehicles

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

---

## Carried over from the damage work

These landed in commits `9768cf4`, `31f0ff3`, `4a8075e` but predate the pause, so their in-game
state is unknown. Prune whatever you've already seen working.

### [ ] D1: Weapon damage loop end to end

1. `npc 1196`, kill it
2. Let a hostile source kill you, or shoot yourself off a height with `float` off
3. Move after respawning

Pass: projectile hits damage characters, death fires, respawn puts the player at the nearest
uncaptured outpost and movement input is accepted again afterwards.

### [ ] D2: Headshot and crit

1. `npc 1196`
2. Land one body shot and one headshot on it
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -10`

Pass: `hit.DamageMod` and `weapon.HeadshotMult` visibly change the number; the `Critical` damage
flag reaches the client on headshots and crit materials. With the 46-damage assault rifle the
expected pair is 39 body and 58 head.

### [ ] D3: `InflictDamage` splash falloff

1. `npc 1196 0 0 0`, `npc 1196 3 0 0`, `npc 1196 6 0 0` — three targets at increasing distance from
   one splash centre
2. Fire the splash ability centred on the first
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: damage scales down linearly from `Pointblankrange` to the splash range, and the target caught
by both the direct hit and the splash is only damaged once.

### [ ] D4: `SetWeaponDamage` restores on effect end

1. `npc 1196`, shoot it once, note the damage
2. Apply the ability that overrides weapon damage, shoot again
3. `listeffects` to confirm it's active, then wait for it to expire
4. Shoot a third time
5. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -10`

Pass: the third number matches the first, so the `OnRemove` restore path in the active-command
pattern actually ran.

### [x] D5: Charge camera lockup — FIXED in D5h, `LocalEffectsController` was never being written

Pass: camera control returns after Charge ends, including when the ability is interrupted or the
target dies mid-charge. Currently it does not: vertical aim stays dead, horizontal is fine, and
firing Absorption Bomb restores it.

What is stuck is the client-side `CustomPlayerCamera` from effect **15253** (command 1635110), whose
pitch limits are 0/0 where Absorption Bomb's camera 1576750 uses -90/+90. 15253 has no remove chain
and a duration chain of `RequireCState(living)` only, so the client cannot end it alone; it goes away
only when the server clears the slot. Absorption Bomb "fixes" it by pushing its own camera (effect
15257) that expires on a client-runnable 5s timer.

Tried and failed, in order: implementing `BullrushCommand`'s `ForcedMovementCancelled`; raising the
`MaxTurnRate` base (actively wrong, reverted); strictly-increasing status-effect change times;
echoing `InitTime` into the netfield `Time` (tried twice, reverted twice); confirming the ability
after the chain instead of before it (D5b, kept but not a fix). The server side is verified: the log
shows the slot-2 clear firing, `CombatView` and the owner's `CombatController` both go out on
`ReliableGss`, and the Aero nullable-clear encoding round-trips.

D5d settles where the bug lives. Running Charge's own chain through the `ability 35366` admin
command, so the client never asked for it and never predicted it, leaves the camera working; pressing
`1` for the same chain in the same session locks it. The server's chain handling is therefore clean
end to end, apply and clear both, and everything that remains is the client failing to reconcile the
copy it predicted at keypress against the copy the server replicates. That copy has no client-runnable
way out (15253 has no remove chain and its duration chain is `RequireCState(living)`), so when the
match fails the camera is stranded for the rest of the session.

What the client has to match on is `StatusEffectData`, and only two of its fields carry anything
identifying: `Stack`, which PIN never set and always sent as 0 against the client's 1, and `Time`,
which PIN set to the moment the server got round to applying rather than the activation time the
client sent. D5e tries both.

### [x] D5a: Is the stuck camera reachable from the netfield path?

The next thing to run, and it decides where the fix has to live. Verifies whether a server-driven
apply/clear of 15253 can still tear down a camera that is already stuck, which separates "the client
holds two copies and the clear only killed one" from "the predicted copy is unreachable from the
netfield path at all".

Run against the reverted build, not the one live on 2026-08-10. With the server stopped:

```
cd ~/Games/PIN && ./use-build.sh current    # expect: live build: current
./start-pin.sh
```

Then in game, with Charge (ability 35366) in ability slot 0, default button 1:

1. Press `1` to Charge, and confirm vertical aim is dead afterwards while horizontal still works
2. `listeffects` — expect no 15253 in the printed list, confirming the server thinks it is gone
3. `applyeffect 15253` — expect no visible change, the camera is already locked
4. `removeeffect 15253`
5. If the camera is still locked, `cancelfm 1635109`, then `cancelfm 1635151`

Then, back at the source machine:

```
grep -aE "Character\.(Set|Clear)StatusEffect.*(15252|15253|15215|15216|15456)" ~/Games/PIN/logs/GameServer.log | tail -30
```

Expect the full Charge sequence: 15252 set, 15253 set, 15252 cleared, 15215 set, 15456 set, 15215
cleared, 15216 set, **15253 cleared**, 15456 cleared, 15216 cleared. The 15253 clear is the one that
should have taken the camera with it. Then the manual pair from steps 3 and 4 as a second set/clear.

- Camera unlocks: a server-driven apply/clear cycle can still reach it, so the two copies are
  reconcilable and a data-shape fix on the apply is still viable. Next step is finding the field the
  client binds on — `Stack` is the untried one, since the working `applyeffect` path sends 0.
- Camera stays locked: the predicted camera is a separate object the netfield path cannot touch at
  all, and no change to what the server sends will fix it. The fix then has to make the client roll
  the prediction back, which means finding what the real server sent that PIN doesn't — the missing
  `ForcedMovement` start events for Bullrush and OrientationLock are the leading candidate, since
  PIN only ever sends the cancels and `cancelfm 1635109` on a stuck camera does nothing.

Either way, record which one happened here.

**Ran 2026-08-10: camera stayed locked through every command.** The second branch, so no change to
what the server sends on the status-effect netfield is going to fix this, and the fix has to reach
the client's predicted copy instead. That's what D5b tries.

One caveat on this run, from the log below. The manual `applyeffect` at 19:28:07 carries change time
16544, which is *lower* than the 34365 the Charge sequence ended on 48s earlier, because the 16-bit
change time wraps about every 65s and `NextStatusEffectChangeTime` deliberately lets a large
backward jump through as a wrap rather than nudging it. If the client does treat that field as a
sequence number, it would have dropped the manual apply and steps 3 and 4 were a no-op. Applying
15253 over an already-locked camera has no visible tell either way, so this run can't separate "the
netfield path can't reach the stuck camera" from "the manual pair was ignored". D5c settles that
with an effect whose arrival is visible; run it if D5b doesn't fix things.
[19:27:19 DBG] Character.SetStatusEffect Index 0, Time 33777, Id 15252
[19:27:19 DBG] Character.SetStatusEffect Index 2, Time 33778, Id 15253
[19:27:19 DBG] Character.ClearStatusEffect Index 0, Time 34113, Id 15252
[19:27:19 DBG] Character.SetStatusEffect Index 0, Time 34114, Id 15215
[19:27:19 DBG] Character.SetStatusEffect Index 3, Time 34115, Id 15456
[19:27:19 DBG] Character.ClearStatusEffect Index 0, Time 34344, Id 15215
[19:27:19 DBG] Character.SetStatusEffect Index 0, Time 34345, Id 15216
[19:27:19 DBG] Character.ClearStatusEffect Index 2, Time 34346, Id 15253
[19:27:19 DBG] Character.ClearStatusEffect Index 3, Time 34347, Id 15456
[19:27:19 DBG] Character.ClearStatusEffect Index 0, Time 34365, Id 15216
[19:28:07 DBG] Character.SetStatusEffect Index 0, Time 16544, Id 15253
[19:28:31 DBG] Character.ClearStatusEffect Index 0, Time 41064, Id 15253

### [!] D5b: Confirm the ability after the chain instead of before it

Verifies the current fix for D5. `ActivateAbility` used to send `AbilityActivated` the moment the
packet arrived, before the aptitude chain ran, so the client got its activation confirmation before
any of the effect netfields that chain applies. In SDB the confirmation is `InstantActivation`
(command 1619009), which sits *last* in Charge's chain 1619015, after the `ImpactApplyEffect` that
applies 15252 and 15253. A client reconciling its predicted ability against server state that hasn't
been sent yet is a plausible reason the predicted 15253 never binds to the netfield copy. The send
now happens after `HandleActivateAbility` returns, in both `ActivateAbility` and `ActivateConsumable`.

With the server stopped:

```
cd ~/Games/PIN && ./use-build.sh current    # expect: live build: current
./start-pin.sh
```

Then in game, with Charge (ability 35366) in ability slot 0, default button 1:

1. Press `1` to Charge, wait for the rush to end, then look up and down

- Pass: vertical aim works. Repeat it five or six times, including charging into a wall and charging
  off a ledge, since the rush ending early is the case most likely to skip the confirmation.
- Fail: still locked. Ordering wasn't it. Go to D5c.

Then check the confirmation now lands after the effects, not before:

```
grep -aE "ActivateAbility 35366|HandleActivateAbility: Ability 35366|Character\.(Set|Clear)StatusEffect.*(15252|15253)" ~/Games/PIN/logs/GameServer.log | tail -20
```

Expect `HandleActivateAbility: Ability 35366` and the 15252/15253 sets *before* the
`ActivateAbility 35366 at ...` line. Before this change that line came first.

**Ran 2026-08-10 over 41 Charges: still locked.** The ordering did flip, confirmed in the log, so the
change did what it claimed and it simply isn't the cause. The reorder is kept because it matches the
order SDB describes, not as a fix. Session was otherwise clean: no `HandlePacket Caught`, no
`Unrecognized MsgID`.

### [x] D5d: Run Charge's chain server-side, with nothing predicted

The decisive one, and it needs the new `ability` admin command. Every test so far has changed what
the server sends and re-run the same predicted keypress. This runs the identical chain with the
client never having asked for it, so it never predicts. That splits the two remaining explanations
apart: either the chain's own apply and clear of 15253 work fine and prediction is the whole story,
or they don't and the fault is in the chain path rather than in prediction, which would mean every
conclusion drawn from the `applyeffect` comparison was measuring the wrong thing.

Needs a build with `ActivateAbilityServerCommand`. With the server stopped:

```
cd ~/Games/PIN && ./use-build.sh current    # expect: live build: current
./start-pin.sh
```

Then in game, **without ever pressing `1`** (a predicted Charge in the same session muddies it, so
relog first if you've charged):

1. `ability 35366` — the character should rush forward exactly as if you'd charged
2. Wait for the rush to end, then look up and down

- Vertical aim works: the chain applies and clears 15253 correctly when nothing is predicted, so the
  bug is entirely in reconciling the client's predicted copy. That rules the server's own chain
  handling clean and puts the fix on the client-facing prediction contract, which is where a capture
  of the real server is the only way forward.
- Vertical aim is dead: prediction is not the cause and never was. The chain's own clear of 15253
  doesn't reach the client the way admin `removeeffect` does, even though both call `ClearEffect`.
  Compare the two paths in the log; the difference is that the chain clears three slots and sets one
  inside about 20ms while `removeeffect` clears one slot in isolation. That points back at batching
  or ordering in `FlushChanges`, this time with a clean way to reproduce it.

Then compare the two paths in the log:

```
grep -aE "Character\.(Set|Clear)StatusEffect.*(15252|15253|15215|15216|15456)" ~/Games/PIN/logs/GameServer.log | tail -20
```

The sequence should be identical to a keypress Charge: 15252 set, 15253 set, 15252 cleared, 15215
set, 15456 set, 15215 cleared, 15216 set, 15253 cleared, 15456 cleared, 15216 cleared. If it differs,
that difference is itself the finding.

**Ran 2026-08-10: the first branch, and it's the first clean signal in six attempts.** `ability 35366`
rushed and ended with vertical aim intact. Pressing `1` right afterwards, same session, same chain,
locked it. So the server applies and clears 15253 correctly, `FlushChanges` batching is fine, and the
whole bug is the client keeping a predicted copy that the server's clear never binds to. Stop looking
at the chain and at delivery; the fix has to change what makes those two copies match.

This also retires the `applyeffect`/`removeeffect` comparison as evidence about data shape. That path
has nothing predicted to reconcile against, so the client just plays what it's told and any value in
`Stack` or `Time` looks like it works. "The working reference sends `Stack = 0`" was never an
argument that 0 is correct.

### [ ] D5c: Does a netfield apply reach the camera while it's stuck?

Only if D5b fails. Redoes D5a with an effect whose arrival is visible, and without the change-time
wrap that muddied it. 15257 is Absorption Bomb's camera (command 1576750), pitch limits -90/+90
against 15253's 0/0, so if it arrives you get your vertical aim back and can see it.

Do this promptly after the Charge, within about 30 seconds, so the change time is still climbing.

1. Press `1` to Charge, wait for the rush to end, confirm vertical aim is dead
2. `applyeffect 15257` — watch whether vertical aim comes back
3. `removeeffect 15257` — watch whether it locks again

- Vertical aim returns at step 2: netfield applies do reach the camera while the ghost is present, so
  the ghost is displaceable and D5a's result was the wrap dropping the apply. Whether it re-locks at
  step 3 says whether the ghost is still underneath (re-locks) or got displaced for good (stays
  free). This is also the mechanism behind Absorption Bomb appearing to fix the bug.
- Nothing happens at step 2: the client isn't acting on status-effect applies at all while stuck,
  which is a much bigger finding than the camera and worth chasing on its own.

### [!] D5e: Give the client something to match its predicted effect on

Run this next. D5d proved the failure is a failed match between the client's predicted effect and the
replicated one, so this fills in the two `StatusEffectData` fields that could carry the match and
that PIN was getting wrong. `Stack` now sends `EffectState.Stacks`, which is 1 on a fresh apply where
PIN sent 0. `Time` now sends `Context.InitTime`, the activation time the client itself sent, where
PIN sent the moment the server got round to applying, typically tens of milliseconds later.

Echoing `InitTime` has been tried and reverted twice before, so this needs saying: the recorded reason
for not trying it again was wrong. The note claimed `InitTime` is the client's clock and
`Shard.CurrentTime` is something else, citing an activation time of 3949877841 against a shard time
of "~23122". They're the same clock. `Shard.CurrentTime` is unix epoch milliseconds truncated to
uint32, and 3949877841 is exactly that for 11:28 UTC on 2026-08-10, so the two agree to within a
second. The ~23122 was a `CurrentShortTime`, which is the low 16 bits, compared against a full uint.
The two failed attempts still stand as evidence, but at least one deploy in that period silently
didn't happen, so they're worth less than a clean run.

Both fields change together on purpose. If it works, bisect after; if it fails, both are dead in one
run instead of two.

With the server stopped:

```
cd ~/Games/PIN && ./use-build.sh current    # expect: live build: current
./start-pin.sh
```

Then in game, with Charge in ability slot 0, default button 1:

1. `ability 35366` first, as the control. Vertical aim must still work afterwards. This path is
   unaffected by the `Time` change (the admin command passes `shard.CurrentTime` as `InitTime`) but
   not by the `Stack` change, so if this regresses, `Stack = 1` is actively wrong and stop there.
2. Relog, then press `1` to Charge, wait for the rush to end, and look up and down
3. If it passes, repeat five or six times including charging into a wall and off a ledge, since an
   early end is where a match is most likely to be missed

- Pass: revert one field at a time to find which one carried it, and write down which.
- Fail: nothing in `StatusEffectData` identifies the instance, and the binding must be something else
  the real server sent. At that point stop guessing at field values. The remaining leads are the
  `ForcedMovement` start events PIN never sends for Bullrush (1635151) and OrientationLock (1635109),
  and a capture of the real server, which is the only thing that settles the contract.

Then confirm the values actually went out:

```
grep -aE "Character\.SetStatusEffect.*15253|HandleActivateAbility: Ability 35366" ~/Games/PIN/logs/GameServer.log | tail -10
```

**Ran 2026-08-10: control passed over several activations, keypress still locked.** So neither field
carries the match and nothing in `StatusEffectData` identifies the instance. That's the end of
guessing at netfield values; the next lever has to be something PIN doesn't send at all.

`Time` is reverted to shard clock. `Stack` is kept, sending the real count where PIN hardcoded 0,
because it's a plain gap rather than a hypothesis and the control run showed it costs nothing. It is
not a fix and the comment in `AddEffect` says so.

### [!] D5f: Send a ForcedMovement start for the aim lock

Run this next. PIN sends `ForcedMovementCancelled` for a forced movement it never told the client had
started, and this adds the start.

The log settles what's in the chain: `Chain 1635110 Command 1635109 - Executing OrientationLockCommand`,
so effect 15253 is an `OrientationLock` and a `CustomPlayerCamera` together, both clamping aim.
`OrientationLockCommand.Execute` only registered for its own removal; the `ForcedMovement` event (113)
went out for spawns and teleports and for nothing else, while `OnRemove` sent a cancel naming command
1635109. `ForcedMovementCancelled.CommandId` is an `apt::BaseCommandDef` id and `ForcedMovementData`
carries a uint in the same position, so a cancel is meant to name a movement the client already knows
about from the server. There has never been one.

That fits every result so far. The client predicts the lock at keypress (`AllowPrediction`) and the
server is the only thing that can end it. If a bare cancel needs a server-issued movement to match
against, PIN's cancel is a no-op, which is exactly what `cancelfm 1635109` does on a stuck camera. It
also fits D5d: unpredicted, the client never started a lock of its own, so nothing was left running.

Only `OrientationLock` sends a start. `Bullrush` and `ApplyFreeze` have the same gap and are
deliberately left alone, because Bullrush is movement rather than aim and a server-authored rush on
top of a predicted one could double up and muddy this.

With the server stopped:

```
cd ~/Games/PIN && ./use-build.sh current    # expect: live build: current
./start-pin.sh
```

Then in game, with Charge in ability slot 0, default button 1:

1. Press `1`, wait for the rush to end, look up and down
2. If aim is dead, keep playing and watch the clock. The start carries a 60 second end time when the
   SDB duration is 0, which Charge's is, so aim coming back on its own about a minute after the
   Charge is a distinct third outcome and worth catching

- Pass: aim works. Repeat five or six times including into a wall and off a ledge. Then give Bullrush
  and ApplyFreeze the same treatment and check the rush still looks right.
- Frees itself after ~60s: the client took the start and ignored the cancel. Big result. The start is
  right and the cancel is what's malformed, so compare `ForcedMovementCancelled` against a capture.
- Still locked, no recovery: the client isn't binding a server forced movement to a predicted one
  either, which exhausts what can be reasoned out from this end. Get a capture of the real server.

Then confirm the start went out ahead of the cancel:

```
grep -aE "OrientationLockCommand Sending ForcedMovement" ~/Games/PIN/logs/GameServer.log | tail -10
```

Expect alternating `Sending ForcedMovement 1635109` and `Sending ForcedMovementCancelled 1635109`.
Before this change only the cancel appeared.

**Ran 2026-08-10: still locked, and no recovery after a couple of minutes.** Reverted.

The 60 second tell was worthless, because dumping 15253's apply chain afterwards showed the
`OrientationLock` isn't what clamps aim. Chain 1635110 is four commands: `CustomPlayerCamera` 1635110
(env=client, pitch 0/0), `OrientationLock` 1635109 (env=both, `max_aim_angle=0`, `duration=0`),
`AbilityAnimation` 1635108 (env=client), and `CombatFlags` 1635107. Ending the lock changes nothing
visible while the camera is still clamped, so the run says nothing either way about whether the
client accepted the start. The test could not have distinguished its own outcomes.

Stepping on a Glider pad restored aim, same as Absorption Bomb. Both push their own
`CustomPlayerCamera`, which pins the stuck object as the camera rather than the lock. 15257 is a
clean reference for that: its apply chain is nothing but a `CustomPlayerCamera` at -90/+90 with a
5000ms `TimeDuration` and `allow_prediction=0`, so it arrives by netfield only and the client expires
it on its own.

Reverted rather than kept. Sending a cancel for a movement that never started is a real defect, but
every `OrientationLock` that matters has `duration=0`, so a start needs an end time nobody can derive.
Shipping an invented one risks a genuine aim clamp lasting that long on some ability where no camera
masks it, and that's a worse defect than the one it replaces. The gap and the message shape are
written into `OrientationLockCommand.OnRemove` so it's cheap to redo against a capture.

### D5 status: stop testing fixes

Seven attempts, all failed. What's established is worth keeping:

- The server's chain handling is correct end to end, apply and clear both (D5d)
- The bug needs client prediction. The identical chain run unpredicted is clean (D5d)
- Nothing in `StatusEffectData` binds a predicted instance to a replicated one (D5e)
- The stuck object is the `CustomPlayerCamera`, and any later camera push displaces it (D5f)

**Superseded by D5g below.** The client's Aptitude log channel answers this directly: the client
predicts the effect, rejects the server's replicated copy as a duplicate, and never removes the
predicted one because 15253 is the only effect in the chain that can't expire on its own. Read D5g
first. The two suggestions kept below are what the position looked like before that, and the second
of them has since been answered (the Glider displaces the ghost, it doesn't remove it).

What's left is how the client reconciles a predicted effect against the replicated one, and that
isn't derivable from this side of the wire. Every remaining idea is a guess at a contract we can't
read, and the last three runs each cost a build, a deploy and a play session to learn nothing. Two
things would actually move it, both outside the fix-test loop:

1. A capture of the real server running a Charge. It settles the whole contract at once, not just
   this bug.
2. One free observation next time it happens anyway: after a Glider or Absorption Bomb frees the
   aim, does it lock again about five seconds later when that camera expires? If it does, the ghost
   is still underneath on a stack and a fix has to remove it. If it doesn't, a camera push displaces
   it for good, and applying 15257 on Charge's end is a workaround that would make the ability
   usable now. That's a hack and it should be labelled one, but Charge is unusable as it stands.

### D5g. Read the answer off the client instead of guessing at it (setup done, not yet run)

The client is instrumentable and nobody had looked. Section D5 above says a packet capture is the
only thing that settles the prediction contract, and that a capture means an archive from 2016 that
we may not be able to get. That's now half wrong: the client can log every game protocol message it
receives, to a file, on demand.

**How the client is configured.** Everything lives in
`~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata/227700/pfx/drive_c/users/steamuser/AppData/Local/Red 5 Studios/Firefall/`.
`settings.con` there is exec'd at startup and takes `SetSave <cvar> "<value>"` lines. The cvar names
and their help text are plain ASCII in `system/bin/FirefallClient.exe`, so `strings` finds them.

There's a console too, but it's gated behind a `.wedge` dev file we don't have and has no default
keybind, so cvars can't be typed at runtime. `settings.con` is the only way in.

Two things to know before trusting an edit to it. The game rewrites the file alphabetically on exit
and silently drops cvars it doesn't recognise, so check your lines are still there before each run.
That drop is also a free existence test: a name that survives a rewrite is a real registered cvar.
And the live log is `console.log`; it only becomes `<timestamp>_last_run.log` at the *next* startup,
so read `console.log` for the run that just ended.

**The one that matters.**

```
SetSave gameproto.logGssMessages "1"      // "Enables logging on every single direct game protocol message."
```

That's the capture, from the receiving end, tagged on the `GSS` log channel. It won't show what the
2016 server sent, but it shows exactly what the client got from PIN and in what order, which is the
half of the contract we've been reconstructing by hand from PIN's own source. Expect a large log and
keep the test session short.

There may also be an ini route. `FirefallClient.exe` reads keys of the form `LogLevel-<channel>` for
channels `GssMessages`, `MatrixMessages`, `FirefallCommands` and `FirefallEvents`, and the section
name looks like `[Debug]`. That's inferred from string adjacency, not confirmed, but it's been added
to `firefall.ini` (backup at `firefall.ini.bak`) since unknown ini keys are ignored either way.
`FirefallCommands` and `FirefallEvents` have no cvar equivalent, so if the section name is right
they're the only way to get at those.

**Two more cvars, one useful, one not.**

`debug.playerCamera` ("Print debug text for the player's camera") is real: it survived a rewrite. It
produces no log lines at all, so it draws on screen. Worth having on while testing, since the stuck
object is a camera, but you have to be looking at the screen to use it.

`statuseffects.showall` ("Show all status effects (including hidden) to the UI") was dropped by the
rewrite, so it isn't reachable this way. No great loss: 15253 has `expose_to_ui=0`, `icon_id=0`, and
its `name_id` 960112 has no row in `dblocalization::LocalizedText`, so it has no name or icon to
show.

**First attempt, 2026-08-10: the cvar never took.** A 40 second run in New Eden produced zero `GSS`
lines, and `gameproto.logGssMessages` was gone from `settings.con` afterwards. `SetSave` on a name
the client doesn't know yet is silently discarded, and `settings.con` is exec'd during early
initialization, before the game protocol layer registers its cvars. Only `debug.playerCamera`
survived, which is why that one worked: it's registered by then.

**The way around it is `bind`.** `bind` is a console command, `settings.con` is a console script, and
a bind only stores a string. The key gets pressed later, in-game, when the cvar does exist. Valid key
names come from the client's own table: `f1`–`f12`, `ctrl`, `shift`, `alt`, `grave`, `tab`, `escape`,
`space`, `uparrow`, `mouse1`–`mouse9`, `kp_*`, `gp1_*`.

The same trick answers the naming question outright. `cvarlist` lists cvars, `grep <match> <command>`
filters another command's output, and `grep.print_to_log 1` sends that output to the log instead of
the console we can't open. So the client will hand over its own authoritative cvar names.

Binds do persist. `SetSave eng.autoSaveConfig "0"` does not stick, so the game still rewrites
`settings.con` on exit, but it re-serialises the binds along with the `SetSave` lines for cvars it
knows. Everything else in the file is dropped, so bare console commands have to be wrapped in a bind
to survive.

**Second attempt, 2026-08-10: binds persisted, still nothing in the log.** f6/f7/f8 produced no
output. Two more things turned up that probably explain it:

```
SetLogLevel <logname> <level>    levels: debug, info, warn, error, nothing
ListLogs                         prints every log channel and its current level
cvarlist                         confirmed, an alias of ListVars
```

No log file in that folder contains a single `DEBUG` line, on any channel, ever. Debug output is
filtered out by default, so a protocol logger writing at debug level would produce exactly what we
saw. The channel is probably `gss`, going by the `GSS` tag next to `gameproto.logGssMessages` in the
binary and the usage text's lowercase examples ("guilib, toolshr, render").

**Third attempt, 2026-08-10: this one worked, and it settles the tooling.** Binds fire (f5 blanked
the FoV box, f6 brought it back), `grep.print_to_log` really does route console output into
`console.log`, and the client answered both questions itself:

- `Unknown command "gameproto.logGssMessages"`. That cvar does not exist. The string in the binary is
  dead. Two runs were spent on it.
- `Unrecognized log name 'gss'`. There's no channel by that name either.

`ListLogs` gave the real list, every channel at `INFO` except `AI` at `DEBUG` and `STEAM` at `ERROR`:

```
ITEMS  IOSYS  SDB    CONSOLE TSANIM TOOLSHR VCS   FX     SNDLOOP SNDACT SOUND
Aptitud  GT   VT     RENDER HAVOK  ENHAVOK ENTROPY HAVOK JSCRIPT GUI    SCRIPT
INPUT  NETWORK NETHTTP ANIMATE AI   PROTO  AUDASST PROTO STEAM  GAME
```

`Aptitud` is the prize, and it's better than the packet capture this section has been chasing. It's
the client's own ability engine, so raising it to debug should log the client's own chain execution:
the predicted copy of 15253, which is the exact thing we've spent seven attempts inferring from the
server side. `PROTO` is the protocol channel, appearing twice, so a name lookup may only reach the
first of the two. `VCS` is the component system that owns `StatusEffectComponentDef`.

The name column is truncated to seven characters, so `Aptitud` is presumably `Aptitude`. Both
spellings are bound; an unrecognised one logs an error and the rest of the line still runs.

```
bind f5 "debug.playerCamera 0"
bind f6 "debug.playerCamera 1"
bind f7 "AlwaysFlushConsole 1; SetLogLevel Aptitude debug; SetLogLevel Aptitud debug; SetLogLevel PROTO debug; SetLogLevel VCS debug"
bind f8 "grep.print_to_log 1; grep s ListLogs"
```

### D5g result: the mechanism, straight from the client

`SetLogLevel Aptitud debug` took (the channel name really is seven characters; `Aptitude` is
rejected) and produced 536 debug lines. `PROTO` accepted the level change on both of its entries and
then logged nothing, so there's no debug output behind that channel.

The whole Charge, client side:

```
00:32  Successfully applied effect 15253
00:32  Successfully applied effect 15252
00:32  Unable to apply status effect 15252: too many stacks (x1)
00:32  Unable to apply status effect 15253: too many stacks (x1)
00:32  Successfully applied effect 15456
00:32  Successfully applied effect 15215
00:32  Successfully removed effect 15252   + Canceled
00:32  Unable to apply status effect 15215: too many stacks (x1)
00:32  Unable to apply status effect 15456: too many stacks (x1)
00:33  applied / removed / Canceled 15216  (twice)
00:33  Successfully removed effect 15215   + Canceled
00:36  Successfully removed effect 15456   + Canceled
```

Two facts fall out, and together they're the whole bug.

**Every effect is applied twice and the second is rejected.** The client predicts the effect at
keypress, the server's replicated copy arrives a moment later, and the client refuses it as a
duplicate because `max_stack_count=1`. That happens to 15252, 15215, 15456 and 15253 alike, so the
double-apply on its own is not the fault.

**15253 is never removed.** It appears exactly twice in the entire log, the apply and the rejected
duplicate, and that's all. Every other effect in the chain gets a matching
`Successfully removed` + `Canceled` pair. What makes it different is where those removals come from:
each one is the client's own predicted copy expiring on its own duration chain. 15253 has
`remove_chain=0` and a duration chain of `RequireCState(living)`, which never goes false while you're
alive, so its predicted copy has no way to end itself. Its only removal is external, and the external
one is aimed at a replicated instance the client rejected and never bound. So nothing removes it, and
the camera stays clamped at 0/0 until you leave the zone.

The apply command is `1593262`, in effect 15252's chain, with `allow_prediction=1` and
`remove_on_rollback=0`. The removal is `1635135` in PIN's `aptgss_agsImpactRemoveEffectCommandDef.json`,
one of the 15 hand-authored entries, commented "guess based on captures". It clears the server's slot
correctly, which is exactly why D5d looked clean: with no prediction there's nothing left behind.

**The Glider does not remove the ghost.** The burst of cancels at 00:40 to 00:44 when the Glider is
boarded covers a dozen effects and 15253 is not among them. It's displacement by a competing
`CustomPlayerCamera`, not removal. That settles the open question this section has been carrying:
the ghost stays underneath, and applying 15257 at Charge's end would mask the symptom the same way
without fixing anything.

**Lead worth checking next.** `Lib/AeroMessages/.../Character/Controller/LocalEffectsController.cs`
is GSS controller 6, 64 slots of `LocalEffectsData { EntityId Entity, uint Effect, uint Time }`, and
PIN never writes a single one of them. It's an owner-private effect array, which is the natural place
for a server to drive the effects the owning client predicts, separate from the `StatusEffects_N`
view that every observer sees. That's a hypothesis, not a finding, but it's now cheap to test:
the Aptitude channel shows the client's side directly.

Other cvars found in the same sweep, not yet used: `debug.logDamageDealtEvents` and
`debug.logDamageTakenEvents` (log every damage event the local character deals or takes, a free check
on the damage work), and the `debuglag.*` family, which draws a network diagnostic overlay including
GSS receive counts and clock deltas.

### [x] D5h: Drive effects through `LocalEffectsController` as well — this was the bug

The one channel in the protocol that's owner-private and about effects, and PIN has never written a
byte of it. Everything except the writes was already there: `CharacterEntity` builds a
`LocalEffectsController`, `EntityManager.ScopeIn` keyframes it to the owning player only, and
`FlushChanges` flushes it alongside the other controllers. All 64 slots have just always been null.

The change is in [CharacterEntity.SetStatusEffect](../UdpHosts/GameServer/Entities/Character/CharacterEntity.cs),
next to the existing `StatusEffects_N` writes: fill `LocalStatusEffects_{index}` with
`{ Entity = data.Initiator, Effect = data.Id, Time = data.Time }` on apply and null it on clear,
using the same slot index the 32-slot array uses. Nothing else moves, and the two arrays can't
disagree.

`Entity` is ambiguous, it could be the initiator or the target, and the field name doesn't say.
Charge is entirely self-applied so both are the player and this test can't tell them apart. That
only matters if D5h works and someone then applies it to an effect cast on someone else.

Deployed as `GameServer.dll` md5 `5662843a1725c212f4a8a6ea2eeab238`.

Steps:

1. Start the server: `cd ~/Games/PIN && ./start-pin.sh`
2. Confirm the client is still set up to log its ability engine. `settings.con` should still carry
   the binds from D5g; if it does, nothing to do.
3. Launch, get in world, press `f7` once to raise the Aptitude channel to debug.
4. Charge once, on flat ground, nothing else bound in the way. Note whether vertical mouselook
   still locks.
5. Wait about ten seconds, then quit the client cleanly so the log is flushed.
6. Read `~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata/227700/pfx/drive_c/users/steamuser/AppData/Local/Red 5 Studios/Firefall/console.log`

What decides it, in the log, is what happens to 15253. In D5g it appeared exactly twice, an apply
and a `too many stacks` rejection, and never again:

- A `Successfully removed effect 15253` plus `Canceled` at the end of the chain means the client
  took the local array as authority over what it predicted. That's the fix.
- The same two lines and nothing else means the client either ignores controller 6 or wants
  something else in it. Hypothesis dead in one run, and no more server-side guessing after that
  without first finding what reads it.
- Anything new and unexpected (a third apply, an error, effects vanishing that shouldn't) is worth
  more than either, because it means the client is reading the controller and we've fed it wrong.

Aim working again while the log shows nothing new about 15253 would mean the camera was displaced
rather than released, same as the Glider. Check the log before believing the camera.

### D5h result: fixed, and the log says why

Camera works after Charge. Three Charges in one run, all clean, and this time the client log backs it
up. Every one of them ends the way no Charge ever had before:

```
00:44  Successfully applied effect 15253
00:44  Unable to apply status effect 15253: too many stacks (x1)   (twice now, see below)
00:45  Successfully removed effect 15253
00:45  Canceled effect 15253
```

The `Successfully removed` plus `Canceled` pair is the whole result. In D5g those two lines never
appeared for 15253 in a 537-line log, and now they appear on all three Charges, in the same burst
that clears 15215, 15456 and 15216.

The single-variable comparison holds. The server cleared `StatusEffects_2` for 15253 in the D5g run
too, right on schedule, and the client ignored it. The only thing that changed is that
`LocalStatusEffects_2` is now nulled at the same moment, and now the effect ends. So the client binds
what it predicted to the owner-private array and treats the 32-slot one as somebody else's business.
That array is the observers' view. It was never going to reach a prediction.

Rejections went from 4 in the D5g run to 33 here, because each effect now arrives twice, once through
`CombatController`/`CombatView` and once through `LocalEffectsController`, and the client bounces both
against its own prediction. It doesn't care, and the removal lands regardless. Still, the real server
probably didn't send both, and sending the owner only the local array would be worth trying now that
there's a way to see the result. Not urgent: nothing about the current shape is broken.

Worth being clear about what this does not establish. Only self-applied effects have been tested, so
whether `Entity` is the initiator or the target is still open. And every other prediction-shaped bug
in the game just became worth re-testing, because until now the owning client was never told about
its own effects through the only channel it listens to.

---

## Known to need the client, not yet scheduled

Not tests so much as things that can't be checked offline at all, kept here so they're not mistaken
for done:

- Whether any new netfield shape or message layout is accepted by the client
- Client prediction agreement on spread, movement and effect timing
- Anything about what the client renders
