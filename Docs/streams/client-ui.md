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

## Active

- [ ] **UI2** — **Probe what the engine hands Lua.** Call `Player.GetInventory()` from a FabCart-style
  addon and dump both return values verbatim: do raw resources appear at all, and with what field
  names? The [garage probe](../../Game%20Testing/) already showed unknown wire keys survive into Lua,
  so the odds are good — but nothing has read the resource half. **If the engine returns nothing for
  the raw tier, no client-side surface can show it and this thesis needs re-writing around a
  different route** (server-side re-typing, or repurposing a displayed item type). Cheap, and it
  decides the shape of everything after it. `@gate`

## Backlog

- [ ] **UI1** — **Measure the gap.** Count a panel against `dbg_inventory` line by line and write down
  what is held versus what is drawn. [CLIENT-4](../gaps/client.md) currently records an impression —
  "some rows are unfindable" — because nobody has done this. Until it exists, every later item is
  guessing at the size of its own problem. Independent of UI2; can run first or alongside.
- [ ] **UI3** — **Decide the surface.** Three candidates, all legitimate under the charter's "fidelity
  is not a constraint": a standalone addon like FabCart; refilling the empty `FabTest` slot, which
  carries the printer's own name; or extending the shipped `Inventory` panel. Deliverable is a
  decision with a spike behind it, not a finished screen. needs: UI2
- [ ] **UI4** — **A readable materials list.** Every material the character holds, named, with a
  quantity, raw and refined together. This is the thesis's own exit: open it and read what you have.
  needs: UI3
- [ ] **UI5** — **A row you can actually read.** Item 81626 draws today with **no name on it**, though
  its text resolves in all six languages; what it lacks is an icon (`web_icon_id` 0, where every
  normally-drawn item carries one). Settle whether an iconless row is the cause, and give PIN a way
  to ship a row that reads even when 1962's data is thin. needs: UI4

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
