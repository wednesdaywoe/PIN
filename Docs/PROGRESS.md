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

**M2: NPCs that fight back.** M1 closed 2026-08-11 — all three combat-model questions (faction
stance encoding, range decay curve, whether shields shipped live) answered at the client in one
afternoon, none of them needing the model rewritten. The sessions since then chased a client-side
blocker instead of M2: an intermittent world-entry freeze that survived four rounds of live
debugging (T4–T8) before being localized to a lost wakeup in Wine's fsync path and closed with a
launch-option workaround (T9, `PROTON_NO_FSYNC=1 PROTON_NO_ESYNC=1`, confirmed over four
consecutive sessions — tracked as [CLIENT-1](gaps/client.md) pending further confirmation, per the
commit's own "until proven otherwise"). With that infra blocker out of the way, M2 is next and
unstarted: [AIEngine.Tick](../UdpHosts/GameServer/AIEngine.cs) is still an empty tick, the single
largest gap between the server and a game. See [streams/m2-npc-combat.md](streams/m2-npc-combat.md).

---

## Milestones

[Full detail](streams/m1-combat-models.md) — 4 of 4 done

- [x] Faction stance encoding confirmed (H1)
- [x] Range decay curve confirmed (R1–R2)
- [x] `Battleframe` shield data confirmed absent from build 1962
- [x] Placeholders replaced with shipped values, remaining divergence written down

[Full detail](streams/m2-npc-combat.md) — 0 of 5 done

- [ ] Threat table, target selection, line of sight
- [ ] Leashed steering and ground clamping
- [ ] Server-side attack entry point (abilities preferred, weapon fire as fallback)
- [ ] Death notification other systems can subscribe to
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
