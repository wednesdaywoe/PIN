---
project: pin
kind: test-stream
title: "Resource Payout (G1-G5)"
relates:
  - ../TEST-REGISTER.md
---

# Resource Payout

Part of the [in-game test queue](README.md). Setup, admin commands and the type-id tables:
[Session Setup](Session-Setup.md).

The check on [M3](../streams/m3-resource-payout.md). The milestone is one sentence — finish a
thumper and come away holding crystite you didn't have — and the code for it was written twice over
without anyone watching it run.

**G1, G2 and G5 pass as of 2026-08-13, all first attempt, and G2 is the milestone's exit
condition.** A thumper called down at a player's feet ran its full cycle, paid 200 crystite to one
participant, and the crystite showed up — the delivery half having been settled by G1 an hour
earlier, when a count climbed 0 to 600 in an inventory that was already open. G5 passed in the same
session without being run, because it is a check on an unattended timer and the timer went off.

**Two entries are left and neither blocks the milestone.** G3 asks whether the client can call a
thumper down without the admin command, and G4 asks what happens when you cut one short.

One thing G2 did not do: exercise the aptitude command. The payout is read straight out of
`CustomDBInterface` by `Thumper.OnSuccess`, so M3's first cut — the uncommented `case` in
`Factory.LoadCommand` — is still code that has never run.

Crystite is resource SDB id **10**.

**M4 replaced the payout model on 2026-08-13, after G1/G2/G5 passed.** The flat 200 from grant
50329 is gone: a thumper now pays what the ground under it holds, sampled from the shipped yield
gradient of the deposit it stands in, scaled by its progress bar. The G2 and G5 records below
describe the old behaviour and stay as history; a re-run of their steps pays deposit-dependent
amounts instead of 200, and the current expectations live in
[Thump Placement](Thump-Placement.md). G4 was rewritten for the new model rather than kept as
history, since it had never run.

**The stream is split at the delivery boundary on purpose**, and the split earned itself. G1 asked
only whether a resource count can change on screen at all, with no thumper anywhere near it, because
a payout that arrives correctly and is never drawn looks exactly like a payout that never happened —
which is what [I1](Inventory.md) has been stuck on since 2026-08-11 for items. Resources travel in a
different array of the same message, and it turns out they travel fine.

**A thumper takes seven and a half minutes.** From the moment it lands: 10s warming up, 300s
thumping, 6s closing, 120s completed, 12s leaving, and the payout is at the end of the last one.
Budget for it, and don't call the run dead early — `TransitionToState` now logs every step, which is
the only way to tell thumping from stalled, since neither looks like anything on screen.

Every entry wants the server log open, and confirm the build banner first the way
[NPC Combat](NPC-Combat.md) describes:

```
tail -f ~/Games/PIN/logs/GameServer.log
grep -a "Running build" ~/Games/PIN/logs/GameServer.log
```

The two lines this stream reads:

```
grep -a "Thumper .* entered" ~/Games/PIN/logs/GameServer.log
grep -a "paying .* resource" ~/Games/PIN/logs/GameServer.log
```

## How a thumper gets called down

There are two routes and they are not equivalent.

**The retail one** is the client's own calldown UI, which sends `ResourceNodeBeaconCalldownRequest`
with a position. The server handles it — `BaseController` takes the message and
`ResourceNodeBeaconCalldownCommand` consumes it — but the client only offers the option if a beacon
item is in the inventory, and putting an item in the inventory is exactly what [I1](Inventory.md)
says is broken. Whether the UI offers anything today is unknown, and **G3 is the entry that finds
out**.

**The admin one** is `thumper`, added so the payout could be tested without waiting on that. It
calls one down at your feet, owned by you, using the same `CreateThumper` the real path ends in. It
skips the request handling and nothing else.

Zone 448 already has a thumper standing in it, near the Battleframe Station, spawned by
`TempSpawnTestEntities`. **It is owned by the Aero, not by you**, and an NPC contributes no
participant, so it pays nobody when it finishes. Don't mistake it for yours — that one is G5.

