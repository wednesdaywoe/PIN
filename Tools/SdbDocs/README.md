# SdbDocs

Renders `clientdb.sd2` into the reference tables under [Docs/Wiki/Reference](../../Docs/Wiki/Reference).
Weapons, abilities and recipes, with their numbers.

The wiki's method is that the client's own data is the source of truth, but a 32 MB binary nobody can
read is not a source anybody can argue with. This makes the argument checkable: the pages carry the
numbers, so a design discussion can cite one instead of asserting it, and a claim that contradicts
the DB is visibly wrong rather than merely unverified.

## What it produces

| Page | What it is |
|------|------------|
| `Attributes.md` | Every attribute id, internal and display name. The legend for the rest — an item card is a list of attribute ids and they mean nothing bare. |
| `Weapon-Templates.md` | All 318 templates: damage, ammo and timing, plus the spread, rise, slide and camera-recoil curves the client reads to decide how a gun feels. |
| `Weapons.md` | Every weapon resolved the way the server resolves it, tiers collapsed. |
| `Abilities.md` | Every ability with its item card and module progression. |
| `Recipes.md` | The crafting graph v1.6 switched off: outputs, ingredients, build times. |

## Two decisions worth knowing about

**It resolves through the server, not the columns.** Weapon figures come from
`SDBUtils.GetDetailedWeaponInfo`, the same call `EntityManager` makes when it equips something, so a
row is the template value with that item's `WeaponTemplateModifiers` applied. That is deliberate: the
pages then describe what PIN actually sends, and a disagreement with the client is a server bug this
tool will surface rather than paper over.

**It collapses tiers.** Firefall generated gear in level and quality tiers, so `dbitems::Weapons` has
6,789 rows for about 750 weapons and `dbitems::AbilityModule` has 30,941 for about 2,400 abilities.
Rows are grouped and each column prints the spread — `40-1200` is one weapon's progression, not a
thousand weapons. A page that listed every row would be seven times longer and would hide exactly the
fact worth knowing. Where rows are dropped the page says how many and why, at the top.

## Running it

```
cd Tools/SdbDocs
cp config.example.json config.json    # then edit "input"
dotnet run
```

| Key | Meaning |
|-----|---------|
| `input` | Path to `clientdb.sd2`. Required. |
| `output` | Where to write. Defaults to `Docs/Wiki/Reference`. |

It has to be the full client database, not one `MinimalSDB prune` produced. The pruned copy drops
`dblocalization::LocalizedText` — the server has no use for display strings and so never loads it —
and every name on every page would come out blank. The tool checks for this and refuses rather than
writing 1 MB of empty cells.

Read-only: it opens the DB, never writes it. Keeping the shipped `clientdb.sd2` pristine is what
makes it the reference copy every finding is checked against — see
[Restoration](../../Docs/Restoration.md).

## Related

- [MinimalSDB](../MinimalSDB) — prunes an SDB down to the tables the server loads, dumps table
  shapes, and checks that foreign keys still resolve.
- [Docs/Wiki](../../Docs/Wiki) — the prose these tables back up.
