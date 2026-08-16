---
project: pin
kind: test-stream
title: "Kill Rewards (K1-K5)"
relates:
  - ../TEST-REGISTER.md
---

# Kill Rewards

Part of the [in-game test queue](README.md). Setup, admin commands and the type-id tables:
[Session Setup](Session-Setup.md).

The check on [M5](../streams/m5-kill-rewards.md). One sentence: kill something and come away holding
crystite you didn't have.

**K1 passed on 2026-08-13 and closed [M5](../streams/m5-kill-rewards.md)** — a Gaia creature paid 4
crystite and the tester saw it arrive. It took two attempts. The first found that crystite was being
rolled correctly and then filed as an unpayable item, because the resource-or-item check asked
`dbitems::ResourceItem` — the gatherable-materials list (ids 75537 and up), which does not contain
crystite at id 10. The check is now the `Resource` flag on the item's own `RootItem` row, which
`createitem` has always used and [G1](Resource-Payout.md) confirmed against the client.

**That run also confirmed the basis-points guess, on a table that is on the kill path.** "Melded Loot
Common" (5463) is reached from monster 528's second loot table and its rows are 5000/1000/100. Read
as percentages that is a distribution that drops something every time; it is out of 10000.
`LootRoller` now decides the scale per table from its own rows. See [DATA-16](../gaps/data.md).

**K2 settled on 2026-08-16 and the stream is now 3 of 5.** Thirty-nine kills of monster 528 in one
post-fix session paid crystite on **8**, which is 20.5% against an expected 25% — inside the noise
band, and large enough to mean something where the two earlier sessions were not. K3 passed the day
before. K4's hazard half passed; its NPC-on-NPC half has still never had the two hostile factions
put in reach of each other, and K5 needs a second client.

## What changed about this milestone

M5 was written as XP first and loot second. It is now crystite first and no XP (decision 2026-08-13,
user-chosen). Two reasons:

- **Nothing consumes XP.** `ProgressionXPRefresh` already sends zero at scope-in, so paying out a
  number nothing reads changes nothing on screen. The half that spends it is a milestone of its own.
- **Nothing shipped to size it from.** `dbcharacter::Monster.xp_resource_id` is non-zero on **2 rows
  of 3109** and `xpreward_type` on 12. The loot tables, by contrast, are complete and tuned.

**Not because levels are out.** An earlier draft of this said so and it was wrong: the beta had XP
without levels, as a currency you earned and spent on upgrading your frame (correction 2026-08-13,
user). XP fits the level-less model rather than contradicting it, and could return once something
consumes it. See [Restoration](../Restoration.md).

## Where the numbers come from

Nothing here is invented. `dbitems::LootTable` shipped with 2472 rows and `LootTableItemDist` with
34159, none of it loaded by PIN until now. Zone 448's two monster types resolve all the way down:

| | |
|--|--|
| monster **528** (Gaia, `difficulty_cost` 20) | loot table 5703, "CORE Gaia Creature Kill Loot 2 (Small)" |
| monster **1196** (Chosen, `difficulty_cost` 35) | loot table 5697, "CORE Chosen Creature Kill Loot 2 (Small)" |
| both, at 100% | table **2**, "CORE NPC Kill Loot 2 (Small) - Crystite/Powerups Subtable" |
| table 2, at 25% | table **25**, "Small Crystite Drops (2-20)" |
| table 25 | crystite (**item 10**): 2–4 at 80, 5–10 at 10, 11–15 at 5, 16–20 at 5 |

**So three kills in four pay nothing, and the fourth pays 2 to 20.** That is retail's rate for a
small creature and it is the single most important thing to know before running any of this: a dry
kill is the expected case, not a failure, and no entry below can be judged on one kill.

Both monsters also carry a second table (`loot_table2_id` 5458 and 5368, the Melded and Chosen
sub-loot) and both roll equipment and resource-item tiers. **The equipment and powerups are not
paid** — they are items rather than resources, and putting an item in a player's hands is
[I1](Inventory.md)'s unsolved problem, so `KillRewardSim` logs each one it rolled and drops it.

**The melded resources off that second table are paid, and K1 is where that turned up.** 77343,
77344 and 77345 carry the `Resource` flag, so they take the same route crystite does and land in the
resource pane. That means a kill can pay you something even when the crystite roll misses, and it
means the resource pane is worth a look before concluding a kill paid nothing.

