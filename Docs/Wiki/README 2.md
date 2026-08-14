# Firefall reference wiki

Two sources, kept apart on purpose.

- `data.js` — hand-authored. What things *do*, from the patch notes and update docs.
  Frames, ability descriptions, per-frame variants, systems prose, the patch chain,
  and `BUILD_OVERRIDES` for wherever the reimplementation departs from what shipped.
- `dump-1962.js` — **generated, do not edit.** What things *measured*, from a
  build-1962 `clientdb.sd2` by way of `Tools/SdbDocs`.

A number from a patch note and a number from the client database are different kinds
of fact, so every figure on a page is labelled with which it is. Where they disagree
the page says so instead of picking a winner.

## Regenerating

```
node emit_hand.js                                  # entity ids for matching
python3 import_1962.py --src <dir of SdbDocs .md>  # -> dump-1962.js
python3 build.py firefall-reference.html           # single-file build
```

`import_1962.py` prints what it failed to match. It currently matches 15/15 weapons
and 50/50 abilities; if that number drops after regenerating, the mapping tables at
the top of the script need a new entry rather than the data needing a fix.

## The two mapping tables

`WEAPON_TEMPLATES` and `ABILITY_ALIASES` are declared, not inferred. Weapon template
names are designer shorthand — template 12115 is "David's Magic HMG" and it is the
Accord Dreadnaught's HMG — and several weapons kept a pre-1.6 template in the database
beside the rebuilt one, so name matching alone picks the wrong weapon. Each entry
names four roles: `pve`, `alt`, `pvp`, `legacy`.

## Adding a deviation

```js
const BUILD_OVERRIDES = {
  'creeping-death': {
    shipped:   'what the game did',
    build:     'what yours does',
    rationale: 'why'
  }
};
```

Renders as a diff on that entity's page. The original is never overwritten.
