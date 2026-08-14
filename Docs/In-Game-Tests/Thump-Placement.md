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
[resource_deposit.json](../../UdpHosts/GameServer/StaticDB/CustomData/resource_deposit.json). All
four centers are coordinates a tester has stood on, and **deposit 1's is the only one the client has
also confirmed you may thump** — it was moved there on 2026-08-13 after the original center turned
out to be inside the station's no-thumping zone ([DATA-17](../ISSUE-REGISTER.md)). The other three
are walked spawn-group footings whose discs extend over unwalked ground, and none of them has been
scanned yet:

| Id | Name | Node type | Center | Radius | Pays at center |
|----|------|-----------|--------|--------|----------------|
| 1 | Basin Head Crystite | 242 | 199.8, 315.7, 401.3 | 30m | 40–100 crystite |
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
grep -a "Resource locations" ~/Games/PIN/logs/GameServer.log
grep -a "Geo report" ~/Games/PIN/logs/GameServer.log
grep -a "mined node type" ~/Games/PIN/logs/GameServer.log
grep -a "paying .* resource" ~/Games/PIN/logs/GameServer.log
```

A thumper still takes up to seven and a half minutes; the state-transition budget is in
[Resource Payout](Resource-Payout.md).

**Restarting mid-sitting used to destroy the transcript**, which is how the 2026-08-14 run lost its
own evidence. Outgoing logs are now moved to `~/Games/PIN/logs/previous/<server>-<stamp>.log`; the
greps above read the current run only, so add `previous/GameServer-*.log` when reading back across a
restart. And a restart re-deploys unless told not to — see
[Session Setup](Session-Setup.md#restarting-mid-sitting), which S6 turns on.

## [x] S1: The map shows where the resources are

**Passed 2026-08-13 on the third sitting**: the station outpost's radar reads
`Crystite, Iron Ore` — the zone's deposits, named on the map, from the layer the client actually
draws. (86668 is "Iron Ore" on screen; the shipped tables call it raw iron, and the deposit table
above uses the data's name.) The heat-blob overlay was not reported on either way; it is a bonus
layer and the entry does not turn on it.

**Two half-passes came first, and reading the client's own interface source ended the guessing.**
The full account is in [Client UI Source](../Client-UI-Source.md); the short version is that the map
never had one resource layer, it had three, and PIN was feeding the only one that is switched off.

| What draws it | Fed by | State |
|---------------|--------|-------|
| A radar disc per outpost, with a resource readout | that outpost entity's own `NearbyResourceItems` | alive — **this is the layer a player sees** |
| Heat blobs, one per deposit | `ResourceLocationInfosResponse` (2:140) | its draw event is commented out in the shipped UI; PIN's install re-enables it |
| A scan report card in the world | `GeographicalReportResponse` (2:139) | alive, and S2's business |

The first sitting proved the server chain and drew nothing, because `FoundResourceAreas` is a
beta-era message this client ignores. The second answered `ResourceLocationInfosRequest` with the
real deposit table and still drew nothing, because `Heatmap.xml` never binds the event that would
consume it. The radars the tester saw were the outpost layer, empty because PIN left all sixteen
`NearbyResourceItems` slots unset on every outpost. Zone 448's station outpost (id 17, radius 485m)
reaches all four deposits, so it is the one that should now read out.

1. Log in to zone 448. Stay near the Battleframe Station.
2. Open the full map and turn on the resource view (Tab).
3. Look at the radar disc over the station: expect a readout naming **Crystite** and **Raw Iron**
   instead of an empty one. Every other outpost in the zone has no deposit in reach and correctly
   stays empty.
4. Look for heat blobs near the four centers in the table above — the client-side patch feeds these
   from `ResourceLocationInfosResponse`. `grep -a "Resource locations" ~/Games/PIN/logs/GameServer.log`
   should show `4 deposit(s) in zone 448`; the client asks once, at player-ready, not on opening the
   map.
5. `ability 34126`, then `grep -a "Resource scan" ~/Games/PIN/logs/GameServer.log` — expect
   `Resource scan 181662 by <your entity id>: 4 deposit(s) within 600m` and **no change on screen**.
   The scan message is dead in this client generation; the grep only proves the server half.

Pass: the station's radar names crystite and raw iron. That alone is the milestone's "you can see
where the resources are", because it is retail's own live mechanism.

**The blobs are a bonus, and their absence is diagnostic**: readout working but no blob means either
the client-side heatmap patch did not take (the two files are listed in
[Client UI Source](../Client-UI-Source.md)) or `ResourceLocationInfo`'s field order is wrong after
all. The order is taken from the heatmap's own Lua, which reads `plot.x/y/z`, `plot.radius` and
`plot.composition[i].{itemTypeId, percent}`, so wrong-but-plausible is unlikely.

**Fail, the radar is still empty:** the outpost view is built once when the outpost spawns, so a
stale deploy still shows unset slots — check the build stamp at the head of the log first. If the
build is current, the slots are being written but not reaching the client, which is a view-scoping
problem rather than a resource one.

Sitting record, 2026-08-13, second sitting: `ResourceLocationInfosRequest` confirmed arriving once
at 21:44 and answered with 4 deposits; nothing drawn; tester reports "empty radars" on the map's
resource view, which is what identified the outpost layer as the real one.

<details>
<summary>First sitting, 2026-08-13 — the scan-message dead end</summary>

`ability 34126` ran five times, the log counted `4 deposit(s) within 600m` every time, and the
client drew nothing anywhere. The 2016 retail capture ([Capture-Replay](Capture-Replay.md)) then
showed that **the scan-era messages never appear in a live session** — not `FoundResourceAreas`,
not `GeographicalReportRequest`, not one of that family, in 400,000 messages. They are beta-era
scanning, and the entry moved to `ResourceLocationInfosRequest`/`Response`, which retail answered
with an empty list because resources had been flattened everywhere by then. PIN answered it with
the real deposit table — correct, and still invisible, which is what the second sitting found out.

</details>

## [x] S2: The ground says what is under it

**Passed 2026-08-14, tester-reported, and the confirming log did not survive the sitting.** The
run was restarted with a plain `./start-pin.sh` to begin S6, which truncated `GameServer.log` and
took the transcript of everything before it with it. That is fixed for future sittings — outgoing
logs are kept in `logs/previous/` — but this entry's own record is a report rather than a grep, and
it is marked passed on the tester's word.

What the surviving post-restart fragment does show, and it is not nothing: **the Scan Hammer fires
reliably** — seven presses in twenty seconds, each one `ActivateAbility Slot 5` followed by
`Ability 34503 starting Chain 884633`, so the trigger found on 2026-08-13 is not a one-off. And the
barren diagnostic added the same day works in game:

```
[08:24:01 INF] Geo report ... at <-312.34647, 488.43283, 401.17035>: feedback OK, barren;
               nearest is [4] North Flats Crystite and Iron at 408m, radius 35m