## What is a guess

`roll_mode` is a guess. The column carries six distinct values, the client is the only thing that
ever read them, and the two behaviours PIN implements were inferred from the arithmetic of the
tables themselves — mode 2 as independent per-entry chances (100/100/8 on one table can't be
anything else), everything else as a weighted single pick (80/10/5/5 summing to exactly 100 can't be
anything else). Modes 1, 4 and 5 are unexercised.

The probability scale is the second guess, and K1's first run turned it from theoretical into a live
defect. Both scales are in the file, nothing marks which is which, and "Melded Loot Common" (5463) —
reached from monster 528 — carries 5000/1000/100, which cannot be percentages. `LootRoller` now
decides per table: any row above 100 means the table is out of 10000, otherwise 100. That gets both
known cases right and is still an inference.

`LootRollerTests` pins the reading offline. It cannot tell you the reading is right — only the
client's own drop rates could, and they are gone. **These entries are the only check on it**, which
is why K2 is a count over many kills rather than a yes/no.

## Setup for every entry

Confirm the build banner first, the way [NPC Combat](NPC-Combat.md) describes, then keep the log
open:

```
grep -a "Running build" ~/Games/PIN/logs/GameServer.log
tail -f ~/Games/PIN/logs/GameServer.log
```

The three lines this stream reads:

```
grep -a "paid .* of resource" ~/Games/PIN/logs/GameServer.log
grep -a "rolled nothing" ~/Games/PIN/logs/GameServer.log
grep -a "not paid — item drops are unbuilt" ~/Games/PIN/logs/GameServer.log
```

Zone 448 has thirteen monsters standing in it at login and they respawn about two minutes after
death, so there is no shortage of targets. `npc 528` and `npc 1196` spawn more at your feet.

## [x] K1: A kill pays crystite

**Passed 2026-08-13 on the second attempt, and it closes [M5](../streams/m5-kill-rewards.md).** A
Gaia creature died and paid 4 crystite, which the tester saw arrive:

```
20:22:47 NPC 2305176846901314560 died to 11072869122414870784
20:22:47 Kill of monster type 528 paid 4 of resource 10 to 11072869122414870784
20:22:47 Kill of monster type 528 paid 1 of resource 77344 to 11072869122414870784
20:23:07 Kill of monster type 1196 rolled item 33815 x1, not paid — item drops are unbuilt
20:23:07 Kill of monster type 1196 paid 1 of resource 77345 to 11072869122414870784
```

4 is the bottom band of "Small Crystite Drops (2-20)", the 80% row — the common case landing on the
common outcome. **Two melded resources were paid alongside it and are easy to miss**: 77344 and
77345 come off the second loot table, the "Melded Loot Common"/"Uncommon" pair, and they carry the
`Resource` flag so they land in the resource pane rather than the bag. They arrived at all only
because of the scale fix below — before it, that table dropped its common row on every kill.

**The first attempt failed, and the failure was one line of PIN rather than anything about loot.**
21 kills, 10 crystite rolls, 0 paid. Every line read
`rolled item 10 x2, not paid — item drops are unbuilt`, and **item 10 is crystite** — the roller was
right and the classifier was wrong. `IsResourceItem` consulted `dbitems::ResourceItem`, a table that
exists, has 111 rows, and is the wrong one: it lists gatherable materials from id 75537 up, not the
contents of the resource pane. It now reads `ItemFlags.Resource` off `RootItem`, the same check
`createitem` uses.

Worth keeping as a shape: **neither of this entry's own failure branches applied.** The server was
not quietly right while the screen was wrong, and the log was not silent. The log was talkative and
accurate about what it had done, and what made the bug findable in one pass was naming the id and
quantity of the thing it declined to pay — the K3 line, written for a different reason, is what
identified it.

The milestone's exit condition. Everything else here is a qualifier on it.

1. Log in to zone 448. `dbg_inventory` and write down the crystite count.
2. Find a monster and kill it. Repeat until the log prints a `paid` line — **expect to need four
   or more kills**, and don't stop at two.
3. `grep -a "paid .* of resource" ~/Games/PIN/logs/GameServer.log`
4. Look at the crystite count in the inventory **without closing and reopening it**.
5. `dbg_inventory`

