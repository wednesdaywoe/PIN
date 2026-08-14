# ConstraintSweep

Answers one question about a `clientdb.sd2`: what do the shipped battleframe loadouts cost in mass,
power and CPU?

The question exists because the used half of the beta's three-bar budget is already on the wire and
nobody has read it. `CharacterLoadout.ApplyItemStats` adds every `dbitems::AttributeRange` row on
the chassis and each slotted item with no filter on the attribute id, and the totals go out in
`CharacterStatsData.ItemAttributes`, so attributes 951, 952 and 953 are already being summed and
already being sent. What's missing is the numbers. Nothing can be authored for the capacity half
until you know what a stock loadout spends, and the beta screenshots can't supply it because beta
priced its items differently (`Docs/streams/battleframe-constraints.md`).

## How it decides

The walk mirrors `CharacterLoadout` rather than reimplementing it: chassis first, then every slot
`GetDefaultLoadoutSlots` returns, resolved through `SDBInterface.GetItemAttributeRange`. Two
departures, both deliberate.

Weapons are totalled separately. PIN's sum covers ability and gear slots only, and the primary and
secondary go out in their own `WeaponA`/`WeaponB` arrays instead, but the beta panel charged for
both. The report gives each loadout twice, with and without them, so the gap is a number rather
than a caveat.

The granted set is `HardcodedCharacterData.TempCharCreateLoadouts`, the map from char-create
loadout to chassis item that every frame purchase goes through. That's the shipped set in the only
sense a running server cares about. Every other `dbcharacter::CharCreateLoadout` row is swept too
and reported apart, flagged dev or experimental where the db says so.

Costs are stored negative because they spend against a budget. The report prints magnitudes and
counts the signs, so the convention is confirmed by the data instead of asserted.

## What it validates

Everything rests on three attribute ids that came out of a db inspection rather than out of the
code, so the run checks them first: 951, 952 and 953 must exist in `dbitems::AttributeDefinition`
and their internal or display names must look like mass, power and cpu. If they don't, it prints
what it found and exits 3 without producing a report, because every total would be measuring
something else.

Two numbers in the report double as a check against the earlier audit of this db, which found 1,125
items carrying the attributes and power at exactly half of mass in 1,006 of 1,015 tuned rows. If
those come back the same, the sweep is reading what that audit read. The items that break the half
rule are listed by name: a ratio the pricing formula can't produce marks a hand-authored row that
survived the rebalances.

## Running it

```
OPENSSL_ENABLE_SHA1_SIGNATURES=1 dotnet build Tools/ConstraintSweep/ConstraintSweep.csproj
cd Tools/ConstraintSweep && dotnet bin/Debug/net10.0/ConstraintSweep.dll
```

`config.json` (copy `config.example.json`) points `input` at the retail `clientdb.sd2`, the
original and not a pruned one. A pruned db has no `dblocalization::LocalizedText`, so it exits
rather than write a report where every item is a bare id. Output is `constraints.md` and
`constraints.json` in the working directory: the whole cost table's shape, then per loadout a total
and a slot-by-slot breakdown showing which items carry costs and which are free.

## The capacity table at the end

The last section divides each loadout's cost by the beta Raptor's headroom, which sat at 691/1400
mass, 446/800 power and 8/13 CPU. It's arithmetic on one screenshot, printed because calibrating
authored capacities is the reason to run this at all. It isn't a recommendation, and it says nothing
about how much room a fully built loadout needs on top of a stock one.

## Blind spot

Only `DefaultPveModule` is swept, matching PIN, which assumes PvE everywhere. Where a slot's
`DefaultPvpModule` names a different item its cost goes unmeasured.
