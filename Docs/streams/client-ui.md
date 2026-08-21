---
project: pin
kind: stream
title: "Client UI: materials you can see"
id-prefix: UI
status-ext: [gate]
thesis: >
  A player can see the materials they hold, raw and refined alike, as named rows
  with quantities they can read at a glance — through a surface PIN authors and
  ships, not one that shipped working in 1962.
satisfied-when: UI1..UI5 all [x]
relates:
  - ../PROGRESS.md
  - ../Restoration.md
  - ../Client-UI-Source.md
  - crafting.md
  - ../gaps/client.md
---

# Client UI

**Opened 2026-08-21, out of [CRAFT2b](../../Game%20Testing/Crafting.html)** (decision 2026-08-21,
user-chosen: its own stream rather than an entry inside crafting). The reason it separated: crafting
is server code in C# verified by reading a log, and this is the client's own Lua verified by looking
at a screen. Different language, different test loop, and an open-ended design where the crafting
entries each had a pass condition.

**It does not gate crafting.** [CRAFT3](crafting.md) can price an economy in refined materials, which
already draw in the ordinary inventory — confirmed on screen 2026-08-21. This stream is what lets
that economy later reach the raw tier, and what the [charter](../Restoration.md) needs before a loop
counts as restored: a thumping payout nobody can see is a loop with its middle missing.

## What is already settled, so it is not re-derived

- **The interface is editable source.** All of `system/gui` is loose, uncompiled Lua and XML —
  see [Client-UI-Source.md](../Client-UI-Source.md). Authoring against it is normal work, not
  reverse engineering.
- **The addon route works end to end.** [Client/Addons/FabCart](../../Client/Addons/FabCart) is 188
  lines of PIN-authored Lua that caught a server event the client never asked for and printed it
  ([CRAFT1b](../../Game%20Testing/Crafting.html), 2026-08-21). The risky question — whether this
  client will load interface we write — is answered yes.
- **Raw resources have nowhere to be drawn, and it is not a classification bug.** They were shown in
  the molecular printer; `system/gui/components/MainUI/Panels/FabTest/FabTest.lua` is 528 lines and
  **zero non-whitespace characters**, and its XML keeps only the `<Info>` block. Red 5 blanked the
  files rather than deleting them, leaving a registered, empty component slot. The inventory panel
  filters subtype **15**, "Crafting Components" (`lib_SubTypeIds.lua`), and *both* tiers hang under
  it — so the data is filed correctly and there is no data-side fix. See
  [CLIENT-4](../gaps/client.md).
- **Refined materials do draw.** Iron Bars, Tungsten Bars and Copper Wiring were listed on screen
  2026-08-21. The split is raw vs refined, not resource vs item.
