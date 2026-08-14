# Thumping and crafting in build 1962

v1.6 "Razor's Edge" (January 2016) switched crafting off:

> Crafting has been temporarily disabled until the new crafting system becomes available in a
> future update. Any research points, Crystite and time spent on researching is refunded to the
> players. Thumping will not yield minerals until crafting is reintroduced, though pilots can thump
> nodes for Crystite.

It was never reintroduced. v1.7 has no crafting section at all, and 1962 is the last build. So the
game PIN targets shipped with its founding system turned off.

**The data did not go with it.** Nothing was stripped from the client DB — the whole crafting and
resource dataset is still there, in the copy on disk, at full row counts.

## What survives

Three layers have to line up for a system to be revivable: the data, the protocol, and the client
UI. Two of the three are intact.

### Static data — intact

Read out of `clientdb.sd2` with `Tools/MinimalSDB dump`. Header:
`patch prod-1962, built 2016-05-05 04:21:54Z, flags ObfuscatedPool, Compressed, Client, 575 tables`.

| Table | Rows | |
|-------|------|--|
| `dbitems::Blueprints` | **9,228** | the recipes |
| `dbitems::Blueprint_Items` | **25,881** | their ingredient lists |
| `dbfabrication::Recipe` | 285 | a second, separate fabrication DB |
| `dbitems::ResourceItem` | 111 | |
| `dbzonemetadata::ResourceNodeTypeResource` | 67 | what a node yields |
| `dbzonemetadata::ResourceNodeType` | 46 | node types, named |
| `dbitems::ResourceNodeItemToProps` | 43 | |
| `dbitems::ResourceNodeBeacon` | 42 | thumper calldowns |
| `dbitems::ResourceNodeAwardProperties` | 14 | |
| `dbitems::ResourceStat` | 5 | |
| `apttf::tfResourceAreaCreateNodeCommandDef` | 2 | the ability that spawns a node |

Not one is `ABSENT`, `EMPTY` or `ALL ZERO`. Nine thousand recipes with twenty-six thousand
ingredient rows are sitting in the file the server already reads.

PIN loads `Blueprints`, `Blueprint_Items` and `ResourceNodeBeacon` today. The other eight are not in
`StaticDBLoader` yet — they need record classes and `Load*` methods, which is mechanical work
against a known schema, not research.

Note the SDB holds **575 tables** and PIN loads 238. There is a lot here nobody has looked at.

### Protocol — intact

`ResourceNode` is a first-class GSS controller in V66 with its own `ObserverView`, so nodes
replicate like any other entity. The full loop is defined in AeroMessages:

| Message | Direction |
|---------|-----------|
| `SinAcquireSource` (Character + Vehicle) | command |
| `FindNearbyResourceAreas` | command |
| `ResourceLocationInfosRequest` | command |
| `ResourceNodeBeaconCalldownRequest` | command |
| `SalvageRequest` | command |
| `FoundResourceAreas` | event |
| `ResourceLocationInfosResponse` | event |
| `ResourceNodeCompletedEvent` | event |
| `SalvageResponse` | event |
| `DevRequestResourceNodeDebug` | dev |

Scan with SIN → find areas → query a location → call down a beacon → node completes. That is the
thumping loop, and the 1962 client still speaks all of it.

Crafting's protocol survives too (checked 2026-08-13 against the client binary and AeroMessages).
The full Fabrication command set is in both:

| Message | Direction |
|---------|-----------|
| `Fabrication_FetchAllRecipes`, `_FetchAllInstances`, `_FetchInstance` | command |
| `Fabrication_Start`, `_ApplyAction`, `_GenerateResult`, `_Finalize`, `_Claim` | command |
| a `_Response` event for each of the eight | event |
| `TrackRecipe`, `ClearTrackedRecipe` | event |

PIN's enums already assign all sixteen ids (Commands 240–247, Events 176–183). Fetch recipes →
start → apply actions → generate result → finalize → claim is a whole crafting session, and the
client still speaks that as well.

### Client UI — thumping yes, crafting no

Present:

```
gui/components/MainUI/Encounters/Thumper/Thumper.lua, Thumper.xml
gui/components/MainUI/HUD/ActivityTracker/Thumpers.lua
gui/components/MainUI/Panels/SuperTutorialTtip/textures/thumper_{placement,defense,takeoff}.png
```

Absent. There is no crafting panel anywhere in the GUI tree. What is left of it is a placeholder:

