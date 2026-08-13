---
project: pin
kind: test-stream
title: Session Setup
relates:
  - ../TEST-REGISTER.md
---

# Session Setup

What every entry in the [test queue](README.md) assumes is already done: a build deployed, the
servers up, the log being watched, and the type ids to hand.

## Starting a session

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

## Who you log in as

Every login is the same character. `NetworkPlayer.Init` asks the RIN for character data over gRPC,
nothing answers, and it falls back to `HardcodedCharacterData.FallbackData` — so the fallback isn't a
fallback in practice, it's the character.

It is female as of 2026-08-12. Gender alone does most of that: `ApplyLoadout` picks the frame's
visual record out of `dbitems::BattleframeVisuals` by matching the row's gender char, so the body
model follows the field. Everything hanging off the body doesn't, and was changed with it — head
10002 is `sex = M` in `dbcharacter::Head`, voice set 1000 is `sex = 0`, and head accessories 10089
and 10106 appear on 112 monster rows, all male. The replacements (head 10026, accessory 10115, voice
1040) are the set Accord female NPCs use, so it's a combination the game shipped rather than one
assembled here. Ornament groups are unchanged: `dbvisualrecords::OrnamentsMap` filters a group's
contents by `sex_flags` client-side, so a group id is already gender-neutral.

`HardcodedCharacterData.FallbackData` points at `FemaleFallbackData`; point it at
`MaleFallbackData` to switch back. Both presets are in the same file.

Remember to watch the GameServer log throughout. Two things are silent failures worth grepping for
after any session:

```
grep -a "HandlePacket Caught" ~/Games/PIN/logs/GameServer.log    # a handler threw and was swallowed
grep -a "Unrecognized MsgID"  ~/Games/PIN/logs/GameServer.log    # the client sent something we don't handle
```

## Admin commands

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
| `target [entityId/name]` | — | no argument ray-casts from your aim; `target me` or `target self` targets you; prints the distance, so it doubles as a rangefinder |
| `clear` | `cleartarget`, `targetclear`, `untarget`, `removetarget`, `remtarget`, `deletetarget`, `deltarget` | clears the command target |
| `hostility` | `stance` | stance both ways against the current target |
| `hazard` | `env` | water level and nearest melding wall, with which side of it you're on |
| `invuln [on\|off]` | `god` | no argument toggles; hits the current target if there is one |
| `dbg_weapon` | — | weapon template, ammo and the resolved decay curve |
| `ability <abilityId>` | `useability`, `activateability`, `apt_ability` | runs the chain server-side, so the client never predicts it |
| `applyeffect <effectId>` | `apply_effect`, `apt_apply` | |
| `removeeffect <effectId>` | `remove_effect`, `apt_remove`, `apt_clear`, `apt_cancel` | |
| `listeffects` | `list_effects`, `apt_status`, `apt_list` | |
| `cancelfm <commandId>` | — | cancel a ForcedMovement by command id |
| `createitem <typeId>` | `create_item`, `giveitem`, `give_item` | second argument is quantity, resources only |
| `dbg_inventory [resend]` | `dbg_inv` | prints the server's items and resources; `resend` pushes the full inventory again |
| `tp <x> <y> <z>` | `teleport` | z is up |
| `pflags` | `pflag`, `float` | toggles `cheat_float`; this is how you get to a measured distance |
| `cflags [value]` | `cflag` | sets CombatFlags |
| `rment` | `killall` | removes every entity except player characters — use between tests |
| `emote <id>` | — | remote views only |
| `say <message>` | — | server-wide chat |
| `rarmy` | `reloadarmy` | reload Army UI |
| `dbgattach <unk2> <unk3>` | — | AttachedTo debugging |

## Monster type ids

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

### How hard they actually hit

Resolved through `SDBUtils.GetDetailedWeaponInfo` and `AttackWindow` on each monster's `Weapon1Id`,
so this is what PIN will really fire, not what the template asks for. A player has 3000 shields and
19192 health — 22192 to chew through — which is why the default picks feel like nothing.

| Type id | Monster | Damage / round | Rounds / burst | Burst | Reach | dps | Time to kill a player |
|---------|---------|---------------|----------------|-------|-------|-----|-----------------------|
| 1196 | Chosen Fiend | 1 | 2 | 250ms | 180m | 8 | 46 min |
| 1304 | Black Hills Bandit | 1 | 2 | 250ms | 180m | 8 | 46 min |
| 2342 | Aranha | 125 | 1 | 3350ms | 50m | 37 | 10 min |
| 528 | Melded Aranha | 49 | 1 | 1280ms | 5m | 38 | 10 min |
| 1375 | Chosen Gatling Gunner | 20 | 1 | 250ms | 230m | 80 | 4.6 min |
| 2407 | Tanken Saboteur | 232 | 1 | 2500ms | 150m | 93 | 4 min — but Neutral, so it won't start |

**1196's 8 dps is the real shipped number**, measured off a 2026-08-12 log at exactly 8
`damage from` lines a second. Firefall's monsters weren't meant to kill a level 45 frame alone;
spawn several, or use `1375`, which is the hardest-hitting id here that is both hostile and ranged.

**Don't reach past that for something faster without checking its clip.** PIN doesn't model ammo or
reloading, so a weapon that shipped with a one-round clip fires continuously instead of once per
reload, and its damage comes out wildly inflated — [DATA-14](../gaps/data.md#data-14). Monster 281
(Chosen Sniper) is the clean example: 500 damage a round, `clip = 1`, `reload = 1000ms`, so retail
gave it about 450 dps and PIN gives it 2000. It will kill you in eleven seconds, and none of that is
a real number. Prefer ids with a large clip — 1375 carries 500 rounds, 2342 carries 9600.

Any of the four Hostile ids work for a check that needs a hostile target. For a wider pick, the
factions hostile to players are 2 chosen, 5 monster, 6 melding, 7 gaea, 8 bandit, 22 Black Hills
Bandits, 42 Aranha, 45 Rebels, 46 Reapers and 47 Ophanim. `dbcharacter::Monster` has 3109 rows and
1731 of them are accord, so the hostile ones concentrate in gaea (431), chosen (270) and bandit
(249).

## Deployable and vehicle type ids

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
