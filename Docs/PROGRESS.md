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

**M2: NPCs that fight back — three of five pieces in, and two of them confirmed against a real
client.** Perception and target selection, the attack pass and death notification are all built
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

**Locomotion is now built too, and is the thing to take to the game machine next.** An NPC steers
straight at its target, stops short at a distance its weapon can reach, and walks back to where it
spawned when it loses interest or chases too far; its speed comes off its chassis, which is where
`dbmonster`'s -1s were pointing all along. Height is the part with no good answer — the server holds
no terrain to clamp to, so an NPC takes its target's footing as the ground and refuses slopes too
steep to be ground. [N8–N12](In-Game-Tests/NPC-Combat.md) are written and unrun; the arithmetic is
covered offline in `SteeringTests`. That leaves the spawn groups as the only piece of M2 with no code
at all. See [streams/m2-npc-combat.md](streams/m2-npc-combat.md).

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

[Full detail](streams/m2-npc-combat.md) — 2 of 5 done

- [x] Threat table, target selection, line of sight (N1, N4, N7)
- [~] Leashed steering and ground clamping — built and unit-tested, awaiting N8–N12. No terrain to
  clamp to, so height comes off the target's own footing instead
- [x] Server-side attack entry point — weapon fire, since `dbmonster` names no abilities to prefer
  (N2, N3, N5, N6)
- [~] Death notification other systems can subscribe to — published, nothing subscribes yet, so
  nothing in game shows it
- [ ] Spawn groups worth fighting, replacing the hardcoded debug row

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
- **Public-server hardening** (identity/auth, input validation, topology, ops, distribution) — not
  scheduled and shouldn't start before the slice closes; full detail in
  [streams/public-server-hardening.md](streams/public-server-hardening.md)

---

## Keeping this current

A milestone is done when its exit criterion has been seen in game, not when the code compiles.
Move the check into [Test Register](TEST-REGISTER.md) as the work lands and record the result
there. When a milestone turns out to be two milestones, split it here rather than quietly widening
it, and when something in Deferred becomes necessary, move it up with the reason it changed.
