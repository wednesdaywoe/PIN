# Thumper Encounter — Reconstruction & Tuning Reference

**Target era:** beta 0.7 design (0.6.x–0.7.169x spawn director)
**Status:** reconstructed. No authoritative table survives.

## Provenance legend

| Tag | Meaning |
|---|---|
| `[notes]` | Official Red 5 patch notes (project files) |
| `[period]` | Contemporaneous community source, era-dated |
| `[late]` | Community source from 1.x, may not back-port to 0.7 |
| `[derived]` | Inference or arithmetic by us — not attested |
| `[shipped]` | Read out of the 1962 client database — data, not testimony |
| `[open]` | Unresolved |

---

## 0. Why this one is hard

Thumper wave composition and cadence were **server-side encounter definitions**. Update 1.6
removed the beta thumper encounter outright — 1.6 notes state that thumping no longer yields
minerals at all pending crafting's return, leaving only Crystite nodes `[notes]`. So there is
no client-side table to recover the way there is for weapon and frame stats.

This is the same inverted sourcing hierarchy as the progression system: **patch notes and
period guides outrank the 1962 dump here**, because the dump postdates the system's removal.

---

## 1. Chronology — the system was rebuilt mid-beta

Critical for source triage. A large fraction of written material about thumping describes a
system that no longer existed by 0.7.

| Era | Thumper set | Cycle | Encounter state |
|---|---|---|---|
| 0.5 (→ Mar 2013) | 3 types: Standard / Improved / Advanced | Variable: ~3 / ~4 / ~5 min `[period]` | Original |
| **0.6 (Mar 2013, "March Milestone")** | 8 types: Personal / Squad × 4 | Uniform 5 min | **Rebuilt** `[notes]` |
| 0.7.1665–1666 (Jun–Jul 2013) | Renamed Stock / I / II / III `[notes]` | 5 min | Con-colour tiers added |
| 1.0 (Jul 2014) | Same 8, capacities rescaled | 5 min `[late]` | Vein/lockout retune |
| 1.6 (2015) | — | — | Mineral thumping disabled `[notes]` |

**0.6 is the break point.** Its notes state that creatures spawned by the thumper became "much
more region-specific" and that "the balance and timing of all thumper spawns have been
adjusted," alongside thumper health changes, new creatures, and changed creature spawn
positions `[notes]`.

