# MinimalSDB

Four things you can do to a `clientdb.sd2`: shrink it, find out what's actually in it, search it for
a string, or check that its references still point at anything.

Both modes read `config.json` from the working directory or next to the binary. Copy
`config.example.json` and point `input` at the real client db.

## prune (default)

```
MinimalSDB
```

Scans `StaticDBLoader.cs` for the tables the server actually loads, drops everything else, and
writes the result to `output`. Errors out if the loader names a table the db doesn't have.

## dump

```
MinimalSDB dump
```

Answers whether a table shipped with usable numbers, which is what you want to know before building
a feature on it. A table fails in three different ways and each means different work:

| Verdict | What it means |
|---------|---------------|
| `ABSENT` | No table by that name. Either it never shipped to the client, or you pointed at a pruned db. |
| `EMPTY` | The schema shipped without rows. Whatever you build on it gets improvised. |
| `ALL ZERO` | The column exists but was never tuned. Same problem, narrower. |
| a min..max spread | Real live values. Build against them. |

Run it against the original `clientdb.sd2`, not a pruned one, or everything the server doesn't
already load reads back as `ABSENT`.

The header line is worth a look too. `flags` says whether the file was built as a client or server
db, which is the top-level reason a server-side table might be missing entirely.

### Asking for columns

Column names aren't in the file, only hashes of them, so there's no way to list a table's columns.
You can only ask whether a name you already suspect is there. The names follow the same snake_case
the loader derives from record properties, so `Battleframe.BaseHealth` is `base_health`.

The built-in table set covers the unfinished parts of the damage pipeline: vitals, shields, damage
type resistance and the range decay inputs. Override it in `config.json`:

```json
"dump": {
  "sampleRows": 3,
  "tables": {
    "dbitems::Battleframe": ["base_health", "base_shields"],
    "dbcharacter::MonsterScaling": []
  }
}
```

An empty column list prints row count and a column type histogram, which is enough to tell whether
a table is worth pursuing.

## find

```
MinimalSDB find combatDist
```

`dump` can only ask about a table you can already name, and names aren't in the file. `find` goes
the other way: it reads every string column of all 575 tables and reports the ones containing a
substring you know the data must have. That's the only way into a table nobody has identified yet.

Each hit prints the table (by loader name where PIN maps it, by numeric id where it doesn't), its
size, the matching column, how many rows matched, and a few distinct values:

```
dbcharacter::Monster
  3109 rows, 68 columns; col 43 matches 76 row(s), 44 distinct
    Arch_FullbodyMelee_Attack(combatDist=2,makesWideTurns=true,wideTurnRadius=5)
```

Several needles at once, either on the command line or from `config.json`:

```json
"find": {
  "samples": 3,
  "needles": ["combatDist", "perceptionDist"]
}
```

Matching is case-insensitive substring, and the exit code is 0 whether or not anything matched. **No
match is a result, not a failure.** It says the string never shipped in this db, which is often the
question worth asking: [DATA-10](../../Docs/gaps/data.md#data-10) established that Firefall's NPC
behaviour parameters live on `dbcharacter::Monster` and nowhere else by searching for `Arch_` and
getting exactly one table back. No amount of guessing at table names could have produced that.

The header line is worth reading here too. It reports how many of the file's tables the loader names
(239 of 575 today), which bounds how much of the file `dump` and `joins` can even reach.

## joins

```
MinimalSDB joins
```

`dump` says a table has rows. It does not say those rows still point at anything. Firefall was
rebuilt twice and migrated its items each time, so a system can look intact by row count and be
unusable in practice because half its foreign keys were orphaned. That difference decides whether
reviving a feature is implementation work or content work, and it is worth knowing before the
implementation work starts.

Each join reports how many references resolve, how many are null, and the distinct ids that point
at nothing:

```
dbitems::Blueprint_Items.item_type  ->  dbitems::RootItem.sdb_id
  25823/25881 resolve (99.78%), 0 null, 58 orphaned
    52 distinct orphan id(s): 75428, 77361, 77362, ...
```

The default set checks the crafting graph, which is the worked example — v1.6 switched crafting
off, and whether it can be switched back on comes down to whether 9,228 blueprints still reference
items that exist. Override it in `config.json`:

```json
"joins": [
  { "from": "dbitems::Blueprint_Items.item_type", "to": "dbitems::RootItem.sdb_id" },
  { "from": "dbitems::Blueprint_Items.blueprint_id", "to": "dbitems::Blueprints.id", "zeroIsNull": false }
]
```

`from` and `to` are `db::Table.column` — the table name carries its own `::`, so the column is
whatever follows the last `.`. `zeroIsNull` defaults to true, which treats 0 as "no reference"
rather than a broken one; set it false where 0 is a real key and a zero should be reported.

Orphaned rows are a finding, not a tool failure — the exit code is 0. A table or column name that
doesn't exist is an error, and exits 7.
