---
project: pin
kind: test-stream
title: "NPC Combat (N1-N12)"
relates:
  - ../TEST-REGISTER.md
---

# NPC Combat

Part of the [in-game test queue](README.md). Setup, admin commands and the monster type id table:
[Session Setup](Session-Setup.md).

The check on everything [M2](../streams/m2-npc-combat.md) has built. N1–N7 cover a monster that
stands still and fights — it notices, turns, and shoots — and N8–N12 cover the locomotion pass that
landed after them, which is the half that decides where it stands in the first place.

**N1–N7 pass as of 2026-08-12**, across two sittings. Both model guesses that half of the stream
existed to check came out right — local forward is +X (N1), and the burst timing read off the
template reads as a weapon rather than a strobe (N5) — and neither is asserted any more.
**N8–N12 have not been run.**

Four of the seven failed the first time and only one of those was the AI's fault. Two failures
(N2, N6) turned out to be the same weapon-resolution bug wearing different disguises, one stripping
a rifle's range and the other a rifle's damage, live in every weapon the server had ever resolved:
[DATA-11](../gaps/data.md#data-11). One (N4) was a real AI defect. One (N3) was the entry itself
being wrong about what the server can see. That ratio is the argument for writing entries that say
what to grep — none of it was visible from the screen.

A stale `GameServer.dll` fails every entry here identically and silently — an empty `AIEngine.Tick`
writes nothing, so the log looks exactly like an NPC that decided to do nothing. `start-pin.sh`
builds and installs before it launches, so this is no longer a step to forget, but read the build
banner it prints and confirm the server reports the same number back:

```
grep -a "Running build" ~/Games/PIN/logs/GameServer.log
```

[Session Setup](Session-Setup.md) explains what the banner means and what a mismatch looks like.

Every entry wants the server log open:

```
tail -f ~/Games/PIN/logs/GameServer.log
```

The AI writes these Debug lines, and if none of them ever appears, suspect the deploy before
suspecting the code:

| Line | Written when |
|------|--------------|
| `NPC {id} target {previous} -> {current}` | selection changes its mind |
| `NPC {id} opens fire on ...` | a burst starts |
| `NPC {id} holding fire on ...: {reason}` | it has a target and isn't shooting, once per change of reason |
| `NPC {id} sets off {where} at {speed}m/s, {distance}m away, stopping at {stopWithin}m` | it starts walking |
| `NPC {id} has chased {target} {distance}m from home and is going back` | it hits its leash |
| `NPC {id} is home` | it arrives back |
| `{Target} took {Amount} damage from {Attacker}` | a round lands (not new, and not AI-specific) |

**`npc <id> <x> <y> <z>` takes a world position, not an offset from where you're standing** —
[SpawnCharacterServerCommand](../../UdpHosts/GameServer/Systems/Admin/Commands/SpawnCharacterServerCommand.cs)
passes the three numbers straight through. With no coordinates at all it spawns at your feet, which
is the form to reach for when you don't care where. To put one a measured distance away, read your
own position off `hazard` first and do the arithmetic — the locomotion entries below all start that
way, because where an NPC begins is half of what they're measuring.

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

**Read this off the log, not the health bar.** The Chosen's rifle does 1 damage a round, and the
template asks for two rounds every 175ms. `AttackWindow`'s 250ms floor slows that to **8 rounds a
second, so 8 dps** — counted straight off a 2026-08-12 log, which runs exactly 8 `damage from` lines
per second for 190 lines. (The template rate would be 11.4; the floor is why it isn't. Both numbers
are real, 8 is the one that lands.) Against the 3000-shield stand-in from
[DATA-1](../gaps/data.md#data-1) that's six minutes before health moves at all, and continuous fire
keeps resetting the recharge delay so the bar just sits there. A working attack pass and a broken one
look identical on screen; the `damage from` lines are the difference. See
[Session Setup](Session-Setup.md) for what to spawn instead when the point is to watch something die.

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

**Locomotion changed how this one behaves, without changing what it proves.** An NPC now follows
you, so walking away no longer opens the gap on its own: it keeps pace at its own speed until it
hits the 50m chase leash from where it spawned, turns round, and only then does the separation grow
fast enough to cross 60m and drop you. The result is the same and the middle of it looks completely
different, so re-run it expecting a chase. N11 is the entry that measures the leash itself. Of the
first seven entries this is the only one locomotion touches — the rest spawn their monster inside the
12m it wants to stand at, so it has no reason to move.

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

## [ ] N8: An NPC closes the distance

The milestone's exit condition, and the one entry here that has to pass for locomotion to count as
landed at all. Everything after it is about how well.

1. `invuln on`
2. `hazard` — its first line is `<your character> at <X, Y, Z>`; those are the numbers the next
   command needs
3. `npc 1196 <X+35> <Y> <Z>` — a Chosen Fiend 35m away, inside the 40m it can notice you from and
   well outside the 12m it wants to stand at
4. Stand still and watch it the whole way in
5. `grep -aE "NPC [0-9]+ (target|sets off|opens fire)" ~/Games/PIN/logs/GameServer.log | tail -10`

Pass: it acquires you, turns, and runs in — `sets off after <your entity id> at 6m/s, 35m away,
stopping at 12m` — covers the ground in about four seconds, stops without walking into you, and
opens fire from where it stopped. On screen the important part is that it *arrives* and *stops*:
an NPC that keeps going until it's standing inside you is a stopping-distance bug, and one that
never sets off at all is either a stale deploy or a target it never acquired, which step 5's first
line tells you apart.

6 m/s is not invented — it is monster 1196's chassis `FastSpeed`, read through
`MoveSpeed.Resolve`. What is invented is the 12m it stops at
([DATA-10](../gaps/data.md#data-10)); nothing in `dbmonster` says how close a monster likes to be.

If it sets off and never arrives, check the log for it repeatedly setting off and stopping — that
is the stopping distance oscillating, and it means the hysteresis in `NpcMovement` isn't holding.

## [ ] N9: The run reads as a run

Separate from N8 because they fail separately: an NPC can arrive at exactly the right place while
looking wrong the whole way, and that is the more likely of the two.

1. Run N8 and watch the monster's legs, not its position

Pass: it plays a run animation while it closes and settles into an idle when it stops.

Fail, it slides along in an idle pose the whole way: `RunningState` in `NpcMovement` is wrong. It's
`0x2004` — `Movestate.Running` in the high nibble, `MovementFlags.Movement` in the low byte —
derived from those two enums and never seen on the wire, on top of a packing that
[NET-12](../gaps/network.md#net-12) already has down as unconfirmed. Nothing else in the server
sets a movement state for a character it owns, so this is the first time the value has been asserted
rather than echoed back from a client. Write down what it did do, because the failure is the
evidence: a sliding idle says the high nibble is being read somewhere else, and a monster that
crouches or falls over says it's being read as a different `Movestate`.

The resting value is unchanged from what every NPC has always had (`0x1000`, standing), so a wrong
value here can only break the moving case.

## [ ] N10: A short-ranged monster comes all the way in

The stopping distance is bounded by what the monster can actually hit you from, so two monsters with
very different weapons should stop in very different places.

1. `invuln on`
2. `hazard`, then `npc 1196 <X+35> <Y> <Z>` — Chosen Fiend, 180m rifle
3. Watch where it stops, then `rment` to clear it
4. `hazard`, then `npc 528 <X+35> <Y> <Z>` — Melded Aranha, 5m reach and 11 m/s
5. `grep -a "sets off" ~/Games/PIN/logs/GameServer.log | tail -5`

Pass: the Fiend stops about 12m out and shoots from there; the Aranha runs past that and keeps
coming until it's about 4m away — `stopping at 4m` in the log — because 80% of a 5m reach is as far
back as it can stand and still land anything. Both then fire.

The Aranha is also the fastest thing in the test set at 11 m/s against the Fiend's 6, so the
difference in how they cross the ground should be obvious without measuring it.

Fail, the Aranha stops at 12m with the Fiend and never fires: the stopping distance isn't reading
the weapon's reach, and `holding fire ... OutOfRange` in the log is what that looks like from the
other side.

## [ ] N11: It gives up and goes home

The leash. Without it a monster follows one player across the zone and never comes back, which is a
worse failure than not moving at all because it empties the place out over a session.

1. `invuln on`
2. `hazard` and note the position — this is where the NPC's home will be
3. `npc 1196` — spawns at your feet, so home is exactly where you're standing
4. Walk away in a straight line, slowly enough that it keeps following, past 50m from that spot
5. `grep -aE "NPC [0-9]+ (has chased|is home)" ~/Games/PIN/logs/GameServer.log | tail -5`

Pass: it follows, then turns round somewhere past 50m — `has chased <you> 50.xm from home and is
going back` — walks back to where it spawned, logs `is home`, and stops. Then walk back towards it
and it should engage again, because arriving home is what lets it chase a second time.

Watch for it turning round and immediately coming back out, which would show up as `has chased` and
`is home` alternating every few seconds. That oscillation is what the `Returning` flag exists to
prevent, and it's the most likely thing to be wrong here.

50m is invented ([DATA-10](../gaps/data.md#data-10)). This entry confirms an NPC leashes, not that
it leashes at the right distance.

It keeps shooting while it walks home, which looks odd and is deliberate — only movement is leashed.
It stops on its own when you pass the 60m N4 measured, since past that it forgets you entirely.

## [ ] N12: It doesn't follow you into the air

The one that needs no client fix and no data to be a real bug. The server has no terrain at all
(`LoadMapsCollision` is off, `MapsPath` is empty), so an NPC has no idea where the ground is; it
takes the height of whatever it's chasing and treats that as ground, on the grounds that a player
standing on the ground is a ground measurement. A jetpack breaks that assumption, and
`NpcMovement` is meant to notice.

1. `invuln on`
2. `hazard`, then `npc 1196 <X+35> <Y> <Z>`
3. Let it start closing, then jetpack straight up and hold
4. Watch it, then land somewhere else and watch it again
5. Find something to stand on — a rock, a container, the roof of the Battleframe Station — and
   watch from up there

Pass: it keeps chasing across the ground underneath you and does not rise. When you land it walks
to where you are. Standing on something a few metres up, it comes to the bottom and stops there
rather than climbing an invisible ramp to your feet.

Two separate rules are being checked and they can fail independently. The first is that a target's
height is only believed while the target is on the ground — `IsAirborne`, which the client reports
on every pose message. The second is `Steering.MaxSlope`: no destination is walked to up a slope
steeper than 45° measured over the whole remaining approach, so a target on a roof 10m up and 3m
away is refused outright. The unit tests in `SteeringTests` cover both as arithmetic; this is
whether they read as an animal on screen.

Fail, it levitates: whichever rule is wrong, say which of the two shapes it took — rising while you
hover is the airborne check, and climbing a smooth invisible ramp toward a rooftop is the slope
limit.