```
gui/components/MainUI/Panels/Teaser/textures/EN/craftingTeaser.dds
gui/components/MainUI/Panels/Teaser/textures/EN/comingSoon.dds
```

That is the "coming soon" card v1.6 put where the crafting screen used to be. The 45 surviving
panels include `Garage`, `Inventory` and `Tinkering`, but nothing to craft on.

The deleted panel has a name: MPU, the Molecular Printing Unit. `lib/lib_Items.lua` still carries
the note *"MPU was using this table, until we deleted it"*, and the Sinvironment hub still maps
its "crafting" page to an `MPU` component that no longer exists. The Salvage panel went the same
way — `Mainframe.lua` still posts to `Salvage:Main`, and only `salvageIcon.png` is left of it.

The engine half did not go with the Lua. `FirefallClient.exe` still exports the recipe API —
`GetRecipe`, `GetRecipeIds`, `GetRecipeInfo`, `GetRecipeList` — and surviving code still calls
it: `Mainframe.lua` keeps a live "CRAFTING FUNCTIONS" section that fetches research recipes via
`Game.GetRecipeIds("Research")`, `Webframe.lua` keeps a crafting callback, and `lib_WebCache.lua`
still knows the `/manufacturing/certs` web endpoint.

## What that means

Thumping is a server-side job. Data, protocol and UI are all present, so it is implementation
work against a client that is still expecting it — the same shape as everything else in
[the progress ledger](../PROGRESS.md).

Crafting has one hard blocker, and it is the surface, not the data or the protocol. Nine
thousand recipes are useless without a screen to pick them on. But the screen was Lua, not
engine code: what v1.6 deleted was the MPU component from the GUI tree, and the binary kept its
whole half — the Fabrication messages and the recipe API.

The blocker is softer than it sounds. **Firefall's UI is plain-text Lua and XML on disk** —
`Thumper.lua` is ASCII, panels are registered in `gui/UISets/MainUI.xml`, and the game shipped an
addon ecosystem that the community wrote against for years. A crafting panel can be authored
rather than recovered, and it would not start from zero: the engine's recipe functions are still
exported and `Mainframe.lua` shows them being called. That is real work, and it is a different
kind of work from the rest of PIN — client-side Lua against an undocumented UI API rather than
C# against a known wire format — but it is not blocked on anything lost.

## Do the recipes still resolve?

Yes. The v1.6 item migration did not orphan the blueprint graph. Joined against
`dbitems::RootItem` (60,126 items):

| Join | Resolves |
|------|----------|
| `Blueprint_Items.blueprint_id` → `Blueprints.id` | **25,881 / 25,881 — 100%** |
| `Blueprints.head_blueprint_id` → `Blueprints.id` | 4,540 / 4,540 — 100% |
| `Blueprints.research_blueprint_id` → `Blueprints.id` | 5,959 / 5,959 — 100% |
| `Blueprint_Items.item_type` → `RootItem.sdb_id` | 25,823 / 25,881 — 99.78% |
| `Blueprints.main_output_item_id` → `RootItem.sdb_id` | 5,808 / 5,858 — 99.15% |

The recipe graph is perfectly self-consistent and item references are 99.78% intact. Fifty-eight
dangling ingredient ids out of twenty-six thousand is noise.

The 5,763 whose output still has a name are listed with their ingredients and build times in
[Reference/Recipes](Reference/Recipes.md).

**5,052 blueprints (54.7%) have both a resolvable output and a resolvable ingredient** — recipes
that would work today if something asked for them.

The other 45% are not broken, they are a different shape. 3,370 blueprints declare no
`main_output_item_id` at all and 5,959 carry a `research_blueprint_id`, which together say the
crafting system had a research/discovery layer: many rows are steps in a research chain rather
than standalone recipes. Only 56 blueprints declare an output that fails to resolve — actual rot
is rare.

Five thousand working recipes is not a foundation problem.

## Open questions

- What is `dbfabrication::Recipe` for, and how does it relate to `Blueprints`? Two separate recipe
  systems, 285 rows against 9,228, suggests one superseded the other.
- How does the research chain (`research_blueprint_id`, `head_blueprint_id`) actually drive
  progression? It is 100% internally consistent, so it can be read straight out.
- Does the surviving Tinkering panel expose enough of a recipe interface to be repurposed before
  anything new is written?
- Does `Game.GetRecipeIds` answer from local SDB data, or does the engine fill its recipe list
  from a `Fabrication_FetchAllRecipes_Response` round-trip? Decides whether a new panel shows
  recipes before the server implements anything.
