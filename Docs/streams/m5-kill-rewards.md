---
project: pin
kind: stream
title: "M5: Killing Something Pays"
relates:
  - ../PROGRESS.md
---

# M5: Killing Something Pays

Nothing rewarded a kill. `CharacterDiedEvent` had been published to nobody since M2 landed.

**The milestone changed shape on 2026-08-13 and it is now crystite, not XP** (decision, user-chosen).
The original plan was XP first because its shape was known. The reason for dropping it is that
nothing consumes XP.

**Nothing to spend it on.** PIN never implemented levelling and is not going to: character level is
a hardcoded 45, `LevelUpEvent` is never sent, and no stat scales off level. Power comes entirely
from the loadout.

**That is an argument against levels, not against XP, and the first draft of this doc conflated
them.** The beta had XP without levels — it was a currency you earned and spent on upgrading your
frame, part of the resource economy rather than a track pulling a level up behind it. So XP is
compatible with where [Restoration](../Restoration.md) is going, and could come back as one. What it
needs first is the half that consumes it, which is a milestone rather than a line in this one.
Paying out a number nothing reads would be the same shape of nothing PIN already ships:
`ProgressionXPRefresh` sends zero at scope-in today.

**Nothing shipped to size it from either.** `dbcharacter::Monster.xp_resource_id` is non-zero on 2
of 3109 rows and `xpreward_type` on 12. That column's name is itself a trace of the beta model — XP
addressed as a resource id — but by 1962 the economy behind it was gone, so the two surviving rows
are not a table anyone could pay out of.

## The loot tables shipped intact

Which is what makes the replacement better than the thing it replaces. `dbitems::LootTable` carries
2472 rows, `LootTableItemDist` 34159 and `LootTableSubTableDist` 2476, and PIN loaded none of them.
The first six tables in the file are named "CORE NPC Kill Loot 1..6 (Tiny/Small/Medium/Large/Giant/
Boss)". Kills were banded by creature size and the bands are still there.

Zone 448's monsters resolve end to end: 528 and 1196 each point at a "CORE ... Creature Kill Loot 2
(Small)" table, both reach "CORE NPC Kill Loot 2 (Small) - Crystite/Powerups Subtable" at 100%, that
reaches "Small Crystite Drops (2-20)" at 25%, and that pays crystite 2–20 across four weighted bands.
So a small creature pays about one kill in four, and this milestone's payout figure is retail's
rather than ours.

`difficulty_cost` (906 of 3109 rows, 0..1000) is the other survivor and nothing reads it yet. It is
the obvious input for scaling anything that wants to be harder-is-worth-more.

## What is built

| Work | Where |
|------|-------|
| Load the three loot tables | [StaticDBLoader](../../UdpHosts/GameServer/StaticDB/Loaders/StaticDBLoader.cs), [SDBInterface](../../UdpHosts/GameServer/StaticDB/SDBInterface.cs) |
| Walk a loot table down through its subtables | [LootRoller](../../UdpHosts/GameServer/Systems/Loot/LootRoller.cs), behind [ILootTableSource](../../UdpHosts/GameServer/Systems/Loot/ILootTableSource.cs) so it tests without a client db |
| Pay the killer on `CharacterDiedEvent` | [KillRewardSim](../../UdpHosts/GameServer/Systems/Loot/KillRewardSim.cs) |

Exit: kill a monster, watch crystite go up. **Met 2026-08-13 by
[K1](../In-Game-Tests/Kill-Rewards.md)** — a Gaia creature paid 4 crystite, the bottom band of "Small
Crystite Drops (2-20)", and the tester saw it arrive.

The melded resources off `loot_table2_id` are paid too, which nobody predicted: 77343/77344/77345
carry the `Resource` flag, so a kill can pay something even when the crystite roll misses.

## What is deliberately not built

**Item drops.** The same tables roll powerups and equipment on the same kill, and both are items
rather than resources. Delivering one means answering how the client wants a drop represented, which
nothing in the codebase establishes — `AddLootTable`, `SpawnLoot` and `RequireLootStore` have def
JSONs sitting unused in `CustomData/Todo/` and that is still the place to start. `KillRewardSim`
rolls them, logs each one, and drops it. Same cut M3 made: pay the resource directly, leave the item
route to the milestone that can afford it.

**The pickup.** Retail spawned something the killer walked over. This credits the killer's inventory
directly, with no object in the world.

**Squad credit.** One killer, one payment. Sharing belongs with M7.

## What is a guess

`roll_mode` — six values in the column, no documentation, and the client was the only reader. PIN
implements two readings inferred from the tables' own arithmetic and documents them on `LootRoller`.
[LootRollerTests](../../Tests/GameServer.Tests/Loot/LootRollerTests.cs) pins the reading; only
[K2](../In-Game-Tests/Kill-Rewards.md) can say whether the reading is right, and even then only
loosely, because the rate it measures is the thing being inferred.
