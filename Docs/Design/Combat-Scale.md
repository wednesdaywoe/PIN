---
project: pin
kind: design
title: Combat Scale
relates:
  - ../Restoration.md
  - ../thumper-encounter-reconstruction.md
  - ../ISSUE-REGISTER.md
---

# Combat Scale

**What a hit is worth, what a body is worth, and how far apart the weakest and strongest things in
the game are allowed to be.**

Every combat number downstream — creature health, wave sizes, weapon damage, thumper difficulty — is
being chosen against these, usually without saying so. This settles them in one place so the next
tuning pass starts from a position rather than rederiving one.

Written 2026-08-16, out of a thumper defence that could not be completed solo and the investigation
that followed.

## Provenance legend

Same tags the [thumper reconstruction](../thumper-encounter-reconstruction.md) uses, plus one.

| Tag | Meaning |
|---|---|
| `[shipped]` | Read out of build 1962's `clientdb.sd2` |
| `[notes]` | Red 5 patch notes |
| `[measured]` | Observed in a PIN session against a real client, with the log to show for it |
| `[derived]` | Worked out from the above |

---

## 1. The diagnosis: two number systems, not one gap

PIN is currently running **beta's creature numbers against retail's player numbers**, and almost
every balance complaint traces back to that.

- The player has **19,192 health** `[measured]`, which is a 1962 figure produced by the level-40
  progression system.
- Creatures are graded through `dbcharacter::MonsterScaling`, and a basic Melded Aranha lands on the
  **level 13** row at 1,224 health `[shipped]`.

That is not a creature ladder anchored slightly low. It is **two separate scales** with nothing
converting between them. The player is roughly **eighteen times** the scale of what shoots at it.

This is the case [Restoration](../Restoration.md)'s rule exists to arbitrate: *numbers from 1962,
design from 0.7*. 19,192 is a 1962 number produced by a system — level-40 progression — that PIN is
explicitly not building. It should be a build override, not an inheritance.

### Why it produces exactly the symptoms observed

| Symptom | Where it came from |
|---|---|
| "Individually, non-threatening" `[measured]` | 49 damage against 19,192 health is 391 hits |
| The thumper dies while the player never does | The machine has 4,000 health; the player has 4.8× that |
| Waves must be absurd before a veteran notices | A table sized to threaten 19,192 erases a newcomer |

The last row is the real cost. **At an 18× band, place-based difficulty cannot function.** Any wave
strong enough to matter to an established player deletes a new one, so there is no single table that
serves both — which is the contradiction that opened this document.

---

## 2. What the two-second anchor does and does not constrain

The one confirmed observation in creature tuning is from beta footage: **a basic creature dies in
about two seconds.** [DATA-6](../ISSUE-REGISTER.md) records how it was reached.

It constrains exactly one ratio:

```
creature_HP / player_DPS ≈ 2 seconds
```

**Player health is not in that equation.** Moving it costs nothing against the anchor, provided
player *damage output* is held. These are not two ends of one lever; the anchor is silent about the
player's own durability and always has been.

Worth stating plainly because treating them as connected is what produced the recommendation to be
cautious about player health, and that caution was unfounded.

---

## 3. The invariant: the thumper is 4,000 and always was

`aptfs::ResourceNodeBeaconCalldownCommandDef` `[shipped]`, all 61 rows:

| Health | Rows |
|---|---|
| 4,000 | 59 |
| 5,000 | 2 |

**The two 5,000s are dead rows.** Both point at beacon **33942** — the same beacon item as five of
the 4,000 rows. Both are the only rows in the table with a 13,000ms calldown where every other row
is 12,000. And both have `death_ability = 0`, which only three rows in the entire table lack. A
thumper with no death ability cannot run its own destruction path, so it was never live. Superseded
revisions of the same thumper, kept in the table.

```
def 167077   health=5000   calldown=13000   death=0        <- dead
def 179223   health=4000   calldown=12000   death=33978
def 179228   health=4000   calldown=12000   death=33978
def 182870   health=4000   calldown=12000   death=0
def 323327   health=4000   calldown=12000   death=33978
def 440854   health=4000   calldown=12000   death=33978
def 440858   health=5000   calldown=13000   death=0        <- dead
```

Discount them and it is **61 of 61 at 4,000**.

### What that implies

Retail never tuned thumper difficulty through the machine. Not the starter thumper, not the best one
you could buy — one health value for every thumper in the game.

So *"the stock thumper is easy for new players"* was never a property of the thumper. It cannot have
been. **It was a property of where you put it**: a new player thumps in a starter zone and the ground
sends weak attackers; a veteran thumps somewhere hard and the ground sends hard ones. Same machine,
same 4,000 health, different neighbours.

That makes 4,000 the fixed point everything else is measured against, and it makes **difficulty a
property of place** rather than of equipment — which is also what the era sources say (§6).

---

