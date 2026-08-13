---
project: pin
kind: test-stream
title: "NPC Combat (N1-N7)"
relates:
  - ../TEST-REGISTER.md
---

# NPC Combat

Part of the [in-game test queue](README.md). Setup, admin commands and the monster type id table:
[Session Setup](Session-Setup.md).

The first check of anything [M2](../streams/m2-npc-combat.md) has built. Nothing moves yet, so every
entry here is about a monster standing still: it notices, turns, and shoots. Walking towards you is
the locomotion pass and isn't in this stream.

**All seven pass as of 2026-08-12**, across two sittings. Both model guesses this stream existed to
check came out right — local forward is +X (N1), and the burst timing read off the template reads as
a weapon rather than a strobe (N5) — and neither is asserted any more.

Four of the seven failed the first time and only one of those was the AI's fault. Two failures
(N2, N6) turned out to be the same weapon-resolution bug wearing different disguises, one stripping
a rifle's range and the other a rifle's damage, live in every weapon the server had ever resolved:
[DATA-11](../gaps/data.md#data-11). One (N4) was a real AI defect. One (N3) was the entry itself
being wrong about what the server can see. That ratio is the argument for writing entries that say
what to grep — none of it was visible from the screen.

Confirm the deployed build is newer than the AI work before running any of these. A stale
`GameServer.dll` fails every entry here identically and silently — an empty `AIEngine.Tick` writes
nothing, so the log looks the same as an NPC that decided to do nothing:

```
ls -la ~/Games/PIN/GameServer/GameServer.dll UdpHosts/GameServer/bin/Release/net10.0/GameServer.dll
```

Every entry wants the server log open:

```
tail -f ~/Games/PIN/logs/GameServer.log
```

The AI writes three Debug lines — `NPC {id} target {previous} -> {current}` when selection changes
its mind, `NPC {id} opens fire on ...` at the start of each burst, and the existing
`{Target} took {Amount} damage from {Attacker}` when a round lands. If none of the first two ever
appear, suspect the deploy before suspecting the code.

## [x] N1: An NPC notices you and turns to face you

1. `npc 1196 5 0 0` — a Chosen Fiend five metres away
2. Stand still and watch it for a few seconds
3. `grep -aE "NPC [0-9]+ target" ~/Games/PIN/logs/GameServer.log | tail -5`

Pass: within about a second the log shows the NPC's target going from empty to your entity id, and
the monster on screen is facing you rather than whatever direction it spawned in.

Passed first run, 2026-08-12. Both halves: perception and target selection work against a real
client, and **`Facing.Towards` has the forward axis right** — local forward is +X. That was the
guess most likely to bite, and it's now the one part of the orientation convention that's been seen
rather than reasoned about, so anything later that needs to point a character at something can lean
on it.

The first attempt looked like a failure and wasn't. The deployed `GameServer.dll` predated the AI
entirely, so nothing logged and nothing turned — which is exactly what a broken perception pass
would look like. That's why the build check is in the preamble now.

## [x] N2: An NPC shoots you and the damage lands

The headline check for the attack pass, and what [H5](Hostility.md) has been blocked on since it was
written.

1. `npc 1196 5 0 0`
2. Stand in front of it and do nothing
3. `grep -a "opens fire" ~/Games/PIN/logs/GameServer.log | tail -5`
4. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -10`

Pass: the NPC opens fire, the damage lines name the monster as the attacker, and hits show
client-side. Shields absorb first, exactly as they do from any other source.

**Read this off the log, not the health bar.** The Chosen's rifle does 1 damage a round, twice every
175ms — about 11 dps, which is the real shipped number and not a bug. Against the 3000-shield
stand-in from [DATA-1](../gaps/data.md#data-1) that's four and a half minutes before health moves at
all, and continuous fire keeps resetting the recharge delay so the bar just sits there. A working
attack pass and a broken one look identical on screen; the `damage from` lines are the difference.
Of the four hostile ids in [Session Setup](Session-Setup.md), 1304 carries the same 1-damage rifle,
528 does 39 dps but only reaches 5m, and 2342 does 125 a hit every 3.35s at 50m — that last one is
the fastest way to watch a health bar actually move.

If it opens fire and nothing lands, the shot is going out but missing — check whether the rounds are
leaving from a sensible place by re-reading N1's facing result first, since the muzzle position is
derived from orientation.

Re-run [H5](Hostility.md) at the same time; this is the same scenario and it's the only check that
exercises `CanDamage` with a monster as the attacker rather than the target.

**First run, 2026-08-12: failed, and the AI wasn't at fault.** The Chosen turned to aim and never
fired. It logged `holding fire ... WeaponUnusable (weapon NPC Assault Rifle, reach 0m)` 20 times a
second: its rifle resolved to a range of zero, so there was no distance at which shooting made
sense. Root cause was [DATA-11](../gaps/data.md#data-11), a zero multiplier in the weapon's
`WeaponTemplateModifiers` row being read as a literal zero — the rifle's real reach is 180m. Fixed
and deployed; re-run.

The failure was only diagnosable because the hold-fire reason gets logged. The run before that one
produced no output at all and could have been anything.

## [x] N3: Cover stops the shot

1. `npc 1196 5 0 0`
2. Let it open fire, then step fully behind something solid
3. `grep -a "opens fire" ~/Games/PIN/logs/GameServer.log | tail -5`

Pass: damage stops while you're behind cover and resumes when you step out. The NPC keeps you as its
target throughout — target selection holds a target through broken line of sight on purpose, so a
`target ... -> ` line going empty the moment you break sight is wrong.

**Cover has to be an entity, not scenery.** `LoadMapsCollision` is `false` in `GameServer.dll.config`,
so the server holds no terrain and no buildings — only entity colliders. Hiding behind a rock is
hiding behind nothing as far as the raycast is concerned, and the shots go straight through. This is
also why an NPC can shoot you through a wall.

Use a deployable that actually has a collision id. `deployable 395` (Battleframe Station) does, is
large, and one already stands in the Coral Forest. Many others don't and log
`Deployable N info has no collision id, what do?` at startup — 28 of them in zone 448 alone — so
check for that line before trusting a given id as cover.

First run, 2026-08-12: **inconclusive, and the entry was at fault.** Damage kept landing behind
scenery, which looked like broken line of sight but only meant there was no scenery on the server to
break it — the shots weren't passing through cover, there was no cover. Passed on the re-run against
an entity collider the same day.

Worth carrying well past this entry: with `LoadMapsCollision` off, every server-side raycast in PIN
sees an empty world with a few entities floating in it. That covers every shot a player fires too,
not just an NPC's.

## [x] N4: Walking out of range disengages

1. `npc 1196 5 0 0`
2. Walk away in a straight line until it stops shooting, counting the distance
3. `grep -aE "NPC [0-9]+ target" ~/Games/PIN/logs/GameServer.log | tail -5`

Pass: firing stops and the log shows the target dropping back to empty, somewhere around 60m out —
the leash, deliberately wider than the 40m it takes to notice you in the first place so that
shuffling across the boundary doesn't make it drop and re-acquire you every tick.

40m and 60m are invented, from [DATA-10](../gaps/data.md#data-10), not numbers from `dbmonster`. This
entry confirms disengagement behaves like a radius, not that it's the right radius.

**First run, 2026-08-12: failed, and found a real one.** The NPC only stopped firing when it left
draw distance, and resumed the instant it came back — no measurable radius at all. Two causes, both
now fixed:

- Engagement was bounded by the **weapon's** reach rather than perception. The Chosen's rifle carries
  180m against a 40m perception radius, so an NPC that noticed you at 40m kept shooting until you
  scoped out. `TargetSelection` now forgets anything past the leash outright.
- Threat was **uncapped**. A target that stood in front of an NPC for a minute banked several hundred
  points, which at 8/sec took minutes to decay under the engage threshold — so it never really let
  go. Capped at 60, which crosses back under the threshold in 6.25 seconds.

Both are covered offline now in `ThreatTableTests`; what those can't tell you is where the boundary
actually falls in metres, which is this entry's job. Passed on the re-run the same day — the NPC now
disengages at a measurable distance well inside draw distance, instead of holding on until scope-out.

## [x] N5: The firing rhythm reads as a weapon

1. `npc 1196 5 0 0` and let it shoot for ten seconds or so
2. `grep -a "opens fire" ~/Games/PIN/logs/GameServer.log | tail -10`

Pass: the gap between consecutive `opens fire` timestamps matches the `next burst in Nms` the line
itself reports, the muzzle flash fires once per burst, and it reads as a weapon being fired rather
than a strobe or one shot every few seconds.

What this can't tell you is whether the rhythm matches Firefall's — `AttackWindow.Resolve` is a
reading of `MsPerBurst`, `MsBurstDuration` and `RoundsPerBurst` that nothing has ever confirmed. A
capture is the only thing that answers that; see [Capture Replay](Capture-Replay.md).

## [x] N6: Two hostile NPCs fight each other

Cheap, and the only entry here that doesn't need the player in the line of fire.

1. `npc 290 0 0 0` — Accord Assault, faction 1
2. `npc 1196 6 0 0` — Chosen Fiend, faction 2
3. Stand well clear and watch
4. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: they turn on each other, trade fire, one dies, and the corpse despawns about 30s later. The
survivor stops firing the moment the other dies rather than emptying rounds into the body.

**First run, 2026-08-12: half of it worked, and the other half found the bug.** The Accord trooper
turned, opened fire and rendered full muzzle VFX — 280 bursts in the log — and the Chosen it was
shooting never lost a point of health. Not one `damage from` line the whole session. The Accord's
Famas resolved to **0 damage per round**, and `TakeDamage` drops anything that rounds to zero before
it logs, so the shots were real, reached the target, and were worth nothing.

Same root cause as N2's failure, from the opposite column: [DATA-11](../gaps/data.md#data-11) zeroed
this weapon's `damage_per_round` where it zeroed the Chosen's `range`. Its real damage is 65. Passed
whole on the re-run, including the half that never got to run the first time: one of them dies, the
corpse despawns, and the survivor stops firing rather than emptying rounds into the body.

That the two monsters failed *differently* is what made this findable. One weapon losing its range
and another losing its damage looks like two unrelated bugs right up until you read both rows.

## [x] N7: A burst that loses its target ends cleanly

1. `npc 1196 5 0 0`
2. Let it open fire, then kill it mid-burst
3. Repeat, this time killing it with `rment` while it's firing

Pass: no stuck firing animation on the corpse, and nothing in the log after the kill mentions that
entity opening fire or dealing damage. `rment` while an NPC is mid-burst is the case where state
outlives the entity, which is what the prune pass in `AIEngine` is for.
