---
project: pin
kind: reference
title: In-Game Test Queue — Method
relates:
  - ../TEST-REGISTER.md
---

# In-Game Test Queue

A running log of checks for me to run with a real client, written down as work
lands so nothing accumulates unverified. Work through it at the game machine, record the result
inline, and delete entries once they've passed and the behaviour is covered elsewhere.

The index of streams — what's passing, what's blocked, what's not started — lives in the
[Test Register](../TEST-REGISTER.md). This file is just the method: how an entry is written and
the marker legend it uses.

[One-Sitting Run Order](Sitting-Plan.md) is the other direction through the same entries: every
check that can be run solo without a logout or a restart, in the order that shares the most setup.
Read it when you are about to sit down, rather than picking entries off the register one at a time.

Status markers: `[ ]` not run, `[x]` passed, `[!]` failed (leave it in with what happened),
`[-]` skipped or not reproducible.

## Spawn coordinates are absolute, and this file used to say otherwise

**`npc <id> <x> <y> <z>` takes a position in the world, not an offset from you.** Entries across
this queue used to write `npc 1196 5 0 0` and gloss it as "a Chosen Fiend five metres away". It is
not: it is the point (5, 0, 0) in zone 448, roughly 500m sideways and 400m below anywhere a player
stands. Corrected everywhere on 2026-08-16, and it really did happen — the log for that morning
holds `Spawned monster 1196 ... at <10, 0, 0>` and `at <0, 0, 0>`, two Chosen Fiends dropped
through the floor of the world by an entry that read as a distance.

**Use the bare form.** `npc <id>` with no coordinates spawns on top of you, which is also the only
ground the server can vouch for — there is no terrain data, so a typed coordinate can as easily be
inside a hillside as on it, and nothing will tell you which. When an entry needs two things a set
distance apart, spawn one, walk the distance, and spawn the next. The same holds for `deployable`
and `vehicle`.

A typed coordinate is worth it only when the entry needs a *specific* place, and then it should be
a position somebody has actually stood on. `grep -a "Ground sample" ~/Games/PIN/logs/GameServer.log`
is the record of every footing the server has ever measured, which is where a trustworthy one comes
from.

Adding an entry: state the task, the steps in order, and every command in full with its arguments
filled in — real type ids, coordinates, item ids, grep lines. These get run on the game machine
away from the source, so a step that says "spawn a monster" instead of `npc 1196` costs a trip back
here, and an under-specified step gets run wrong. If a check needs a specific world state (two
clients, a particular NPC), say so; that's usually what makes an entry sit unrun.

One file per feature, and entry ids stay unique across the whole queue — a `D5h` or an `H1` in a
commit message or a code comment still names exactly one entry. Put a new entry in the file whose
feature it exercises; start a new file when a feature arrives that none of these cover, then add it
to the [Test Register](../TEST-REGISTER.md)'s index.

A `[!]` that turns out to name a real, currently-live defect (not just an unbuilt feature) also
gets a line in the [Issue Register](../ISSUE-REGISTER.md) — see that doc's
[method notes](../gaps/method-notes.md) for the line between the two.
