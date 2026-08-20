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

- [ ] **CRAFT1** — **The client surface.** Settle how a panel reaches fabrication: repurpose
  `Tinkering`, or author a minimal Lua panel against the surviving `GetRecipeIds`/`GetRecipeInfo`.
  The garage probe already showed the route is open — unknown wire keys survive into Lua, and
  `system/gui` is loose, readable source. Deliverable is a decision with a working spike behind
  it, not a finished screen.
- [ ] **CRAFT2** — **Fabrication server-side.** Handle the surviving Fabrication command set,
  resolve a recipe's ingredients against inventory, spend them (`RequireResource` /
  `RequireResourceFromTarget` stop being stubs, `Blueprint_Resources` gets a reader), and place
  the output item. Not gated on any decision below — the charter's "none of these block starting"
  still holds.
- [ ] **CRAFT3** — **The economy decisions**, now decidable because resources exist in the world:
  which resource economy (five-stat graded materials, or the level-banded veins the node types
  are written for); where crafted output lands (1962's item system, or a parallel track free of
  level-45 balance); how resources are carried in inventory. User's call; the data supports
  either economy and imposes neither.
- [ ] **CRAFT4** — **A curated recipe set.** The first recipes that consume what a thumper
  brings up and produce something worth equipping, chosen from the 5,052 resolvable blueprints
  and priced against the economy CRAFT3 picks.
- [ ] **CRAFT5** — **The loop closes in game.** One sitting: thump a deposit, craft from the
  payout, equip the result, relog, still have it. Gets its own run sheet in
  [In-Game-Tests](../In-Game-Tests/) when the build lands.

## Deferred

- **The research chain** (`research_blueprint_id` / `head_blueprint_id`, resolving at 100%) — a
  discovery layer on top of crafting. Gate: whether the core loop (CRAFT1–CRAFT5) is fun without
  it decides if it is wanted at all.
- **The full recipe set** — gated on CRAFT4 proving the curation approach; expanding a working
  subset is cheap, authoring 5,052 up front is not.

## Order

CRAFT1 first, because the client surface is the only part with a real unknown in it and the risk
should be retired before the server work assumes an answer. CRAFT2 can run beside it. CRAFT3 is
wanted before CRAFT4, not before CRAFT2.
