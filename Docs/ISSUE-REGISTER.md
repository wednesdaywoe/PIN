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

**Creatures stopped being interchangeable on 2026-08-15, and the change is small because both halves
shipped.** PIN gave every NPC in the game the same health and the same damage, because the shipped
power curve `dbcharacter::MonsterScaling` is keyed by a creature's *level* and levels were server
content that never shipped. `Monster.difficulty_cost` — the threat grade 906 of 3109 creature types
carry — now supplies that key: the grade picks the nearest row of the curve and the row gives health
and damage together, which is the property that closes
[DATA-6](gaps/data.md#data-6) and [DATA-20](gaps/data.md#data-20) with one lookup where two more flat
constants could not. The only invented step is the conversion, and it has exactly one free parameter,
expressed as a quotient of two constants so that moving it moves the whole ladder and keeps its
shape: `1200 health / grade 20 = 60 health a grade point`, where both numbers come from
[D6](In-Game-Tests/Damage-Loop.md#-d6-a-basic-creature-dies-in-about-two-seconds) rather than from
preference. A grade-300 miniboss lands at 17,334 health against the player's 19,192 — a real fight
that is still winnable — and nothing between the anchor and there had to be argued for separately,
because the shipped grades already order them. Levels stay internal, which is what the
[charter](../README.md)'s leveless goal actually requires and is already how the player character
works. **Neither entry closes.** 2,203 creature types carry no grade at all and still share one pool;
they fall back to the anchor row, so nothing regresses, but grade 0 means *unrated*, not harmless —
`EliteWanderer` sits there. And DATA-20 keeps its own question, sharpened rather than answered: the
tier multiplier is a *ratio* of two curve rows, which cancels whatever unit the `damage` column is
in, but a weapon template shipping `damage_per_round` **1** still rounds to 1 or 2 across every grade
below 65. Verified against the real SDB on the running server; the in-game check is
[D7](In-Game-Tests/Damage-Loop.md#-d7-creatures-are-no-longer-all-the-same-size), which uses two
creatures that share a weapon so the tier is the only thing that can differ.

**The 2026-08-15 sitting's most useful hour was spent on a bug that wasn't one, and it closed
[DATA-1](gaps/data.md#data-1) by reversing its reasoning.** The tester reported `invuln` off but
taking no damage. The server log said otherwise — 9,202 hits landed — and the explanation was that
a 3000-point shield pool PIN adds on purpose absorbed all of it while health sat at 19192. The pool
existed to make damage *observable*; the 1962 client turns out to have no shield display at all, so
it did the exact opposite, and it is 0 now. Chasing it turned up
[DATA-20](gaps/data.md#data-20), the last big combat gap: `MonsterScaling` ships a **damage** column
next to health, keyed by level at a flat 2:1, and PIN scales neither because monsters have no level.
[DATA-6](gaps/data.md#data-6) answered the health half in 2026-08-13 with a flat 500 and left the
damage half alone, which is exactly why monsters stopped being sponges and the player became one.
The weapon pipeline itself was checked and exonerated on the way past — a melee weapon resolving
225 × 0.22 to an observed 49 re-confirms [DATA-11](gaps/data.md#data-11) on a live path.

**The follow-up read the same day found the instrument both halves were missing, and it had been
sitting in a column the project had already looked at once.** `Monster.difficulty_cost` was recorded
in DATA-6 as "an encounter budget rather than a level", which was true and undersold it: read
against `Monster.behavior` — AI script names that survive as plain strings — the rating grades 906
creature types into exactly the tiers a designer would assign, every `*MiniBoss` script sitting at
150–300 and `GiantAranhaMiniBoss` shipped at both 150 and 300. So the relative ordering of every
rated creature is already done, and what is left is anchoring the band, which is two numbers rather
than 906. Such a mapping closes [DATA-6](gaps/data.md#data-6) and
[DATA-20](gaps/data.md#data-20) together, because one row yields health and damage for the same
creature, which no second flat constant can do.

**Then the anchor test was run, and it went the other way — the flat 500 lost the evidence it was
built on.** The 500 came from a tester's beta-video reading that monster 528 died to "a few seconds
of chaingun fire", and that conversion had never been done with a fire rate. The Heavy MG lands 39 a
round every 65 ms, which is 600 dps, so **500 health is 0.83 seconds** — a few seconds is 1,200 to
1,800, or level 13–16. The one-shot half of the same reading cannot be checked as posed: the
mainline plasma cannon (12129, 262 items, the A4→A32 tier line) does 100 a shot behind a 4,000 ms
chargeup and one-shots almost nothing, so a remembered one-shot is far more likely a Tigerclaw
ability — `Pulsar` reads `Base Damage 155 to 17763`. One anchor survives because it is absolute
rather than converted: 200 for shell-less hissers and skivers, from 0.6 patch notes, is level 4
within 3% — though it is a 0.6-era figure measured against 0.6-era weapons, so it evidences the
curve rather than supplying a value PIN can use. **`MonsterMaxHealth` is 1200 as of 2026-08-15**
(user-approved), the conservative end of the 1200–1800 band, chosen from below because N16's sponge
complaint at 2500 marks the known failure direction. It is a falsifiable prediction: a tester
holding the Heavy MG on a rated-20 creature should see about two seconds.

The same read paid the thumper reconstruction, whose §7 pulse sizes were invented for want of a
mechanism: a column named *cost*, graded across 906 creatures, is a spawn currency, and spending
points instead of counting bodies makes "squad thumpers draw more **and bigger** enemies" one dial
instead of two rules. It also gives the concurrent-alive cap a unit that bounds threat rather than
entity count. Recorded as §6a, the document's first `[shipped]`-tagged section.

**A 2026-08-15 offline pass moved three NET entries without a client, and its most useful result is
a theory it killed.** [NET-26](gaps/network.md#net-26) said the client was checking a server-owned
character state PIN never writes; the SDB read it asked for says both failing effects — and 15253,
the charge camera — require `living`, which PIN writes and sends. There is no missing stance flag.
The entry survives, re-pointed at why the client's own answer to that check goes false.
[NET-23](gaps/network.md#net-23) gained a root cause from the client's own Lua: the death screen
opens on character state `incapacitated`, a stage PIN skips entirely by going straight from alive to
`Dead`, so `RespawnTimesData` was the second half of the answer rather than the whole of it. And
[NET-18](gaps/network.md#net-18)'s next comparison ran: the message envelope already matches retail,
but the item's `DynamicFlags` was calibrated against the one single-item add in the capture that is
an outlier — 13 ordinary ones carry `2`, PIN sends `1`. Two dead ends recorded so they aren't
re-run: the capture contains no death at all, and item durability was never a divergence.

**The 2026-08-14 evening sitting closed three NET entries and opened three, all on evidence from a
screen.** Closed: [NET-18](gaps/network.md#net-18)'s question answered and re-scoped in the same
stroke (delivery works; the client declines the *partial* item update, `dbg_inventory resend` is
the workaround, and the remaining defect is confined to the item arrays of one message);
[NET-19](gaps/network.md#net-19), both prediction-gate fixes confirmed by P0; and
[NET-24](gaps/network.md#net-24), the haunted thumper, which really was one scope-out on a channel
that never resent — 2 stale requests against a baseline of 648, model gone from the ground.
Opened: [NET-26](gaps/network.md#net-26), the sitting's biggest find — predicted effects that
require a **server-owned** character state self-cancel while the server keeps the effect, shown on
two effects with different duration classes and bounded by a control case that held —
[DATA-19](gaps/data.md#data-19), Ultimates never recharge, and Hover Mode's lift never engaging
(recorded into [DATA-5](gaps/data.md#data-5)'s territory via P3). The same sitting closed
[M6 and M8](PROGRESS.md) — 13 of 13 test entries — and with them [NET-1](gaps/network.md#net-1)
itself; [NET-25](gaps/network.md#net-25)'s inbound mirror stays deliberately open.

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

[Full detail](gaps/data.md) — 4 of 20 closed

- [x] **DATA-1** — Battleframe shield pool was kept at 3000 instead of build 1962's real 0 as a
  deliberate observability trade-off. **The trade was backwards and it is 0 now** (2026-08-15,
  user-chosen): the 1962 client has no shield display at all — `ShieldBar` is an orphaned texture
  region and both HUD vitals widgets bind health only — so the pool was 3000 invisible hit points
  whose real effect was a 45-second window at the start of every fight where damage lands and
  health doesn't move. A tester read that as a broken `invuln` command on 2026-08-15 and filed it.
  Recharge values (150/sec, 10000ms) are shipped data and unchanged
- [x] **DATA-2** — Thump `nodeType` hardcoded to `20`, every deposit identical. Fixed in code
  2026-08-13 by M4 — node type resolves from the deposit under the calldown, barren ground stays
  20 — and **confirmed in game 2026-08-14** by [S3–S5](In-Game-Tests/Thump-Placement.md): four
  distinct node types resolved from position in one sitting (242, 241, 233, and 20 for barren), and
  the same deposit paid 48 near its center against 33 at 59% out
- [~] **DATA-3** — `Battleframe.base_health` reads ~1000 in SDB vs. 19192 observed live, no scaling
  logic. **Changed 2026-08-16, unverified in game: the player pool is now 1000.** Taken as a build
  override under [Restoration](Restoration.md)'s rule rather than as a correction to the reading —
  19192 was always what the capture said, it is simply the output of a progression system PIN is not
  building. Player damage output is deliberately untouched, so the two-second creature anchor does
  not move. Stays open until a sitting confirms it, and stays open afterwards on the real fix, which
  is reading the shipped column per battleframe instead of carrying one constant. **2026-08-16: the ~1000 is corroborated from two further directions that share no
  assumptions with it** — beta trash health (0.6 cut shell-less Hissers and Skivers to 200, six times
  under `MonsterScaling`'s level-13 row) and the thumper worked backwards (4000 health invariant, a
  ~180s undefended death, ~48 DPS from three attackers, a 20-second player death → 960). Three routes,
  one number. This stopped being an unexplained discrepancy and became **the entry that governs
  combat balance**: 19192 is a 1962 figure produced by the level-40 progression system PIN is not
  building, which is exactly the case [Restoration](Restoration.md)'s numbers-from-1962,
  design-from-0.7 rule exists to arbitrate. See [Combat Scale](Design/Combat-Scale.md)
- [ ] **DATA-4** — Splash and ability-projectile range falloff are guessed/unmodeled, unlike the
  confirmed weapon curve. Only reachable through an ability — weapons never splash at all, see
  **DATA-21**
- [ ] **DATA-5** — 3797 of 3812 server-side aptitude command defs are empty stubs
- [~] **DATA-6** — Monster health/shields are hardcoded placeholders, not read from SDB. One flat
  pool for every creature, and at 2500 that was ~64 player rifle shots each — the "bullet sponge"
  reading [N16](In-Game-Tests/NPC-Combat.md) came back with. Researched 2026-08-13: `MonsterScaling`
  shipped whole (80 levels, 100..153726 health) but is keyed by level, and level was server content.
  Dropped to 500 on 2026-08-13, then **raised to 1200 on 2026-08-15** when the beta-video reading
  that produced the 500 was re-converted with a real rate of fire — 600 dps of Heavy MG makes 500
  health 0.83 seconds, not the "few seconds" the video showed. 1200 is two seconds, the conservative
  end of a 1200–1800 band. Still one number for every creature, so the entry stands, and the new
  figure is unconfirmed in game.
  **2026-08-15 found the missing input's substitute.** `Monster.difficulty_cost` is not merely an
  encounter budget as this entry first read it: against the `behavior` script strings it grades 906
  creature types into the same tiers a designer would, with every `*MiniBoss` script at 150–300 and
  `GiantAranhaMiniBoss` shipped at *both* 150 and 300. It orders every creature for free, leaving
  only the band anchor to choose. **That anchor is now the open part, and the 500 lost its own
  evidence.** Converting the beta-video reading ("a few seconds of chaingun fire") through 1962's
  real rates — Heavy MG 39 a round every 65 ms, so 600 dps — makes 500 health **0.83 seconds**, not
  a few. A few seconds is 1,200–1,800, or level 13–16. The one-shot half can't be checked: the
  mainline plasma cannon does 100 a shot behind a 4,000 ms chargeup, so a remembered one-shot was
  almost certainly a Tigerclaw ability (`Pulsar` reads 155 to 17,763). Surviving anchor: 200 for
  shell-less hissers, from 0.6 patch notes, = level 4. `MonsterAttributeRange` looks like a better
  answer and is not: `per_level` is 0.0 in all 167 rows.
  **Built the same day and the entry is now partly closed.**
  [`MonsterTier`](../UdpHosts/GameServer/Systems/Combat/MonsterTier.cs) converts the grade into a
  level by taking health as proportional to it — a column named *cost*, graded across 906 types, is
  what a spawn budget spends, so the grade is what the designers priced the creature at — and the
  constant of proportionality is not chosen but falls out of D6's tested anchor: 1200 health at grade
  20 gives **60 health a grade point**. The grade then picks the *nearest shipped row* rather than
  becoming health directly, which puts every creature on a level Red 5 actually shipped and hands
  back `damage` from the same row. 906 types are tiered; **2,203 carry no grade and still share one
  pool**, falling back to the anchor row so nothing regresses — but 0 means unrated, not harmless
  (`EliteWanderer` rates 0), which is what keeps this open. Verified against the real SDB on the
  running server, 13 unit tests, in-game check is
  [D7](In-Game-Tests/Damage-Loop.md#-d7-creatures-are-no-longer-all-the-same-size)
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
  [G4](In-Game-Tests/Resource-Payout.md) is written to take that reading.
  **The side-by-side ran 2026-08-16 and confirms the split, but rules out the mechanism this entry
  assumed.** Early collection played the full launch animation; the ordinary departure shot straight
  up with none, in the same session on the same deposit. However **both abilities carry a
  `NO-OP (Client PlayAnimation)`** in their chains, so `34216` is not missing an animation step and
  swapping the ability is not obviously the fix. What differs is the status effect applied — 123
  applies effect **154**, 34216 applies effect **155** — which puts the launch on the *effect*, not
  the ability. `34216` additionally runs an `ImpactRemoveEffectCommand` that fails outright
  (`Don't know which effect to remove`) with both 154 and 155 active. Next step is to read effects
  154 and 155, not to hunt a missing animation command. A second report the same
  day — "sound effect and launch prep animation played, thumper remained on the ground" — is a
  different fault on the same event and belongs to [NET-24](#networking--protocol--net): the
  departure the client plays is the animation, and the model staying behind is the client never
  being told the entity is gone

- [ ] **DATA-19** — No ultimate-charge model: slotting an Ultimate empties its charge meter and
  nothing ever refills it, so every Ultimate is a one-way trip to unusable. Found 2026-08-14 by
  [P1](In-Game-Tests/Prediction-Sweep.md): module 141814 (the second Charge, ability 41232) is an
  Ultimate, slotting it emptied the meter, and the in-combat refill the client expects never came —
  which blocks P1 outright and any other sweep candidate that turns out to be an Ultimate. Retail
  charged the meter through combat activity; whatever stat or message carries that gain, PIN never
  sends it. Same family as [DATA-7](gaps/data.md#data-7)'s hardcoded progression: a regeneration
  the server is supposed to drive and doesn't

- [~] **DATA-20** — Monster damage is never scaled by level, so most NPC gunfire lands for **1
  point**. Measured 2026-08-15: 8,977 hits of 1 damage against 265 of 49, and a player takes 4m40s
  to die from 19192 health ([DATA-3](gaps/data.md#data-3)). **PIN's resolution is correct** — the
  melee weapon reads 225 × 0.22 = 49 against an observed 49, and the rifle's `damage_per_round` is
  genuinely `1` in shipped `WeaponTemplates`. What's missing is that
  `dbcharacter::MonsterScaling` carries a **`damage` column beside `health`**, keyed by level at a
  flat 2:1 ratio across all 80 rows, and PIN scales neither because a monster has no level. So this
  is [DATA-6](gaps/data.md#data-6)'s twin: that entry dropped health to a flat 500 (~level 8) on
  2026-08-13 and the damage half was never given the same treatment, which is how monsters stopped
  being sponges while the player became one. **2026-08-15: a `difficulty_cost`-to-level mapping
  closes both halves at once** — the rating picks the row and the row gives health *and* damage from
  the same creature, which a second flat number cannot do. Open first: whether that column is per
  round, per burst, or a budget. The 2:1 ratio holding on all 80 rows without one exception now
  argues for a budget — per-shot damage has to interact with fire rate and clip size, and those vary
  too much across templates to survive `damage = health / 2`.
  **Built the same day, and it sidesteps that question rather than answering it.** The tier scalar is
  a **ratio of two curve rows**, not the column's absolute value, so whatever unit the column is in
  cancels — the shipped 2:1 curve is used only for the thing it unambiguously encodes, how much more
  dangerous one level is than another. Applied in `GetEffectiveWeaponDamage`, the single funnel every
  damage path already resolves through, and kept in a **separate field** from
  `WeaponDamageMultiplier` because that one is saved and restored by the `SetWeaponDamage` ability and
  would destroy a tier written into it. **What survives is sharper than what it replaced**: the rifle
  that named this entry ships `damage_per_round` 1, so ×1.64 rounds it to **2** — a real doubling that
  is still 2 against 19,192 — and every grade below 65 rounds a unit template to 1 or 2, collapsing
  the shipped ladder to two damage values at the bottom. So the open question is no longer "what unit
  is the column" but "can a template value of 1 mean one point at all", and that is now the only
  thing keeping this entry from closing
- [x] **DATA-21** — A weapon's blast radius is shipped and never read, so no weapon splashes.
  **Built and verified in game 2026-08-16**, both the same day. The evening run fired a Grenade
  Launcher between two Melded Aranha and the log holds the shape this entry was opened for:

  ```
  [19:25:27 DBG] CharacterEntity (…305280) took 350 damage … 873 health left
  [19:25:27 DBG] CharacterEntity (…305536) took 38 damage … 1186 health left
  [19:25:27 DBG] Splash from Main 78062 (Type 11 - Grenade Launcher (secondary))
                 (Frag Grenade (no DoT)) at 4m caught 1 entities beyond the direct hit
  ```

  Two entities off one round, 350 to the one struck and 38 to the one standing near the edge of a
  4m blast, and the direct target appears exactly once — the blast skipped it rather than paying it
  twice, which was the failure mode most likely to be silently wrong. Fired again a second later
  for the identical pair of numbers, so the falloff is stable rather than a one-off.
  **Reading the result in game is the hard part, and it is not this system's fault**: the shipped
  client only draws a creature's health bar while your aim is on it, so the entity the splash
  reaches is by definition the one whose bar is hidden (see
  [the method notes](In-Game-Tests/README.md#health-bars-are-aim-driven-so-splash-cannot-be-read-off-them)).
  The `Splash from` line is the reading, not the screen.
  `WeaponSplash` resolves the ammo columns and
  `ProjectileSim` runs a second pass over everything in reach of the impact, sharing `SplashFalloff`
  with the ability path so both fall off the same way. The load-bearing change is underneath it: a
  shot that struck the world used to be discarded by the raycast, which is why the grenade did
  nothing — the impact position is now reported with no entity attached and the weapon path decides
  whether a miss still explodes. Three readings of the shipped columns are pinned by tests because
  each could have gone the other way: **-1 is a sentinel** (3 rows, all "Desecrated" ammo), **a
  radius of 0 means no blast** so "Normal Bullet (assault rifle)" and 648 other rows are untouched,
  and **`max_hits` is not a splash cap** — it reads 1 on 584 of the 612 rows that do carry a radius,
  including every thrown grenade and mortar shell, so it counts projectile penetration instead.
  `dbg_weapon` now prints the resolved radius and what the blast is worth at four distances, which
  is how to tell a weapon that cannot splash from one that should have.
  [V7](In-Game-Tests/Deployables-And-Vehicles.md) and [D3](In-Game-Tests/Damage-Loop.md) are the
  check and both are rewritten to run against a weapon.
  `dbitems::Ammo.impact_radius` carries a real radius on **612 of 1264 ammo rows**, from 2m up to
  30m, and PIN loads the record without ever looking at that field. `ProjectileSim.TryResolveHit`
  traces one ray and damages exactly what it struck; there is no second pass over anything nearby.
  Found in game 2026-08-16 by a tester firing a Grenade Launcher between two touching enemies and
  killing neither — the grenade hit the ground, the ground is not damageable, and the shot was a
  miss. **Ability splash is a different path and does work**: `InflictDamageCommand` reads
  `Splashrange` (set on 2081 of 2449 damage steps) and applies `SplashFalloff`, so the falloff
  curve [DATA-4](gaps/data.md#data-4) questions is only ever reached through an ability. Closing
  this means giving the weapon path the same second pass, keyed on the ammo radius rather than on a
  command def. It also blocks [V7 and D3](In-Game-Tests/Deployables-And-Vehicles.md) from being run
  with a weapon at all

## Networking & Protocol — NET

[Full detail](gaps/network.md) — 6 of 26 closed

- [x] **NET-1** — No retransmit queue; "reliable" only acked, never resent. Built 2026-08-14:
  `RetransmitQueue` holds every Matrix and ReliableGss packet until the client acks it and resends
  after 450ms, giving up loudly after three attempts, every constant measured off the 2016 capture
  rather than chosen. **Verified the same day by [L1–L6](In-Game-Tests/Reliability.md), 6 of 6**:
  ten minutes at 5% induced loss played indistinguishably from a clean session while ~2,550
  resends did the repair work — 94% accepted on the first attempt, none reaching attempt 3,
  nothing abandoned, the queue peaking at 82 unacked. Closing it also closed
  [NET-24](gaps/network.md#net-24). Its inbound mirror, [NET-25](gaps/network.md#net-25), remains
  deliberately open
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
- [ ] **NET-18** — re-scoped 2026-08-14 from "unverified" to a confirmed live defect: **the
  client declines to merge PIN's partial item `InventoryUpdate`** — a created item appears only
  after `dbg_inventory resend` pushes the full inventory. Confirmed twice in one sitting (20003,
  then 86074). Everything else is ruled out: the item struct is right (the full send lists it, the
  garage equips it, flags round-trip), fullness is dead
  ([I3](In-Game-Tests/Inventory.md) measured no ceiling), and resources merge fine (G1), so the
  defect is confined to the item arrays of the partial message. **Capture message [9] compared
  2026-08-15**: the envelope is already retail's exactly, but message [9] is the session's one
  outlier single-item add and PIN was calibrated on it — 13 ordinary adds carry `DynamicFlags = 2`
  (guessed `is_new?`) where PIN sends `1`. Untested against a client. Durability ruled out. The
  `createitem` → `resend` workaround unblocks the queue meanwhile
- [x] **NET-19** — Two 2026-08-11 prediction fixes (certificates, XP entity id) confirmed by
  [P0](In-Game-Tests/Prediction-Sweep.md) on 2026-08-14: the cert-gated 86074 slots on the
  Dreadnaught and every garage frame reads level 45
- [~] **NET-20** — The 47-effect Prediction Sweep is almost entirely unverified
- [x] **NET-21** — An encounter throwing from `Tick` killed the whole shard thread; the Coral Forest
  thumper did it every session via a null participant, a set mutated mid-iteration, and no isolation
  around the update loop. Fixed 2026-08-12, verified headless
- [x] **NET-22** — Nothing replicated an NPC's pose: the movement view is not flushed and no pose was
  sent on its behalf, so both position and aim only reached the client when a keyframe corrected
  them. Monsters teleported, and fired at where you used to be for seconds before snapping round —
  the burst itself travels on the combat view and arrived on time. Damage was always resolved from
  the live direction. Fixed 2026-08-13 with `NpcPose`, confirmed the same day by N8–N13 passing

- [x] **NET-24** — A finished thumper never left the client: paid out and removed server-side, it
  stayed standing on screen while the client asked for a fresh keyframe of the dead entity every
  ~5.5 seconds forever — 648 failed requests across four full-cycle thumpers in one 32-minute
  sitting, growing by one loop per thumper. A code read pinned it as [NET-1](gaps/network.md#net-1)
  in costume — one scope-out, sent once, on a channel that never resent — and landed three changes
  alongside the retransmit queue. **Closed 2026-08-14 by
  [L6](In-Game-Tests/Reliability.md)**: the first post-fix full-cycle thumper left 2 stale requests,
  static on a re-read, and the ground was empty when the tester returned. Which change did it —
  the retransmit queue or the failed-request-answers-with-scope-out fallback — is undetermined;
  [G6](In-Game-Tests/Resource-Payout.md)'s Debug-line dating remains the optional measurement, and
  the full history is in [the gap entry](gaps/network.md#net-24)

- [~] **NET-23** — A dead player had no way back: `Die` ran correctly and `RequestRespawn` was
  implemented, but the client never sent it, so death ended the session in a reconnect. Found by
  [N16](In-Game-Tests/NPC-Combat.md) on 2026-08-13, the first time the game rather than a command
  killed a player. **Root-caused 2026-08-15 from the client's own `Bleedout.lua`**: retail's death
  is two stages, and the screen carrying the give-up prompt opens only on character state
  `incapacitated`. PIN went straight from alive to `Dead`, so the screen never opened and
  `RespawnTimesData` — this entry's original suspect — never got a chance to be read; it feeds the
  countdown once the screen is already up. `CharacterStatus` has seven values and PIN wrote four.
  **Built the same day, unverified**: a player now goes to `Incapacitated` with the `respawn_input`
  permission and a `RespawnTimesData` pair, taps out on Reload, and is respawned after 30s by
  `BleedoutSim` if they don't. **Run the same day and both routes pass**:
  [B1](In-Game-Tests/Death-And-Respawn.md) on the give-up key (down 11:17:02, respawned 11:17:05)
  and B3 on the fallback (down 11:03:51, respawned 11:04:22), with both sessions ending in menu
  logouts rather than the reconnect every previous death forced. **The symptom is gone.** B1 also
  retires the units guess: the tap-out gate reads the same clock value the countdown does, so a
  prompt appearing on time proves the client converts absolute shard times. Stays `[~]` on the
  three entries still unrun — the visible countdown, monsters dying properly, and dying mid-thumper.
  The 2016 capture cannot help — nobody dies in it

- [ ] **NET-25** — An ack claims a packet that never arrived. `Channel` acks the highest inbound
  sequence it has seen rather than the highest with no gap behind it, so a lost client packet is
  reported to the client as received and never resent — the exact mirror of NET-1 on the inbound
  side, and it survives NET-1's fix. Found by reading on 2026-08-14 and deliberately not fixed the
  same day: holding the ack at a gap is easy, and what to do when the gap never fills has no
  evidence behind it, since a wrong answer stalls the channel for the session rather than losing one
  message. Costs a lost shot or interaction about as often as the link drops a reliable packet,
  which on loopback is never

- [ ] **NET-26** — A predicted toggle cancels itself while the server still holds the effect.
  Found by [P2](In-Game-Tests/Prediction-Sweep.md) on 2026-08-14, the first prediction entry run
  after P0 opened the gate: Turret Mode (effect 10810) engages on keypress and shuts off seconds
  later, unprompted. The client log shows apply, the server's confirmation landing (`too many
  stacks`), then removal and cancel with no toggle-off pressed; the server log shows 10810 set and
  **never cleared**, its companions 10812–10815 each set and cleared within ~40ms, and 1184 gone
  after 1.5s. The mirror image of the stuck camera ([D5h](In-Game-Tests/Charge-Camera.md)): there
  the client kept an effect the server had dropped, here it abandons one the server keeps.
  **Confirmed a class bug the same sitting**: Frontline Medic I (effect 2322, FRAME+CSTATE, no
  `serverconfirmed`) self-cancels identically, so the common denominator is the `requirecstate`
  check — the client requiring a character state the server never sets — and the
  missing-confirmation theory is out as the common cause. The critical experiment —
  [P3](In-Game-Tests/Prediction-Sweep.md) Hover Mode, whose required state (airborne) the client
  tracks locally — ran the same sitting and **held for the whole flight**, so the failing CSTATEs
  are specifically **server-owned states** the server never sets. A post-sitting server-log read
  sharpened it further: Turret's chain set its effects in full and the client cancelled anyway,
  Medic's chain set nothing and the client cancelled, Hover's chain set nothing and the client
  held — so a status effect is neither necessary nor sufficient, and the missing thing reads as a
  character **state** (the stance/mode machine, [NET-12](gaps/network.md#net-12)'s family) that
  retail's server drove and PIN never writes. **That SDB read ran 2026-08-15 and refutes it**:
  10810, 2322 and 15253 all require `living`, one of the four states PIN does write and send.
  There is no missing stance flag, so the fault is in why the client's own answer to that check
  goes false — its value, its `Time` stamp, or when it was last delivered. `CharacterStateData`
  carries a `Time` alongside the state and [NET-2](#networking--protocol--net)'s ~65-second wrap
  was sighted in the same log window, which is the first thing to rule out. (Hover's missing lift
  is a separate gap, likely [DATA-5](gaps/data.md#data-5), recorded in P3)

## Client & Environment — CLIENT

[Full detail](gaps/client.md) — 2 of 3 closed

- [~] **CLIENT-1** — World-entry freeze traced to a lost wakeup in Wine's fsync path; closed via
  `PROTON_NO_FSYNC=1 PROTON_NO_ESYNC=1`, confirmed over 4 sessions, not yet proven un-recurring
- [x] **CLIENT-2** — HTTPS login fails under Proton's WinHTTP; closed via all-HTTP client APIs
  plus a binary patch (must be reapplied after Steam file verification)
- [x] **CLIENT-3** — **FIXED & VERIFIED 2026-08-15.** A Melded Aranha rendered about 90° off while
  a Chosen faced correctly, which read as a per-model forward axis until the 2016 capture was
  measured (`CaptureReplay --facing`): every rig retail ever oriented — 40+ monster types and the
  clients' own remote players — sits at exactly −90° under PIN's +X-forward reading. There was
  never a per-model offset: PIN's convention was 90° wrong for every character, the client's local
  forward is +Y, and the Chosen only looked right because a humanoid's rendered body follows the
  aim vector closely enough to mask the quaternion. `Facing.Towards` now yaws a quarter turn short
  of the bearing, the convention client-authored orientations already use. Verified same day:
  Aranha attack head-on, and the watchtower thumper's own wave sappers face the machine correctly,
  which checks the convention against a non-character objective too

---

## Method notes

[Full detail](gaps/method-notes.md) — how an entry gets found, the split between this register and
the progress ledger, and what "closed" means when the underlying bug is an intermittent race.

*Update this register whenever an audit is rerun or a recorded issue changes state. An entry
leaving this file must leave as fixed-with-a-guard, not silently deleted.*
