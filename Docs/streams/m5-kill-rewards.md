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
[K1](../../Game Testing/Kill-Rewards.html)** — a Gaia creature paid 4 crystite, the bottom band of "Small
Crystite Drops (2-20)", and the tester saw it arrive.

The melded resources off `loot_table2_id` are paid too, which nobody predicted: 77343/77344/77345
carry the `Resource` flag, so a kill can pay something even when the crystite roll misses.

## What is deliberately not built

~~**Item drops.**~~ **Built 2026-08-21**, out of [the client-UI stream](client-ui.md)'s finding that a
thumper's non-resource yield was never delivered either. The question the cut was waiting on — how
the client wants a drop represented — had already been answered by
[NET-18](../gaps/network.md#net-18): a single-item `InventoryUpdate` carrying the `0x02` arrival flag
lists itself, confirmed on screen 2026-08-20, and crafting has handed items back that way since
CRAFT2. So there is no drop protocol to build. `KillRewardSim` passes anything without the
`Resource` flag to [ItemPayout](../../UdpHosts/GameServer/Systems/Loot/ItemPayout.cs), which creates
one bag item per copy. [KILL-REWARDS-6](../../Game Testing/Kill-Rewards.html) is the check.

**What the tester will mostly see is nothing, and the log says why.** The two items a small creature
drops often are powerups 33815 and 33816, and their `flags` are `0x2000` — no listing bits, so the
inventory panel never draws them ([DATA-25](../gaps/data.md#data-25)). Across every loot table in
the file: 23,457 distinct drop ids, 5,605 resources, 17,808 items, 44 naming an item that no longer
exists. Of the items **12,858 carry the listing bits and 4,950 do not**, and the undrawable set is
every Blueprint (3,168) and every Powerup (120). A drawable drop off a zone 448 kill means reaching
"Creature Kill - Small Equipment Drop", whose rows are 5/4/3/2/1 out of 100.

~~**The pickup.**~~ **Built 2026-08-21, and the capture wrote the specification.** The 2016 recording
carries **62 `LootObjectView` messages and 41 `CollectLoot` commands** — the whole loop live. The
server publishes a 24-slot table of drops on an `AreaVisualData` entity; the client answers with the
entity, the slot index and the item id it believes it is taking; the item only reaches an inventory
after that. [WorldLoot](../../UdpHosts/GameServer/Systems/Loot/WorldLoot.cs) is that, and
[KILL-REWARDS-6](../../Game Testing/Kill-Rewards.html) is the check — **passed the same evening, first
attempt, with nothing in the log to argue about.** A dropped weapon was picked up into the bag, kills
dropped powerups where the creature fell, boards were reused for drops 7m apart, and three health
pickups restored 187, 189 and 132. The ammo powerup is the one piece still uncollected.

Four things the capture settled that no amount of reading could:

- **A board is per-place.** Its 62 messages name about forty entities, and one pickup run took slots
  1, 2, 3, 7, 16, 18 and 23 off a single one. So a board is a table that fills with whatever falls
  near it, not one per drop and not one for the world. PIN reuses a board within 50m that still has a
  free slot and closes one that has stood empty for a minute.
- **Ownership is a field, and the split is exact.** A powerup carries no owning entity and sets
  `Unk3 = {1,0}`; crystite and equipment name an owner and omit `Unk3`. So `Unk3.Unk1 = 1` reads as
  "anyone may take this". PIN writes both shapes: a powerup is free, a weapon is the killer's.
- **A loot entity carries no `ObserverView`.** None of the forty ever sent one, so PIN drops the one
  its constructor builds rather than announcing a board as an object in its own right.
- **Retail dropped crystite on the ground too**, in stacks of 2 to 58. PIN does not yet — resources
  still pay straight into the ledger, because that path is verified and this one is new. It is the
  obvious next step, not an oversight.

**What the powerups do is PIN's invention, and it has to be.** 33815 "Health Powerup" and 33816 "Ammo
Powerup" (names resolved from the client's own text table) had their effects in aptitude chains whose
parameters were server-side, so 1962 ships the item and nothing about what it grants. Health restores
25% of maximum. Ammo refills both magazines to the starting figure — and **nothing in PIN spends
ammo**, so that write is correct and its visible effect is unproven. Detail on both in
[Powerups.cs](../../UdpHosts/GameServer/Systems/Loot/Powerups.cs).

Two more numbers with nothing behind them: a drop lies there for **two minutes**, and `Unk4` — three
halves running about -3.5 to 3 in the capture — is sent as zero, because nothing in the loop reads it
back and a guess would be worse than a blank.

The one thing that could not be checked by reading either side is whether an emptied slot travels as
an emptied slot; a pickup that stays drawn on everyone else's ground is what it would cost.
[LootObjectViewTests](../../Tests/GameServer.Tests/Loot/LootObjectViewTests.cs) pins it at the wire.

**Squad credit.** One killer, one payment. Sharing belongs with M7.

## What is a guess

`roll_mode` — six values in the column, no documentation, and the client was the only reader. PIN
implements two readings inferred from the tables' own arithmetic and documents them on `LootRoller`.
[LootRollerTests](../../Tests/GameServer.Tests/Loot/LootRollerTests.cs) pins the reading; only
[K2](../../Game Testing/Kill-Rewards.html) can say whether the reading is right, and even then only
loosely, because the rate it measures is the thing being inferred.