**Consequence:** the Feb–Mar 2013 guides (isitworthplaying's 10,000 / 12,500 / 15,000 HP and
250 / 1,500 / 4,500 capacities; GuideScroll's Stock/Improved/Advanced breakdown) describe the
**superseded** system. Useful for tracking how numbers moved. Not valid for 0.7.

### Capacities — conflicting figures, both real

| Thumper | 0.7.1666 `[period]` | 1.0/1.1 `[late]` |
|---|---|---|
| Stock Personal | 250 | 250 |
| Personal I | 400 | 275 |
| Personal II | 650 | 300 |
| Personal III | 1,000 | 325 |
| Stock Squad | 350 | 300 |
| Squad I | 600 | 325 |
| Squad II | 900 | 350 |
| Squad III | 1,250 | 375 |

Not a contradiction — a rescale between 0.7 and 1.0. **Use the left column.**

---

## 2. Cycle length — the 7.5-minute question

Every source that states a duration says **five minutes**, from 0.6 through at least Aug 2014
`[period]` `[late]`. PIN's client is **1.7.1962**, which postdates the 1.6 gutting of thumping.
A 450 s timer is therefore not automatically wrong; it just isn't the 0.7 number.

Ranked hypotheses:

**H1 — Timer includes post-completion grace.** A completed thumper auto-returns roughly two
minutes after reaching 100% if unattended `[period]`. 300 s drill + 150 s grace = 450 s exactly.
If the grace was 2.5 min rather than 2, the fit is perfect.
→ **Diagnostic:** watch capacity. If it caps at T+5:00 while the timer runs to 7:30, the drill
is still 300 s and nothing is wrong.

**H2 — Genuine 1.7 value (build drift).** By 1.6 thumping was vestigial. Systems that stop
being load-bearing get retuned or left in odd states. 450 s may simply be what 1962 carries.
→ Per the project anchor (*numbers from 1962, design from 0.7*), this is a case where the two
disagree and the anchor resolves to **0.7 design**: implement 300 s, log 450 s as a
`BUILD_OVERRIDE` diff with rationale.

**H3 — Different encounter entirely.** 1.6 introduced the **Accord Thumper** event at
watchtowers — a Skydock supply mechanic with its own warmup period, and 1.6.1942 specifically
fixed a bug where its timer failed to display `[notes]`. This is *not* the player-calldown
thumper. If PIN's encounter is wired to the 1.6 definition, 7.5 min belongs to a different system.
→ **Diagnostic:** does it have a warmup phase before drilling starts? Accord Thumpers did.

**H4 — Unit or rate mismatch.** 7.5 / 5 = exactly 1.5, which is a suspiciously clean ratio —
the signature of a tick-rate mismatch (20 Hz vs 30 Hz) or a frames-vs-seconds conversion.
→ **Diagnostic:** time the countdown against a wall clock. If it ticks at 1× and merely starts
at 450, it's a value problem, not a rate problem.

**H5 — Re-differentiated by tier.** Pre-0.6 duration varied by size; 0.6 unified it. 1.x may
have re-split it.
→ **Diagnostic:** compare Stock Personal vs Squad III timers.

### Why it matters less than it looks

**Key the pressure curve to capacity percentage, not seconds.** That is what the original
system did (§3), and it makes the 300-vs-450 question orthogonal to balance. If you tune on
progress fraction, changing the cycle length changes pacing and reward rate but not encounter
shape.

If you *do* run 450 s with an unchanged per-pulse curve `[derived]`:
- Total bodies spawned rises ~50%
- Required DPS-on-structure for the same fail-point drops ~33% (a 15,000 HP thumper dying at
  60% needs 83 DPS at 300 s, but only 56 DPS at 450 s)
- You would need to raise spawn rate or lower thumper HP to preserve the intended
  finish-at-critical-integrity feel

---

## 3. Architecture: progress-gated

The single most important structural fact, from a guide updated specifically for 0.7.1666:
waves spawn **based on the thumper's progress**, and larger thumpers spawn proportionately
more hostiles while the pattern stays approximately the same `[period]`.

Fill rate is fixed and independent of player performance, so progress and wall-clock run
together. Why key on progress anyway? `[derived]`

1. **The encounter is a fixed-duration siege with a continuous cash-out decision.** Skill
   cannot shorten the thump. The only levers are integrity and *when you pull out*. Early
   extraction pays proportionally to completion.
2. **It lets the pressure curve be tuned against the payout curve — and it clearly was.**
   Reaching 100% doubles yield, and the bonus is not split among squad members `[period]`.
   The boss wave sits at **70–75%** `[period]`. That is the point of maximum regret: bail and
   forfeit a 2× multiplier you're three-quarters of the way to; push and risk the thumper.
   The boss wave is not late — it is placed where the gamble bites hardest.

A second source puts the escalation slightly earlier, at 60–70%, with multiple Chosen drop
pods `[late]`. Treat **60–75% as one escalation band** rather than picking.

Bookends:
- **Ramp-in:** no burst at drop. Ambient aggro plus a trickle.
- **Terminal wave:** bigger thumpers spawn a wave at 100% that can interfere with sending the
  thumper back safely `[period]`. Without it the last quarter is a victory lap; with it,
  extraction stays a step.

---

## 4. Phase model `[derived]`

Ten pulses at ~10% intervals lands the known events on clean decade boundaries, which is why
this is the starting shape. At 300 s that's one pulse per ~30 s.

| Progress | @300 s | @450 s | Composition |
|---|---|---|---|
| 0–10% | 0:00–0:30 | 0:00–0:45 | Ambient aggro only |
| 10–35% | 0:30–1:45 | 0:45–2:38 | Weak tier trickle |
| 35–50% | 1:45–2:30 | 2:38–3:45 | Weak + normal; **first Explosive Aranha pulse** |
| 50–60% | 2:30–3:00 | 3:45–4:30 | Normal + first large (sieger, thresher) |
| 60–75% | 3:00–3:45 | 4:30–5:38 | Heavy ramp; second explosive pulse; drop pods possible |
| **70–75%** | ~3:30–3:45 | ~5:15–5:38 | **Boss wave** |
| 75–99% | 3:45–4:57 | 5:38–7:26 | Sustained heavy |
| 100% | 5:00 | 7:30 | Terminal wave (tier II/III only) |

Model as **continuous trickle + discrete pulses**, not pure waves — later guides describe mobs
spawning continuously at randomised predefined spawn locations around the thumper `[late]`.

---

## 5. Composition

Tiering per the best 0.7-era source `[period]`:

| Tier | Members | Notes |
|---|---|---|
| Weak | Skivers, shell-less hissers, worker aranhas | Highly AoE-susceptible; one grenade clears a swathe. **HP anchor: 200** (0.6 reduced shell-less Hisser and Skiver from 700 → 200) `[notes]` |
| Normal | Regular hissers, culexes, aranhas | |
| Large | Stormer and sieger aranhas, threshers (all types), nautili | Ranged threat; engage at perimeter |
| Boss | Terrorclaws, rageclaws, giant culexes | Up to ~6 terrorclaws on the largest thumpers |
| Universal | Explosive Aranhas | Several waves **regardless of terrain** |

**The Explosive Aranha exception is the tell.** Terrain-independence means there were two
spawn sources: a **biome table** and a **fixed encounter table**. Explosive Aranhas are the
encounter's designated pressure spike — their job is punishing anything that reaches the drill —
and that job cannot be delegated to local fauna. `[derived]`

Relevant 0.6 change: Explosive Aranhas killed *by players* detonate small; only self-destructs
are full-size `[notes]`. This is what makes them a skill check rather than a coin flip.

### Biome tables `[period]`

| Biome | Roster |
|---|---|
| Coastal | Skivers, nautili, tidal scavengers, argonauts |
| Inland | Aranhas (worker/toxic/icy/geyser/stormer/sieger), hissers, threshers, culexes, wasps |
| Melding-adjacent | Tortured Souls, Grunts, Melded Hissers, Melded Scorchers, Melded Culexes; plus Chosen Assault Troopers, Juggernauts, Cannoneers, Explosive Melded Aranhas |

Meld thumping swaps the roster entirely but **follows the same curve** `[period]`. Same
director, different table.

Melding-pocket zones (Sargasso Sea, Diamond Head, Antarctica, 0.7.1665 `[notes]`) reportedly
did *not* follow the New Eden heavy-wave pattern `[period]` — a separate table. `[open]`

---

## 6. Scaling knobs

Four, and they do different jobs:

1. **Thumper tier (Stock / I / II / III)** — scales count *and level*, not kind. Higher tiers
   spawn the same creatures scaled up, readable via con colour; scaled enemies deal more damage
   and take more to kill `[period]`. Colours per 0.7.1665 `[notes]`: **yellow = Stage I,
   orange = II, purple = III, red = IV**. (One period source writes "pink" for the third —
   same slot, different colour name. Minor conflict, recorded not resolved.)
2. **Personal vs Squad class** — a *composition* multiplier, not just a count one: squad
   thumpers attract more **and bigger** enemies `[period]`.
3. **Biome / melding table** — roster swap.
4. **Repeat-thump escalation** — the first drop gives no substantial boss wave, but the second
   or third drop on the same location brings a Chosen wave roughly one minute before
   completion, potentially six drop pods `[late]`.
   **No 0.7-era corroboration found.** May be a 1.x addition. `[open]`

Supporting evidence that player-count tuning was explicit in the encounter system: 0.7.1665
notes that some ARES missions had spawning logic adjusted "to prevent unintended ambushes with
multiple players," and that ARES icons were updated to reflect tuning for player composition
`[notes]`. The thumper's recommended-player count (1, 1–2, 4–5) is that same dial exposed in
the item name.

---

## 6a. `difficulty_cost` — a shipped spawn currency `[shipped]`

Added 2026-08-15. This section is the only part of the document backed by data out of the 1962
dump rather than by period sources, and it changes what §7 has to guess.

`dbcharacter::Monster` carries a **`difficulty_cost`** column: one integer per creature type, 28
distinct grades from 1 to 1,000, non-zero on **906 of 3,109** types. It is not health and not a
level. Read against the `behavior` script names — which are plain strings in the same table — it
grades creatures the way §5 does by hand:

| Rating | Behaviour scripts in the band | §5 tier this matches |
|---|---|---|
| 20–30 | unnamed wanderers, `StockMelee` | Weak |
| 50–100 | `EliteWanderer`, `ThresherSpitter`, `ThresherTailWhipper` | Normal / Large |
| 120 | `SiegebreakerWithCharge`, `Brontodon` | Large |
| 150–300 | every `*MiniBoss`: `GiantAranha`, `LandShark`, `CrystalAranha`, `RaiderBaron` | Boss |

**The name is the argument.** A column called *cost*, graded and assigned across 906 creature types,
is a spawn currency: a director is handed an allowance and buys bodies until it runs out. That is a
mechanism, and §7's pulse sizes are currently `[derived]` guesses precisely because no mechanism
survived.

What it buys, in order:

1. **§6 knob 1 (thumper tier) and knob 2 (Personal vs Squad) collapse into one dial.** Raise the
   per-pulse allowance and a director spending it produces **more enemies and bigger ones** with no
   second rule — which is exactly what the period source says squad thumpers did (§6). "More *and*
   bigger" stops being two tuning problems.
2. **§7's "pulse size 2–3 bodies × recommended-player-count" becomes points, not bodies.** A pulse
   that buys 100 points spends it on four rating-25 skivers or one rating-100 thresher, and the
   encounter stays legible either way.
3. **The 70–75% boss wave (§3) is expressible as a budget spike** rather than a hardcoded creature
   list, which lets the same curve drive every biome table in §5.
4. **§7's concurrent-alive cap gets a natural unit.** Capping spent-and-alive points rather than
   bodies bounds the actual threat instead of the entity count, and it bounds server load better
   too, since one mini-boss is not one skiver.

Two cautions before building on it:

- **2,203 types rate 0**, and that set includes `EliteWanderer` and `RaiderBaronMiniBoss` entries
  that are plainly not harmless. A 0 most likely means "not placed by the budgeted spawner" —
  mission and story NPCs — rather than "no threat". Any director reading this column needs a
  fallback for unrated types instead of treating 0 as free.
- **The rating orders creatures; it does not price them in health.** Converting a rating to hit
  points goes through `MonsterScaling`, and the anchoring for that is
  [DATA-6](gaps/data.md#data-6), not this document. §5's own **200 for shell-less hissers and
  skivers** `[notes]` is the one anchor that holds: it is level 4 on that curve, within 3%, and
  because it is an absolute figure from patch notes rather than a converted time-to-kill it does not
  depend on which build's weapons you measure against. A second anchor — a tester's beta-video
  reading of a small aranha dying to "a few seconds of chaingun fire" — was checked on 2026-08-15
  against 1962's real rates (Heavy MG: 39 a round every 65 ms = 600 dps) and puts that creature at
  **1,200–1,800**, level 13–16, not the ~500 it had been recorded as. `[open]`

**The pricing half is no longer open — PIN built it on 2026-08-15.**
[`MonsterTier`](../UdpHosts/GameServer/Systems/Combat/MonsterTier.cs) converts a rating into a level
by taking health as proportional to the rating, at **60 health a rating point**, and then snapping to
the nearest shipped `MonsterScaling` row. That constant is the tested anchor written as a quotient
(1,200 health at rating 20), not a preference. So a director written against this document can now
ask the code what a rating is worth in health and damage instead of inventing it — which matters most
for §7, where a wave's cost in rating points can be checked against what that wave will actually do
to a thumper. The 0-rated fallback this section warned about is implemented as the warning suggested:
unrated types get the anchor row and the server says so in the log rather than treating them as free.

`[open]` Whether `difficulty_cost` was the *thumper's* currency specifically or a shared
encounter-wide one. Nothing ties it to thumping in particular; the inference is from the column's
name, its grading, and the absence of any other candidate.

## 7. Pressure budget `[derived]`

The structure-damage side is the one place real numbers can be worked backwards.

- 0.5-era Advanced: 15,000 HP over a 300 s cycle `[period]`.
- For an undefended thumper to die *before* completion: aggregate incoming > **50 DPS**.
- To die around 50–60% completion (plausible design target): **~85–100 DPS** sustained.
- Skill ceiling marker: the achievement "Return 10 Full Advanced Thumpers at 1% Health"
  `[notes]` — a full defended thump should routinely end near-critical.
- **Integrity does not affect yield.** Full value even finishing at 1% health `[period]`.
  Integrity is a pure binary fail-state, not a soft-fail resource. Deliberate: it makes the
  last 30 seconds maximally tense with no partial-credit softening.

### Suggested starting config

| Parameter | Value | Basis |
|---|---|---|
| Spawn points | 6–8, ring at 30–60 m | `[derived]`, matches "randomised predefined spawn locations" |
| Pulse interval | every 10% progress | `[derived]` |
| Pulse size | 2–3 bodies × recommended-player-count | `[derived]` |
| Concurrent alive cap | 15–25 | `[derived]`, perf-bound (0.5.1494 AI perf, 0.7.1679 server scalability `[notes]`) |
| Boss pulse @ 70% | 1–2 (personal) → 6 (Squad III) | `[period]` upper bound |
| Placement lockout | 100 m radius, 30 s | `[period]` (1.0 raised to 150 m `[notes]`) |
| Vein radius | ~235 m | `[period]` ~500 m diameter; 1.0.1791 reduced 235 → 225 m `[notes]` — consistent |

---

## 8. Open questions

- [ ] Exact pulse interval; fixed or jittered
- [ ] Concurrent-alive cap value — **now has a unit** (§6a): cap spent points, not bodies
- [ ] Whether repeat-thump escalation existed pre-1.0
- [ ] Whether the 100% terminal wave was gated by capacity or by class
- [ ] What "region-specific" meant mechanically in 0.6 — per-zone table, or nearest-ambient sample
- [ ] Melding-pocket wave pattern (explicitly different, never documented)
- [ ] **Resolve the 450 s timer** (§2 diagnostics)
- [ ] Whether `difficulty_cost` was the thumper's currency or a shared encounter-wide one (§6a)
- [ ] What a rating of 0 means on 2,203 creature types, including ones that are plainly dangerous
      (§6a) — "unplaced by the budgeted spawner" is the working reading, unconfirmed

Pulse **composition** is no longer fully open: §6a supplies a shipped per-creature rating, so a
director can be written against real relative weights rather than invented body counts. What stays
open is the allowance curve, which is a tuning question rather than a recovery one.

### Recovery lead

Even though encounter logic was server-side, the 1962 client may carry orphaned assets —
pre-1.6 weapon templates survived in that dump, so encounter stubs may too. Suggested grep
terms: `thumper`, `resource_extract`, `encounter`, `spawn_group`, `wave`, plus the Explosive
Aranha asset name.

**Partly paid off already.** The 2026-08-15 sweep that found `difficulty_cost` (§6a) also confirmed
the creature AI scripts survive as readable strings in `dbcharacter::Monster.behavior`, complete with
their tuning parameters — `Arch_Charger_Base(chargeCommitDist=8, chargeWindupDuration=1000, ...)`,
`GiantAranhaMiniBoss(eggSackCooldown=10000)`, `PoisonSalamander(cloakAbilityId=82626, ...)`. That is
per-creature encounter behaviour that nobody had catalogued, and it is a better lead than the asset
grep: the *creatures* of the wave survived even though the *wave* did not.

One table that looks like a lead and is not: `dbcharacter::MonsterAttributeRange` has a `per_level`
column, and it is 0.0 in all 167 rows. Ruled out, see [DATA-6](gaps/data.md#data-6).

---

## Sources

- Red 5 patch notes: 0.5.1475, 0.5.1494, 0.5.1524, 0.6, 0.6.1621, 0.6.1637, 0.6.1641,
  0.7.1665, 0.7.1666, 0.7.1672, 0.7.1679, 1.0.1791, 1.6.1942, 1.6.1946, Update 1.6 Parts 2–3
- Astrek Association, *The Firefall Thumping Guide* (updated for 0.7.1666, Jul 2013) — primary
  0.7-era source
- IsItWorthPlaying, *Thumping* / *Meld Thumping* (Feb 2013) — 0.5-era
- GuideScroll, *Thumping Detailed Guide* / *Meld Thumping Strategy Guide* (Mar 2013) — 0.5-era
- GameplayInside, *Thumping: How to use a thumper* (Aug 2014, rev. Jun 2015) — 1.x
- Firefall Wiki (Fandom archive), *Thumping* / *Thumper*
