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

## A player logs under its name, and everything else under an id

**Grepping the log for the player's entity id finds nothing, and that is not evidence of anything.**
`CharacterEntity.ToString` returns the display name when `IsPlayerControlled` and falls back to
`CharacterEntity (<id>)` otherwise, so a hit on the tester reads:

```
[19:15:01 DBG] Fallback took 49 damage from CharacterEntity (2237404354619578112), 0 of it on shields, 0 shields and 951 health left
```

The id in that line is the **attacker's**. The player is `Fallback` — the character name, whatever
it happens to be on the account being used.

This cost a real conclusion on 2026-08-16: a sweep of every log for the day reported that the player
had never taken damage and that the 1,000-health change was therefore untestable, when in fact the
player had been downed once and brought under 15% twice. The tester's own account of the session was
what corrected it. **When a grep says a system never ran, check that the grep can match the subject
before believing it.**

So: grep for damage *by name* when the subject is a player, and by id when it is anything else. If
the character name is not known, this finds it and every other actor in one pass:

```
grep -aoE "^\[[0-9:]+ [A-Z]+\] [^(]+ took [0-9]+ damage" ~/Games/PIN/logs/GameServer.log \
  | sed -E 's/^\[[0-9:]+ [A-Z]+\] //' | sort -u
```

Anything in that list that is not `CharacterEntity`, `Thumper`, `Deployable` or `Vehicle` is a
player.

## Health bars are aim-driven, so splash cannot be read off them

**A creature's health bar is not a readout of its health. It is a readout of what you are looking
at.** Established 2026-08-16 from the client's own interface source, after the splash run came back
"I think it's technically working, it's hard to tell".

`system/gui/components/MainUI/HUD/EntityPlates/EntityPlates.lua` decides this, and the rule is one
line (1223):

```lua
local interested = ((PLATE.focus and PLATE.status.visible) or PLATE.inMediview or (g_sinView and PLATE.rules.use_icon));
```

If `interested` is false the bar is hidden outright, whatever the creature's health is. `focus`
means the client's aim tracking has settled on that entity; losing it schedules the fade after
`RELEASE_PLATE_FOCUS_DELAY = 1.5` seconds. Damage does not enter into it — damage only animates a
bar that is already on screen. So a bar that comes and goes as you sweep past two enemies is the
shipped behaviour working, not a delivery problem, and nothing the server does will change it.

**What this means for any test that damages something you are not aiming at** — splash, damage over
time, a hazard, an ability that hits behind you: the screen structurally cannot show it. The second
target's bar is hidden *because* it is the second target. Do not tune, re-run, or open an issue on
the strength of not seeing a bar move.

Read the log instead. Every damage application prints the amount and the health remaining:

```
grep -aE "took [0-9]+ damage from" ~/Games/PIN/logs/GameServer.log | tail -20
```

Two entities inside one second, with the larger number on the one you aimed at, is a blast landing.
For splash specifically the `Splash from` line names the weapon, the radius and the count. If a
sitting needs a live reading rather than an after-the-fact one, aim at the *far* target and let the
blast reach the near one, which puts the bar you can see on the entity the splash has to travel to.

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
