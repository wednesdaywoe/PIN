---
project: pin
kind: test-stream
title: "Charge Camera Lockup (D5, D5a-D5h)"
relates:
  - ../TEST-REGISTER.md
---

# Charge Camera Lockup (D5)

Part of the [in-game test queue](README.md). Setup and admin commands:
[Session Setup](Session-Setup.md). The client-side logging these entries lean on is set up in
[Client Logging](Client-Logging.md).

Eight attempts at one bug, kept in full because the dead ends are the argument for the fix. Read
[D5h](#-d5h-drive-effects-through-localeffectscontroller-as-well--this-was-the-bug) for the answer;
read the rest for why nothing else was it. Every other prediction-shaped bug in PIN is worth
re-testing against this result — that re-test is the [Prediction Sweep](Prediction-Sweep.md).

## [x] D5: Charge camera lockup — FIXED in D5h, `LocalEffectsController` was never being written

Pass: camera control returns after Charge ends, including when the ability is interrupted or the
target dies mid-charge. Currently it does not: vertical aim stays dead, horizontal is fine, and
firing Absorption Bomb restores it.

What is stuck is the client-side `CustomPlayerCamera` from effect **15253** (command 1635110), whose
pitch limits are 0/0 where Absorption Bomb's camera 1576750 uses -90/+90. 15253 has no remove chain
and a duration chain of `RequireCState(living)` only, so the client cannot end it alone; it goes away
only when the server clears the slot. Absorption Bomb "fixes" it by pushing its own camera (effect
15257) that expires on a client-runnable 5s timer.

Tried and failed, in order: implementing `BullrushCommand`'s `ForcedMovementCancelled`; raising the
`MaxTurnRate` base (actively wrong, reverted); strictly-increasing status-effect change times;
echoing `InitTime` into the netfield `Time` (tried twice, reverted twice); confirming the ability
after the chain instead of before it (D5b, kept but not a fix). The server side is verified: the log
shows the slot-2 clear firing, `CombatView` and the owner's `CombatController` both go out on
`ReliableGss`, and the Aero nullable-clear encoding round-trips.

D5d settles where the bug lives. Running Charge's own chain through the `ability 35366` admin
command, so the client never asked for it and never predicted it, leaves the camera working; pressing
`1` for the same chain in the same session locks it. The server's chain handling is therefore clean
end to end, apply and clear both, and everything that remains is the client failing to reconcile the
copy it predicted at keypress against the copy the server replicates. That copy has no client-runnable
way out (15253 has no remove chain and its duration chain is `RequireCState(living)`), so when the
match fails the camera is stranded for the rest of the session.

What the client has to match on is `StatusEffectData`, and only two of its fields carry anything
identifying: `Stack`, which PIN never set and always sent as 0 against the client's 1, and `Time`,
which PIN set to the moment the server got round to applying rather than the activation time the
client sent. D5e tries both.

## [x] D5a: Is the stuck camera reachable from the netfield path?

The next thing to run, and it decides where the fix has to live. Verifies whether a server-driven
apply/clear of 15253 can still tear down a camera that is already stuck, which separates "the client
holds two copies and the clear only killed one" from "the predicted copy is unreachable from the
netfield path at all".

Run against the reverted build, not the one live on 2026-08-10. With the server stopped:

```
cd ~/Games/PIN && ./use-build.sh current    # expect: live build: current
./start-pin.sh
```

Then in game, with Charge (ability 35366) in ability slot 0, default button 1:

1. Press `1` to Charge, and confirm vertical aim is dead afterwards while horizontal still works
2. `listeffects` — expect no 15253 in the printed list, confirming the server thinks it is gone
3. `applyeffect 15253` — expect no visible change, the camera is already locked
4. `removeeffect 15253`
5. If the camera is still locked, `cancelfm 1635109`, then `cancelfm 1635151`

Then, back at the source machine:

```
grep -aE "Character\.(Set|Clear)StatusEffect.*(15252|15253|15215|15216|15456)" ~/Games/PIN/logs/GameServer.log | tail -30
```

Expect the full Charge sequence: 15252 set, 15253 set, 15252 cleared, 15215 set, 15456 set, 15215
cleared, 15216 set, **15253 cleared**, 15456 cleared, 15216 cleared. The 15253 clear is the one that
should have taken the camera with it. Then the manual pair from steps 3 and 4 as a second set/clear.

- Camera unlocks: a server-driven apply/clear cycle can still reach it, so the two copies are
  reconcilable and a data-shape fix on the apply is still viable. Next step is finding the field the
  client binds on — `Stack` is the untried one, since the working `applyeffect` path sends 0.
- Camera stays locked: the predicted camera is a separate object the netfield path cannot touch at
  all, and no change to what the server sends will fix it. The fix then has to make the client roll
  the prediction back, which means finding what the real server sent that PIN doesn't — the missing
  `ForcedMovement` start events for Bullrush and OrientationLock are the leading candidate, since
  PIN only ever sends the cancels and `cancelfm 1635109` on a stuck camera does nothing.

Either way, record which one happened here.

**Ran 2026-08-10: camera stayed locked through every command.** The second branch, so no change to
what the server sends on the status-effect netfield is going to fix this, and the fix has to reach
the client's predicted copy instead. That's what D5b tries.

One caveat on this run, from the log below. The manual `applyeffect` at 19:28:07 carries change time
16544, which is *lower* than the 34365 the Charge sequence ended on 48s earlier, because the 16-bit
change time wraps about every 65s and `NextStatusEffectChangeTime` deliberately lets a large
backward jump through as a wrap rather than nudging it. If the client does treat that field as a
sequence number, it would have dropped the manual apply and steps 3 and 4 were a no-op. Applying
15253 over an already-locked camera has no visible tell either way, so this run can't separate "the
netfield path can't reach the stuck camera" from "the manual pair was ignored". D5c settles that
with an effect whose arrival is visible; run it if D5b doesn't fix things.

```
[19:27:19 DBG] Character.SetStatusEffect Index 0, Time 33777, Id 15252
[19:27:19 DBG] Character.SetStatusEffect Index 2, Time 33778, Id 15253
[19:27:19 DBG] Character.ClearStatusEffect Index 0, Time 34113, Id 15252
[19:27:19 DBG] Character.SetStatusEffect Index 0, Time 34114, Id 15215
[19:27:19 DBG] Character.SetStatusEffect Index 3, Time 34115, Id 15456
[19:27:19 DBG] Character.ClearStatusEffect Index 0, Time 34344, Id 15215
[19:27:19 DBG] Character.SetStatusEffect Index 0, Time 34345, Id 15216
[19:27:19 DBG] Character.ClearStatusEffect Index 2, Time 34346, Id 15253
[19:27:19 DBG] Character.ClearStatusEffect Index 3, Time 34347, Id 15456
[19:27:19 DBG] Character.ClearStatusEffect Index 0, Time 34365, Id 15216
[19:28:07 DBG] Character.SetStatusEffect Index 0, Time 16544, Id 15253
[19:28:31 DBG] Character.ClearStatusEffect Index 0, Time 41064, Id 15253
```

## [!] D5b: Confirm the ability after the chain instead of before it

Verifies the current fix for D5. `ActivateAbility` used to send `AbilityActivated` the moment the
packet arrived, before the aptitude chain ran, so the client got its activation confirmation before
any of the effect netfields that chain applies. In SDB the confirmation is `InstantActivation`
(command 1619009), which sits *last* in Charge's chain 1619015, after the `ImpactApplyEffect` that
applies 15252 and 15253. A client reconciling its predicted ability against server state that hasn't
been sent yet is a plausible reason the predicted 15253 never binds to the netfield copy. The send
now happens after `HandleActivateAbility` returns, in both `ActivateAbility` and `ActivateConsumable`.

With the server stopped:

```
cd ~/Games/PIN && ./use-build.sh current    # expect: live build: current
./start-pin.sh
```

Then in game, with Charge (ability 35366) in ability slot 0, default button 1:

1. Press `1` to Charge, wait for the rush to end, then look up and down

- Pass: vertical aim works. Repeat it five or six times, including charging into a wall and charging
  off a ledge, since the rush ending early is the case most likely to skip the confirmation.
- Fail: still locked. Ordering wasn't it. Go to D5c.

Then check the confirmation now lands after the effects, not before:

```
grep -aE "ActivateAbility 35366|HandleActivateAbility: Ability 35366|Character\.(Set|Clear)StatusEffect.*(15252|15253)" ~/Games/PIN/logs/GameServer.log | tail -20
```

Expect `HandleActivateAbility: Ability 35366` and the 15252/15253 sets *before* the
`ActivateAbility 35366 at ...` line. Before this change that line came first.

**Ran 2026-08-10 over 41 Charges: still locked.** The ordering did flip, confirmed in the log, so the
change did what it claimed and it simply isn't the cause. The reorder is kept because it matches the
order SDB describes, not as a fix. Session was otherwise clean: no `HandlePacket Caught`, no
`Unrecognized MsgID`.

## [x] D5d: Run Charge's chain server-side, with nothing predicted

The decisive one, and it needs the new `ability` admin command. Every test so far has changed what
the server sends and re-run the same predicted keypress. This runs the identical chain with the
client never having asked for it, so it never predicts. That splits the two remaining explanations
apart: either the chain's own apply and clear of 15253 work fine and prediction is the whole story,
or they don't and the fault is in the chain path rather than in prediction, which would mean every
conclusion drawn from the `applyeffect` comparison was measuring the wrong thing.

Needs a build with `ActivateAbilityServerCommand`. With the server stopped:

```
cd ~/Games/PIN && ./use-build.sh current    # expect: live build: current
./start-pin.sh
```

Then in game, **without ever pressing `1`** (a predicted Charge in the same session muddies it, so
relog first if you've charged):

1. `ability 35366` — the character should rush forward exactly as if you'd charged
2. Wait for the rush to end, then look up and down

- Vertical aim works: the chain applies and clears 15253 correctly when nothing is predicted, so the
  bug is entirely in reconciling the client's predicted copy. That rules the server's own chain
  handling clean and puts the fix on the client-facing prediction contract, which is where a capture
  of the real server is the only way forward.
- Vertical aim is dead: prediction is not the cause and never was. The chain's own clear of 15253
  doesn't reach the client the way admin `removeeffect` does, even though both call `ClearEffect`.
  Compare the two paths in the log; the difference is that the chain clears three slots and sets one
  inside about 20ms while `removeeffect` clears one slot in isolation. That points back at batching
  or ordering in `FlushChanges`, this time with a clean way to reproduce it.

Then compare the two paths in the log:

```
grep -aE "Character\.(Set|Clear)StatusEffect.*(15252|15253|15215|15216|15456)" ~/Games/PIN/logs/GameServer.log | tail -20
```

The sequence should be identical to a keypress Charge: 15252 set, 15253 set, 15252 cleared, 15215
set, 15456 set, 15215 cleared, 15216 set, 15253 cleared, 15456 cleared, 15216 cleared. If it differs,
that difference is itself the finding.

**Ran 2026-08-10: the first branch, and it's the first clean signal in six attempts.** `ability 35366`
rushed and ended with vertical aim intact. Pressing `1` right afterwards, same session, same chain,
locked it. So the server applies and clears 15253 correctly, `FlushChanges` batching is fine, and the
whole bug is the client keeping a predicted copy that the server's clear never binds to. Stop looking
at the chain and at delivery; the fix has to change what makes those two copies match.

This also retires the `applyeffect`/`removeeffect` comparison as evidence about data shape. That path
has nothing predicted to reconcile against, so the client just plays what it's told and any value in
`Stack` or `Time` looks like it works. "The working reference sends `Stack = 0`" was never an
argument that 0 is correct.

## [ ] D5c: Does a netfield apply reach the camera while it's stuck?

Only if D5b fails. Redoes D5a with an effect whose arrival is visible, and without the change-time
wrap that muddied it. 15257 is Absorption Bomb's camera (command 1576750), pitch limits -90/+90
against 15253's 0/0, so if it arrives you get your vertical aim back and can see it.

Do this promptly after the Charge, within about 30 seconds, so the change time is still climbing.

1. Press `1` to Charge, wait for the rush to end, confirm vertical aim is dead
2. `applyeffect 15257` — watch whether vertical aim comes back
3. `removeeffect 15257` — watch whether it locks again

- Vertical aim returns at step 2: netfield applies do reach the camera while the ghost is present, so
  the ghost is displaceable and D5a's result was the wrap dropping the apply. Whether it re-locks at
  step 3 says whether the ghost is still underneath (re-locks) or got displaced for good (stays
  free). This is also the mechanism behind Absorption Bomb appearing to fix the bug.
- Nothing happens at step 2: the client isn't acting on status-effect applies at all while stuck,
  which is a much bigger finding than the camera and worth chasing on its own.

## [!] D5e: Give the client something to match its predicted effect on

Run this next. D5d proved the failure is a failed match between the client's predicted effect and the
replicated one, so this fills in the two `StatusEffectData` fields that could carry the match and
that PIN was getting wrong. `Stack` now sends `EffectState.Stacks`, which is 1 on a fresh apply where
PIN sent 0. `Time` now sends `Context.InitTime`, the activation time the client itself sent, where
PIN sent the moment the server got round to applying, typically tens of milliseconds later.

Echoing `InitTime` has been tried and reverted twice before, so this needs saying: the recorded reason
for not trying it again was wrong. The note claimed `InitTime` is the client's clock and
`Shard.CurrentTime` is something else, citing an activation time of 3949877841 against a shard time
of "~23122". They're the same clock. `Shard.CurrentTime` is unix epoch milliseconds truncated to
uint32, and 3949877841 is exactly that for 11:28 UTC on 2026-08-10, so the two agree to within a
second. The ~23122 was a `CurrentShortTime`, which is the low 16 bits, compared against a full uint.
The two failed attempts still stand as evidence, but at least one deploy in that period silently
didn't happen, so they're worth less than a clean run.

Both fields change together on purpose. If it works, bisect after; if it fails, both are dead in one
run instead of two.

With the server stopped:

```
cd ~/Games/PIN && ./use-build.sh current    # expect: live build: current
./start-pin.sh
```

Then in game, with Charge in ability slot 0, default button 1:

1. `ability 35366` first, as the control. Vertical aim must still work afterwards. This path is
   unaffected by the `Time` change (the admin command passes `shard.CurrentTime` as `InitTime`) but
   not by the `Stack` change, so if this regresses, `Stack = 1` is actively wrong and stop there.
2. Relog, then press `1` to Charge, wait for the rush to end, and look up and down
3. If it passes, repeat five or six times including charging into a wall and off a ledge, since an
   early end is where a match is most likely to be missed

- Pass: revert one field at a time to find which one carried it, and write down which.
- Fail: nothing in `StatusEffectData` identifies the instance, and the binding must be something else
  the real server sent. At that point stop guessing at field values. The remaining leads are the
  `ForcedMovement` start events PIN never sends for Bullrush (1635151) and OrientationLock (1635109),
  and a capture of the real server, which is the only thing that settles the contract.

Then confirm the values actually went out:

```
grep -aE "Character\.SetStatusEffect.*15253|HandleActivateAbility: Ability 35366" ~/Games/PIN/logs/GameServer.log | tail -10
```

**Ran 2026-08-10: control passed over several activations, keypress still locked.** So neither field
carries the match and nothing in `StatusEffectData` identifies the instance. That's the end of
guessing at netfield values; the next lever has to be something PIN doesn't send at all.

`Time` is reverted to shard clock. `Stack` is kept, sending the real count where PIN hardcoded 0,
because it's a plain gap rather than a hypothesis and the control run showed it costs nothing. It is
not a fix and the comment in `AddEffect` says so.

## [!] D5f: Send a ForcedMovement start for the aim lock

Run this next. PIN sends `ForcedMovementCancelled` for a forced movement it never told the client had
started, and this adds the start.

The log settles what's in the chain: `Chain 1635110 Command 1635109 - Executing OrientationLockCommand`,
so effect 15253 is an `OrientationLock` and a `CustomPlayerCamera` together, both clamping aim.
`OrientationLockCommand.Execute` only registered for its own removal; the `ForcedMovement` event (113)
went out for spawns and teleports and for nothing else, while `OnRemove` sent a cancel naming command
1635109. `ForcedMovementCancelled.CommandId` is an `apt::BaseCommandDef` id and `ForcedMovementData`
carries a uint in the same position, so a cancel is meant to name a movement the client already knows
about from the server. There has never been one.

That fits every result so far. The client predicts the lock at keypress (`AllowPrediction`) and the
server is the only thing that can end it. If a bare cancel needs a server-issued movement to match
against, PIN's cancel is a no-op, which is exactly what `cancelfm 1635109` does on a stuck camera. It
also fits D5d: unpredicted, the client never started a lock of its own, so nothing was left running.

Only `OrientationLock` sends a start. `Bullrush` and `ApplyFreeze` have the same gap and are
deliberately left alone, because Bullrush is movement rather than aim and a server-authored rush on
top of a predicted one could double up and muddy this.

With the server stopped:

```
cd ~/Games/PIN && ./use-build.sh current    # expect: live build: current
./start-pin.sh
```

Then in game, with Charge in ability slot 0, default button 1:

1. Press `1`, wait for the rush to end, look up and down
2. If aim is dead, keep playing and watch the clock. The start carries a 60 second end time when the
   SDB duration is 0, which Charge's is, so aim coming back on its own about a minute after the
   Charge is a distinct third outcome and worth catching

- Pass: aim works. Repeat five or six times including into a wall and off a ledge. Then give Bullrush
  and ApplyFreeze the same treatment and check the rush still looks right.
- Frees itself after ~60s: the client took the start and ignored the cancel. Big result. The start is
  right and the cancel is what's malformed, so compare `ForcedMovementCancelled` against a capture.
- Still locked, no recovery: the client isn't binding a server forced movement to a predicted one
  either, which exhausts what can be reasoned out from this end. Get a capture of the real server.

Then confirm the start went out ahead of the cancel:

```
grep -aE "OrientationLockCommand Sending ForcedMovement" ~/Games/PIN/logs/GameServer.log | tail -10
```

Expect alternating `Sending ForcedMovement 1635109` and `Sending ForcedMovementCancelled 1635109`.
Before this change only the cancel appeared.

**Ran 2026-08-10: still locked, and no recovery after a couple of minutes.** Reverted.

The 60 second tell was worthless, because dumping 15253's apply chain afterwards showed the
`OrientationLock` isn't what clamps aim. Chain 1635110 is four commands: `CustomPlayerCamera` 1635110
(env=client, pitch 0/0), `OrientationLock` 1635109 (env=both, `max_aim_angle=0`, `duration=0`),
`AbilityAnimation` 1635108 (env=client), and `CombatFlags` 1635107. Ending the lock changes nothing
visible while the camera is still clamped, so the run says nothing either way about whether the
client accepted the start. The test could not have distinguished its own outcomes.

Stepping on a Glider pad restored aim, same as Absorption Bomb. Both push their own
`CustomPlayerCamera`, which pins the stuck object as the camera rather than the lock. 15257 is a
clean reference for that: its apply chain is nothing but a `CustomPlayerCamera` at -90/+90 with a
5000ms `TimeDuration` and `allow_prediction=0`, so it arrives by netfield only and the client expires
it on its own.

Reverted rather than kept. Sending a cancel for a movement that never started is a real defect, but
every `OrientationLock` that matters has `duration=0`, so a start needs an end time nobody can derive.
Shipping an invented one risks a genuine aim clamp lasting that long on some ability where no camera
masks it, and that's a worse defect than the one it replaces. The gap and the message shape are
written into `OrientationLockCommand.OnRemove` so it's cheap to redo against a capture.

## D5 status: stop testing fixes

Seven attempts, all failed. What's established is worth keeping:

- The server's chain handling is correct end to end, apply and clear both (D5d)
- The bug needs client prediction. The identical chain run unpredicted is clean (D5d)
- Nothing in `StatusEffectData` binds a predicted instance to a replicated one (D5e)
- The stuck object is the `CustomPlayerCamera`, and any later camera push displaces it (D5f)

**Superseded by [D5g](Client-Logging.md).** The client's Aptitude log channel answers this directly:
the client predicts the effect, rejects the server's replicated copy as a duplicate, and never
removes the predicted one because 15253 is the only effect in the chain that can't expire on its own.
Read [Client Logging](Client-Logging.md) first. The two suggestions kept below are what the position
looked like before that, and the second of them has since been answered (the Glider displaces the
ghost, it doesn't remove it).

What's left is how the client reconciles a predicted effect against the replicated one, and that
isn't derivable from this side of the wire. Every remaining idea is a guess at a contract we can't
read, and the last three runs each cost a build, a deploy and a play session to learn nothing. Two
things would actually move it, both outside the fix-test loop:

1. A capture of the real server running a Charge. It settles the whole contract at once, not just
   this bug.
2. One free observation next time it happens anyway: after a Glider or Absorption Bomb frees the
   aim, does it lock again about five seconds later when that camera expires? If it does, the ghost
   is still underneath on a stack and a fix has to remove it. If it doesn't, a camera push displaces
   it for good, and applying 15257 on Charge's end is a workaround that would make the ability
   usable now. That's a hack and it should be labelled one, but Charge is unusable as it stands.

## [x] D5h: Drive effects through `LocalEffectsController` as well — this was the bug

The one channel in the protocol that's owner-private and about effects, and PIN has never written a
byte of it. Everything except the writes was already there: `CharacterEntity` builds a
`LocalEffectsController`, `EntityManager.ScopeIn` keyframes it to the owning player only, and
`FlushChanges` flushes it alongside the other controllers. All 64 slots have just always been null.

The change is in
[CharacterEntity.SetStatusEffect](../../UdpHosts/GameServer/Entities/Character/CharacterEntity.cs),
next to the existing `StatusEffects_N` writes: fill `LocalStatusEffects_{index}` with
`{ Entity = data.Initiator, Effect = data.Id, Time = data.Time }` on apply and null it on clear,
using the same slot index the 32-slot array uses. Nothing else moves, and the two arrays can't
disagree.

`Entity` is ambiguous, it could be the initiator or the target, and the field name doesn't say.
Charge is entirely self-applied so both are the player and this test can't tell them apart. That
only matters if D5h works and someone then applies it to an effect cast on someone else.

Deployed as `GameServer.dll` md5 `5662843a1725c212f4a8a6ea2eeab238`.

Steps:

1. Start the server: `cd ~/Games/PIN && ./start-pin.sh`
2. Confirm the client is still set up to log its ability engine. `settings.con` should still carry
   the binds from [D5g](Client-Logging.md); if it does, nothing to do.
3. Launch, get in world, press `f7` once to raise the Aptitude channel to debug.
4. Charge once, on flat ground, nothing else bound in the way. Note whether vertical mouselook
   still locks.
5. Wait about ten seconds, then quit the client cleanly so the log is flushed.
6. Read `~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata/227700/pfx/drive_c/users/steamuser/AppData/Local/Red 5 Studios/Firefall/console.log`

What decides it, in the log, is what happens to 15253. In D5g it appeared exactly twice, an apply
and a `too many stacks` rejection, and never again:

- A `Successfully removed effect 15253` plus `Canceled` at the end of the chain means the client
  took the local array as authority over what it predicted. That's the fix.
- The same two lines and nothing else means the client either ignores controller 6 or wants
  something else in it. Hypothesis dead in one run, and no more server-side guessing after that
  without first finding what reads it.
- Anything new and unexpected (a third apply, an error, effects vanishing that shouldn't) is worth
  more than either, because it means the client is reading the controller and we've fed it wrong.

Aim working again while the log shows nothing new about 15253 would mean the camera was displaced
rather than released, same as the Glider. Check the log before believing the camera.

## D5h result: fixed, and the log says why

Camera works after Charge. Three Charges in one run, all clean, and this time the client log backs it
up. Every one of them ends the way no Charge ever had before:

```
00:44  Successfully applied effect 15253
00:44  Unable to apply status effect 15253: too many stacks (x1)   (twice now, see below)
00:45  Successfully removed effect 15253
00:45  Canceled effect 15253
```

The `Successfully removed` plus `Canceled` pair is the whole result. In D5g those two lines never
appeared for 15253 in a 537-line log, and now they appear on all three Charges, in the same burst
that clears 15215, 15456 and 15216.

The single-variable comparison holds. The server cleared `StatusEffects_2` for 15253 in the D5g run
too, right on schedule, and the client ignored it. The only thing that changed is that
`LocalStatusEffects_2` is now nulled at the same moment, and now the effect ends. So the client binds
what it predicted to the owner-private array and treats the 32-slot one as somebody else's business.
That array is the observers' view. It was never going to reach a prediction.

Rejections went from 4 in the D5g run to 33 here, because each effect now arrives twice, once through
`CombatController`/`CombatView` and once through `LocalEffectsController`, and the client bounces both
against its own prediction. It doesn't care, and the removal lands regardless. Still, the real server
probably didn't send both, and sending the owner only the local array would be worth trying now that
there's a way to see the result. Not urgent: nothing about the current shape is broken.

Worth being clear about what this does not establish. Only self-applied effects have been tested, so
whether `Entity` is the initiator or the target is still open. And every other prediction-shaped bug
in the game just became worth re-testing, because until now the owning client was never told about
its own effects through the only channel it listens to. That re-test is the
[Prediction Sweep](Prediction-Sweep.md).
