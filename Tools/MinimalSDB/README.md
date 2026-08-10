# MinimalSDB

Two things you can do to a `clientdb.sd2`: shrink it, or find out what's actually in it.

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