Pass: step 3 prints a line reading `Kill of monster type 528 paid N of resource 10 to <id>` with N
between 2 and 20, the count on screen goes up by N while you're looking at it, and step 5 agrees.

**Fail, `rolled nothing` on every kill across a dozen or more:** either the chance reading is wrong
or the chain isn't being walked. `grep -a "names no loot table"` separates them — that line means
the monster resolved but carries no table, which for 528 and 1196 would mean the SDB the server
opened isn't the full `clientdb.sd2`. Check `GameServer.dll.config`'s `StaticDBPath`; a pruned db
will not have `dbitems::LootTable` in it.

**Fail, the server has it and the screen doesn't:** the partial `InventoryUpdate` isn't merging.
That contradicts [G1](Resource-Payout.md), which established that it does, so check a relog before
concluding anything.

**Fail, nothing at all in the log for a kill:** `CharacterDiedEvent` isn't reaching the subscriber.
It is enqueued in `CharacterEntity.Die` and dispatched by `EventBus.Flush` at the end of the tick,
so a kill that logs `died to` and nothing after it means the subscription didn't happen —
`KillRewardSim` is constructed in the `Shard` constructor and held in a field for exactly that
reason.

## [x] K2: The rate is about one kill in four

**Measured 2026-08-16, and it settles.** Thirty-nine kills of monster 528 in one post-fix session —
the sample this entry asked for and two earlier sessions could not supply.

| Outcome | Kills | Share |
|---|---|---|
| Paid crystite (resource 10) | **8** | **20.5%** |
| Paid resource 77343 | 4 | 10% |
| Rolled an item, unpaid (33815 / 33816) | 13 | 33% |
| Rolled nothing | 14 | 36% |

**20.5% against an expected 25%, on 39 kills.** That is comfortably inside the noise band and it is
the answer: the weighted pick is happening and mode 2 is not being misapplied. The two disagreeing
sessions above (48% and 8%) were both too small to mean anything, and this supersedes them.

**Steps 2 and 3 do not add up to the kill count, and that is the entry's arithmetic being wrong
rather than a defect.** 8 + 14 = 22 against 39 kills, because a third outcome exists that the entry
never allowed for: 13 kills rolled an *item* instead of crystite or nothing. Any future run should
count four outcomes, not two.

The 4 payments of **resource 77343** are the melded sub-loot this stream already documents above,
arriving as designed — worth noting only because a tester counting crystite alone will read those
four kills as dry when the resource pane says otherwise.

The only check that exists on the `roll_mode` reading, and the reason it is written as a count.

1. Kill twenty monsters of type 528 or 1196. Use the standing spawn groups and let them respawn, or
   `npc 528` repeatedly.
2. `grep -ac "paid .* of resource 10" ~/Games/PIN/logs/GameServer.log`
3. `grep -ac "rolled nothing" ~/Games/PIN/logs/GameServer.log`

Pass: step 2 and step 3 add up to roughly the number of kills, and step 2 is somewhere around a
quarter of them. Two to nine out of twenty is a normal spread and passes.

**Two K1 sessions took this measurement by accident, they disagree, and neither settles anything.**

| Session | Kills | Crystite | Rate | Notes |
|---------|-------|----------|------|-------|
| K1 attempt 1 | 21 | 10 | 48% | pre-fix; crystite was rolled and not paid, so these are rolls |
| K1 attempt 2 | 12 | 1 | 8% | post-fix, and the run that passed |

Expected is 25%. One session came out roughly double, the next roughly a third, on 21 and 12 kills.
At that size both are ordinary noise — 12 kills has a 95% interval running from about 0 to 6 — so
the honest reading is that **nothing has been measured yet.** Do not tune anything on these.

Take it in one sitting of 40 or more, all post-fix, and count from a single session rather than
adding these together: attempt 1 predates the scale change, which altered how the second loot table
rolls, so the two are not the same experiment. If 40 kills still land near half, the 25% subtable is
not the only crystite source on the path and the chain wants tracing again from `loot_table2_id`.

**Fail, every kill pays:** mode 2 is being applied where a weighted pick belongs, or the 25%
subtable roll isn't happening. This is the failure that looks like success in game and is worth
watching for on that account.

**Fail, no kill ever pays across twenty:** the weighted branch is consuming the roll wrong, or
probability isn't a percentage on this path after all.