- [x] **UI2 — The engine hands Lua everything, raw tier included.** `/invprobe` on 2026-08-21 read
  both return values of `Player.GetInventory()`: 73 items and 2 resources, and **Iron Ore was one of
  them**, complete — `name`, `icon_id` 263237, `total` 24, `stack_size`, a full `flags` block, and the
  subtitle `Raw Metals > Raw Resource > Crafting Components`. Its own description calls it raw ("Must
  be refined at a molecular printer"). Nothing is missing on the wire and nothing needs re-typing
  server-side, so the thesis stands as written and every later item is a client-side authoring job.
  Log: `console.log` lines 823-838, session of 2026-08-21.
- **The `.raw` / `.refined` split is not raw vs refined material.** The panel unwraps each resource
  entry into a `.raw` and a `.refined` side and draws both (`Inventory.lua:3062`). In practice
  **`.raw` was absent on every entry** and Iron Ore arrived under `.refined`. So `.refined` is simply
  where the engine puts a resource, and `.raw` is a second slot nothing has been seen to fill. Do not
  read the two names as the raw/refined tiers; they are not the same distinction.
- **The category filter is not what hides them.** Both entries carry subtype **15**, and
  `SubTypeIds.Resource = 15` is in the panel's default enabled categories (`Inventory.lua:183`).
  Whatever keeps a raw row off the screen is downstream of the filter, not the filter.

## Active

- [x] **UI1** — **Measure the gap.** Count a panel against `dbg_inventory` line by line and write down
  what is held versus what is drawn. [CLIENT-4](../gaps/client.md) currently records an impression —
  "some rows are unfindable" — because nobody has done this.

  **First numbers, 2026-08-21.** Both sides counted in the same session, minutes apart:

  | | server (`dbg_inventory`) | client (`/invprobe`) |
  |---|---|---|
  | items | 70 (Gear 67, Bag 3) | 73 |
  | resources | **6** | **2** |

  **Two thirds of the resources never reach Lua**, and the panel is not what loses them: UI2 proved
  the ones that do arrive come through complete. All six, named from the server log after the
  resource listing was added to it (`DebugInventoryServerCommand`, 2026-08-21):

  | id | qty | what it is | reached Lua |
  |---|---|---|---|
  | 10 | 368 | Crystite ([M4](m4-thump-placement.md)) | no |
  | 30404 | 1 | "Thumper Sifted Earth", the barren-ground payout ([M4](m4-thump-placement.md)) | no |
  | 77343 | 9 | melded resource off `loot_table2_id` ([M5](m5-kill-rewards.md)) | no |
  | 77344 | 1 | melded resource off `loot_table2_id` ([M5](m5-kill-rewards.md)) | no |
  | 86668 | 24 | Iron Ore | **yes** |
  | 86703 | 2 | Copper Wiring | **yes** |

  **Two of the four are false positives.** Crystite is drawn as a currency in the wallet, not as an
  inventory row. **30404 is "Useless Dirt"** — description "It's dirt.", and its own flags are
  `hidden=true, resource=true, no_persist=true`. Both are absent by design. So the real gap is two,
  not four.

  **UI1's real finding: the client holds the two melded resources and does not return them.**
  `/invprobe id` on 2026-08-21 resolved both in full — **Melded Chitin Fragment** (77343, subtype
  3291, `web_icon_id` 234393, "Tier 1 raw biomaterial ... Refines to Chitin Fiber, Dark Crystite")
  and **Melded Blood Sample** (77344, same subtype). Neither is hidden; both carry `resource=true`.
  Decisively, **`Player.GetItemCount(77343)` returned 9**, matching the server exactly. So the client
  has the quantity, the name, the description and an icon, and `Player.GetInventory()` still omits
  them from its resources table. Nothing is missing — something is filtering.

  Also corrected here: this is **not** a server-side loss. `SendFullInventory` logged `6 resource(s)`
  and the client demonstrably received them.

  **The category tree is not the filter either** (`/invprobe cat`, 2026-08-21). Raw Metals (3288) and
  Raw Biomaterials (3291) are siblings: both hang off **149 "Raw Resource"**, which hangs off **15
  "Crafting Components"**, and both carry `market_category=false` and an empty `resource_stats`. Their
  trees are the same shape at every level. Copper Wiring's 3614 is a *direct* child of 15, a different
  shape again, and it arrives — so neither depth nor branch predicts what comes through.

  **The server puts all six on the wire.** `SendFullInventory` writes `Resources = [.. _resources.Values]`
  with no filter and a 254 ceiling nothing is near (`CharacterInventory.cs:381`). So the drop is inside
  the client engine, between the packet and `Player.GetInventory()`. Note the wire has a second array,
  `SecondResources`, which PIN always sends empty — the likely source of the always-absent `.raw` side.

  **Closed 2026-08-21: the client holds all six, correctly, and no single accessor shows them all.**
  `Player.GetItemCount` returns 368 / 1 / 9 / 1 / 24 / 2 for the six ids — **exact agreement with
  `dbg_inventory` on every one**. The resource entry above `raw`/`refined` also turned out to be
  per-item, not a category grouping (`{icon_id, item_sdb_id, name, stat_names}`, naming Iron Ore
  itself), so there is no grouping rule to have been excluded by.

  So `Player.GetInventory()`'s resource half is simply a narrow view, and **a panel built on it would
  be wrong by construction** — which is what the shipped Inventory panel is built on. That retires the
  question of *why*: it is engine code PIN cannot edit, and it no longer blocks anything.

  **The two accessors are complementary, and their union is complete** (`/invprobe enum`, 2026-08-21).
  `Player.GetInventoryItemsOfType(15)` returns **exactly the two `GetInventory` drops** — Melded Chitin
  Fragment and Melded Blood Sample — and neither of the two it keeps. Disjoint sets, four materials
  between them, which is every material the character holds that is meant to be visible:

  ```
  Player.GetInventory()              -> 86668 Iron Ore, 86703 Copper Wiring
  Player.GetInventoryItemsOfType(15) -> 77343 Melded Chitin Fragment, 77344 Melded Blood Sample
  ```

  Only the top of the tree answers: every one of the ~50 child categories below 15 returned nothing,
  so this is one call at `SubTypeIds.Resource`, not a tree walk. The enumeration also printed the
  category tree in full, which is the first list PIN has of what 1962 thinks materials are.

  **UI1's answer, in one line:** the character holds 6 resources, 2 are correctly invisible (Crystite
  is currency, 30404 is flagged `hidden`), 4 should be drawn, and **no shipped call returns all 4** —
  which is exactly why [CLIENT-4](../gaps/client.md) could only ever record an impression.

  The item count disagreeing by 3 in the other direction is a separate loose end, not yet chased.

