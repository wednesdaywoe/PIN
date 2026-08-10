# Layer 8: Static Data (SDB)

Firefall ships its game data in `clientdb.sd2`: every weapon, ability, chain, command definition,
faction, zone, item and localisation string. PIN reads that file directly rather than maintaining
its own content, so the client's data is the source of truth and most "add content" work is really
"read the right table".

## Two data sources

| Source | Interface | Backing |
|--------|-----------|---------|
| Client SDB | [SDBInterface.cs](../../UdpHosts/GameServer/StaticDB/SDBInterface.cs) | `clientdb.sd2` via FauFau, path from `StaticDBPath` |
| Custom data | [CustomDBInterface.cs](../../UdpHosts/GameServer/StaticDB/CustomDBInterface.cs) | JSON files in [StaticDB/CustomData](../../UdpHosts/GameServer/StaticDB/CustomData) |

Custom data covers two cases: server-only command definitions that were never in the client DB
(`aptgss_*.json`, where the `ags*` prefix marks server-side aptitude commands), and hand-authored
world content (`outpost.json`, `deployable.json`, `melding.json`, `lgv_race.json`).

Both are static classes initialised once in the `GameServer` constructor before any client can
connect, which is why they're reachable from anywhere without injection.

```
GameServer ctor
 ├─ SDBInterface.Init(sdb)      → StaticDBLoader reads each table into a Dictionary
 └─ CustomDBInterface.Init()    → CustomDBLoader deserialises JSON (snake_case naming policy)
```

## Layout

```
StaticDB/
  SDBInterface.cs           ~200 static Get* accessors over prebuilt dictionaries
  CustomDBInterface.cs      the same shape, for JSON-backed defs
  SDBUtils.cs               derived/composite lookups (see below)
  Loaders/
    StaticDBLoader.cs       one Load* method per table (~1550 lines)
    CustomDBLoader.cs       one Load* method per JSON file
    ISDBLoader.cs
  Records/                  one folder per source DB, plain records mirroring table columns
    apt/ aptfs/ apttf/      aptitude: abilities, chains, command definitions
    dbitems/                weapons, ammo, items, attributes
    dbcharacter/            factions, monsters, deployables, damage types & responses
    dbstats/ dbzonemetadata/ dbvisualrecords/ … 
    customdata/             records for the JSON files, incl. Encounters/
  CustomData/               the JSON itself
```

Record classes are mechanical mirrors of table columns; see
[Records/dbitems/Ammo.cs](../../UdpHosts/GameServer/StaticDB/Records/dbitems/Ammo.cs) for a
representative one. Field names come from the client's schema, so odd casing
(`Damagetype`, `DamageDecayRangefrac`) is deliberate.

## SDBUtils, the part you'll actually use

[SDBUtils.cs](../../UdpHosts/GameServer/StaticDB/SDBUtils.cs) composes raw tables into the shapes
gameplay needs. The important one is `GetDetailedWeaponInfo(weaponId)`, which resolves a weapon
into `Main` / `Alt` `WeaponTemplateResult` values, merging the weapon, its template, template
modifiers, scope and underbarrel. `CharacterEntity.GetActiveWeaponDetails()` builds on it and picks
`Alt` when a fire mode is active.

If you find yourself joining three tables inside a command, the join probably belongs in
`SDBUtils`.

## Adding a lookup

1. Add a record under `Records/<db>/` matching the table's columns.
2. Add a `Load<Table>()` to `StaticDBLoader` following the existing pattern.
3. Add the dictionary field, the `Init` line, and the `Get<Table>(id)` accessor to `SDBInterface`.

For custom data, the equivalent is a record under `Records/customdata/`, a JSON file in
`CustomData/`, a `Load*` in `CustomDBLoader`, and the field/Init/accessor trio in
`CustomDBInterface`. Note the loader uses a snake_case naming policy with case-insensitive
matching, so `damage_points` binds to `DamagePoints`.

## Tools

[Tools/MinimalSDB](../../Tools/MinimalSDB) produces a stripped `clientdb.sd2` containing only the
tables `StaticDBLoader.cs` actually reads, which it discovers by parsing the loader source. Useful
for sharing a small test database, and as a quick way to see every table the server depends on.

## Gotchas

- `Get*` accessors use `GetValueOrDefault`, so a missing id yields `null`, not an exception. Most
  callers don't null-check. A wrong id usually surfaces as a `NullReferenceException` swallowed by
  the controller dispatch try/catch ([layer 2](02-networking.md)).
- Ids are recycled across tables; an "ability id" and a "chain id" are different id spaces.
- Aptitude command definitions are spread over `apt`, `aptfs`, `apttf`, and `customdata`. When
  wiring a command in `Systems/Aptitude/Factory.cs`, the commented-out case already names the right
  interface and getter. That comment is the documentation for which source a def comes from.
