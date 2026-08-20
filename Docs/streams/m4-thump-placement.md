---
project: pin
kind: stream
title: "M4: Where You Thump Matters"
relates:
  - ../PROGRESS.md
---

# M4: Where You Thump Matters

**Code complete 2026-08-13, none of it seen in game.** Every row of the table below landed in one
pass; [S1–S6](../../Game Testing/Thump-Placement.html) are the check, and the exit condition at the
bottom is unchanged and unmet. What follows first is the original framing, kept because the
decisions only make sense against it; the record of what was actually built, and the two guesses it
stands on, is at the end.

Thumping isn't a decision yet. `nodeType` is the literal `20` at both call sites, with the TODO
next to it saying as much, so every deposit in the world is the same deposit and the ground you
pick means nothing. This milestone is the scan, the map behind it, and a payout that comes from
the spot rather than a constant. Tracked as [DATA-2](../gaps/data.md) until it lands.

The protocol survives. Four messages describe the whole mechanic and none of them carry real data
today:

- `FoundResourceAreas` (2:138) is the density overlay, an array of `{Center, Unk4, NodeTypeId}`.
  Nothing sends it. `Unk4` is unidentified, radius or richness being the obvious guesses.
- `GeographicalReportRequest` (2:184) is the ground reading, and it arrives carrying the client's
  own verdict on the spot: `OK`, `NOTHUMPINGZONE` or `INVALIDSURFACE`. No handler exists.
- `GeographicalReportResponse` (2:139) answers with a `ScanId`, a position, a `Valid` byte and a
  composition array. This one the server does send, but only from `MapOpened` and hardcoded to
  `Valid = 0`, which the client renders as an empty report.
- `ResourceNodeCompletedEvent` (2:137) closes the loop with the same `ScanId`, a quantity and the
  composition actually extracted. Nothing sends it.

The yield model survives too, and it's better than expected.
[ResourceNodeTypeResource](../../UdpHosts/GameServer/StaticDB/Records/dbzonemetadata/ResourceNodeTypeResource.cs)
is keyed by node type and carries `CenterLow`/`CenterHigh`, `EdgeLow`/`EdgeHigh` and
`ItemQualityLow`/`ItemQualityHigh` per item. A deposit was never uniform: quantity and quality both
fall off from the middle to the rim, sampled from a range. That's what the overlay was drawing and
why standing in the right place paid. Neither it nor `ResourceNodeType` is loaded.

What doesn't survive is where anything is. The client only ever learned deposit positions by
scanning, so `clientdb.sd2` never carried them and there's nothing to import. The distribution has
to be generated, which makes this the one milestone with no ground truth to check against and the
one place where a design decision is genuinely ours. A seeded per-zone scatter is
indistinguishable from the original as far as the client is concerned, and the SDB gradient gives
each blob its falloff for free.

Placement isn't checked server-side either. `MaxSlope`, `MaxRange`, `OutdoorsHeight` and
`GroundAlignment` all sit unused on the calldown def because the client validates the spot and
reports its own verdict. Fine for one player, and it belongs with the rest of the trust problem in
the [public server appendix](public-server-hardening.md).