## [x] G1: A resource count changes without a relog

**Passed 2026-08-13, first attempt, and it answers more than it asks.** The tester started on 0
crystite, went to 200 on one `createitem 10 200`, and then to 400 and 600 on two more with the
inventory open, watching the number climb each time alongside the pickup toast. Three consecutive
partial updates merged into an open UI.

That settles the delivery half of M3 before a thumper is involved, and it takes
`SendResourceUpdate` off the suspect list for good: **the client does accept a partial
`InventoryUpdate` and does merge it into an inventory already on screen.** Which narrows
[I1](Inventory.md) usefully — whatever hides an item is in the item struct or the item arrays, not
in partial updates as a mechanism, since the same message shape delivers a resource correctly.

The starting 0 is worth keeping. Nothing gives a character resources at login any more —
`FallbackInventoryResources` is gone from the code — so every later entry gets an unambiguous
baseline, and 200 arriving is 200 that came from somewhere.

The control for everything below, and the one entry here that needs no thumper. It also stands in
for `SendResourceUpdate` generally, which nothing had ever confirmed the client accepts.

1. Log in to zone 448 and open the inventory. Find crystite and write down the number.
2. `dbg_inventory`
3. `createitem 10 200`
4. Look at the crystite count **without closing and reopening the inventory**, then look again after
   reopening it.
5. `dbg_inventory`

Pass: the count goes up by 200 while you are looking at it, and step 5 shows the same total the
screen does.

**Fail, the server has it and the screen doesn't:** step 5 is 200 higher than step 2 but the UI
never moves. That is the partial `InventoryUpdate` not being merged, which is
[I1](Inventory.md)'s second bullet arriving on the resource side, and it would mean the M3 exit
condition ("updated in the UI without a relog") cannot be met without pointing `SendResourceUpdate`
at the full inventory send. Check whether a relog shows the 200 before concluding anything: it
separates "not merged" from "not delivered".

