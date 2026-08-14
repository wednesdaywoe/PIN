---
project: pin
kind: test-stream
title: "Thump Placement (S1-S6)"
relates:
  - ../TEST-REGISTER.md
---

# Thump Placement

Part of the [in-game test queue](README.md). Setup, admin commands and the type-id tables:
[Session Setup](Session-Setup.md).

The check on [M4](../streams/m4-thump-placement.md). The milestone is that where you thump matters:
zone 448 now carries four resource deposits, a scan shows them, a ground report reads one, and a
thumper's payout comes from the spot it stands on rather than from a constant. **The flat 200-crystite
grant is gone** — [Resource Payout](Resource-Payout.md)'s G2 record describes the old behaviour.

The four deposits, from
[resource_deposit.json](../../UdpHosts/GameServer/StaticDB/CustomData/resource_deposit.json). Only
the first is on a coordinate a tester has actually stood on (the G2 thumper site); the other three
centers are walked spawn-group footings, but their discs extend over unwalked ground:

| Id | Name | Node type | Center | Radius | Pays at center |
|----|------|-----------|--------|--------|----------------|
| 1 | Station Shelf Crystite | 242 | 158.3, 249.3, 491.9 | 30m | 40–100 crystite |
| 2 | Basin Mouth Iron | 233 | 138.8, 181.3, 401.0 | 45m | 25–35 raw iron (86668) |
| 3 | Basin West Crystite Trace | 241 | 85.5, 226.8, 400.9 | 35m | 10–25 crystite |
| 4 | North Flats Crystite and Iron | 239 | 31.6, 268.9, 401.0 | 35m | 8–15 crystite + 16–32 raw iron |

Quantities fall linearly from those center ranges to the rim ranges (mostly 0–5), so **standing at
the middle of a disc pays several times what its edge does**, and a rim roll can legitimately come
up empty. Deposit membership and the gradient are measured flat, in X and Y — height never decides
anything, so the shelf deposit reaches over the cliff it sits on.

A thumper on ground no deposit covers mines node type 20, "Thumper Sifted Earth", whose shipped
yield is one unit of item 30404. Whether that item carries the `Resource` flag is unknown — either
way a barren thump pays approximately nothing, which is the point.

The log lines this stream reads:

```
grep -a "Resource scan" ~/Games/PIN/logs/GameServer.log
grep -a "Geo report" ~/Games/PIN/logs/GameServer.log
grep -a "mined node type" ~/Games/PIN/logs/GameServer.log
grep -a "paying .* resource" ~/Games/PIN/logs/GameServer.log
```

A thumper still takes up to seven and a half minutes; the state-transition budget is in
[Resource Payout](Resource-Payout.md).

## [ ] S1: A scan comes back with the zone's deposits

