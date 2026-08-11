# Sources

What survives of Firefall's game knowledge, what build each source describes, and how much to
trust it. Every source below was reachable and counted on 2026-08-11.

The ordering principle: **the client outranks the internet.** `clientdb.sd2` is build 1962's own
data and cannot be stale. Prose is for the things the tables don't hold — what a system was *for*,
how it played, what changed and when.

## Primary — shipped with the client

These are authoritative for [build 1962](Client-Version.md) by construction.

| Source | Size | What it answers |
|--------|------|-----------------|
| `system/db/clientdb.sd2` | 32 MB | Every weapon, ability, chain, item, frame, faction, zone, localisation string. 238 tables already loaded by PIN — see [Layer 8](../Architecture/08-static-data.md) |
| `system/assetdb` | 10 GB | Models, textures, audio, UI. Names and hierarchy alone answer a lot |
| `FirefallClient.exe` | — | Strings, ini keys, command names |

`Tools/MinimalSDB dump` already answers "did this table ship with real numbers", which is the
question that decides whether a wiki page can be written from data or has to be written from
memory. Nothing else here needs building.

## Secondary — reverse-engineering projects

Same era, same rigour as PIN, and reusable directly.

| Source | Contents | Era |
|--------|----------|-----|
| [themeldingwars/Documentation](https://github.com/themeldingwars/Documentation) | 3 packet captures, OpenAPI specs for the web hosts, `sdb_names.txt`, wiki | 1802 / 1869 / 2016 |
| [AeroMessages](../../Lib/AeroMessages/README.md) | Every GSS V66 message and view, named | 1962 |
| [Sift](https://github.com/themeldingwars/Sift) | Protocol data | 1962-ish |

`sdb_names.txt` is worth pulling early: SDB column names are stored as hashes, so a name list is
the difference between reading a table and guessing at it.

## Tertiary — community prose

This is where the era problem lives. Article counts are real; era fitness is the caveat.

| Source | Volume | Era it documents | Verdict |
|--------|--------|------------------|---------|
| [firefall.fandom.com](https://firefall.fandom.com) | 260 pages / 217 articles | retail 1.x — Broken Peninsula, Copacabana, Daily Login Rewards | **Closest fit.** Small but on-era |
| [firefall-archive.fandom.com](https://firefall-archive.fandom.com) | 4,020 pages / 1,888 articles, 1,397 images | mostly v0.5–v0.7 beta; patch notes run 2011 → v1.6.1946 | **Large and mostly wrong-era.** Mine the patch notes, distrust the articles |
| forums.firefallthegame.com (Wayback) | 25,370 archived URLs | official forums, all eras | **Mirrored: 47 patch-note threads**, incl. the only notes for build 1962 |
| firefall.com (Wayback) | not yet surveyed | the *later* official domain | **Mirrored: Update 1.6 and 1.7 feature pages.** Rest untouched |
| firefallthegame.com (Wayback) | 70,688 archived URLs | the *earlier* official domain | Knowledge base, dev blogs. Largely untapped |
| rawr4firefall.com (Wayback) | 1,285 archived URLs | v0.6–v0.8 crafting/resource guides | Deep on beta economy. Domain is **dead** — search engines still index it, DNS does not resolve |
| YouTube | — | all eras | The only source for feel, timing and VFX. Unindexed |

Both Fandom wikis expose `api.php`, so they can be mirrored properly — `list=allpages` plus
`prop=revisions` gets clean wikitext, not scraped HTML. Neither has had an active editor in years.
Content is CC BY-SA; attribution matters only if any of it is ever republished.

The archive wiki's patch notes are its real asset. 109 pages covering 2011 through v1.6.1946 is a
changelog of the entire game, and a changelog is exactly what resolves era conflicts — it says when
a thing changed, which is the question a stale article can't answer.

## What is mirrored, as of 2026-08-11

6.6 MB in `~/Games/PIN/wiki-sources/`, kept out of the repo. Each directory carries an
`_index.json` recording source URL, fetch date, revision ids and per-page byte counts.

| Directory | Contents |
|-----------|----------|
| `fandom-archive/patchnotes/` | 109 patch-note pages, wikitext |
| `fandom-retail/updates/` | 9 Update 1.6 pages incl. the 71 KB three-part feature writeup |
| `wayback/official-patch-notes/` | 47 forum threads (raw HTML + extracted first post), the v1.6/v1.7 feature pages, 6 subforum index pages |

**Every one of the 88 known builds is covered by at least one source.** The two sets are almost
exactly complementary: 29 forum threads failed to fetch — Wayback never captured them — and the
wikis carry all 29. Going the other way, build **1962 is forum-only**: it is the single build no
wiki ever documented, and the official thread is the only description of our own client that
exists.

Neither source alone would have been enough. That is the argument for mirroring more than one.

## Rules for anything written here

1. **Stamp the build.** Every claim carries the build it was verified against, or it is marked
   unverified. A page with no stamp is a rumour.
2. **Cite where it came from.** SDB table and column, or capture and message, or a URL.
3. **Prefer data to prose.** If the number is in `clientdb.sd2`, read it there and cite the table.
   Prose is for intent and history.
4. **Contradictions get recorded, not resolved by vote.** Beta prose disagreeing with 1962 data is
   the normal case and is itself a finding — it usually means a system was reworked.
