---
project: pin
kind: stream
title: "Crafting: the second loop"
id-prefix: CRAFT
thesis: >
  A player can turn thumped resources into an item they equip: fabrication runs
  server-side over the surviving protocol, a curated recipe set resolves ingredients
  to outputs, a working panel reaches it in the client, and the crafted item
  persists like any other.
satisfied-when: CRAFT1..CRAFT5 all [x]
relates:
  - ../PROGRESS.md
  - ../Restoration.md
  - solid-world.md
  - ../ISSUE-REGISTER.md
---

# Crafting

**Opened 2026-08-19, the day the vertical slice closed** (decision 2026-08-19, user-chosen: new
stream with new goals rather than a ninth milestone on a finished plan). The
[charter](../Restoration.md) ordered the loops "thumping first, crafting second" and gave the
reason: the economy decision is easier to make once resources actually exist in the world. They do
now — thumping is built, verified, and paying out on real ground.

This draft is a proposal. The items below come from the charter's own survival audit; the user
edits before any of it is scheduled.

## What the audit already settled (all checked 2026-08-13/14, recorded in [Restoration.md](../Restoration.md))

- **Only the screen is gone.** v1.6 deleted the crafting panel's Lua, but the full Fabrication
  command set survives in both the client binary and AeroMessages, and the engine still exports
  `GetRecipeIds`/`GetRecipeInfo` — `Mainframe.lua` still calls them. Missing Lua can be authored;
  a dead engine feed could not be. Crafting is the recoverable case.
- **No obligation to the whole recipe set.** 9,228 blueprints ship and 5,052 have a resolvable
  output and ingredients. A curated subset that makes the loop work is a complete answer.
- **Fidelity is not a constraint.** Repurposing the surviving `Tinkering` panel or authoring a
  minimal one are both legitimate; the charter says so in as many words.
- **The server side has named stubs.** `RequireResource` and `RequireResourceFromTarget` are
  stubs, and `Blueprint_Resources` is shipped data that nothing reads. The work has an address.

## Items

**Reordered 2026-08-21** (decision 2026-08-21, user-chosen): **CRAFT5 runs next, ahead of CRAFT3 and
CRAFT4.** The reason in the user's words — the whole endeavour hinges on whether you can go out into
the world, thump for resources, fight, return and craft an item from what you mined. Deciding an
economy before knowing that loop closes is arranging furniture in a house nobody has walked through.

**And it turns out no economy decision is needed to run it.** The ground already pays a material that
recipes already want: node types 235, 237, 238 and 240 yield **Iron Bars** (item 86667), which sit in
material class **128, "Metals"** — the class **263 blueprints** ask for, **157 of them priced in that
class and nothing else**. The run uses **82081**: 50 Metals, no item ingredients, one output, Cryogenic
Recharger I. Thumping pays 6-20 Iron Bars a run, so the loop is three to eight thumps long, which is a
loop rather than a formality. Every piece of that is shipped data and already-built code.

**Those four node types are the whole list, and that is a finding rather than a detail**
([DATA-27](../ISSUE-REGISTER.md), 2026-08-21). The other **34 node types pay ore in class 3288 "Raw
Metals"** — Iron, Tungsten, Titanium, Uranium Ore — and **no blueprint asks for class 3288 at all**.
Refining ore into bars was the molecular printer's job and the printer's panel ships empty, so the
raw tier is the input to a deleted step. The earlier end-to-end thumping run stood on node 233 and
filled the bag with a material nothing can spend. CRAFT5 must stand on 235/237/238/240, and CRAFT3
inherits the question of what PIN does about the other 34.

**What changed under it on 2026-08-21, after the entry was first written.** The loop no longer has to
be read out of a log. `/mats` ([MatList](../../Client/Addons/MatList), PIN's own panel) lists every
material held, iconed and counted, and redraws the moment a payout lands — a thumper paying 21 Iron
Ore was watched arriving live. Kills drop health and ammo on the ground to be walked over
([m5](m5-kill-rewards.md)), so defending the machine is a fight with a floor under it, and a kill can
drop equipment into the bag as well. The run is now watchable end to end by a player rather than
verifiable by a maintainer.

- [x] **CRAFT1** — **The client surface.** Settled 2026-08-20 by reading the binary, not by a spike:
  748 exported `Game.*` names contain no fabrication sender, so no panel can reach fabrication and
  the question reverses — the server drives, the client displays. CRAFT1b proved the client still
  accepts a server-authored recipe list (254 named rows, 2026-08-21).
- [x] **CRAFT2** — **Fabrication server-side.** Passed in game 2026-08-21. Ingredients are spent and
  the output arrives; `RequireResource` is real; `Blueprint_Resources` has a reader.
  **CRAFT2b** followed the same day: raw-material costs are priced in material *classes*, and a class
  line draws across every member at once. Two premises in the original entry were wrong and are
  recorded in [DATA-26](../ISSUE-REGISTER.md) and the run sheet.
- [ ] **CRAFT5** — **The loop closes in game.** One sitting: author a deposit on node type **238**,
  thump it, fight what it attracts, craft 82081 from the payout, equip the result, relog, still have
  it. **This is the load-bearing entry, and everything below waits on it.** Needs no economy decision
  and no new recipes — see the note above. Run sheet:
  [Crafting.html](../../Game%20Testing/Crafting.html), eight steps, rewritten 2026-08-21 to watch
  `/mats` rather than a log.

## Deferred

- [ ] **CRAFT3** — **The economy decisions**: which resource economy (five-stat graded materials, or
  the level-banded veins the node types are written for); where crafted output lands; how resources
  are carried; **and now what happens to the raw tier** — 34 of the 42 node types pay an ore no recipe
  wants ([DATA-27](../ISSUE-REGISTER.md)), so the choice is restore a refining step, reprice recipes
  onto the raw tier, or leave those nodes paying a souvenir. **Gated on CRAFT5** — the loop's shape is
  the strongest evidence about what the economy should be, and pricing one before walking it means
  pricing it twice. Two questions are already narrowed: the five-stat system is intact and readable
  ([DATA-26](../ISSUE-REGISTER.md), and the stat values ride on the item rather than living in static
  data), and the display objection is gone — `/mats` draws every material a character holds, raw tier
  included ([client-ui.md](client-ui.md), UI4 met 2026-08-21), so an economy priced in raw resources
  is no longer unplayable on those grounds. needs: CRAFT5
- [ ] **CRAFT4** — **A curated recipe set.** The first recipes that consume what a thumper brings up
  and produce something worth equipping. **Gated on CRAFT3** for a price to meet, and on CRAFT5 for
  proof there is a loop to price. needs: CRAFT3

## Deferred — further out

- **The research chain** (`research_blueprint_id` / `head_blueprint_id`, resolving at 100%) — a
  discovery layer on top of crafting. Gate: whether the core loop (CRAFT1–CRAFT5) is fun without
  it decides if it is wanted at all.
- **The full recipe set** — gated on CRAFT4 proving the curation approach; expanding a working
  subset is cheap, authoring 5,052 up front is not.

## Order

CRAFT1 first, because the client surface is the only part with a real unknown in it and the risk
should be retired before the server work assumes an answer. CRAFT2 can run beside it. CRAFT3 is
wanted before CRAFT4, not before CRAFT2.
