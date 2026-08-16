---
project: pin
kind: test-stream
title: "Death and Respawn (B1-B5)"
relates:
  - ../TEST-REGISTER.md
---

# Death and Respawn

Part of the [in-game test queue](README.md). Setup, admin commands and the type-id tables:
[Session Setup](Session-Setup.md).

The check on [NET-23](../gaps/network.md#net-23), which is the last thing standing between the
project's loop and a session that survives losing a fight. Until now a player who died stayed dead:
the server killed them correctly, the client offered nothing, and the only way on was to reconnect.
[N16](NPC-Combat.md) found it on 2026-08-13, and [M7](Thumper-Defence.md) made it matter — a
defended thumper is a fight the tester can lose.

**Written 2026-08-15 and closed on both routes the same day: [B1](#-b1-a-dead-player-gets-back-up)
and [B3](#-b3-doing-nothing-still-gets-you-back-up) pass, 2 of 5.** Two deaths, one for each way
back. At 11:03:51 the tester went down and waited, and the fallback returned them 31 seconds later.
At 11:17:02 they went down and pressed Reload, and were back in three.

Everything the design depends on is now evidenced: the state change lands (all six attackers dropped
target inside the same second, the M2 signature of a character going non-alive), the offer goes out,
the give-up prompt appears within its 2-second gate, the key reaches the server, the fallback covers
anyone who ignores it, and both respawns put the character on real footing.

**Both sessions ended in menu logouts with clean character saves.** Every previous PIN session that
contained a player death ended in a reconnect. [NET-23](../gaps/network.md#net-23)'s symptom is
gone.

What is left is small: the countdown's visible reading (B2, whose main risk B1 already retired),
monsters still dying properly (B4), and dying inside a thumper defence (B5).

What was built, in one paragraph. Retail's death was two stages, and the client still ships both:
you go down and bleed out first, and only then die. The death screen —
`gui/components/MainUI/HUD/Bleedout/Bleedout.lua` in the installed client — opens on character
state `incapacitated` and nothing else, and the give-up prompt that actually sends the respawn
command lives inside it, gated on a permission flag. PIN went straight from alive to `Dead`, so the
screen never opened. A player now goes to `Incapacitated`, gets the `respawn_input` permission and a
`RespawnTimesData` pair, and either taps out on the Reload key or is respawned by
[BleedoutSim](../../UdpHosts/GameServer/Systems/Combat/BleedoutSim.cs) after 30 seconds. NPCs are
untouched and still die outright.

**Two invented numbers and one unverified guess, all of which this stream is here to catch.** The
2-second wait before the prompt appears and the 30-second forced respawn have no source — nothing
shipped carries either, and the client draws whatever it is told. The guess is the *units*: PIN
sends both times as absolute shard times, following `TimedDailyRewardData.CountdownToTime`, the only
other countdown target in this protocol PIN already sends, while the client's Lua consumes them as
remaining seconds. If the engine does that subtraction, B2 passes. If it doesn't, B2 shows a wrong
or missing timer while B1 still passes, which is exactly why they are separate entries.

The 2016 capture cannot help with any of this: nobody dies in it. See
[Capture Replay](Capture-Replay.md).

The log lines this stream reads:

```
grep -a "went down to" ~/Games/PIN/logs/GameServer.log
grep -a "downed, tap-out" ~/Games/PIN/logs/GameServer.log
grep -a "did not tap out" ~/Games/PIN/logs/GameServer.log
grep -a "died to" ~/Games/PIN/logs/GameServer.log
```

Run the whole stream with `invuln` **off**. That is the point of it, and it is the one stream where
being killed is the pass condition rather than the accident.

## [x] B1: A dead player gets back up

The exit condition. Everything else in this stream diagnoses a failure of it.

**Passed 2026-08-15 on the second death of the day.** The tester was killed by the Basin Mouth pack,
the give-up prompt appeared, they pressed Reload, and the respawn was immediate:

```
11:17:02 INF Player 11072869122414870784 downed, tap-out in 2000ms, forced at 30000ms
11:17:02 INF Player 11072869122414870784 went down to 2235467298599207936
11:17:05 INF SendFullInventory: 246 item(s) [Gear 246], 2 resource(s), 20 loadout(s)
11:17:06 DBG Ground sample <176.65, 250.13, 491.94> from 11072869122414870784
```

Three seconds from down to respawned, and **no `did not tap out in time` line above it**, which is
what distinguishes this from [B3](#-b3-doing-nothing-still-gets-you-back-up). The fallback is the
only other route into `Respawn`, and it announces itself.

**This entry was first recorded as unattempted, and the mistake is worth keeping.** The reader
checking the log grepped for `RequestRespawn` — a string that appears in the source and never in the
output, because the handler had no log line — got zero hits, and reported a passing entry as a client
that had never sent the command. The evidence was always there in what `Respawn` does rather than in
what the handler says. `RequestRespawn` now logs `Player <id> tapped out`, so the next reader does
not have to reason about absences. Same lesson as K1's `rolled item 10 x2, not paid`: the log line
written so two outcomes can be told apart is the one that pays for itself.

1. Log in to zone 448. Confirm `invuln` is off — if you have run it this session, run `invuln`
   again and check the feedback says it is disabled.
2. Walk to the Basin Mouth pack and let it kill you. Do not fight back; the pack that killed the
   tester in [N16](NPC-Combat.md) is the reliable way to die.
3. On screen: expect the bleedout panel across the bottom, a red bar draining, and a prompt naming
   the **Reload** key.
4. Press Reload.
5. `grep -a "went down to" ~/Games/PIN/logs/GameServer.log` — expect one
   `Player <id> went down to <id>` line.

Pass: you are back in the world, at a spawn point, alive and able to move and shoot, without
reconnecting.

Fail, no panel at all: the state change is not reaching the client. Check the `went down to` line
exists — if it does, the server did its half and the fault is the `Incapacitated` state or the
permission flag not arriving.

Fail, panel appears but Reload does nothing: the permission flag is the suspect. The prompt is drawn
from the same flag that lets the engine send the command, so a *visible* prompt that does nothing
means the flag arrived and the command did not — record the exact on-screen text.

## [ ] B2: The countdown is a real number

The one that tests the units guess, and the only reason it is separate from B1.

Read from B1's death, before pressing anything.

**Not formally run 2026-08-15, but B1's pass retires most of its risk.** The failure this entry
exists to catch is the client reading an absolute shard time as a number of seconds. If that were
happening, the tap-out gate — `elapsed >= resurrect`, the same value on the same clock — would be a
number in the millions and the give-up prompt would **never** appear. It appeared, and the tester
used it, three seconds after going down against a 2-second gate.

So the engine does convert absolute to remaining, and the units guess is right. What is still
unread is the visible countdown itself: whether it starts near 30 and falls one per second, or shows
something wrong while the gate underneath it works. That is a glance on the next death, not a
sitting.

1. Die as in B1.
2. Watch the timer on the bleedout panel for a few seconds without pressing Reload.
3. `grep -a "downed, tap-out" ~/Games/PIN/logs/GameServer.log` — expect
   `tap-out in 2000ms, forced at 30000ms`.

Pass: the timer counts down from roughly 30 seconds, one second per second, and the give-up prompt
becomes available about 2 seconds in.

Fail, timer reads a huge number or blank: the absolute-versus-relative guess is wrong. Record what
it read. The fix is sending an offset rather than an absolute time, and B2 is the only thing that
can tell the two apart.

## [x] B3: Doing nothing still gets you back up

The safety net. `IsOverdue` is pinned offline by `BleedoutTests`, so this entry is about whether it
is reached at all, not about the arithmetic.

**Passed 2026-08-15, first attempt, and it passed by accident** — the tester meant to run B1 and
watched instead, which is the most honest way this entry could ever have been run. The sequence:

```
11:03:51 INF Player 11072869122414870784 went down to 2235455710777443840
11:03:51 INF Player 11072869122414870784 downed, tap-out in 2000ms, forced at 30000ms
11:04:22 INF Player 11072869122414870784 did not tap out in time, respawning them
11:04:22 INF SendFullInventory: 246 item(s) [Gear 246], 2 resource(s), 20 loadout(s)
11:04:22 DBG Ground sample <176.65, 250.13, 491.93323> from 11072869122414870784
```

31 seconds against a 30-second deadline, which is the deadline plus one 250ms tick plus the second
the log rounds to. The ground sample is the part worth keeping: it only gets written for a grounded
character, so the respawn put the player on real footing rather than leaving them nominally alive
somewhere unreachable.

1. Die as in B1.
2. Press nothing. Wait out the full 30 seconds.
3. `grep -a "did not tap out" ~/Games/PIN/logs/GameServer.log`.

Pass: the line appears once and you respawn without touching the keyboard.

Fail, nothing after a minute: the sim is not seeing the character. This is the failure that looks
identical to NET-23 unfixed from the player's seat, which is why it has its own entry.

## [x] B4: Monsters still die properly

**Passed 2026-08-15.** Five `NPC <id> died to <id>` lines — died, not went down — and the kills kept
paying resources and rolling items alongside. The tester saw the body dissolve with visual effects
after about five seconds.

**Five seconds is right, and the "about 30 seconds" below is measuring something else.** The server
holds the corpse for 30,000ms (`SetRemainingLifetime` in `Die`) before the entity is removed. What
the client does inside that window is its own dissolve animation, and it hides the body when the
animation finishes. So the pass condition is: the body stays put, then goes, and no respawn prompt
appears. The wall-clock number belongs to the server and is not visible in game.

The regression. `Die` now branches on who is dying, and NPCs take the other path.

1. `npc 528` and kill it.
2. Watch the corpse.
3. `grep -a "died to" ~/Games/PIN/logs/GameServer.log`.

Pass: the log says `NPC <id> died to <id>` — **died**, not went down — the body stays put and
disappears after about 30 seconds, and the kill still pays as in [Kill Rewards](Kill-Rewards.md).

Fail, a monster goes down instead of dying: the player check in `Die` is wrong and every corpse in
the zone is now waiting for a respawn prompt it will never get.

## [x] B5: Dying in a thumper defence doesn't break the encounter

**Passed 2026-08-16.** The player went down at 11:33:16, mid-defence, between waves 2 and 3. The
encounter carried on without them and finished at **completion 1.00** at 11:35:39, with waves 3 and 4
standing up on schedule after the death. Nothing about the encounter depended on the defender being
alive.

**The kill did not come from the wave, and that is its own finding.** The tester had to move the
thumper to ground that already had creatures on it, because *the thumper event cannot kill the player
at all* — with a 19,192 health pool against attackers landing 49 a swing, no table of this size
threatens anything. What killed them was a standing spawn-group Aranha, not a wave member.

That is the player-scale problem in [Combat Scale](../Design/Combat-Scale.md) showing up as a testing
obstacle rather than a balance complaint. Note that the 2026-08-16 wave retune cut escorts from four
to two, which makes it *less* lethal still — the retune was aimed at the machine surviving, and it
moved this in the wrong direction on purpose, because the player's pool is the thing to fix.

The interaction worth checking deliberately, because [M7](Thumper-Defence.md) is where players will
actually die, and death now leaves a character in a state no encounter has ever seen.

A code read says this should be uneventful, and the entry exists to confirm the read rather than to
express a doubt: encounter deaths are addressed by an `EncounterComponent`, only NPCs, the machine,
vehicles and terminals are ever given one, and the kill-reward subscriber skips player victims by
name. So a player's death should route nowhere. What no read can settle is the second half — whether
the defence survives the player being absent for thirty seconds.

1. Call a thumper down at deposit 1's center, `199.8 315.7 401.3`, with `thumper`.
2. Let wave 2 or 3 kill you. Do not use `invuln`.
3. Respawn by either route.
4. Walk back and finish the defence.
5. `grep -a "mined node type" ~/Games/PIN/logs/GameServer.log`.

Pass: the encounter carries on while you are down, the waves keep attacking the machine, and the
cycle still ends in a payout or a destruction line. Your death must not appear as a wave-member
kill.

Fail, the shard stops: read the tail of the log for an exception. A player death routes through the
same event M7 uses to count wave kills ([EncounterComponent](../streams/m7-encounter-combat.md)),
and a downed player is a new case on that path.