**Fail, neither has it:** `createitem` didn't treat 10 as a resource. `grep -a "createitem"
~/Games/PIN/logs/GameServer.log | tail -5` prints what it decided — the line says `as resource` or
`as item`, and `as item` means the `Resource` flag isn't set on the type id.

## [x] G2: A thumper you called down pays you

**Passed 2026-08-13, first attempt, and [M3](../streams/m3-resource-payout.md)'s exit condition with
it.** The greps were run afterwards off the session log rather than during, which turned out to be
enough — the whole run is in there, to the second:

```
18:48:51 thumper 766269 for 11072869122414870784 at <-123.92, 438.70, 401.32>, calldown 12000ms
18:49:03 Thumper 2305086966120779008 entered WARMINGUP, for 10000ms
18:49:13 Thumper 2305086966120779008 entered THUMPING, for 300000ms
18:54:13 Thumper 2305086966120779008 entered CLOSING, for 6000ms
18:54:19 Thumper 2305086966120779008 entered COMPLETED, for 120000ms
18:54:22 Thumper 2305086966120779008 entered LEAVING, for 12000ms
18:54:34 Encounter 2305086966120779264 paying 200 of resource 10 to 1 participant(s)
```

Every countdown ran to its stated length, which is the part nothing had ever confirmed. **The one
line that isn't the entry's script is `COMPLETED` lasting three seconds instead of 120** — that is
the tester collecting rather than waiting, and it is the interaction path doing exactly what it
should: `OnInteraction` sets the countdown to now, the state moves on, and the payout lands twelve
seconds later. `to 1 participant(s)` is the number that matters. Ownership reached the encounter.

The whole cycle took 5m43s, not the 7½ minutes this entry budgets for, because the 120-second
`COMPLETED` wait is only there if you let it run.

**The aptitude command was not exercised.** `Thumper.OnSuccess` reads grant 50329 out of
`CustomDBInterface` and pays directly; it never constructs a `ModifyOwnerResourcesCommand`, and
`grep -ac ModifyOwnerResources` on the session log returns 0. So M3's first cut — the uncommented
`case` in `Factory.LoadCommand` — is still code that has never run. Nothing here tests it.

The steps, for a re-run. Run G1 first.

1. Walk — do not `tp` — to open flat ground away from the station. The command warns if you were
   put where you're standing rather than having walked there, and a thumper under the terrain is
   the same failure [N14](NPC-Combat.md) hit three times.
2. Note the crystite count.
3. `thumper`
4. Watch it land. Then leave it alone for seven and a half minutes — don't interact with it, that's
   G4.
5. `grep -a "Thumper .* entered" ~/Games/PIN/logs/GameServer.log`
6. `grep -a "paying .* resource" ~/Games/PIN/logs/GameServer.log`
7. `dbg_inventory`

Pass: step 5 shows the full sequence — `LANDING`, `WARMINGUP`, `THUMPING`, `CLOSING`, `COMPLETED`,
`LEAVING` — step 6 prints one line reading `paying 200 of resource 10 to 1 participant(s)`, the
thumper is gone from the world, and crystite is 200 higher on screen and in step 7.

**Fail, `to 0 participant(s)`:** the encounter was built with no participant, so the payout went
nowhere. That is what happens for an NPC-owned thumper (G5) and should be impossible for this one;
if it happens here, `CharacterEntity.Player` was null for your own character.

**Fail, the log stops at a state and stays there:** read which one. Stopping at `THUMPING` for
longer than five minutes is the encounter no longer being updated —
`EncounterMan.StartUpdatingEncounter` is called from the `Thumper` constructor, and
`StopUpdatingEncounter` only from `OnSuccess`.

**Fail, step 6 prints nothing but the thumper vanished:** `OnSuccess` ran and the grant lookup came
back empty. `CustomDBInterface.GetModifyOwnerResourcesCommandDef(50329)` is the call; check
`StaticDB/CustomData/Todo/aptgss_ModifyOwnerResourcesCommandDef.json` reached the deploy directory.

**Fail, nothing lands at all:** `grep -a "thumper 766269" ~/Games/PIN/logs/GameServer.log` — the
command logs the def it resolved and the calldown time. No line means the command didn't run; a line
with a calldown time and no `LANDING` afterwards means the entity spawned and the encounter didn't.

## [ ] G3: The client can call a thumper down on its own

The retail path, and the entry most likely to come back with nothing — which is itself the result
worth having, because it decides whether M3 closes on the real route or on an admin command.

1. Log in and look for a thumper or beacon in the calldown UI, however the client offers it.
2. If nothing is offered, try `createitem` with a beacon item type id and look again. The type ids
   aren't written down anywhere yet — this stream has never been run, and the client db search that
   would find them hasn't been done.
3. If the client offers one, call it down and watch the log.

Pass: `grep -a "ResourceNodeBeaconCalldown" ~/Games/PIN/logs/GameServer.log` shows the request
arriving, a thumper lands where you placed it, and the rest of G2 follows from step 4.

**Not runnable is a valid outcome.** If no beacon can be got into the inventory, mark this `[-]`,
say so, and note that M3's exit was met through `thumper` instead. It reopens as soon as
[I1](Inventory.md) does.

**Fail, the request arrives and nothing lands:** `TryConsumeResourceNodeBeaconCalldownRequest`
returned the request to a chain that didn't run, or the ability the client fired isn't one that
contains a `ResourceNodeBeaconCalldownCommand`. The request is logged as discarded if a second one
arrives unconsumed — `grep -a "unconsumed thumper" ~/Games/PIN/logs/GameServer.log`.

## [ ] G4: Collecting early pays the fraction it managed

Rewritten for M4 on 2026-08-13, before ever running. The original entry asked whether the flat
grant was genuinely flat (it was, by code reading); now `Thumper.OnSuccess` multiplies every rolled
quantity by the progress bar, so a thumper cut short pays the fraction it mined. That is the answer
G2 accidentally previewed — the tester collected at `COMPLETED`, when progress was already 1.00,
and was paid in full.

Run it inside the Basin Head Crystite deposit (walk to within a few metres of 199.8, 315.7, 67m
from the Battleframe Station), because a fraction of a 40–100 roll is measurable and a fraction of
barren ground's single unit is not. That deposit moved on 2026-08-13: its old center by the station
was inside a no-thumping zone ([DATA-17](../ISSUE-REGISTER.md)).

1. Note the crystite count. `thumper` — expect `in deposit [1] Basin Head Crystite`.
2. Let it reach `THUMPING` — `grep -a "Thumper .* entered" ~/Games/PIN/logs/GameServer.log | tail -3`
   confirms it — then wait roughly 2½ minutes, about half the 300-second thumping window.
3. Interact with it (hold to use).
4. `grep -a "mined node type" ~/Games/PIN/logs/GameServer.log | tail -1` — the line prints
   `completion 0.xx`, which should sit near 0.50.
5. `grep -a "paying .* resource" ~/Games/PIN/logs/GameServer.log | tail -1`

Pass: the payout is about half of a 40–100 center roll — 20–50, and consistent with step 4's
completion figure (payout ≈ roll × completion). `dbg_inventory` matches.

**Fail, it pays the full roll despite `completion 0.xx`:** the scaling isn't applied on the
interaction path — `OnInteraction` sets `LEAVING` without a final `SetProgress`, so check what
`Progress` held when `OnSuccess` read it.

**Fail, it pays twice** — once for the interaction and once when the natural cycle would have
finished — the state machine is being driven from two places. That would be a real defect.

### Watch the departure, not just the payout

**Observed 2026-08-14, and it has a cause in the code.** A thumper collected early "played the
entire animation with SFX"; every thumper sent away before that had shot straight upward at full
speed, instantly and in silence. Nothing about the deposit is involved — a deposit only carries a
node type and a yield gradient, and touches neither the state machine nor any animation. **What
differs is which ability fires**, and there are two paths through
[Thumper.OnInteraction](../../UdpHosts/GameServer/Systems/Encounters/Encounters/Thumper.cs#L27-L40):

| You press E while it is | What fires | Where that id comes from |
|-------------------------|-----------|--------------------------|
| `THUMPING` — cut short, this entry's case | the beacon's own `CompletedAbility` | shipped data, `aptfs::ResourceNodeBeaconCalldownCommandDef.completed_ability`, which reads **123** on all 40 rows |
| `COMPLETED` — waited out, then collected | ability **34216** | a literal in PIN's source, read from nothing |

So the early path plays retail's own extraction and the ordinary path plays PIN's stand-in, which
is [DATA-18](../ISSUE-REGISTER.md). Note in step 3 which one you took and whether the departure had
an animation and a sound; that is the observation the issue is waiting on, and this entry is the
only one that takes the early path deliberately.

The natural cycle fires two more literals on its way past — `34579` when warm-up ends and `34215`
when thumping ends — and neither is read from data either. `34215` is confirmed firing in game
(`Ability 34215 starting Chain 223317`, 2026-08-14); its chain ends in a `NO-OP (Client
PlayAnimation)`, PIN's marker for a command it deliberately leaves to the client.

## [x] G5: The Aero's thumper pays nobody, quietly

**Passed 2026-08-13 without anyone running it**, which is the ideal way for this particular entry to
pass — it is a regression check on an unattended timer, and it ran unattended during G2's session:

```
18:47:22 Thumper 2305085278198562560 entered WARMINGUP, for 10000ms
18:52:32 Thumper 2305085278198562560 entered CLOSING, for 6000ms
18:54:38 Thumper 2305085278198562560 entered LEAVING, for 12000ms
18:54:50 Encounter 2305085278198562816 paying 200 of resource 10 to 0 participant(s)
```

`to 0 participant(s)` is the line the entry was written for. The shard kept ticking, the session
outlived it, and the only exceptions in the whole log are the twelve known pose-file ones. Its
`COMPLETED` ran the full 120 seconds, because nobody interacted with it — the contrast with G2's
three seconds is what a collected thumper looks like against an ignored one.

Worth noting how close together the two payouts landed: 18:54:34 and 18:54:50. Both thumpers
finished inside the same twenty seconds, one paying a player and one paying nobody, and the only
thing distinguishing them in the log is the participant count. That is the whole argument for
logging it.

A regression check on a crash that has already happened once. The zone's debug thumper is owned by
an NPC, so its participant set is empty, and reaching through it at completion took the whole shard
tick down before `BaseEncounter` started stripping nulls. It completes about seven and a half
minutes after the server starts, unattended.

1. Start the server and log in. Do nothing about the thumper near the Battleframe Station.
2. Stay logged in past the eight-minute mark.
3. `grep -a "paying .* resource" ~/Games/PIN/logs/GameServer.log`
4. `grep -aic "exception\|unhandled" ~/Games/PIN/logs/GameServer.log`

Pass: step 3 includes a line reading `to 0 participant(s)`, step 4 finds nothing new, the session is
still alive, and your own crystite is unchanged.

**Fail, the shard stops ticking at that moment:** the null participant is back. It arrives from
`EncounterManager.CreateThumper`, which builds the set out of `owner.Player`.

**Fail, you were paid 200 by a thumper you never called:** ownership isn't reaching the participant
set correctly and G2's result means less than it looks like.

## [ ] G6: A finished thumper leaves the world, and stops being asked about

The check on [NET-24](../ISSUE-REGISTER.md). Until 2026-08-14 a thumper that ran its full cycle paid
out, vanished from the shard, and **stayed standing on screen** — while the client asked for a fresh
keyframe of the dead entity every ~5.5 seconds for the rest of the session. One 32-minute sitting
logged 648 of those across four finished thumpers, and the server's whole response was a warning.

Three changes went in on the strength of a code read alone, and this entry is what tells us which of
them mattered:

- a failed keyframe request for a **view** now answers with the scope-out that removal should already
  have delivered, instead of only logging
- successful keyframe requests log at Debug, so the loop can be dated. **This is the measurement the
  entry exists for** — the old log recorded only failures, so nothing could tell "the client has been
  asking all along" from "it started asking when the entity died"
- a queued scope-in whose entity died in the meantime is dropped rather than handing the client a
  fresh copy of something already removed

Run the server at Debug. The lines:

```
grep -a "KeyframeRequest" ~/Games/PIN/logs/GameServer.log
grep -a "Dropped a queued scope-in" ~/Games/PIN/logs/GameServer.log
grep -a "entered LEAVING\|mined node type" ~/Games/PIN/logs/GameServer.log
```

1. Call a thumper down and let it run the whole cycle. Collect at `COMPLETED` — that is the path all
   four affected thumpers took, and the one a cut-short thumper does not.
2. Watch the spot for a minute after the payout. Note whether the model is still there.
3. Grep the three lines above. Note the timestamp of the **first** `KeyframeRequest` naming the
   thumper's entity id, and whether it is before or after `mined node type`.

Pass: the model goes, and no `KeyframeRequest` names that entity id more than a second or two after
it was removed.

**Pass on the model but the requests continue:** the client is ignoring a scope-out it is now being
sent twice. That moves the fault into the client and makes
[Client UI Source](../Client-UI-Source.md) the next place to read — the resource-node components are
the only remaining oracle for what makes the client drop a node.

**The requests start well before the payout:** the loop is desync recovery, not a removal artifact,
and the thumper's view deltas going out on `UnreliableGss` are the suspect — a full cycle sends about
106 of them against roughly 7 for a thumper cut short at 4%, which is the shape of the split the
2026-08-14 sitting saw. `Character_CombatView` is already pinned to `ReliableGss` for the same
reason. Do not make that change before this reading; it is the thing being measured.

**The requests start at the payout and stop after one scope-out:** the first scope-out was lost, and
NET-24 is [NET-1](../gaps/network.md#net-1) — a reliable channel that acks but never resends — in
costume. That is the one outcome that closes this entry and reopens a bigger one.
