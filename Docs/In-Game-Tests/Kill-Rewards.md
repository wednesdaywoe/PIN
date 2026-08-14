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

**K1 ran once on 2026-08-13 and failed on a wrong table lookup. Fixed, not re-run.** 21 kills rolled
crystite ten times and paid it zero times: `IsResourceItem` asked `dbitems::ResourceItem`, which is
the gatherable-materials list (Brimstone, Ferrite and the rest, ids 75537 and up) and does not
contain crystite at id 10. So every roll was correct and every one of them was filed as an unpayable
item. The check is now the `Resource` flag on the item's own `RootItem` row, which is what
`createitem` has always used and what [G1](Resource-Payout.md) confirmed against the client.

**The same run confirmed the basis-points guess, on a table that is on the kill path.** "Melded Loot
Common" (5463) is reached from monster 528's second loot table and its rows are 5000/1000/100. Read
as percentages that is a distribution that drops something every time; it is out of 10000.
`LootRoller` now decides the scale per table from its own rows. See [DATA-16](../gaps/data.md).

**Nothing else in the stream has run.**

## What changed about this milestone

M5 was written as XP first and loot second. It is now crystite first and no XP at all
(decision 2026-08-13, user-chosen). Two reasons, and the second is the one that settles it:

- Levelling is inert in PIN and is staying that way. Character level is a hardcoded 45, nothing
  scales off it, and power comes entirely from the loadout — the beta model, arrived at by accident.
  See [Restoration](../Restoration.md).
- The data agrees. `dbcharacter::Monster.xp_resource_id` is non-zero on **2 rows of 3109** and
  `xpreward_type` on 12. Nobody ever finished wiring XP to monsters. Its loot tables, by contrast,
  are complete and tuned.

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
sub-loot) and both roll equipment and resource-item tiers. **None of those are paid.** They are
items rather than resources, and putting an item in a player's hands is [I1](Inventory.md)'s
unsolved problem. `KillRewardSim` logs each one it rolled and drops it.

## What is a guess

`roll_mode` is a guess. The column carries six distinct values, the client is the only thing that
ever read them, and the two behaviours PIN implements were inferred from the arithmetic of the
tables themselves — mode 2 as independent per-entry chances (100/100/8 on one table can't be
anything else), everything else as a weighted single pick (80/10/5/5 summing to exactly 100 can't be
anything else). Modes 1, 4 and 5 are unexercised.

Probability is read as a percentage. It fits every table on this path, but the column reaches 10000
elsewhere in the file, so some table somewhere is in basis points and will roll wrong.

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

## [!] K1: A kill pays crystite

**Failed 2026-08-13 on the first run, and the failure was one line of my code rather than anything
about loot.** 21 kills, 10 crystite rolls, 0 paid. Every line in the log read
`rolled item 10 x2, not paid — item drops are unbuilt`, and **item 10 is crystite** — the roller was
right and the classifier was wrong. `IsResourceItem` consulted `dbitems::ResourceItem`, a table that
exists, has 111 rows, and is the wrong one: it lists gatherable materials from id 75537 up, not
things that live in the resource pane. Now reads `ItemFlags.Resource` off `RootItem`, the same check
`createitem` uses.

Worth keeping as a shape: the entry's own "Fail, the server has it and the screen doesn't" branch
did not apply, and neither did "nothing in the log". The log was talkative and correct about what it
had done. What made it findable in one pass was **logging the id and quantity of the thing it
declined to pay** — the K3 line, written for a different reason, is what identified the bug.

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

## [ ] K2: The rate is about one kill in four

The only check that exists on the `roll_mode` reading, and the reason it is written as a count.

1. Kill twenty monsters of type 528 or 1196. Use the standing spawn groups and let them respawn, or
   `npc 528` repeatedly.
2. `grep -ac "paid .* of resource 10" ~/Games/PIN/logs/GameServer.log`
3. `grep -ac "rolled nothing" ~/Games/PIN/logs/GameServer.log`

Pass: step 2 and step 3 add up to roughly the number of kills, and step 2 is somewhere around a
quarter of them. Two to nine out of twenty is a normal spread and passes.

**The failed K1 run took this measurement by accident and it came out high**: 21 kills, 10 crystite
rolls, 6 dry, 7 powerups, 1 melded resource. Ten of 21 is 48% against an expected 25%, which is
about 2.4 standard deviations — unlikely rather than impossible at that sample size. Two things make
it not yet a finding. The sample is 21, and the run predates the scale fix, which changed how the
second loot table rolls. **Re-take it at 40 kills or more before drawing anything from it.** If it
stays near half, the 25% subtable roll is not the only crystite source on the path and the chain
needs tracing again from `loot_table2_id`.

**Fail, every kill pays:** mode 2 is being applied where a weighted pick belongs, or the 25%
subtable roll isn't happening. This is the failure that looks like success in game and is worth
watching for on that account.

**Fail, no kill ever pays across twenty:** the weighted branch is consuming the roll wrong, or
probability isn't a percentage on this path after all.

Record the actual count either way. It is the only measurement of these tables anyone has.

## [ ] K3: Equipment and powerups are rolled and visibly not paid

A check that the gap is honest rather than silent — the thing G5 taught, that "nothing arrived" and
"nothing was owed" must be distinguishable from the log alone.

1. Kill monsters until the log has at least one line about an unpaid item. Powerups (33815, 33816)
   come off table 2 at 20% and 15%, so this should take fewer kills than K1.
2. `grep -a "not paid — item drops are unbuilt" ~/Games/PIN/logs/GameServer.log`
3. `dbg_inventory`

Pass: step 2 names an item id and a quantity, and step 3 shows no new item. The point is that the
roll is recorded even though nothing is delivered.

**Fail, an item does arrive in the bag:** `SDBInterface.IsResourceItem` is answering true for
something that isn't in `dbitems::ResourceItem`, which would mean the resource path is being handed
item ids.

## [ ] K4: An environmental death and an NPC-on-NPC kill pay nobody

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