- [x] **UI3** — **Decide the surface.** Three candidates, all legitimate under the charter's "fidelity
  is not a constraint": a standalone addon like FabCart; refilling the empty `FabTest` slot, which
  carries the printer's own name; or extending the shipped `Inventory` panel. Deliverable is a
  decision with a spike behind it, not a finished screen.

  **UI1 narrowed this.** Whatever the surface, it reads **both** accessors and merges them; the
  shipped panel reads only the one that drops half. Extending `Inventory` therefore means changing
  installed client files (as the [heatmap patch](../Client-UI-Source.md) did) rather than adding to
  them, and inherits a 4,400-line file. An addon owns its own list and can be correct from line one.

  **Decided 2026-08-21: the addon, and the spike is on screen.**
  [Client/Addons/MatList](../../Client/Addons/MatList) — 175 lines, `/mats` — drew **all four
  materials in one window, iconed and counted**: Copper Wiring 2, Iron Ore 24, Melded Blood Sample 1,
  Melded Chitin Fragment 9, headed "4 held". **This is the first time in PIN that the melded
  resources have been visible to a player at all.** Refilling `FabTest` stays available for UI7 if a
  crafting panel wants the printer's own name, but it buys nothing a list needs.

  Two things the spike proved beyond the decision: `MultiArt:SetIcon(web_icon_id)` draws a material
  icon with no further data, and `ON_INVENTORY_CHANGED` refreshes the list live. **Widgets are
  addressed by `id=`, not `name=`** — with `name=` every `Component.GetWidget` returns nil silently
  and the window renders empty, which is the trap that cost this spike its first run.

- [x] **UI4** — **A readable materials list.** Every material the character holds, named, with a
  quantity, raw and refined together. This is the thesis's own exit: open it and read what you have.

  **Met 2026-08-21**, confirmed on screen. [MatList](../../Client/Addons/MatList) `/mats` draws all
  four materials grouped under headings the client's own category tree supplies — ELECTRONICS, RAW
  BIOMATERIALS, RAW METALS — each row an icon, a name and a quantity, with a running total. It scrolls
  (`lib_RowScroller`), it re-draws on `ON_INVENTORY_CHANGED`, and **hovering a row shows the client's
  own item tooltip** (`LIB_ITEMS.CreateToolTip` + `Tooltip.Show`), rarity-tinted, carrying the
  category path and description.

  Three sources are merged by item id, not two: both accessors from UI1, plus a sweep of the items
  half for anything `Game.IsItemOfType(id, 15)` calls a crafting component. Neither accessor is
  documented as complete and the third pass is a loop over a list already in hand — a panel that
  silently omits a stack is the only failure that matters here. Quantities come from
  `Player.GetItemCount`, the one number UI1 found agreeing with the server on every id.

  Two client facts worth keeping: grouping by the **immediate** category reads better than by the
  shared parent ("Raw Metals" and "Raw Biomaterials" say more than one "Raw Resource" heading), and
  `parentResourceTypeId` must be compared through `tonumber` — `lib_Items` does the same, and a raw
  `==` silently never matches.

- [ ] **UI5** — **A row you can actually read.** Item 81626 draws today with **no name on it**, though
  its text resolves in all six languages; what it lacks is an icon (`web_icon_id` 0, where every
  normally-drawn item carries one). Settle whether an iconless row is the cause, and give PIN a way
  to ship a row that reads even when 1962's data is thin.

  UI4 already ships half of it: an iconless material falls back to a default icon rather than drawing
  blank, and no row can render nameless because the name is looked up again from the item database
  when the entry carries none. What is untested is **81626 specifically** — whether those two
  fallbacks are enough to make that row read, or whether an icon of `0` breaks something earlier.

## Backlog

Empty — everything not done is either active or deferred.

## Deferred

- [ ] **UI6** — **The five stats on a material.** Purity, Power, Mass, CPU and the unnamed fifth,
  shown per stack. The client already parses them off a packed string it receives
  (`lib_Items.lua`, `GetResourceStats`). **Gated on the server writing that string at all** — PIN
  sends the field empty today, and CRAFT4 is where a stat first changes an outcome. Pointless before
  there is a value to draw. needs: crafting.md#CRAFT4
- [ ] **UI7** — **A crafting panel.** Recipes, costs, a build control. **Gated on UI4 landing and on
  [CRAFT3](crafting.md) choosing an economy** — a panel priced against an undecided economy would be
  authored twice. `craft` is a server command today and that is sufficient for testing; this is for
  players, and only becomes evaluable once there is a loop to put in front of them.

## Not in this stream

Evicted on 2026-08-21 as co-discovered rather than caused: the crafting sub-inventory guess
(`Unknown InventoryType for ItemType CraftingComponent, defaulting to Bag`) is server-side placement
with no retail precedent to check against — the 2016 capture holds no crafting component in a
356-entry inventory. It belongs to crafting or to a data gap, not here.
