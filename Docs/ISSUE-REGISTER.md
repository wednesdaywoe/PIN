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

**Two days of sittings on, 2026-08-14, and every entry that moved moved on evidence.**
[DATA-2](gaps/data.md#data-2) closed when four node types resolved from position in one sitting,
[DATA-17](gaps/data.md#data-17) when the zone's richest deposit turned out to be sitting inside the
starting station's no-thumping zone and was moved onto ground the client agrees can be thumped, and
[NET-22](gaps/network.md#net-22) when N8–N13 passed against replicated NPC poses. That is
4 of 18 DATA closed, 3 of 25 NET, 1 of 3 CLIENT.

**[NET-3](gaps/network.md#net-3) is the first entry ever closed by the packet capture rather than by
a sitting or a code read**, and it is worth knowing the shape of it. The entry existed because
`Channel` carried a TODO asking whether its own resend decoding was right and nobody had a case that
could answer it. The capture is that case: 24 resent packets, 15 of them with the original alongside,
all 15 byte-identical once the XOR is undone. Reading it also found two faults sitting next to the
one being confirmed — a resend that was recognised and then handled a second time anyway, and a
resent split fragment that threw on the shard thread the way [NET-21](gaps/network.md#net-21) did.
Neither would have been found by running the code, because neither happens on a link that doesn't
drop anything. **[NET-1](gaps/network.md#net-1) came out of the same read**, built the same day and
calibrated entirely off that capture; it stays `[~]` until [L1](In-Game-Tests/Reliability.md) plays a
session under induced loss.

**[NET-25](gaps/network.md#net-25) is the one that same read opened and left open, on purpose.** PIN
acks the highest sequence it has seen rather than the highest with no gap behind it, so a lost client
packet is reported back as received and the client never resends it — NET-1's own failure, inbound,
and untouched by NET-1's fix. Holding the ack at the gap is a small change. What to do when the gap
never fills is not, and the capture only ever shows retail's client acking, never retail's server
recovering, so there is nothing to calibrate the giving-up half against. Stalling a channel for the
rest of a session is a worse outcome than losing one message, which is the whole reason this is a
register entry today rather than a commit.

**The two entries opened in the same period are the ones worth reading**, because neither was found
by reading code: [NET-24](gaps/network.md#net-24), a finished thumper the client leaves standing in
the world forever while asking for keyframes of it every 5.5 seconds, and
[DATA-18](gaps/data.md#data-18), the hardcoded ability that sends it away. They come off the same
`OnInteraction`/`OnUpdate` split, and NET-24's code read makes it a likely costume for
[NET-1](gaps/network.md#net-1) — one scope-out sent once on a channel that acks but never resends.
That channel resends now, so [L6](In-Game-Tests/Reliability.md) is written to settle it either way:
a count that drops to zero closes NET-24, and one that keeps climbing retires the theory, which is
worth as much.

**What this register is short of is a re-audit, not entries.** Everything below was mined in one
sweep on 2026-08-12 across [the progress ledger](PROGRESS.md)'s milestone write-ups, the
[Architecture Guide](Architecture/README.md), the [Test Register](TEST-REGISTER.md)'s incident
narratives (Charge-Camera and Transport-And-Lifecycle especially), and a grep for
TODO/hardcoded/guess markers across the codebase. Only entries a sitting touched have been looked at
since, so the parts of the codebase the test queue hasn't reached are still on their first pass.
M6 landed the first system PIN has ever written that owns data of its own, and this register still
has no entries in that category, which reflects nothing having run rather than nothing being wrong.

---

## Static Data — DATA

[Full detail](gaps/data.md) — 4 of 18 closed

- [x] **DATA-1** — Battleframe shield pool kept at 3000 instead of build 1962's real 0, a
  deliberate observability trade-off, one-line revert if fidelity wins later
- [x] **DATA-2** — Thump `nodeType` hardcoded to `20`, every deposit identical. Fixed in code
  2026-08-13 by M4 — node type resolves from the deposit under the calldown, barren ground stays
  20 — and **confirmed in game 2026-08-14** by [S3–S5](In-Game-Tests/Thump-Placement.md): four
  distinct node types resolved from position in one sitting (242, 241, 233, and 20 for barren), and
  the same deposit paid 48 near its center against 33 at 59% out
- [ ] **DATA-3** — `Battleframe.base_health` reads ~1000 in SDB vs. 19192 observed live, no scaling
  logic
- [ ] **DATA-4** — Splash and ability-projectile range falloff are guessed/unmodeled, unlike the
  confirmed weapon curve
- [ ] **DATA-5** — 3797 of 3812 server-side aptitude command defs are empty stubs
- [ ] **DATA-6** — Monster health/shields are hardcoded placeholders, not read from SDB. One flat
  pool for every creature, and at 2500 that was ~64 player rifle shots each — the "bullet sponge"
  reading [N16](In-Game-Tests/NPC-Combat.md) came back with. Researched 2026-08-13: `MonsterScaling`
  shipped whole (80 levels, 100..153726 health) but is keyed by level, and level was server content.
  **Dropped to 500 on 2026-08-13** (~13 shots, about level 8 on the shipped curve), which fixes the
  slog without fixing the entry — it is still one number for every creature, and unconfirmed in game
- [ ] **DATA-7** — Character level comes from `HardcodedCharacterData`, not real progression
- [ ] **DATA-8** — Three aptitude commands read a hardcoded constant instead of their def parameter
  (muzzle offset, GlobalCooldown, ForcePush force)
- [~] **DATA-9** — Two web endpoints return invented or hardcoded-stub character data
- [ ] **DATA-10** — NPC perception, standoff and leash tuning are invented. Researched 2026-08-13:
  retail's numbers ship inline on 2100 of 3109 `dbmonster` rows, the instance table behind the other
  695 never shipped to the client at all, and PIN's 40m perception is wider than every
  `perceptionDist` in the file
- [x] **DATA-11** — A zero in a `WeaponTemplateModifiers` multiplier column was read literally and
  annihilated the stat, resolving 269 weapons to no range and 220 to no damage; found by
  [N2/N6](In-Game-Tests/NPC-Combat.md), fixed 2026-08-12
- [ ] **DATA-12** — Melding wall damage rate is invented; no melding-wall effect survives in the
  client db to read one from, unlike drowning's 787/789
- [ ] **DATA-13** — The water description nibble indexes per-zone map data the server doesn't have,
  so every body of water is read as the standard row 10001; [E1](In-Game-Tests/Environment.md)
  confirmed zone 448 uses at least two of them
- [ ] **DATA-14** — No ammo or reload model, so an NPC weapon with a small clip fires continuously
  and its damage comes out inflated — monster 281's sniper reads 2000 dps against a real ~450
- [ ] **DATA-15** — Zone 448's spawn group placements, pack sizes and respawn delays are PIN's own
  content; retail's spawn tables were server-side and nothing in the client db or the zone file
  holds a monster placement. Open by necessity, not pending work
- [ ] **DATA-16** — `dbitems::LootTable.roll_mode` is unmapped, so how a loot table's entries combine
  is inferred from the tables' own arithmetic; costs drop rates, not correctness, and
  [K2](In-Game-Tests/Kill-Rewards.md) is the only measurement available

- [x] **DATA-17** — Deposit 1, the zone's richest, sat inside the starting station's no-thumping
  zone: its center was 14.7m from outpost 17's, and every scan taken on the shelf on 2026-08-13 came
  back from the client as `NOTHUMPINGZONE` before the server saw a position. **Moved the same
  evening to 199.8, 315.7** — one of three coordinates the client itself answered `OK` at that
  session, 67m from the station and walked, so it is confirmed thumpable rather than merely clear of
  a base. Renamed Basin Head Crystite. The other three deposits have never been scanned and could
  have the same problem; a barren reading now logs the nearest deposit and its distance so finding
  out costs one press of G

- [ ] **DATA-18** — Three of a thumper's four state-change abilities are literals in PIN's source
  rather than reads from the beacon's own def: `34579` at the end of warm-up, `34215` at the end of
  thumping, `34216` when a completed thumper departs. Only the early-collection path reads data —
  `OnInteraction` fires the beacon's `CompletedAbility` when you cut a thumper short. **Reported
  2026-08-14**: a thumper collected early played its full extraction animation with sound, while
  every thumper sent away the ordinary way shot upward instantly and silently. That is exactly the
  split between the two paths, so the hardcoded `34216` is the suspect and the shipped
  `completed_ability` (123 on all 40 `aptfs::ResourceNodeBeaconCalldownCommandDef` rows) is what
  retail played. Cosmetic, and a one-line read to fix once a side-by-side confirms it —
  [G4](In-Game-Tests/Resource-Payout.md) is written to take that reading. A second report the same
  day — "sound effect and launch prep animation played, thumper remained on the ground" — is a
  different fault on the same event and belongs to [NET-24](#networking--protocol--net): the
  departure the client plays is the animation, and the model staying behind is the client never
  being told the entity is gone

## Networking & Protocol — NET

[Full detail](gaps/network.md) — 3 of 25 closed

- [~] **NET-1** — No retransmit queue; "reliable" only acked, never resent. **Built 2026-08-14**,
  unverified in game: `RetransmitQueue` holds every Matrix and ReliableGss packet until the client
  acks it and resends after 450ms, giving up loudly after three attempts. Every constant in it was
  measured off the 2016 capture rather than chosen — 24 resends across 456619 sub-packets say the
  timeout is 322–665ms (median 452), the header's resend count is always 3, a resend is byte-identical
  to its original, and an ack is cumulative.
  [L1–L6](In-Game-Tests/Reliability.md) are the check and L1 is the exit condition
- [ ] **NET-2** — `CurrentShortTime` wraps every ~65 seconds, already a known source of bugs at one
  player
- [x] **NET-3** — Inbound resend detection / XOR decode correctness was unverified, and the 2016
  capture settled it on 2026-08-14: 24 resent packets, and the 15 whose original also survives decode
  to byte-identical payloads. Reading it found two live faults beside it — a recognised resend was
  decoded and then handled a second time, so anything the client resent ran twice, and a resent
  fragment arriving mid-split threw out of `SortedDictionary.Add` on the shard thread, the same shape
  as NET-21. Both fixed, neither seen in game
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
- [~] **NET-18** — `createitem` makes the item; whether the client ever shows it is unproven.
  2026-08-13 with the Scan Hammer (56826): toast fired, no item found, and `dbg_inventory` then
  proved the server side is fine — 246 items before, **247 after**, logged as
  `createitem 56826 as item, item type Weapon, x1`. So this is a display or lookup question, not a
  creation one, and it has two live suspects that the next sitting can separate:
  (a) the partial `InventoryUpdate` is declined — PIN's fresh item differed from the 2016 capture's
  only live single-item add (82337) in exactly one field, `DynamicFlags = IsBound`, now matched and
  untested; (b) nothing is wrong and a Weapon simply lands in the **Gear** sub-inventory among the
  246 pieces every hardcoded loadout brings, where it is indistinguishable from clutter.
  `dbg_inventory <typeId>` now answers (b) directly and `equipitem <typeId> [slot]` bypasses the
  browsing entirely by slotting it server-side — the Scan Hammer was equipped that way the same
  evening, so (b) is looking likely. `removeitem` undoes a session's mistakes
- [~] **NET-19** — Two 2026-08-11 prediction fixes (certificates, XP entity id) await confirmation
- [~] **NET-20** — The 47-effect Prediction Sweep is almost entirely unverified
- [x] **NET-21** — An encounter throwing from `Tick` killed the whole shard thread; the Coral Forest
  thumper did it every session via a null participant, a set mutated mid-iteration, and no isolation
  around the update loop. Fixed 2026-08-12, verified headless
- [x] **NET-22** — Nothing replicated an NPC's pose: the movement view is not flushed and no pose was
  sent on its behalf, so both position and aim only reached the client when a keyframe corrected
  them. Monsters teleported, and fired at where you used to be for seconds before snapping round —
  the burst itself travels on the combat view and arrived on time. Damage was always resolved from
  the live direction. Fixed 2026-08-13 with `NpcPose`, confirmed the same day by N8–N13 passing

- [ ] **NET-24** — A finished thumper never leaves the client. Reported 2026-08-14: "on sending
  thumper away, sound effect and launch prep animation played, thumper remained on the ground."
  The server removes the entity and pays out correctly; the client keeps its copy and asks for a
  fresh keyframe of the dead entity's `ResourceNode_ObserverView` every ~5.5 seconds, **forever**.
  One 32-minute sitting accumulated **648 failed requests across four abandoned thumpers**
  (259/218/134/37, each rate-constant from the moment its thumper was removed), and the loop grows
  by one thumper each time. The server is answering correctly — `NetworkClient` masks the
  controller byte and looks up an entity that is genuinely gone — so this is a scope-out that never
  arrives or never takes. **Not universal**: the two thumpers cut short by a player that day left no
  stale requests at all, and only the four that ran their full cycle did, which points at the same
  `OnInteraction`/`OnUpdate` split as [DATA-18](#static-data--data) rather than at removal itself.
  No session has yet been shown to end because of it, but the 2026-08-14 client quit with two loops
  outstanding. **A code read the same day cleared the message and moved the suspicion to the
  channel**: msg 6 on the sole view is how the 2016 capture shows retail removing a single-view
  entity, and nothing else in the server ever names a thumper's entity id — so the likeliest reading
  is that this is [NET-1](gaps/network.md#net-1) in costume, one scope-out sent once on a channel
  that acks but never resends. The client's `AddView` at state 7 is the server *answering* a request
  during `LEAVING`, which puts the loop before removal rather than after it and makes the split a
  question of how many `UnreliableGss` deltas each path sends (~106 against ~7). Three changes
  landed, unverified: a failed keyframe request for a view now answers with a scope-out instead of
  only warning, successful requests log at Debug so the loop can be dated, and a queued scope-in
  whose entity died is dropped. [G6](In-Game-Tests/Resource-Payout.md) is the check

- [ ] **NET-23** — A dead player has no way back. `Die` runs correctly and `RequestRespawn` is
  implemented, but the client never sends it, so death ends the session. Found by
  [N16](In-Game-Tests/NPC-Combat.md) on 2026-08-13, the first time the game rather than a command
  killed a player. Suspect `RespawnTimesData`, which `Die` never writes

- [ ] **NET-25** — An ack claims a packet that never arrived. `Channel` acks the highest inbound
  sequence it has seen rather than the highest with no gap behind it, so a lost client packet is
  reported to the client as received and never resent — the exact mirror of NET-1 on the inbound
  side, and it survives NET-1's fix. Found by reading on 2026-08-14 and deliberately not fixed the
  same day: holding the ack at a gap is easy, and what to do when the gap never fills has no
  evidence behind it, since a wrong answer stalls the channel for the session rather than losing one
  message. Costs a lost shot or interaction about as often as the link drops a reliable packet,
  which on loopback is never

## Client & Environment — CLIENT

[Full detail](gaps/client.md) — 1 of 3 closed

- [~] **CLIENT-1** — World-entry freeze traced to a lost wakeup in Wine's fsync path; closed via
  `PROTON_NO_FSYNC=1 PROTON_NO_ESYNC=1`, confirmed over 4 sessions, not yet proven un-recurring
- [x] **CLIENT-2** — HTTPS login fails under Proton's WinHTTP; closed via all-HTTP client APIs
  plus a binary patch (must be reapplied after Steam file verification)
- [ ] **CLIENT-3** — A Melded Aranha renders about 90° off the orientation it is sent; a Chosen in
  the same place faces correctly, so it is the creature model's forward axis, not the server's
  maths. Nothing PIN can read carries a per-model facing offset. Cosmetic — shots are aimed from
  live positions, never from the rendered facing — and left open rather than papered over with an
  invented table

---

## Method notes

[Full detail](gaps/method-notes.md) — how an entry gets found, the split between this register and
the progress ledger, and what "closed" means when the underlying bug is an intermittent race.

*Update this register whenever an audit is rerun or a recorded issue changes state. An entry
leaving this file must leave as fixed-with-a-guard, not silently deleted.*
