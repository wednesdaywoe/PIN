---
project: pin
kind: plan
title: PIN — Progress
id-prefix: M
relates:
  - ISSUE-REGISTER.md
  - TEST-REGISTER.md
---

# PIN — Progress

Checklist toward a vertical slice: one player, one zone, a loop that closes — logs into zone 448,
gets noticed by NPCs that close and shoot back, kills them for XP and loot, scans the ground for a
deposit worth working, calls down a thumper and defends it through extraction, logs out, and comes
back to the same character. Full narrative for each milestone lives in [streams/](streams/).

For what the code does today, read the [Architecture Guide](Architecture/README.md). This doc only
covers what's missing and what order to fix it in.

```
M1 confirm the combat models
 └─> M2 NPCs that fight back
      ├─> M3 resources come out of the ground
      │    ├─> M4 where you thump matters
      │    └─> M7 an encounter that plays
      └─> M5 killing something pays

M6 persistence across sessions   wants M3 and M5 landed, so there's something worth saving
M8 session stability             independent, but "playable" isn't honest without it
```

## Current frontier

**M2: NPCs that fight back is done, closed 2026-08-13 by [N14–N16](In-Game-Tests/NPC-Combat.md).**
Zone 448 has thirteen monsters standing in it when you log in, none of them put there by a command.
They notice you, close, shoot, respawn ninety seconds after they fall, and on the closing run the
Basin Mouth pack killed the tester. The one piece that stays open is death notification, and only
because it has nothing to show yet — `CharacterDiedEvent` is published and nobody subscribes, which
is M5 and M7's job.

The next frontier is **M3, resources out of the ground** — two cuts are code-complete and have never
been seen in game.

Perception and target selection, the attack pass and death notification are all built
under [Systems/AI](../UdpHosts/GameServer/Systems/AI/) and driven from
[AIEngine.Tick](../UdpHosts/GameServer/AIEngine.cs), which is no longer an empty tick.
[N1–N7](In-Game-Tests/NPC-Combat.md) ran on 2026-08-12 and all pass: an NPC notices you, turns to
face you, respects cover, opens fire, damages you, disengages when you leave, and dies mid-burst
without leaving a corpse stuck firing. Death notification is the exception, and only because it has
nothing to show — `CharacterEntity.Die` publishes `CharacterDiedEvent` onto the `EventBus` and
nothing subscribes yet; M5 and M7 are what give it a listener.

Building it paid for itself twice over. [DATA-11](ISSUE-REGISTER.md) — a `WeaponTemplateModifiers`
multiplier of 0 read literally instead of as "unset" — had been zeroing the range of 269 weapons and
the damage of 220 in every weapon the server ever resolved, player and NPC alike, and surfaced only
because an NPC picks its weapon out of `dbmonster` rather than choosing one that works. A crash in
the tail of one session log closed [NET-21](ISSUE-REGISTER.md).

**Locomotion is built and has had one client sitting, on 2026-08-13, which found exactly one thing
and it was not the steering.** An NPC steers straight at its target, stops short at a distance its
weapon can reach, and walks back to where it spawned when it loses interest or chases too far; its
speed comes off its chassis, which is where `dbmonster`'s -1s were pointing all along. Height is the
part with no good answer — the server holds no terrain to clamp to, so an NPC takes its target's
footing as the ground and refuses slopes too steep to be ground.

The first of the day's two sittings found that none of it reached the client. Nothing replicated an
NPC's movement at all: the movement view isn't flushed to scoped clients and no pose was sent on its
behalf, so monsters teleported whenever a keyframe happened to correct the client
([NET-22](ISSUE-REGISTER.md), fixed the same day with
[NpcPose](../UdpHosts/GameServer/Systems/AI/NpcPose.cs)). The second went green:
**[NPC Combat](In-Game-Tests/NPC-Combat.md) went 13 of 13 that day**, and finished at 16 of 16 once
the spawn groups had entries of their own — the first stream in the queue to close. A monster
notices you, turns, runs the ground down, stops where its own weapon can reach, shoots, tracks you
while it fires, and walks home when it loses you.