| Work | Where |
|------|-------|
| A per-zone resource map that answers "what's near here" | new; `CustomDBInterface` is where per-zone data already lives, [ZoneLoader](../../Lib/Shared.Collision/ZoneLoading/ZoneLoader.cs) knows the extent |
| Load the node type and yield tables | [StaticDBLoader.cs](../../UdpHosts/GameServer/StaticDB/Loaders/StaticDBLoader.cs), [SDBInterface.cs](../../UdpHosts/GameServer/StaticDB/SDBInterface.cs) |
| Wire the scan command and send `FoundResourceAreas` | [Factory.cs:481](../../UdpHosts/GameServer/Systems/Aptitude/Factory.cs#L481-L482), [ResourceNodeScanDefCommand.cs](../../UdpHosts/GameServer/Systems/Aptitude/Commands/Other/ResourceNodeScanDefCommand.cs) |
| Handle `GeographicalReportRequest`, stop hardcoding `Valid = 0` | [BaseController.cs](../../UdpHosts/GameServer/Controllers/Character/BaseController.cs), its `MapOpened` handler |
| Resolve node type from position instead of `20` | [ResourceNodeBeaconCalldownCommand.cs:23](../../UdpHosts/GameServer/Systems/Aptitude/Commands/Calldown/ResourceNodeBeaconCalldownCommand.cs#L23) |
| Sample the gradient where the thumper landed and pay that out | the M3 grant path, plus `ResourceNodeCompletedEvent` |

Exit: scan, see deposits that aren't all alike, thump a rich one and a poor one, and come away with
visibly different piles.

Medium size and the least predictable milestone on the list. Everything else is reconnecting parts
built to fit each other; this one has a hole where the original data was. The scan def is the
second unknown, since its record has only `Id` mapped and all five entries in its JSON are shells,
though the comments name two of them: "Scan for resource nodes near the player" and "Scans for
thumper nodes in a 600 meter radius".

## What landed, 2026-08-13

**Both tables shipped with live values, which the framing above didn't promise.** 46 node types, 67
yield rows across 42 of them, loaded by `SDBInterface` like any other table. The names alone settle
what zone 448 should scatter: "Crystite Only - (lvls 1-19)" (242, 40–100 at center), "Tier 1 -
Rare - Crystite" (241, 10–25), the Tier 1 iron family (233–238), and crystite is item id 10 in the
rows — the same resource id M3 and M5 already pay. Node type 20, the old hardcoded literal, turns
out to be "Default, Thumper Sifted Earth - Resource Vein 0", paying one unit of item 30404: **the
shipped data's own word for barren ground**, so the literal survives as the fallback for a spot no
deposit covers rather than being deleted.

**The deposit map is explicit positions, not the seeded scatter proposed above.** The spawn-group
sittings ended offline authoring for content that needs a footing; a deposit only decides things in
X and Y — the thumper stands on the player's own footing and the overlay projects to the map — so
the stakes are lower, but a center nobody can stand on is still a deposit nobody can thump. So the
same doctrine applies: [resource_deposit.json](../../UdpHosts/GameServer/StaticDB/CustomData/resource_deposit.json)
carries one entry per deposit (center, radius, node type), the `deposit` admin command places one
where you are standing with the same airborne/`PlacedPosition` guards as `spawngroup`, and the four
starter deposits sit on already-walked coordinates. [DepositSampler](../../UdpHosts/GameServer/Systems/Resources/DepositSampler.cs)
does the arithmetic — nearest covering disc, linear center-to-edge blend, composition percentages —
and is pinned offline by `DepositSamplerTests` on the real 242/239 rows.
[ResourceMapSim](../../UdpHosts/GameServer/Systems/Resources/ResourceMapSim.cs) holds the per-shard
state: deposit lookup, scan ids, and each character's latest report so `MapOpened` can re-show it
and a completion can echo it.

All four protocol messages now carry real data: the scan command sends `FoundResourceAreas`,
`GeographicalReportRequest` has a handler that reads the ground under the character's feet,
`MapOpened` resends the latest report instead of hardcoding `Valid = 0`, and `Thumper.OnSuccess`
sends `ResourceNodeCompletedEvent` with the extracted composition and the report's `ScanId` when
the owner's last reading was taken in the same deposit. The payout itself is the gradient sample
scaled by the thumper's own progress bar — which is the figure `SetProgress` kept and nothing read,
so **G4's partial-yield question is answered in the same stroke** and its entry was rewritten before
it ever ran. Non-resource rolls are logged and dropped, the cut M3 and M5 made, applied a third
time. The flat 200-crystite grant is gone with the milestone that needed it.

**The two guesses, both flagged where they bite.** `Unk4` on a scan area is filled with the
deposit's radius in metres — an overlay needs a size before a shade, but richness is the live
alternative, and S1 is written to catch blobs rendering at a wrong uniform size. And the scan def's
new `Range` column is invented: 600m for 34126 straight from its shipped comment, 100m for the
rest. What nothing here answers is which UI action makes the client send
`GeographicalReportRequest` at all; S2 goes looking.

## What the capture corrected, 2026-08-13

S1's first sitting was the predicted half-pass: five scans, four deposits sent every time, nothing
drawn. The [2016 retail capture](../../Game Testing/Capture-Replay.html) then reframed the milestone's
protocol picture. **All four messages the framing above is built on are absent from a full live
session** — `FoundResourceAreas`, `FindNearbyResourceAreas` and both geographical-report directions
appear zero times in 400,000 messages. They are beta-era scanning; the 2016-era client learned
where deposits are from a different exchange entirely: it sends `ResourceLocationInfosRequest`
(2:187) once, and the server answers `ResourceLocationInfosResponse` (2:140) — per deposit a
position, a radius and an (item, percent) composition list, which is the same shape as
[resource_deposit.json](../../UdpHosts/GameServer/StaticDB/CustomData/resource_deposit.json) plus
the node type's shares.

The one recorded retail answer is an empty list. That is not a gap in the capture — it is live
Firefall's actual 2016 resource model, where deposits had been flattened away and the map had
nothing to show. PIN had faithfully copied that empty answer into its own handler. The handler now
answers with the deposit table, shares computed from center-midpoint quantities
(`DepositSampler.AdvertisedShares`, pinned by tests on the real 242/239 rows): the charter's
"restore what they threw away" applied to a single message. The scan command keeps sending
`FoundResourceAreas` — proven server-side, most likely dead client-side — and the field mapping of
`ResourceLocationInfo` (`Unk1/2/3` = x/y/z, `Unk4` = radius, inner `Unk2` = percent) is a
capture-informed guess the empty retail body could not confirm — **settled the same evening**, first
by the heatmap's own Lua and then by watching it render real deposits in game.

## What the client's own UI source settled, 2026-08-13

**S1 passed the same evening**: the station outpost's radar reads `Crystite, Iron Ore`. Deposits are
visible on the map for the first time, by retail's own live mechanism.

**And the scan came back to life an hour later.** The Scan Hammer's ability module (56811, ability
34503 "Scan Hammer") in the `GearAuxWeapon` slot puts it on **G**, and pressing it plays the
animation, runs the ability, and makes the client send `GeographicalReportRequest` — a message the
2016 capture does not contain once. The overlay draws density and names what is in the scanned area,
and since every report the server answered that evening was invalid, those labels can only be coming
from `ResourceLocationInfosResponse`: the field mapping below is no longer a guess, it has been seen
rendering real deposits. What is still unproven is a valid ground reading, because the station shelf
is a no-thumping zone and deposit 1 sits inside it ([DATA-17](../ISSUE-REGISTER.md)).

The re-run half-passed the same way: the request arrived, PIN answered with four deposits, nothing
drew. The answer was not in the protocol at all. **The 1962 client ships its entire interface as
loose, uncompiled Lua**, and reading it ended three open questions at once — the method is written
up in [Client UI Source](../Client-UI-Source.md), and it belongs ahead of capture replay whenever
the question is "what does the client do with this", not "what went over the wire".

The map never had one resource layer. It has three, and PIN had been feeding the one that is
switched off:

- **The layer a player actually sees is the outpost radar.**
  `Panels/WorldMap/WorldMap_ResourceScans.lua` draws one disc per outpost and captions it from that
  outpost's own resource list — which is `Outpost::ObserverView.NearbyResourceItems_0..15`, sixteen
  item-id slots PIN never set. Every radar therefore read empty. `OutpostEntity` now fills them from
  the deposits within the outpost's radius, most abundant first
  (`DepositSampler.ResourcesWithin`). In zone 448 exactly one outpost qualifies: the station, id 17,
  radius 485m, reaching all four deposits, advertising crystite and raw iron.
- **The heat-blob overlay is real but disabled.** `HUD/Heatmap/Heatmap.lua` is the consumer of
  `ResourceLocationInfosResponse`, and its `ON_HEATMAP_UPDATED` binding is commented out in the
  shipped XML, with a second trap behind it: the default filter is the string `"none"`, compared
  against item ids, so it matches nothing and scores every plot at zero heat. Two lines in the
  install re-enable it. Its `DequeuePlots` reads `plot.x/y/z`, `plot.radius` and
  `plot.composition[i].{itemTypeId, percent}` — **which confirms the `ResourceLocationInfo` field
  order the capture could not**, so the guess flagged above is now settled.
- **The scan-report card is alive and bound.** `HUD/ResourceScans/ResourceScans.lua` renders
  `GeographicalReportResponse` as a world-space card with an icon and percentage per resource, and
  renders the invalid answer as one of four failure messages. S2 is blocked on the trigger, not the
  drawing: no Lua sends the request, and the shipped data carries a "Scan Hammer" item plus a "Scan
  Hammer ability", which matches the tester's memory of how a survey was taken.

The lesson generalises past resources. Any milestone that ends in "the client should draw this" can
be checked against the component that would draw it before a line of server code is written.

## S2 passed, and the sitting lost its own log

**2026-08-14: a ground report reads back.** The trigger is ability module 56811 in `GearAuxWeapon`,
which puts ability 34503 "Scan Hammer" on the **G** key; the client's own code then sends
`GeographicalReportRequest` — a message the 400,000-message 2016 capture contains not one of. Two
milestone unknowns closed with it: the field order the capture could not settle, and the "what UI
action sends this" question, whose answer is that no UI action does.

**The record for it is a report rather than a grep, because the restart that followed truncated the
log.** Every start wrote `logs/GameServer.log` from empty, so beginning S6 — which is a restart by
definition — destroyed the transcript of everything tested before it. That is a tooling failure with
a real cost: under this project's own doctrine a result without a log is not a result, and this one
had to be recorded on the tester's word instead.

Both halves are fixed. `start-pin.sh` moves the outgoing logs to
`logs/previous/<server>-<YYYYMMDD-HHMMSS>.log` before truncating, ten kept per server. And
`deploy.sh` no longer overwrites in-game-authored static data silently: `deposit add` and
`spawngroup add` write their JSON inside the deployment, the install is an rsync out of the build
output, and a plain `./start-pin.sh` therefore deployed the repo's copy straight over a deposit
placed by walking — which is S6's entire subject matter. The repo still wins, deliberately, because
preserving the local file would mean silently testing stale data; but the outgoing file is now kept
under `builds/authored/` and the deploy says where it went. S6 runs with `--no-build`.

## Closed: the numbers a player watched arrive

**2026-08-14, [S1–S6](../../Game Testing/Thump-Placement.html) all passing.** The milestone asked
whether where you thump matters. One sitting answered it four times over:

| Where the thumper stood | Node type | Paid |
|-------------------------|-----------|------|
| the rich crystite vein, near its center | 242 | **48 crystite** |
| the same vein, 59% of the way to the rim | 242 | 33 crystite |
| the poor crystite trace, 72% out | 241 | **8 crystite** |
| barren ground, no deposit | 20 | 1 unit of sifted earth |

The pair on deposit 1 is the entry worth keeping. Two runs on the same vein, minutes apart, differing
in nothing but position, and the payout tracked the distance from the center the way the shipped
gradient says it should. That is a stronger result than the rich-versus-poor comparison the
milestone was written around, because it removes the deposit itself as a variable.

**The barren case settled a question the entry could not assume.** Node type 20's single unit of item
30404 came out as `paying 1 of resource 30404`, not `not paid` — so sifted earth carries the
`Resource` flag and a barren thump does deliver something. It is simply worthless, which is retail's
own joke about drilling nowhere.

**S6 is the one that outlives the milestone.** Deposit 5 was placed by walking to a spot on the far
west of the basin and typing `deposit add 233`; it was thumped, the server was restarted, and it came
back with the same id, position and radius, thumpable again from the other side of its disc. Zone
content can be authored from inside the running game. That was the doctrine `spawngroup` established
for monsters, and the reason it holds for deposits too is that a deposit is a disc in X and Y and
never needs a ground height — [there is no server-side terrain](../gaps/data.md#data-15) to give it
one.

### What the sitting found that no code read had

Two defects, both on the same event — the moment a thumper leaves.

**[NET-24](../gaps/network.md#net-24): the client never removes a finished thumper.** The server pays
out and deletes the entity; the model stays standing, and the client asks for a fresh keyframe of the
dead entity every 5.5 seconds for the rest of the session. One 32-minute stretch logged 648 of these
across four abandoned thumpers. The narrowing that matters: **thumpers a player cut short left none
at all**, only the ones that ran their full cycle, which points at the `CLOSING`/`COMPLETED` leg
rather than at removal.

**[DATA-18](../gaps/data.md#data-18): three of the four state-change abilities are literals.** Only
the landing and early-collection paths read the beacon's own def. The hardcoded `34216` is what sends
away a thumper that finished on its own, and the shipped `completed_ability` is what sends away one
you cut short — two different departures depending on when you press E.

Neither is a payout problem, and neither was visible from the code alone. Both came out of somebody
standing in the world watching a rig leave.
