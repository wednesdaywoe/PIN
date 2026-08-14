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
dotnet test PIN.sln                       # all green before leaving the desk
cd ~/Games/PIN && ./start-pin.sh          # builds, installs, announces, launches
```

That is the whole procedure. `start-pin.sh` builds all three servers from the repo, installs them
over the deployment, prints the build it is about to run, and starts them —
WebHostManager on 4400-4411 / 44300-44311, MatrixServer on UDP 25000, GameServer on UDP 25001. An
incremental build of the three takes about six seconds, which is why it is unconditional: there is
no version of skipping it that saves enough to be worth what it costs. A build failure aborts
before anything is installed or launched, so a broken tree can never leave the previous binary
running under a test entry written for the new one.

There is no separate deploy step any more. `./deploy.sh` exists and does the build-and-install half
on its own, but the only reason to call it directly is to install without starting.

### Knowing which build you are on

Testing a stale build has cost several sessions: the result gets written down against code that was
never running, and nothing on screen contradicts it. Three independent readings of one number now
have to agree before that can happen again — what `deploy.sh` recorded when it installed, what is
hashed off the DLL at launch, and what the server hashes itself as and logs on its first line
(`UdpHosts/GameServer/BuildInfo.cs`). The number is the first 12 hex of the assembly's SHA-256, so
`sha256sum` is a fourth reading available at any time.

The banner prints before launch and is repeated as the last line before you go to the client:

```
────────────────────────────────────────────────────────────────
  build      29040939701d  (GameServer.dll, written 2026-08-13 09:05:20)
  source     3d72051 on proton-config  "Doc update"  +14 uncommitted
  installed  2026-08-13T09:05:53-04:00
────────────────────────────────────────────────────────────────
```

The same three lines are written into the head of every log before the server appends to it, so a
transcript read back weeks later still names the binary that produced it — by which time the DLL
itself has been overwritten many times. Confirm the match against the server's own line:

```
grep -a "Running build" ~/Games/PIN/logs/GameServer.log
```

Anything wrong appears as `!!` lines inside the banner and in the logs: a DLL that does not match
what was installed, or `--no-build` left on while the repo has moved ahead. A `source` line reading
`unknown` means the installed DLL is not the one `deploy.sh` put there, so no commit can honestly
be attributed to it.

Builds are deterministic — the same source rebuilt gives the same hash — so the number identifies
the source, not just the file.

### Restarting mid-sitting

Every start truncates `logs/`, so a restart used to destroy the transcript of everything tested
before it — which cost a whole M4 run on 2026-08-14. **The outgoing logs are now moved aside first**,
into `~/Games/PIN/logs/previous/<server>-<YYYYMMDD-HHMMSS>.log`, ten kept per server. Grep them the
same way, and note that the plain `logs/*.log` greps quoted throughout these streams read the
current run only:

```
grep -a "Geo report" ~/Games/PIN/logs/previous/GameServer-*.log
```

A restart also **re-deploys by default**, and the install is a plain copy out of the build output.
Anything authored from inside the game — a `deposit add`, a `spawngroup add`, both of which write
their JSON in the deployment because the server knows nothing about a repo — is therefore
overwritten by the repo's copy. The repo still wins on purpose, since keeping the local file would
silently run stale data, but the outgoing one is now kept in
`~/Games/PIN/builds/authored/` and the deploy says so loudly. **To restart without re-deploying,
`./start-pin.sh --no-build`** — that is the only way an in-game placement survives one.

### Running an older build

`./start-pin.sh --no-build` runs what is installed without touching it. This is the only mode in
which the build under test is not the current tree, and it says so loudly every time, because the
reason it exists is also the reason it gets left on by accident.

```
cd ~/Games/PIN && ./use-build.sh          # lists what is installed and what can be swapped in
./use-build.sh <hash>                     # any archived build, by the hash the banner shows
./use-build.sh latest                     # back to the current tree
```

`deploy.sh` archives the outgoing `GameServer.dll` into `builds/` before overwriting it, keeping the
last ten. That matters because once a session has produced results against a binary, that binary is
evidence, and it was previously destroyed by the next `cp`.

Two named tags predate the archive and are kept only because test entries written at the time name
them: `good` is the `c659335` build from before the Damage/Effect work, and **`current` is a
misnomer** — it is the 2026-08-11 build, not the newest, and selecting it silently rolls back
environmental damage, `invuln`, the [DATA-11](../ISSUE-REGISTER.md) weapon-modifier fix and NPC
locomotion. The listing prints real dates beside every entry so no tag has to be trusted.

### Where the scripts live

In the repo, at [Tools/Session/](../../Tools/Session/) — `start-pin.sh`, `deploy.sh`,
`use-build.sh` and `openssl-legacy.cnf`. `~/Games/PIN` holds symlinks to them, so every path in
this doc and in the older entries still works and the pipeline is version-controlled with the code
it builds. It used to exist only in the deployment directory, one `rm -rf` from gone.

The split has one consequence worth knowing when editing them: a script has two homes, and which
one it wants depends on the line. Resolving `$0` through the symlink finds the repo copy — the
source tree, the openssl profile. Not resolving it finds the deployment — where the servers run.
Run a script directly out of `Tools/Session/` and it will refuse rather than guess, because
guessing would install a build on top of the source tree.

### What the pipeline will not touch

The deployed configs are not copies of the repo's. `GameServer/GameServer.dll.config` carries this
machine's paths to `clientdb.sd2` and the asset DB where the repo's copy points at a Windows Steam
install, and `WebHostManager/config/appsettings.json` has `DevMode` on locally and off in the repo.
Both are excluded from the install, so a deploy can never break the server in a way that looks like
a code bug. Their upstream templates are hash-tracked instead: when one changes in the repo,
`deploy.sh` prints the `diff` to run and leaves the local file alone.

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
| `thumper [<beaconCalldownDefId>]` | `spawn_thumper` | at your feet, owned by you, so it pays you when it finishes; defaults to def 766269. Since M4 the feedback names the deposit (or barren ground) it landed on, and the payout comes from that — see [Thump Placement](Thump-Placement.md). A full cycle is 7½ minutes — see [Resource Payout](Resource-Payout.md) |
| `deposit list \| add <nodeTypeId> [radius] \| remove <id> \| radius <id> <metres> \| reload` | — | places a resource deposit centered where you stand and saves it immediately, same footing guards as `spawngroup`; see [Thump Placement](Thump-Placement.md) |
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
| `removeitem <typeId\|guid\|loose>` | `remove_item`, `delitem` | `loose` drops everything no loadout is using — the clutter a test session makes; add `force` to take slotted items too |
| `equipitem <typeId> [slot]` | `equip_item`, `equip` | slots an item you already own into the loadout you are wearing; default slot is `Secondary`, run it bare to list the slot names |
| `dbg_inventory [typeId\|resend]` | `dbg_inv` | bare prints everything to the client console; a type id logs just that item's guid, sub-inventory and flags to the server log; `resend` pushes the full inventory again, which is the update the client is known to accept |
| `tp <x> <y> <z>` | `teleport` | z is up |
| `pflags [flag]` | `pflag`, `float` | toggles a named permission flag, `pflags list` to see them; bare `pflags` still toggles `cheat_float`, which is how you get to a measured distance |
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