**The spawn groups were the last piece of M2, and they closed it.** Zone 448 now comes with thirteen
monsters in three groups of its own, described in
[spawn_group.json](../UdpHosts/GameServer/StaticDB/CustomData/spawn_group.json) and kept populated
by [SpawnGroupSim](../UdpHosts/GameServer/Systems/Spawning/SpawnGroupSim.cs), which refills a place
about two minutes after the thing standing in it dies. The dead faction-test row in
`TempSpawnTestEntities` came out with it. [N14–N16](In-Game-Tests/NPC-Combat.md) are the entries,
N14 is the milestone's exit condition and the only one in the stream that forbids the `npc` command,
and all three passed.

**Three sittings on 2026-08-13 all failed on content rather than code, in three different ways, and
each one produced a guard rather than just a correction.** The first four groups used the three
monsters the test queue had already proven, on the reasoning that all three were known hostile to
the player. They are also in three mutually hostile factions, so the zone fought itself out in
twenty seconds and killed Aero on the way through. `SpawnGroupSim` now audits every standing NPC
pair at startup and warns about any hostile pair inside perception, which is the check nobody would
think to repeat.

The other two were both about height, and together they ended the idea that this content can be
authored offline at all. A group anchored on a lone object's Z ended up under the terrain, shooting
a player who never saw it. Requiring several objects to agree on a height survived one more run and
then failed too: around the valley the ground moves 12m vertically inside 9m horizontally, so
scattering a group over a radius put monsters in the hillside.

So placement is measured now. `MovementRelay.RecordGroundSample` logs a grounded player's position
once a second, because a player's pose is the only terrain measurement that reaches this server and
nothing was writing it down. The
[`spawngroup`](../UdpHosts/GameServer/Systems/Admin/Commands/SpawnGroupServerCommand.cs) command
places a monster where you are standing, saves, and respawns the group, so the loop is walk, place,
look, adjust. Anchor-and-radius is gone: every monster carries its own verified position, and zone
448's thirteen are all on footings from a walk ([DATA-15](ISSUE-REGISTER.md)).

**The closing sitting found a fourth way to end up underground and it was the tester.** N16's own
`tp` target was a coordinate nobody had stood on, so the fight was run from about 8m inside a
hillside — and worse, that destination went straight into the footing record, because the client
reports grounded inside terrain exactly as it does on top of it. A tool that trusts the player's
footing is only as good as the footing being real, so `CharacterEntity.PlacedPosition` now marks a
position a character was *put* at rather than walked to, no footing is recorded until it leaves that
spot, and `spawngroup add` refuses until then.