The scan chain has never run — `ResourceNodeScanDefCommand` was a stub until M4, and the `case`
constructing it was commented out. The two named test abilities from the shipped def comments are
33918 ("Scan for resource nodes near the player") and 34126 ("Scans for thumper nodes in a 600 meter
radius"). Both send `FoundResourceAreas`; what the client draws with it is the unknown this entry
exists to see.

1. Log in to zone 448. Stay near the Battleframe Station.
2. `ability 34126`
3. `grep -a "Resource scan" ~/Games/PIN/logs/GameServer.log` — expect
   `Resource scan 181662 by <your entity id>: 4 deposit(s) within 600m`.
4. Open the map and look for an overlay — blobs, rings, anything near the four centers in the table
   above.
5. `ability 33918` and repeat the grep — expect the same 4 deposits within 100m (the range check is
   flat and every disc edge is inside 100+radius of the station).

Pass: the log line counts 4 both times, and the client draws *something* it didn't draw before.

**Half-pass is a real outcome here**: the server sending 4 areas and the client drawing nothing
still proves the chain end to end on the server side, and says the overlay wants something else —
likely `Unk4`, which PIN fills with the radius in metres on an explicit guess. Record what the map
shows either way; if blobs render at a wrong-looking uniform size, the radius guess is the first
suspect (see the remark in `ResourceNodeScanDefCommand`).

**Fail, no log line:** the ability's chain didn't reach the command. `grep -a "ability 34126"` for
the admin command's own output, then check the chain id printed by `ability` — the def comments'
ability-to-command mapping (33918→176829, 34126→181662) is recovered, not confirmed.

## [ ] S2: The ground says what is under it

`GeographicalReportRequest` finally has a handler. The client sends it carrying only its own verdict
on the spot (`OK`, `NOTHUMPINGZONE`, `INVALIDSURFACE`); the server reads the ground under your feet
and answers with a composition, or with an invalid (empty) report off a deposit. **How the client UI
triggers the request is unknown** — likely the survey/scan option in the thumper calldown flow.
Finding the trigger is part of the entry.

1. Stand on the station shelf, within 30m of 158.3, 249.3 — the G2 thumper site is the center.
2. Trigger a geological report however the client offers it (calldown UI, map, scanner). If S1's
   scan made the map show deposits, try clicking one.
3. `grep -a "Geo report" ~/Games/PIN/logs/GameServer.log` — expect
   `feedback OK, scan 1, node type 242, 1 resource(s)`.
4. Walk somewhere no deposit covers (100m+ from every center in the table) and trigger another.
   Expect the grep to end in `barren`.
5. Open the map. `MapOpened` now re-sends your latest report instead of a hardcoded empty one —
   note whether the map shows the reading taken in step 2/4.

Pass: the two greps say deposit then barren, and the client renders the deposit reading as a
non-empty report (composition percentages, however it draws them).

**Not runnable is a valid outcome** if no UI action sends the request — mark `[-]`, note it, and the
server half stays covered by the grep in S3's setup. The handler also fires on whatever the client
does send, so the grep after any session says whether the message ever arrived at all.

## [ ] S3: Thumping the rich center pays center values

The milestone's exit condition, first half. Same shape as G2, on the one deposit whose center is
walked ground.

1. Run [G1](Resource-Payout.md) first if this is a fresh character, for the baseline.
2. Walk — do not `tp` — to within a few metres of 158.3, 249.3 (beside the G2 thumper site, near
   the Battleframe Station). Note the crystite count.
3. `thumper` — the feedback line now names the ground: expect
   `in deposit [1] Station Shelf Crystite (node type 242)`.
4. Let it run its full cycle untouched — interacting early is G4's entry.
5. `grep -a "mined node type" ~/Games/PIN/logs/GameServer.log` — expect
   `mined node type 242 at distance fraction 0.0x of deposit [1] Station Shelf Crystite,
   completion 1.00: 1 resource kind(s)`.
6. `grep -a "paying .* resource" ~/Games/PIN/logs/GameServer.log` — expect
   `paying <40–100> of resource 10 to 1 participant(s)`.
7. `dbg_inventory` and the on-screen count: up by the same 40–100.

Pass: the payout is inside 40–100, matches on screen, and the completion summary (the client's
thump-results screen, if `ResourceNodeCompletedEvent` renders) shows crystite at 100%.

**Fail, it pays some other number:** if it's exactly 200, the old flat grant is still on the path —
wrong build deployed. If it's 0–5, the distance fraction read the rim at the center; step 5's
`distance fraction` says which happened.

## [ ] S4: Thumping barren ground pays approximately nothing

The other half of "where you thump matters". Anywhere 100m+ from all four centers works; the
walked-ground candidates are along the basin road between deposits.

1. Walk to open ground no deposit covers. The G2 site at -123.9, 438.7, 401.3 is 220m from the
   nearest center and was walked to once already.
2. Note the crystite count. `thumper` — expect the feedback line to say
   `on barren ground (node type 20)`.
3. Full cycle untouched.
4. `grep -a "mined node type" ~/Games/PIN/logs/GameServer.log` — expect
   `mined node type 20 ... 1 resource kind(s), 1 total` or `0 resource kind(s)`.
5. `grep -a "not paid" ~/Games/PIN/logs/GameServer.log` — if item 30404 lacks the `Resource` flag,
   this prints `extracted item 30404 x1, not paid`; if it has it, step 6 pays 1 of it instead.
6. `dbg_inventory` — crystite unchanged.

Pass: crystite unchanged, and the log accounts for the sifted earth one way or the other. Seven and
a half minutes for a single unit of dirt is retail's own answer to thumping nowhere.

## [ ] S5: A poor deposit visibly underpays a rich one

Exit condition, second half: "thump a rich one and a poor one, and come away with visibly different
piles". Basin West Crystite Trace pays 10–25 at center against the shelf's 40–100 — the ranges
cannot overlap, so two single runs are conclusive.

1. With S3's payout in the log, walk down to Basin West — center 85.5, 226.8, on the basin floor.
   The walked footing is the Basin West spawn-group line; the pack there will notice you inside
   25m, so clear it or accept the fight.
2. `thumper` — expect `in deposit [3] Basin West Crystite Trace (node type 241)`.
3. Full cycle untouched.
4. `grep -a "paying .* resource" ~/Games/PIN/logs/GameServer.log | tail -1` — expect 10–25 of
   resource 10.

Pass: this payout is 10–25, S3's was 40–100, and the two numbers are not adjacent — the poor pile
is visibly poorer.

If the Aero's debug thumper has finished by now it adds its own line — it stands inside deposit 1
and resolves like everything else since M4, so expect a `paying <40–100> of resource 10 to 0
participant(s)` from it. **The participant count still tells the thumpers apart**; that is G5's
check surviving the payout model change.

## [ ] S6: A deposit placed by walking survives a restart

The `deposit` command is the authoring loop, same doctrine as `spawngroup`: the running game is the
editor. This is the entry that proves new zone content doesn't need this repo.

1. Walk somewhere flat that no deposit covers. Walk a few steps on arrival — the command refuses a
   teleported footing, same as `spawngroup add`.
2. `deposit add 233` — expect a feedback line naming the new id, node type 233's name, and a 35m
   default radius.
3. `deposit list` — the new deposit is there, `0m away`.
4. `thumper`, and confirm the feedback says `in deposit [<new id>]`.
5. Collect it early (this is allowed here — the point is the deposit, not the payout size), or
   `deposit remove <id>` and skip to 6 if seven minutes is too long.
6. Restart the server. `deposit list` again.

Pass: the deposit exists after the restart with the same id, position and radius — it was written to
`StaticDB/CustomData/resource_deposit.json` in the deploy directory at `add` time, not at shutdown.
Copy it back to the repo if it should stay:
`cp ~/Games/PIN/GameServer/StaticDB/CustomData/resource_deposit.json ~/Github/PIN/UdpHosts/GameServer/StaticDB/CustomData/`

**Fail, gone after restart:** the save wrote somewhere else or threw — `grep -ai "exception"
~/Games/PIN/logs/GameServer.log` around the `add` timestamp.