```

That is a correct barren reading 408m out, and it names where to walk instead of only saying no.

<details>
<summary>How the trigger was found, 2026-08-13</summary>

**The trigger is found and the whole loop runs, 2026-08-13.** With ability module 56811 in
`GearAuxWeapon`, pressing **G** plays the Scan Hammer animation, fires ability 34503, and the client
sends `GeographicalReportRequest` — the message the 2016 capture contains none of. The scan overlay
draws, showing density and naming what is in the scanned area.

**Those labels are not coming from this entry's message.** All three reports the server answered
were invalid, so the composition the tester read can only have come from
`ResourceLocationInfosResponse` — which is the heat-blob layer, fed by the client-side patch, and
**that confirms the `ResourceLocationInfo` field mapping outright**: position, radius, and an
(item, percent) list, guessed from the heatmap's Lua and now seen rendering real deposit data. S1's
bonus layer is working.

**What is still unproven is a valid reading, and the reason was placement.** Ten scans were taken
across the evening. Seven came back `NOTHUMPINGZONE` — the client refusing the spot before the
server sees a position — and **three came back `OK`**, which is the proof that the refusals are
about where the tester stood rather than anything broken. But all three accepted spots were 78m,
94m and 182m from the nearest deposit, so `barren` was the right answer every time. No scan has yet
been taken standing on a deposit with the client's blessing.

Two fixes came out of that. Deposit 1's old center sat 14.7m from the station outpost, entirely
inside its no-thumping zone, so **it was moved to 199.8, 315.7 — one of the three coordinates the
client itself said `OK` to**, 67m from the station and reachable on foot
([DATA-17](../ISSUE-REGISTER.md), now closed). And a barren reading now names the nearest deposit
and its distance, so a miss says which way to walk instead of only that you missed.

</details>

### How to run it again

`GeographicalReportRequest` finally has a handler. The client sends it carrying only its own verdict
on the spot (`OK`, `NOTHUMPINGZONE`, `INVALIDSURFACE`); the server reads the ground under your feet
and answers with a composition, or with an invalid (empty) report off a deposit. **The trigger is an
equipped Scan Hammer**, per the client's own tutorial text; no interface code sends the request, so
the client's native code does, and this entry now supplies the hammer rather than hunting for a
button.

1. `createitem 56811` then **`equipitem 56811 GearAuxWeapon`**. 56811 is the Scan Hammer's
   **ability module**, granting ability 34503 "Scan Hammer"; `GearAuxWeapon` is the loadout slot the
   client calls ability slot 5, which is bound to **G** by default. The weapon 56826 is the model you
   hold and grants no ability — equipping that instead is what the 2026-08-13 sitting did, and G
   stayed a plain melee swing.
2. **Walk down to deposit 1, Basin Head Crystite, centered at 199.8, 315.7** — 67m from the station,
   on the one coordinate in the zone the client has already accepted for thumping. Do not scan on the
   station shelf; it is a no-thumping zone and every scan there is refused before the server sees a
   position.
3. **Press G.** `grep -a "ActivateAbility Slot 5" ~/Games/PIN/logs/GameServer.log` proves the press
   arrived; the server now also logs `nothing slotted in GearAuxWeapon` when the slot is empty and
   `is slotted but is not an ability module` when the wrong item is in it, so a dead key names its
   own cause.
4. `grep -a "HandleActivateAbility: Ability 34503" ~/Games/PIN/logs/GameServer.log` — the scan
   ability ran. If the press arrives and this does not, the module is not resolving.
5. If no report follows, `pflags detect_resources` and retry — that permission gates the client's
   resource-scanning HUD, and turning it on also makes the client re-ask for the deposit list.
6. `grep -a "Geo report" ~/Games/PIN/logs/GameServer.log` — expect
   `feedback OK, scan 1, node type 242, 1 resource(s)`. The two failure modes now read differently:
   **`feedback NOTHUMPINGZONE` means the spot was refused** by the client, so move away from any
   base and press G again; **`feedback OK, barren`** means the spot was fine and holds nothing, and
   the same line names the nearest deposit and its distance so you know which way to walk.
7. Walk somewhere no deposit covers (100m+ from every center in the table) and scan again. Expect
   the grep to end in `barren`.
8. Open the map. `MapOpened` now re-sends your latest report instead of a hardcoded empty one —
   note whether the map shows the reading taken in step 6/7.

Pass: the two greps say deposit then barren, and the client renders the deposit reading as a
non-empty report (composition percentages, however it draws them).

**Not runnable is a valid outcome** if no UI action sends the request — mark `[-]`, note it, and the
server half stays covered by the grep in S3's setup. The handler also fires on whatever the client
does send, so the grep after any session says whether the message ever arrived at all.

**What the client's interface source says, 2026-08-13** ([Client UI Source](../Client-UI-Source.md)):
the receiving half of this entry is alive and well. `HUD/ResourceScans/ResourceScans.lua` is fully
bound and draws a world-space report card — an icon per resource with its percentage — plus a map
marker, straight off `GeographicalReportResponse`. It also handles the invalid answer, which it
shows as one of "thumping prohibited", "invalid surface", "thumper nearby" or an empty report.

So the entry is not blocked on rendering, it is blocked on the trigger. **No Lua anywhere sends the
request**, which means the client's own code sends it — and the tester's recollection is that it
took an equipped Scan Hammer and an ability press. The shipped data agrees: it carries a "Scan
Hammer" item, a "Scan Hammer ability", and tutorial text reading "Equip your scan hammer" and "Use
the Scan Hammer to find a valid thumping spot". Getting one into a character's hands is the next
piece of work, and until then a `[-]` here is about equipment, not about a dead message.

## [x] S3: Thumping the rich center pays center values
> I saw 33 Crystite in the in-game toast, I was off-center
The milestone's exit condition, first half. Same shape as G2, on the one deposit whose center is
walked ground.

1. Run [G1](Resource-Payout.md) first if this is a fresh character, for the baseline.
2. Walk — do not `tp` — to within a few metres of 199.8, 315.7, deposit 1's center at the head of
   the basin, 67m from the Battleframe Station. Note the crystite count.
3. `thumper` — the feedback line now names the ground: expect
   `in deposit [1] Basin Head Crystite (node type 242)`.
4. Let it run its full cycle untouched — interacting early is G4's entry.
5. `grep -a "mined node type" ~/Games/PIN/logs/GameServer.log` — expect
   `mined node type 242 at distance fraction 0.0x of deposit [1] Basin Head Crystite,
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
6. Restart with **`./start-pin.sh --no-build`**. Then `deposit list` again.

Pass: the deposit exists after the restart with the same id, position and radius — it was written to
`StaticDB/CustomData/resource_deposit.json` in the deploy directory at `add` time, not at shutdown.
Copy it back to the repo if it should stay:
`cp ~/Games/PIN/GameServer/StaticDB/CustomData/resource_deposit.json ~/Github/PIN/UdpHosts/GameServer/StaticDB/CustomData/`

**`--no-build` is not optional, and skipping it is what happened on 2026-08-14.** A plain
`./start-pin.sh` re-deploys, and the install is an rsync out of the build output — so the repo's
four-deposit file lands on top of whatever the game just wrote, and the placement is gone for a
reason that has nothing to do with what this entry measures. The deploy now keeps the outgoing file
in `~/Games/PIN/builds/authored/` and warns on the way past, so a rebuild-restart is recoverable
rather than silent, but it still fails the entry. The copy-back above is the durable move and is
best done right after the `add`, not at the end of the sitting.

**Fail, gone after a `--no-build` restart:** the save wrote somewhere else or threw — `grep -ai
"exception" ~/Games/PIN/logs/previous/GameServer-*.log` around the `add` timestamp.