Record the actual count either way. It is the only measurement of these tables anyone has.

## [x] K3: Equipment and powerups are rolled and visibly not paid

**Passed 2026-08-15.** Both halves, including the one K1 left open. Two rolls of powerup 33816 —
22:36:11 off a 528 and 22:37:43 off a 1189 — each logged
`rolled item 33816 x1, not paid — item drops are unbuilt`. The bag was counted either side:
`249 item(s) [Gear 249], 2 resource(s), 20 loadout(s)` at 22:34:51 and the identical line at
22:47:57. Nothing arrived, and the log said so both times rather than staying quiet. The
`Resource` flag is behaving.

Resource payout kept working across the same kills — 2, 3 and 4 of resource 10 on three separate
kills — so the unpaid item path is not swallowing the paid resource path.

A check that the gap is honest rather than silent — the thing G5 taught, that "nothing arrived" and
"nothing was owed" must be distinguishable from the log alone.

1. Kill monsters until the log has at least one line about an unpaid item. Powerups (33815, 33816)
   come off table 2 at 20% and 15%, so this should take fewer kills than K1.
2. `grep -a "not paid — item drops are unbuilt" ~/Games/PIN/logs/GameServer.log`
3. `dbg_inventory`

Pass: step 2 names an item id and a quantity, and step 3 shows no new item. The point is that the
roll is recorded even though nothing is delivered.

**Fail, an item does arrive in the bag:** the `Resource` flag on that item's `RootItem` row is set
when it shouldn't be, so the resource path is being handed an item id. Check the flag before
blaming the code — the powerups 33815 and 33816 both read 8192, with bit 0x10 clear.

**Half-answered by K1's passing run.** The log printed
`rolled item 33815 x1, not paid — item drops are unbuilt`, so the roll is recorded and named. What
was not checked is step 3: nobody looked at the bag afterwards to confirm nothing arrived.

## [~] K4: An environmental death and an NPC-on-NPC kill pay nobody

**Hazard half passed 2026-08-16.** The player was killed outright by the melding at 11:44:59 —
`went down to nothing`, no killer entity — and no `paid`, `rolled` or `Kill of monster type` line
appears anywhere around it. A death with no attacker pays nobody, which is the half that could
plausibly have crashed or credited a null.

**The NPC-on-NPC half did not happen.** No `NPC ... died to <another NPC>` line exists in the
session at all. Aranha and Chosen are mutually hostile, but nothing put them in reach of each other
this run. Still open, and it needs the two factions deliberately stood next to each other rather
than a wave.

The guards. Both are reachable without another player.

1. Stand at a melding wall or walk into deep water and die to it. See [E1](Environment.md) for which
   hazards are live.
2. `grep -a "paid .* of resource" ~/Games/PIN/logs/GameServer.log | tail -3`
3. Let two hostile NPCs fight — `npc 528` next to a Chosen group will do it, and it is the same
   mutual hostility that killed zone 448 four times over in [N14](NPC-Combat.md).
4. `grep -a "paid .* of resource" ~/Games/PIN/logs/GameServer.log | tail -3`

Pass: neither step adds a line. A hazard death has no killer at all, and an NPC killer has no
`Player` to pay.

**Fail, your own death pays you:** the victim/killer check is inverted. That would also mean a
player death is being treated as a lootable monster, which is the PvP case
[Restoration](../Restoration.md) says is out of scope.

## [ ] K5: The kill that pays is the killer's, not the last player to log in

Only runnable with two clients, which is what usually makes an entry like this sit unrun. Left in
the queue because credit is the whole point of the feature.

1. Two clients logged in to zone 448, standing apart.
2. One kills a monster on its own. The other does not fire.
3. `grep -a "paid .* of resource" ~/Games/PIN/logs/GameServer.log | tail -1`
4. `dbg_inventory` on both.

Pass: the entity id in the log line is the killer's character, and only that client's crystite moved.

**Fail, the wrong character is named:** `CharacterDiedEvent.Killer` is being filled from whatever
last damaged the entity rather than what landed the killing blow. Worth checking against
`CharacterEntity.Die`'s own `killer` parameter before blaming the reward code.

**Not runnable is a valid outcome** — mark it `[-]` and say so. Retail also shared credit across a
squad and PIN does not; that gap belongs to M7, not here.
