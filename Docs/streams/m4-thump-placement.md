---
project: pin
kind: stream
title: "M4: Where You Thump Matters"
relates:
  - ../PROGRESS.md
---

# M4: Where You Thump Matters

**Code complete 2026-08-13, none of it seen in game.** Every row of the table below landed in one
pass; [S1–S6](../In-Game-Tests/Thump-Placement.md) are the check, and the exit condition at the
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
| A per-zone resource map that answers "what's near here" | new; `CustomDBInterface` is where per-zone data already lives, [ZoneLoader](../../UdpHosts/GameServer/Physics/ZoneLoader/ZoneLoader.cs) knows the extent |
| Load the node type and yield tables | [StaticDBLoader.cs](../../UdpHosts/GameServer/StaticDB/Loaders/StaticDBLoader.cs), [SDBInterface.cs](../../UdpHosts/GameServer/StaticDB/SDBInterface.cs) |
| Wire the scan command and send `FoundResourceAreas` | [Factory.cs:481](../../UdpHosts/GameServer/Systems/Aptitude/Factory.cs#L481-L482), [ResourceNodeScanDefCommand.cs](../../UdpHosts/GameServer/Systems/Aptitude/Commands/Other/Todo/ResourceNodeScanDefCommand.cs) |
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