## 4. Three independent routes to a player pool of ~1,000

The number to replace 19,192 with is corroborated from three directions that share no assumptions.

**Route 1 — beta trash health.** 0.6 reduced shell-less Hissers and Skivers from 700 to 200
`[notes]`. That is an attested beta figure and it is six times smaller than `MonsterScaling`'s
level-13 row of 1,224 `[shipped]`. With 200-health trash and the two-second anchor, player output is
about **100 DPS**, not the 612 the level-13 row implies.

**Route 2 — the thumper, worked backwards.** 4,000 health, invariant. An undefended thumper dying
around 60% of a 300-second cycle is ~180 seconds, so ~22 DPS incoming with one or two attackers in
contact, so creature output around **15 DPS** — which is 49 damage on a roughly three-second swing.
Run the same creature at the player: three in contact is ~48 DPS, and a twenty-second death gives a
pool of **960**.

**Route 3 — the shipped value.** `dbitems::Battleframe.base_health` reads about **1,000**
`[shipped]`, recorded as [DATA-3](../ISSUE-REGISTER.md) and never explained.

Three routes, one number. Treated as settled enough to build on.

---

## 5. What is actually running right now `[measured]`

All from the 2026-08-16 session, log-backed.

| Quantity | Value | Note |
|---|---|---|
| Player health | 19,192 | inherited, unjustified |
| Player effective output | **~234 DPS** | includes aim and reposition time; peaks ~338 |
| Time to kill one Melded Aranha | **~5 seconds** | 1,224 health at 234 DPS |
| Creature damage | 49 a swing | weapon 20046, ×1.00 at the anchor grade |
| Creature swing interval | **1,280 ms** | shipped `MsPerBurst`, unmodified |
| Creature output | **38 DPS** | 49 ÷ 1.28s |
| Creature movement | **11 m/s** | shipped; crosses the 20m wave ring in 1.5s |
| Thumper | 4,000 health | shipped, invariant |

### The rate finding

Route 2 calls for creature output around **15 DPS** — 49 damage on a three-second swing. PIN ships
**38**. The interval is 2.5× faster than the derivation wants, and nothing chose that; it is simply
`MsPerBurst` read straight through.

**That is the whole thumper problem, and it is not the wave count.** 4,000 health at 15 DPS is 267
seconds of one attacker in contact, which comfortably outlasts a 300-second cycle with the
interruptions a defender provides. At 38 DPS it is 105 seconds.

**The lever is already wired.** `AttackWindow.Resolve` takes a rate-of-fire multiplier and divides
the intervals by it. For creatures it sits at 1.0 for a mundane reason — monster weapons carry no
item attributes, so nothing ever supplies one. A tier-derived rate multiplier drops into exactly the
slot the damage scalar already occupies in `MonsterTier`. No plumbing, only a value.

---

## 6. `MonsterScaling` cannot be the spine

Eighty rows of geometric growth is the shape of a levelled game. You cannot build levelless
difficulty out of it, only disguise it. **"The curve at level 40" is not a fact about this game —
there is no level 40.**

The current tiering is half honest about this already:

- **Damage** is computed as a *ratio of two rows*, so the level system cancels and only the relative
  statement survives. That part is level-free and fine.
- **Health** is `row.Health`, an absolute lookup. Every creature's health is literally a level-table
  entry. That is where levels get smuggled back in.

### The replacement is already in the sources

0.7.1665 defines four difficulty tiers by con colour `[notes]`:

| Con colour | Tier |
|---|---|
| Yellow | Stage I |
| Orange | Stage II |
| Purple | Stage III |
| Red | Stage IV |

Four tiers matched to **gear stages**, not forty rows. And the zone mapping is attested too — New
Eden and Diamondhead are Stage I/II areas, Antarctica and Sargasso are Stage III/IV `[notes]`. That
is "difficulty as a property of place" already documented and already partitioned.

Under four tiers the health lookup disappears entirely: health comes from the tier, the curve is used
for nothing, and the ratio trick is unnecessary because a tier states the relationship directly.

It also collapses the awkward part of the current design. `difficulty_cost` grades 906 creature types
and leaves 2,203 ungraded — an open question in
[§6a of the thumper reconstruction](../thumper-encounter-reconstruction.md). Four tiers is a partition
small enough to fill by hand where the data is silent.

---

## 7. The thesis: a 2–3× band, not eighteen

If Stage IV gear is 140–225% of stock `[notes]`, the entire power band from first login to endgame is
roughly **two to three times**. Not eighteen. Not forty levels.

That is not a compromise. **It is what levelless means**, and it is the only configuration where
place-based difficulty functions:

- A Stage IV zone at twice a Stage I zone puts a new player in real trouble without deleting them.
- A veteran in the starter zone stays engaged rather than invulnerable.

At 18× neither works, which is §1's contradiction. It dissolves once the band is 2–3×.