Two things came out of that run and neither is an M2 defect. **A player who dies has no way back**:
the server kills them correctly, all six attackers drop target in the same second, and the client
never sends `RequestRespawn`, so the session ends with a reconnect
([NET-23](ISSUE-REGISTER.md)). And the first balance reading ever taken against real content —
monsters are bullet sponges while doing little damage — resolves to one existing gap rather than to
the pack: incoming is roughly retail already (19192 player health is
[DATA-3](ISSUE-REGISTER.md)'s observed figure, the Chosen's ~8 dps is
[DATA-14](ISSUE-REGISTER.md)'s measured one), and outgoing is
[DATA-6](ISSUE-REGISTER.md)'s flat 2500 health pool for every creature in the game.

Those sittings also showed what the invented perception radius costs, and it is not cosmetic. At 40m
nothing hostile fits on zone 448's starting shelf while Aero stands on it, because the shelf is
about 45m across, and it is the only ground near the station anything has been seen standing on.
Zone 448 therefore has nothing to fight within 120m of where a player logs in. Retail's widest
shipped `perceptionDist` is 25m and its most common are 10 and 15. Keeping 40m until N14–N16 had run
was a deliberate call (decision 2026-08-13, user-chosen) so a spawn-group failure couldn't be
confused with a tuning change. They have now run and passed, so that reason has expired and the
question is open again — narrowing it would let something stand on the starting shelf, and N16 read
the pack's 45m spread as arriving in ones and twos, which is 40m perception against that spread
rather than a defect.

Two log floods came out of the same runs and are fixed: 8472 warnings for pose asset `00000000`
(a character with no collision id, re-asked on every physics query because only successes were
cached) and 3803 each of `Failed to get WeaponSpread/RateOfFire Attribute`, thrown and caught once
per round fired.

Where the placements come from is the part worth knowing: nowhere. Retail's spawn tables were
server-side, and the client's own `system/maps/448.zone` holds terrain, melding perimeters and
cinematic paths but not one monster. That makes the groups PIN's own content, unlike the melding and
deployable data next to them, which is recovered.

Two findings from the closing sitting are recorded rather than fixed —
[CLIENT-3](ISSUE-REGISTER.md), a creature model that renders 90° off the orientation it is sent, and
a lead out of [DATA-10](ISSUE-REGISTER.md) that turned into **a research pass, run 2026-08-13, with
a bigger answer than the entry that prompted it.** Retail's standoff distances do sit in the shipped
behaviour strings as `combatDist=`, and they sit on `dbcharacter::Monster` itself: 2100 of its 3109
rows carry their parameters inline, not the 451 the entry assumed. The instance table behind the
other 695 does not exist to be found. Searching the string content of all 575 tables turns up
behaviour text in exactly three columns of one table and nowhere else, and this db was built with
the client flag, so those rows went to a server database PIN can't get. The same strings also carry
`perceptionDist`, which never exceeds 25m anywhere in the file against PIN's flat 40m, and
`triggerPullTime`/`fireRestDuration` on 301 monsters, which is the pause
[DATA-14](ISSUE-REGISTER.md) says is missing. `MinimalSDB find` reproduces all of it. See
[streams/m2-npc-combat.md](streams/m2-npc-combat.md).

Two things landed alongside the milestone. **Environmental damage** went in on 2026-08-12 — deep
water and melding walls both kill now, through
[HazardSim](../UdpHosts/GameServer/Systems/Hazards/HazardSim.cs) — and permanent player
invulnerability came out with it, replaced by an `invuln` command. [E1, E2 and
E6](In-Game-Tests/Environment.md) passed the same night: drowning reproduces retail effect 789's
compounding curve tick for tick in the log, and `invuln` suppresses the damage while the hazard
reading keeps running. The three melding entries have not run, and E3 — which side of a perimeter is
the lethal one — gates the rest of them. Earlier, while waiting on client access, the first two cuts
of **M3** landed (code complete, not yet seen in game) — see
[streams/m3-resource-payout.md](streams/m3-resource-payout.md).

Sessions are still launched with the `PROTON_NO_FSYNC=1 PROTON_NO_ESYNC=1` workaround for the
world-entry freeze ([CLIENT-1](gaps/client.md), open pending further confirmation).

---

## Milestones

[Full detail](streams/m1-combat-models.md) — 4 of 4 done

- [x] Faction stance encoding confirmed (H1)
- [x] Range decay curve confirmed (R1–R2)
- [x] `Battleframe` shield data confirmed absent from build 1962
- [x] Placeholders replaced with shipped values, remaining divergence written down

[Full detail](streams/m2-npc-combat.md) — 4 of 5 done, 1 in progress

- [x] Threat table, target selection, line of sight (N1, N4, N7)
- [x] Leashed steering and ground clamping (N8–N13). No terrain to clamp to, so height comes off the
  target's own footing instead
- [x] Server-side attack entry point — weapon fire, since `dbmonster` names no abilities to prefer
  (N2, N3, N5, N6)
- [~] Death notification other systems can subscribe to — published, nothing subscribes yet, so
  nothing in game shows it
- [x] Spawn groups worth fighting, replacing the hardcoded debug row — three groups and thirteen
  monsters in zone 448, respawning, placed in game with `spawngroup` (N14–N16)

[Full detail](streams/m3-resource-payout.md) — 0 of 3 done

- [ ] Uncomment the `ModifyOwnerResources` case (Factory.cs)
- [ ] Pay out on thumper completion, hardcoded per beacon
- [ ] Fill in the grant defs that matter

[Full detail](streams/m4-thump-placement.md) — 0 of 5 done

- [ ] Per-zone resource map ("what's near here")
- [ ] Load node type and yield tables
- [ ] Wire the scan command, send `FoundResourceAreas`
- [ ] Handle `GeographicalReportRequest`, stop hardcoding `Valid = 0`
- [ ] Resolve node type from position instead of the literal `20`

[Full detail](streams/m5-kill-rewards.md) — 0 of 3 done

- [ ] Award XP on kill, track it, push progression updates
- [ ] Work out the client's drop protocol, then spawn loot
- [ ] Pick up a drop and put it in the bag

[Full detail](streams/m6-persistence.md) — 0 of 3 done

- [ ] Decide RIN-owned vs local store, and what happens with no RIN
- [ ] Persist XP, level, inventory and the resource ledger
- [ ] Save on a timer and on logout, not just on zone change

[Full detail](streams/m7-encounter-combat.md) — 0 of 4 done

- [ ] Spawn waves keyed to encounter state
- [ ] Route deaths back to the owning encounter
- [ ] A failure path, not just `OnSuccess`
- [ ] Scale payout to defence performance and M4's yield

[Full detail](streams/m8-session-stability.md) — 0 of 3 done

- [ ] Track sent reliable messages, retransmit on missing ack
- [ ] Confirm the inbound resend path
- [ ] Test under induced packet loss

---

## Completed milestones

- **M1: Confirm the combat models** — COMPLETE 2026-08-11

---

## Deferred

Out of scope because the slice closes without them:

- **PvP** — every player character is faction 1, so `HostilityRules` already forbids it; nothing
  in the loop needs it
- **Other zones** — the server hosts one `ZoneId` per process; a second zone is configuration, not
  code, until zone transitions matter
- **The remaining ~197 aptitude stubs** — implement the ones the slice's abilities actually hit,
  when they turn out to be hit; working the folder alphabetically is unbounded
- **Projectile travel time, gravity, bounce** — hitscan resolves hits correctly; travel time
  changes feel, not whether the loop closes
- **Damage type resistance tables** — loaded but unused; damage lands without them, they make it
  correct
- **Turret damageability** — `Turret_ObserverView` has no health field, so the client doesn't
  model turrets as shootable either; fixing it means protocol work for a small payoff
- **The rest of environmental damage** — the base case is built, not deferred: drowning and melding
  walls both kill now, through
  [HazardSim](../UdpHosts/GameServer/Systems/Hazards/HazardSim.cs), and the checks are
  [E1–E7](In-Game-Tests/Environment.md). What is still out of scope is everything around it —
  **NPCs are exempt from both hazards** (melded creatures live in the melding and no NPC reports a
  water level), vehicles have their own drowning effects (2148, 2149) that nothing applies, retail's
  own status effects are not used in place of the damage PIN applies directly, and the perimeter
  sets are all live at once rather than rotating through melding phases. Also unbuilt: fall damage,
  which is the other environmental death and has no data problem behind it, only nobody has needed
  it
- **Army, mail, market, chat beyond what exists** — social systems, none gate a session
- **Crafting and blueprints** — spending resources needs `RequireResource` and
  `RequireResourceFromTarget`, both stubs, plus `Blueprint_Resources` which nothing reads;
  gathering is the loop, crafting is a second one (see [Restoration](Restoration.md))
- **Server-side terrain** — `LoadMapsCollision` is off, so every raycast in the game sees an empty
  world. The consumer is already built and proven: `ZoneLoader` plus `TagfileLoader` turn Havok
  collision into Bepu statics, and that same path loads deployable collision today. Only the
  extractor from the client's world chunks is missing. Deferred because the slice closes without
  it, and flagged because it would close [DATA-15](ISSUE-REGISTER.md), replace the NPC
  ground-clamping hack with a real downward raycast, and give every shot real cover. See
  [streams/world-authoring.md](streams/world-authoring.md)
- **Public-server hardening** (identity/auth, input validation, topology, ops, distribution) — not
  scheduled and shouldn't start before the slice closes; full detail in
  [streams/public-server-hardening.md](streams/public-server-hardening.md)

---

## Keeping this current

A milestone is done when its exit criterion has been seen in game, not when the code compiles.
Move the check into [Test Register](TEST-REGISTER.md) as the work lands and record the result
there. When a milestone turns out to be two milestones, split it here rather than quietly widening
it, and when something in Deferred becomes necessary, move it up with the reason it changed.
