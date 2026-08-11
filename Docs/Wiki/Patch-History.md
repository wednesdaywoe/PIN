# Patch history

Firefall's build numbers run monotonically across every version prefix, so a build number alone
dates a claim. That makes this page the tool the rest of the wiki depends on: given a source, find
its build, and you know whether it describes [our client](Client-Version.md) or a game that no
longer existed by then.

Nothing here is authored from memory. It is the metadata of 87 archived patch-note pages mirrored
from both Fandom wikis, plus the official patch-notes subforum and update pages recovered from the
Wayback Machine, all on 2026-08-11. [Sources](Sources.md) says where the raw files live.

## The version lines

| Line | Builds | Dates | Wiki notes | Official notes |
|------|--------|-------|-----------|----------------|
| v0.5 | 1409–1545 | 2012-08-09 → 2013-03-05 | 16 | yes |
| v0.6 | 1586–1641 | 2013-03-28 → 2013-06-06 | 10 | yes |
| v0.7 | 1665–1735 | 2013-06-27 → 2013-12-18 | 27 | partial |
| v0.8 | 1736–1757 | 2014-01-30 → 2014-04-22 | 10 | — |
| v0.9 | 1766–1780 | 2014-05-13 → 2014-07-08 | 3 | — |
| v1.0 | 1783–1797 | 2014-07-15 → 2014-08-27 | 10 | yes |
| v1.1 | 1802–1833 | 2014-09-16 → 2014-11-04 | 9 | yes, + "Elemental Destruction" |
| v1.2 | 1836–1853 | 2014-11-18 → 2015-01-15 | 8 | yes, + "Together Toward Victory" |
| v1.3 | 1856–1869 | 2015-02-10 → 2015-04-21 | 5 | yes, + "War in the Amazon" |
| v1.4 | — | — | **none** | **none** |
| v1.5 | — | — | **none** | **none** |
| v1.6 | 1934–1946 | → 2016-03-02 | 5 | yes, + "Razor's Edge" |
| **v1.7** | **1962** | **2016-04-26 →** | **none** | **yes, + "Devil's Due"** |

## v1.7 "Devil's Due" — what we actually run

Recovered from the official forum and `firefall.com`, both via Wayback:

| Document | Recovered as |
|----------|--------------|
| Patch notes for v1.7.1962 | `patch-notes-for-v1-7-1962.8510722.txt` |
| Update 1.7: Devil's Due — feature notes | `site_update-17-devils-due.txt`, 19 KB |
| Update 1.6: Razor's Edge — feature notes | `site_update-16-razors-edge.txt` |

Build 1962 itself is a **bug-fix patch**, not a feature release — Devil's Tusk ARES job fixes, a
TDM frame-switch bug, squad chat leaking to kicked players, the PvP loadout button disabled because
the feature was never finished. The features arrived in v1.7 proper, a few weeks earlier.

Two facts from the Devil's Due notes bear directly on the server:

- **The level cap is 45.** Anything built against a lower cap is pre-1.7.
- **Devil's Tusk was refreshed** onto the same content model as Coral Forest and Sertao, and given
  a three-phase zone-wide world event — capture/defend, dropship evacuation, then the finale.

The last line of the 1962 notes is worth keeping in mind while reading anything else: *"The PvP
loadout button in the garage is no longer available as this feature is not yet implemented."* The
game we target shipped with pieces missing. Not every gap in PIN is a gap in PIN.

## Did v1.4 and v1.5 ever ship?

Probably not, but this is not settled.

Between build 1869 (2015-04-21) and 1934 (early 2016) there are 64 builds and roughly eleven
months with no release under any label. Three independent sources are silent across it: both
Fandom wikis, and the official patch-notes subforum, which jumps v1.3 → v1.6 directly.

What stops this being conclusive: only **4 of the subforum's 6 index pages** were ever archived —
`page-5` and `page-6` return 404 from Wayback, so two pages of thread listings are simply gone.
The four that survive do span the full range, v0.5.1409 through v1.7.1962, which argues the missing
pages hold overflow rather than a hidden era. It is evidence, not proof.

The competing read — that v1.4/v1.5 shipped and every trace was lost — has to explain silence in
three places at once, and the client works against it too: build 1962's own forum thread is titled
*patch notes for v1.7.1962*. Note that this makes the client's internal `Firefall (v1.5.1962)`
window title provably wrong about its own version, which is worth remembering before trusting any
other string in that binary.

The smaller beta-era gaps scattered through v0.5–v0.7 are not worth chasing. They describe a game
that was deleted at v1.0.

## Nearest the target

| Build | Line | Date | Source |
|-------|------|------|--------|
| 1856 | v1.3 | 2015-02-10 | `Patch_Notes_v1-3-1856.wiki` |
| 1858 | v1.3 | 2015-02-12 | hotfix |
| 1859 | v1.3 | 2015-02-19 | hotfix |
| 1861 | v1.3 | 2015-02-27 | hotfix |
| 1869 | v1.3 | 2015-04-21 | `Patch_Notes_v1-3-1869.wiki` — also the middle packet capture |
| 1934 | v1.6 | — | retail wiki |
| 1940 | v1.6 | — | retail wiki |
| 1942 | v1.6 | — | retail wiki |
| 1946 | v1.6 | 2016-03-02 | both wikis + official thread |
| **1962** | **v1.7** | **2016-05-05** | **official notes recovered — this is our client** |

The retail wiki also carries v1.6's three-part feature writeup — battleframes and combat, PvP and
systems, PvE content — 71 KB in total. With Devil's Due on top of it, the last two releases of
Firefall are now both documented from primary sources.

## Method note

Where prose and data disagree, the client wins. `clientdb.sd2` holds build 1962's actual numbers,
so a v1.6 patch note claiming a stat change can be checked against the table rather than believed
or discarded. Patch notes are for intent and sequence — what changed, when, and why — not for
values.