### Starting table `[derived]`

| | Stock | Stage IV |
|---|---|---|
| Player health | 1,000 | ~2,000–2,250 |
| Player output | ~100 DPS | ~200 DPS |
| Trash creature health | 200 | ~400 |
| Creature output | ~15 DPS | ~30 DPS |
| **Thumper health** | **4,000** | **4,000** |

The thumper stays fixed, which is the point — it is the invariant everything else is measured
against.

### Sanity checks `[derived]`

- Undefended stock thumper, one or two leakers: survives **2–4 minutes**.
- Eight leakers: dead in about **30 seconds**, so any real leak on a hard table is fatal.
- A Stage IV player caught by that same pack of eight: about **17 seconds**. You barely outlast the
  machine you are protecting, which is the right relationship between defender and objective.

---

## 8. Order to change things in

Ordered by how much each needs data that does not exist yet.

1. **Creature rate of fire.** Needs no new data and no new plumbing — a multiplier into
   `AttackWindow.Resolve`. Largest single effect on the thumper. Do this first.
2. **Player health, 19,192 → ~1,000.** Corroborated three ways (§4). A build override under
   [Restoration](../Restoration.md)'s rule, recorded against [DATA-3](../ISSUE-REGISTER.md).
   Hold player *output* while doing it, or the two-second anchor moves.
3. **Four tiers replacing the `MonsterScaling` lookup.** Larger, and it wants the tier-to-creature
   mapping filled in for 3,109 types. Health comes from the tier; `MonsterScaling` stops being read.
4. **Per-zone wave tables.** Once tiers exist, difficulty becomes a property of place and the stock
   thumper is easy because of *where it is*, not because its table is the only one.

---

## 9. Already changed

**Thumper wave table retuned, 2026-08-16.** `Systems/Encounters/Encounters/Thumper.cs`.

The old table stood up ten sappers across four waves and put more than 4,000 damage on the machine,
so a solo completion was not merely hard but arithmetically impossible. A run on 2026-08-15 lost it
at 93% having killed ten attackers.

| | Old | New |
|---|---|---|
| Wave 1 | 20% — 2 sappers | 20% — 1 sapper |
| Wave 2 | 40% — 2 sappers, 1 escort | 45% — 2 sappers |
| Wave 3 | 65% — 3 sappers, 1 escort | 70% — 2 sappers, 1 escort |
| Wave 4 | 90% — 3 sappers, 2 escorts | 90% — 2 sappers, 1 escort |
| **Total** | **10 sappers, 4 escorts** | **7 sappers, 2 escorts** |

The driver is not how many attackers arrive but **how many stand there at once**, because a player
can only shoot one at a time and the rest keep swinging while they wait. Two sappers cost (5 + 10)
seconds of chewing between them; three cost (5 + 10 + 15). That is why the old threes were fatal and
why the new table never exceeds two.

Against a player who engages promptly the new table lands roughly 2,000 of 4,000, finishing at about
half health — the defence is worth something without being required. A slow player doubles that and
loses it.

### Verified in game the same day `[measured]`

Three cycles run solo. The prediction was "about half health"; the measurement is 66% and 52%.

| Run | Completion | Thumper left | Attackers killed | Payout |
|---|---|---|---|---|
| 11:20 | 0.51 — collected early on purpose (G4) | 3657 / 4000 (91%) | 3 | 30 |
| 11:28 | **1.00** | 2628 / 4000 (**66%**) | 7 | 40 |
| 11:35 | **1.00** | 2089 / 4000 (**52%**) | 9 | 24 |

**Two full solo completions where the previous table produced none.** The defence multiplier moved
too — 0.83 and 0.76 — so damage taken cost part of the haul without costing the run, which is the
relationship the event wants. The ask is met.

**This is the starting-zone stock table and nothing else.** It is a holding measure until §8's items
land, and it is the easiest table the event should ever have.

**What it does not fix, and moves the wrong way.** The event still cannot kill the player — a
[B5](../In-Game-Tests/Death-And-Respawn.md) run had to import creatures from a standing spawn group
to die at all, because 49 a swing against 19,192 threatens nothing. Cutting escorts from four to two
made that worse. That is deliberate: this pass was aimed at the machine surviving, and the defender's
survivability is §8 item 2's problem, not a wave table's.

---

## 10. Open questions

- [ ] Which creature types map to which of the four tiers, for 3,109 types
- [ ] Whether `difficulty_cost` survives as the tier input, or whether four tiers make it redundant
- [ ] Player *output* at ~100 DPS — Route 1 derives it but nothing has confirmed it against a weapon
- [ ] Whether the wave spawn ring and creature perception range should be set independently. At 11
      m/s a 20m ring gives 1.5 seconds of warning; widening it past 25m costs escorts their
      perception of the defender, so the two are currently entangled
- [ ] Stage II and III of the band, which §7 interpolates rather than sources
