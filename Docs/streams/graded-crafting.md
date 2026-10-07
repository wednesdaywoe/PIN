---
project: pin
kind: stream
title: "Graded crafting: what you mine decides what you build"
id-prefix: GRADE
thesis: >
  The quality of what a player mines carries through to what they craft: a thump pays a
  batch with a rolled quality and stats, refining turns ore into a craftable material and
  keeps that quality, and the same weapon built from a better batch shows a better number
  in game than one built from a worse batch.
satisfied-when: GRADE1..GRADE8 all [x]
relates:
  - crafting.md
  - client-ui.md
  - ../ISSUE-REGISTER.md
  - ../Restoration.md
---

# Graded crafting

**Opened 2026-10-07 by the CRAFT3 decision** (decision 2026-10-07, user-chosen: "Option B with
Option 2 is pretty much the goal"). Two answers, made together:

- **Refining comes back.** The ground keeps paying ore, and refining turns ore into the material
  recipes want. This makes the 34 node types whose ore no recipe uses worth thumping
  ([DATA-27](../ISSUE-REGISTER.md)), and gives Tungsten, Titanium and Uranium a source.
  Rejected: veins paying finished bars directly (quicker, but drops the refining step and leaves
  ore a souvenir); repricing recipes onto ore (rewrites thousands of shipped rows).
- **Quality is real.** Each mined batch has a quality and the five stats of its material family;
  recipes read the stat they name; components move the finished item's numbers. This is the
  pre-1.6 graded system, whose every link still ships in blueprints 75430..85611 (recon 2026-08-22,
  listed below).
  Rejected: counts only (works today, but there is no reason to mine somewhere better);
  counts now and quality later (the user chose quality as the goal outright).

Its own stream rather than more items under [crafting.md](crafting.md), because it widens that
stream's thesis from "a mined resource becomes an equipped item" to "the quality of the mined
resource decides the item". Crafting.md's remaining run (the Burst Rifle, CRAFT5's equip and relog)
does not wait on any of this: it stands on Iron Bars, which the ground already pays.

## What already ships, and the one thing that doesn't

- **The material's own stats have a wire slot and a reader.** The client unpacks a resource stack's
  `resource_type` text, `"{Version}-{SDB id}-{Quality}-{Stat1}..{Stat5}-..."` in base 36
  (`system/gui/lib/lib_Items.lua` `LIB_ITEMS.GetResourceStats`), and the item tooltip draws the
  non-zero stats under their family's names (`lib_ItemCard.lua:804`). PIN's `Resource.TextKey` in
  `InventoryUpdate` is the string that carries it, and PIN sends it empty.
- **Stat names per family**: `dbitems::Resource_Stat_Names`. **Which stat a recipe reads**:
  `Blueprint_Resources.resource_stat`. **Component slots on an item recipe**: `Blueprint_Resources`
  rows with an `item_attribute`. **The number range per attribute**:
  `dbitems::ItemTypeAttributeModifier`. **Raw-to-refined pairs for the sixteen graded materials**:
  `dbitems::ResourceItem.refines_into` (Raw Copper 78014 to Copper 77703, and so on).
- **Never shipped: the values.** No table holds a batch's quality or stats; retail generated them
  and had stopped sending them by 2016. PIN rolls them.
- **Unknown: where a crafted item keeps its own numbers.** Each `InventoryUpdate.Item` carries an
  unnamed list of number pairs (`Unk6`, `ItemUnkData`), the likeliest home for per-item attribute
  values. Nothing confirms that yet. GRADE2 settles it.

## Items

Order: the two probes first, because each can change the shape of what follows.

- [ ] **GRADE1** — **The client shows a batch's quality, and keeps two batches apart.** Send a
  material stack with a filled stats text and see the tooltip draw its stats; send two stacks of the
  same material at different qualities and see two rows with the right counts. If the client merges
  same-material stacks, a player holds one quality per material and GRADE3 averages on pickup
  instead.
- [ ] **GRADE2** — **A crafted item can carry its own numbers.** Find what the client reads per
  item instance (start with `Item.Unk6`), write a changed attribute onto one item, and see it in the
  item's tooltip. If no per-item channel exists, quality picks among the shipped Mk I to IV
  variants of a part instead of setting a number. needs: GRADE1
- [ ] **GRADE3** — **The server holds materials per batch.** Inventory, saving and the wire keep
  separate stacks of one material by quality and stats, and spending draws from a named batch.
  needs: GRADE1
- [ ] **GRADE4** — **Thumping rolls a batch.** Each deposit has a quality range; a payout is one
  batch with a rolled quality and stats for its material family, written into the stats text. The
  ranges ship: `dbzonemetadata::ResourceNodeTypeResource.ItemQualityLow/High` per node type and item
  (node 238 pays Iron Bars at 0..400 and Iron Ore at 0..750; found 2026-10-07).
  needs: GRADE3
- [ ] **GRADE5** — **Refining.** Ore becomes the material recipes want, and the batch's quality
  comes with it: the sixteen shipped raw-to-refined pairs, plus PIN-written recipes for Iron,
  Tungsten, Titanium and Uranium Ore into their bars. Ratios are a PIN choice, recorded when made.
  needs: GRADE4
- [ ] **GRADE6** — **Recipes read the stat they name.** A part built from supplied batches gets a
  quality from the stat its recipe reads, weighted by how much of each batch went in; the craft
  command lets the player choose which batches to spend. needs: GRADE3
- [ ] **GRADE7** — **Parts set the finished item's numbers.** An item recipe's component slots map
  each part's quality onto its attribute through `ItemTypeAttributeModifier`'s range, and the
  result is written onto the crafted item. needs: GRADE2, GRADE6
- [ ] **GRADE8** — **The loop shows it.** In game: two Burst Rifles, one from a poor iron batch and
  one from a good batch, both mined and refined, show different numbers in their tooltips.
  needs: GRADE5, GRADE7

## Deferred

- **The crafting screen** ([UI7](client-ui.md)) — was gated on an economy decision, which this
  stream makes. A screen that picks batches is shaped by GRADE3 and GRADE6, so it waits for those.
  needs: GRADE6
