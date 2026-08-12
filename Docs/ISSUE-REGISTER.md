---
project: pin
kind: register
title: Issue Register
id-prefix: DATA / NET / CLIENT
relates:
  - PROGRESS.md
  - TEST-REGISTER.md
---

# Issue Register

The audited state of everything PIN gets wrong today, on purpose or not: invented static data,
hardcoded stand-ins, protocol divergences, and environment bugs in the real client under Wine.
Every known issue is either closed with a guard, or recorded here with its severity and what it's
waiting on. The bar is "no unrecorded unknowns," not "no issues" — a recorded, understood issue is
an acceptable state; an unknown one is not. Full narrative for every entry lives in
[gaps/](gaps/). How an entry gets found, and the difference between this register and
[PROGRESS.md](PROGRESS.md), is in [gaps/method-notes.md](gaps/method-notes.md).

## Current frontier

**First pass, 2026-08-12.** This register didn't exist before today — everything below was mined
in one sweep across [the progress ledger](PROGRESS.md)'s milestone write-ups, the
[Architecture Guide](Architecture/README.md), the [Test Register](TEST-REGISTER.md)'s incident
narratives (Charge-Camera and Transport-And-Lifecycle especially), and a grep for
TODO/hardcoded/guess markers across the codebase. Two entries are closed with a guard
([DATA-1](gaps/data.md#data-1)'s deliberate shield-pool divergence, [CLIENT-2](gaps/client.md#client-2)'s
HTTP-only login patch); one more is closed pending further confirmation
([CLIENT-1](gaps/client.md#client-1), the Wine fsync world-entry freeze — four clean sessions on
record, not yet certain it can't recur). Nothing below has been re-audited yet; this is the
baseline the next pass diffs against.

---

## Static Data — DATA

[Full detail](gaps/data.md) — 1 of 9 closed

- [x] **DATA-1** — Battleframe shield pool kept at 3000 instead of build 1962's real 0, a
  deliberate observability trade-off, one-line revert if fidelity wins later
- [ ] **DATA-2** — Thump `nodeType` hardcoded to `20`, every deposit identical (scheduled as M4)
- [ ] **DATA-3** — `Battleframe.base_health` reads ~1000 in SDB vs. 19192 observed live, no scaling
  logic
- [ ] **DATA-4** — Splash and ability-projectile range falloff are guessed/unmodeled, unlike the
  confirmed weapon curve
- [ ] **DATA-5** — 3797 of 3812 server-side aptitude command defs are empty stubs
- [ ] **DATA-6** — Monster health/shields are hardcoded placeholders, not read from SDB
- [ ] **DATA-7** — Character level comes from `HardcodedCharacterData`, not real progression
- [ ] **DATA-8** — Three aptitude commands read a hardcoded constant instead of their def parameter
  (muzzle offset, GlobalCooldown, ForcePush force)
- [~] **DATA-9** — Two web endpoints return invented or hardcoded-stub character data

## Networking & Protocol — NET

[Full detail](gaps/network.md) — 0 of 20 closed

- [ ] **NET-1** — No retransmit queue; "reliable" only acks, never resends (scheduled as M8)
- [ ] **NET-2** — `CurrentShortTime` wraps every ~65 seconds, already a known source of bugs at one
  player
- [~] **NET-3** — Inbound resend detection / XOR decode correctness unverified
- [~] **NET-4** — `MTUProbe` received and silently dropped, no response sent
- [ ] **NET-5** — Oversized UGSS messages needing RGSS split aren't handled
- [ ] **NET-6** — Physics material id 0 has no fallback, drops hit attribution
- [ ] **NET-7** — HKX loader desyncs shape child index, can misattribute headshots
- [ ] **NET-8** — Shapeless tagfiles silently fall back to a placeholder box collider
- [ ] **NET-9** — Entity scope-in bypasses proper tick logic via a marked `TEMP: Hack`
- [~] **NET-10** — `ScopeRange == 0` semantics unconfirmed
- [ ] **NET-11** — Weapon ammo overrides aren't applied when resolving fired ammo
- [~] **NET-12** — `MovementState` packed wider than before, flagged unresolved in its own comment
- [ ] **NET-13** — Vehicle seat assignment blocks the last seat on some vehicles; entry does a
  blunt view refresh
- [ ] **NET-14** — `SpawnDeployable` computes a faction then discards it, using `DefaultFaction`
  directly
- [ ] **NET-15** — `OrientationLockCommand` sends an unpaired `ForcedMovementCancelled`, deferred
  by design
- [ ] **NET-16** — Predicted effects reach the owning client twice, likely diverging from retail
- [~] **NET-17** — `LocalEffectsData.Entity` semantics (initiator vs. target) unconfirmed
- [~] **NET-18** — `createitem`'s wire-format fix unverified against the original delivery symptom
- [~] **NET-19** — Two 2026-08-11 prediction fixes (certificates, XP entity id) await confirmation
- [~] **NET-20** — The 47-effect Prediction Sweep is almost entirely unverified

## Client & Environment — CLIENT

[Full detail](gaps/client.md) — 1 of 2 closed

- [~] **CLIENT-1** — World-entry freeze traced to a lost wakeup in Wine's fsync path; closed via
  `PROTON_NO_FSYNC=1 PROTON_NO_ESYNC=1`, confirmed over 4 sessions, not yet proven un-recurring
- [x] **CLIENT-2** — HTTPS login fails under Proton's WinHTTP; closed via all-HTTP client APIs
  plus a binary patch (must be reapplied after Steam file verification)

---

## Method notes

[Full detail](gaps/method-notes.md) — how an entry gets found, the split between this register and
the progress ledger, and what "closed" means when the underlying bug is an intermittent race.

*Update this register whenever an audit is rerun or a recorded issue changes state. An entry
leaving this file must leave as fixed-with-a-guard, not silently deleted.*
