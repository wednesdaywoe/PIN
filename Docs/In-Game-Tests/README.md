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
