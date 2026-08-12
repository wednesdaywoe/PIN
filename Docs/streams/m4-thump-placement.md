---
project: pin
kind: stream
title: "M4: Where You Thump Matters"
relates:
  - ../PROGRESS.md
---

# M4: Where You Thump Matters

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
