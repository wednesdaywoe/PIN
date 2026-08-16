---
project: pin
kind: test-stream
title: "Prediction Reconciliation Sweep (P0-P6)"
relates:
  - ../TEST-REGISTER.md
---

# Prediction Reconciliation Sweep

Part of the [in-game test queue](README.md). Setup and admin commands:
[Session Setup](Session-Setup.md). Client-side logging: [Client Logging](Client-Logging.md).

[D5h](Charge-Camera.md)'s closing note said every other prediction-shaped bug just became worth
re-testing, because until 2026-08-10 the owning client was never told about its own effects on the
only channel it listens to. This file is that re-test, made systematic instead of anecdotal.

[Tools/EffectSweep](../../Tools/EffectSweep) enumerates the class straight from the SDB. It walks
every ability chain the way the client's predictor would — skipping `env=server` commands the
client never runs, following `allow_prediction=1` applies into the applied effect's own four
chains — and keeps every effect whose predicted copy cannot end on its own: no time-based duration
command, removal only external. That is exactly 15253's shape, and 15253 falling out of the walk
under ability 35366 is the tool's built-in validation (exit code 3 if it ever doesn't).

To regenerate the full report:

```
OPENSSL_ENABLE_SHA1_SIGNATURES=1 dotnet build Tools/EffectSweep/EffectSweep.csproj
cd Tools/EffectSweep && dotnet bin/Debug/net10.0/EffectSweep.dll
```

`config.json` (gitignored; the example carries a Windows path) points at the retail
`clientdb.sd2`. Output is `candidates.md`/`candidates.json` in the working directory.

Result on the 2026-08-10 db: **47 ghost-shaped effects, 27 reachable from an equippable
ability.** The other 20 sit on NPC/system chains no player can activate, and prediction only
happens for the local player's own keypress, so they cannot ghost and are listed in the generated
report only for completeness.

Three things the enumeration surfaced beyond the list itself:

- **15253 has a second owner.** Ability 41232 (unnamed, module 141814) applies the same camera
  lock through its own apply command 1615459. Charge was never the only way to get the D5 bug.
- **`apttf_serverconfirmed_scf[client]` exists.** A client-side duration command whose whole job
  is to hold an effect until server confirmation — the reconciliation channel D5h started feeding,
  named as a first-class concept in the client's own data. Nine of the 27 equippable candidates
  carry it in their duration chain; those effects are *designed* to wait for the write PIN wasn't
  making.
- **Two of the D5 band-aids are now re-examinable.** The strictly-increasing
  `NextStatusEffectChangeTime` invariant and the D5e `Stack = Stacks` fill in
  [BaseAptitudeEntity.cs](../../UdpHosts/GameServer/Entities/BaseAptitudeEntity.cs) both predate
  the real fix. Both are harmless and neither blocks anything, but if a future cleanup wants them
  gone, the procedure below is how to prove they're not load-bearing.

## The shared procedure

Every entry below uses the same loop; the entries only say what to activate and what to look for.

1. Client logging is already wired from [D5g](Client-Logging.md): in game, press `f7`
   (`AlwaysFlushConsole 1; SetLogLevel Aptitude debug; ...` — the binds persist in `settings.con`)
2. `createitem <moduleId>` from the entry, then slot the module in the garage. Equipping is a
   client-side loadout action; if the garage refuses the module, check the gate table below —
   most refusals are certification gates, not frame-family mismatches
3. Activate **by keypress**. `ability <abilityId>` runs the chain server-side with nothing
   predicted — it proves the server chain, not reconciliation, so it is exactly the wrong tool here
4. End the effect the way its design ends it (toggle off, leave the mode, let the impact land),
   then read the client's view:

   ```
   grep -a "effect <effectId>" "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata/227700/pfx/drive_c/users/steamuser/AppData/Local/Red 5 Studios/Firefall/console.log"
   ```

   Pass: `Successfully applied effect <id>` at activation and `Successfully removed effect <id>` /
   `Canceled effect <id>` when it ends — the same four-line signature D5h produced for 15253.
   Fail: the apply with no removal, which is a live ghost and a new D5-class bug on that effect
5. Server side, confirm the slot cleared on schedule:

   ```
   grep -a "StatusEffect" ~/Games/PIN/logs/GameServer.log | tail -30
   ```

## Candidates

From `candidates.md` (duration classes: SERVERCONFIRMED waits on server say-so, FRAME
until battleframe switch, RESPAWN until death, CSTATE a character-state check that may never flip,
NONE no duration chain at all; names are the client's German localization):

| Effect | Class | Ability | Module (`createitem`) |
|--------|-------|---------|----------------------|
| 15253 | CSTATE | 41232 (unnamed second Charge) | 141814 |
| 10810 | SERVERCONFIRMED+FRAME+CSTATE | 39434 „Geschützturmmodus" (Turret Mode) | 86074 |
| 2212 | CSTATE | 34554 „Schwebemodus" (Hover Mode) | 75111 |
| 3395, 14727 | SERVERCONFIRMED+CSTATE, FRAME+CSTATE | 35229 „Energiewand" (Energy Wall) | 77462 |
| 15422, 15438 | CSTATE | 42011 (unnamed; 15422 applies to **target**) | 143906 |
| 12845 | SERVERCONFIRMED+CSTATE | 40482 „PvP-Haltefeld" | 139301 |
| 12944 | SERVERCONFIRMED+CSTATE | 40529 „PvP-Heilkugel" | 139556 |
| 2322 | FRAME+CSTATE | 34606 „Frontsanitäter - I" | 75579 |
| 1875 | FRAME+CSTATE | 34408 „Chosen Grunt shotgun Mode" | 52501 |
| 274, 278, 279, 2211, 7168 | CSTATE/OTHER | 187 (interaction/revive-channel shaped) | 34461 |
| 1064 | SERVERCONFIRMED+FRAME+CSTATE | 31367 | 31367 |
| 8924 | SERVERCONFIRMED+FRAME+CSTATE | 38345 | 118792 |
| 2681 | SERVERCONFIRMED+CSTATE | 34903 | 77072 |
| 2783 | SERVERCONFIRMED+CSTATE | 30273 | 30273 |
| 2793 | SERVERCONFIRMED+CSTATE | 34918 | 77086 |
| 2562 | CSTATE | 34773 | 76118 |
| 8514 | CSTATE | 38129 | 118285 |
| 1651 | NONE | 34146 | 34146 |
| 15435 | CSTATE | 42009 | 143899 |
| 15443 | RESPAWN+CSTATE | 42013 | 143910 |
| 15460 | CSTATE | 42023 | 143924 |

## Slotting gates (found 2026-08-11, first Turret Mode attempt)

Trying P2 in-game refused to slot 86074 on the Dreadnaught, and the server log shows the client
never even sent `SlotGearRequest` — the garage blocked it locally. The gate is certifications,
not frame recognition: every Turret Mode variant carries `class_cert_id` 741 („Zerstörer-Baureihe",
the Dreadnaught line cert) in `dbitems::RootItem` plus the same cert again in
`dbitems::ItemCertificateRequirements`, and PIN never tells the client it owns any certificate —
the `UnlocksUpdate` message (Character event 130, group key `"certificate"`) exists in AeroMessages
but has no sender anywhere in the server. Charge never hit this because its base module 77585 is
the rare one with no cert requirement at all.

The all-frames-level-1 display is a second, independent server bug:
[EntityManager.cs:1160](../../UdpHosts/GameServer/Systems/EntityManager/EntityManager.cs#L1160)
sends `ProgressionXPRefresh` without an `entityId` argument, which defaults to 0, so the one message
carrying a real frame level is addressed to entity 0 and dropped — and it only ever lists the
currently equipped chassis anyway. It does not block the level-1 modules below, but it will block
any `required_level` gate that reads frame level rather than the hardcoded character level 45.

What that means per candidate (cert audit of every module variant per ability, 2026-08-10 db):

**Slottable today** — these abilities have a cert-free `required_level=1` module, so the sweep can
run on them with no server changes (use these ids, they replace the table's picks where they differ):

| Ability | Module (`createitem`) | Effect(s) |
|---------|----------------------|-----------|
| 35366 „Ansturm" (Charge, the D5h baseline) | 77585 | 15253 |
| 34606 „Frontsanitäter - I" | 75579 | 2322 |
| 34408 Chosen Grunt shotgun mode | 52501 | 1875 |
| 34773 | 76118 | 2562 |
| 34146 | 34146 | 1651 |
| 187 | 34461 | 274, 278, 279, 2211, 7168 |

**Cert-gated** — every variant requires a class/line certificate the client believes it lacks:
P1's 141814 needs 743 („Nashorn"/Rhino — so P1 as written can never slot on a basic Dreadnaught
account), P2's 86074 needs 741 (Dreadnaught line), P3's 75111 needs 732/749 (Assault line),
P4's 77462 needs 737 (Bastion), plus 38129→744, 38345→741, 34903→690, 34918→656, 30273→7,
31367→741, and both PvP abilities (40482→734, 40529→739, also `required_level` 50).

**Level-gated only** — 42009/42011/42013/42023 (modules 143899/143906/143910/143924) have no cert
but `required_level=40`. Character level is hardcoded 45, frame level reads 1; whether these slot
is itself diagnostic for which level the garage checks (see P5).

**Fix deployed 2026-08-11, verification is P0 below.** The server now loads `dbitems::Certificate`
(SDBInterface) and, at character scope-in
([EntityManager.cs](../../UdpHosts/GameServer/Systems/EntityManager/EntityManager.cs)), sends
`UnlocksUpdate` with every certificate id under the `"certificate"` group (chunked ≤200 per message
— `AddEntries` is a byte-counted array), plus `ProgressionXPRefresh` and `EliteLevels_InitAllFrames`
now listing **every** owned chassis and addressed to the character entity instead of entity 0. In
the same pass the prestocked fallback inventory (~240 items + ~140 resource stacks) was removed:
the inventory now holds only each frame's chassis and default-slot items generated from
`CharCreateLoadoutSlots`, and test items are created individually with `createitem`.

## [x] P0: Verify the gate fixes (certs delivered, frame levels real, clean inventory)

**Passed 2026-08-14.** 86074 — the exact module the garage refused on 2026-08-10 — slotted on the
Dreadnaught; every battleframe in the garage reads level 45; and the prestock removal is live
(`SendFullInventory: 246 item(s) [Gear 246], 0 resource(s), 20 loadout(s)`). One wrinkle found on
the way, which every entry below inherits: a created module only reaches the garage after
`dbg_inventory resend`, because the client declines PIN's partial item update
([NET-18](../ISSUE-REGISTER.md), re-scoped the same day). So the working step 2 for P1–P6 is:
`createitem <moduleId>`, then `dbg_inventory resend`, then slot it in the garage.

The server-side fix for everything above, awaiting its first login. Everything in P1–P6 depends on
this entry passing.

> **Run [I1](Inventory.md) first.** Step 4 below and every `createitem` in P1–P6 depend on a created
> item reaching the client, and on 2026-08-11 one didn't. If I1 fails, a module that won't slot
> here says nothing about certificates.

1. Restart the servers (`~/Games/PIN/start-pin.sh` — the new `GameServer.dll` is already deployed),
   log in
2. Inventory check: the bag/gear clutter is gone; only per-frame default gear remains
3. Garage check: battleframes should read level 45, not level 1
4. `createitem 86074`, then slot it on the Dreadnaught — the exact action that failed on 2026-08-10
5. If it refuses again, the client reads certs from somewhere other than `UnlocksUpdate`; next
   suspects are the web endpoints (`garage_slots` serves only a hardcoded Firecat,
   `character_sheet.json` a hardcoded Rhino). Capture both logs:

   ```
   grep -a "HandlePacket Caught" ~/Games/PIN/logs/GameServer.log
   grep -a -i "certificate\|unlock" "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata/227700/pfx/drive_c/users/steamuser/AppData/Local/Red 5 Studios/Firefall/console.log" | tail -30
   ```

Pass: 86074 slots on the Dreadnaught and frame levels display 45. From there P2 continues
directly (toggle Turret Mode on/off, watch effect 10810).

## [ ] P1: The other Charge (ability 41232, effect 15253)

The cheapest possible test: the exact effect D5h fixed, applied by an ability nobody has pressed.
If the fix generalizes at all, it generalizes here first.

> **Blocked again 2026-08-14, one gate behind the last one:** P0 cleared the cert gate (141814
> created, resent, and slotted fine) — but ability 41232 turns out to be an **Ultimate**. Slotting
> it empties the ultimate charge meter, the in-combat refill never happens in PIN, and an Ultimate
> with an empty meter can never be pressed. That is [DATA-19](../ISSUE-REGISTER.md), new that day.
> Effect 15253's regression coverage meanwhile is plain Charge (35366 / 77585), which is cert-free
> and not an Ultimate.
>
> *(Superseded 2026-08-14 by the above: the original block was cert 743, cleared by P0.)*

1. `createitem 141814`, slot it in the garage
2. Activate by keypress, let the rush land
3. Vertical mouselook still works, and the client log shows the removal burst for 15253

Pass: same signature as D5h's run — apply, `too many stacks` rejections, removal, cancel.
Fail: locked pitch again, meaning the fix is somehow chain-specific rather than effect-specific,
which would be genuinely surprising and worth a full D5g-style log read.

## [!] P2: Turret Mode (ability 39434, effect 10810) — a predicted toggle

**Failed 2026-08-14, first press after P0, in a shape the entry didn't predict.** The module
slotted, the mode engages on keypress — and shuts itself off within seconds, unprompted. The
client log shows the textbook end signature with nobody pressing toggle-off: apply, two
`too many stacks` rejections (the server's confirmation landing on the predicted copy, same as
D5h's pass), then `Successfully removed` and `Canceled`, twice in two attempts. The server's side
is the telling half: **10810 was set and never cleared** — still standing at grep time — while its
companions ran a rapid cascade: 10812, 10813, 10814, 10815 each set and cleared within ~40ms, 1184
set alongside and cleared 1.5s later. So the client abandoned the mode while the server still
holds it: the mirror image of the stuck camera, and a desync in the direction D5h's fix cannot
cause.

Suspects, in rough order: the `requirecstate` in 10810's duration chain failing client-side
because the state it checks is never set by the server; one of the instantly-cleared companion
effects (10812–10815) being what actually holds the stance, killed by the server's own chain; and
[NET-16](../ISSUE-REGISTER.md)'s double delivery flipping the toggle straight back off. Next step
is offline, no game time needed: name 1184 and 10812–10815 in the SDB and read 10810's duration
chain to see which state it requires.

**Same sitting, later: not a Turret quirk.** Frontline Medic I (P6's 34606, effect 2322 —
FRAME+CSTATE, no `serverconfirmed`) self-cancelled the same way, which reorders the suspects: the
CSTATE check is the common thread and the missing-confirmation theory is out as the common cause.
[NET-26](../ISSUE-REGISTER.md) carries the analysis; P3 is the experiment that splits
server-owned states from client-tracked ones.

Different reconciliation path from Charge: `ImpactToggleEffect` rather than `ImpactApplyEffect`,
and the duration chain carries `serverconfirmed`, so this effect is explicitly built to wait for
the server's word. Pre-fix, that word never came.

> **Blocked 2026-08-11 — this is the entry that exposed the gate.** Slotting 86074 on the
> Dreadnaught was refused client-side (no `SlotGearRequest` ever reached the server). The frame
> was right all along: 86074 is Dreadnaught-line, gated on cert 741, which PIN never granted.
> Slotting it is now literally P0's step 4 — this entry starts where P0 passes.

1. `createitem 86074`, slot it (heavy-frame module; if the current frame refuses it, switch in
   the garage)
2. Toggle the mode on by keypress, stay in it a few seconds, toggle it off
3. Client log for effect 10810, server log for the matching set/clear

Pass: apply on toggle-on, removal on toggle-off, and the character actually leaves the mode
(stance, movement speed, camera all back to normal).
Fail: the mode's client-side state lingers after toggle-off — the toggle equivalent of the stuck
camera.

## [ ] P3: Hover Mode (ability 34554, effect 2212) — duration gated on energy and airborne

**Run 2026-08-14 as the control case for [NET-26](../ISSUE-REGISTER.md), and it behaved like
one.** Tester-observed: activated mid-air, the ability and its FX kept running the whole time
airborne — no instant self-cancel — while booster energy drained as normal. That is the split the
experiment wanted: the two effects that self-cancel (10810, 2322) require server-owned states,
and the one whose state the client tracks itself (airborne) holds. **What failed instead is the
lift**: the character falls at normal speed with the effect running, so hover's actual physics
never engage — a gameplay gap, not a prediction one, most likely one of
[DATA-5](../ISSUE-REGISTER.md)'s empty aptitude-command stubs. The log half of the pass condition
could not be recovered after the fact: the presses ran in a session where the `f7` aptitude
logging was off, so no client log names 2212 (checked across the client's archived
`*_last_run.log` files). The server's side was read instead — chain 426507 started five times in
five seconds and **set no status effect at all**, confirming the whole effect ran client-side.
Marker stays open for one detail only: a re-press with `f7` on, showing 2212's removal firing on
landing rather than lingering — a lingering copy would be a D5-class ghost, and nothing yet rules
it out.

The duration chain is `requirecstate → airborneduration → requireenergy`: the predicted copy ends
when the client itself decides you've landed or run dry. That makes it the control case — an
effect that should have healed itself even pre-fix. If THIS one misbehaves, the model is wrong
somewhere.

> **Blocked 2026-08-11:** 75111 is cert-gated (732/749, Assault line) — unblocked once P0 passes.

1. `createitem 75111`, slot it
2. Activate mid-air by keypress, hover until landing or energy exhaustion
3. Client log for effect 2212

Pass: removal fires on landing/exhaustion with or without the server's help, and the server's own
clear arrives in the same window rather than being rejected late.

## [ ] P4: Energy Wall (ability 35229, effects 3395 and 14727) — two ghosts in one chain

One keypress predicts two ghost-shaped effects, one of them FRAME-classed, which adds the frame
switch as a second removal path to verify.

> **Blocked 2026-08-11:** 77462 is cert-gated (737, Bastion) — unblocked once P0 passes.

1. `createitem 77462`, slot it
2. Place the wall by keypress, let it expire or destroy it, client log for 3395 and 14727
3. Re-activate, and while the wall is up, switch battleframe in the garage
4. Client log again: 14727 (FRAME) must be removed by the switch, not survive it

Pass: both effects apply and remove on both paths. The frame-switch path is the interesting one —
pre-fix, a predicted FRAME-classed effect surviving a frame switch would have been invisible to
every test we had.

## [ ] P5: Predicted effect on a target (ability 42011, effect 15422) — the `Entity` question

The one entry that isn't a regression test. Apply command 1625299 has `self=0`: the client
predicts an effect landing on its **target**. `LocalEffectsData.Entity` currently gets
`data.Initiator` on the theory the array describes who caused the effect, and D5h explicitly
couldn't distinguish initiator from carrier because Charge only self-applies.
`CharacterEntity.SetStatusEffect` also only mirrors the character's own slots into
`LocalStatusEffects_N` — an effect sitting on an NPC touches no local array anywhere, so if the
client binds target-applied predictions there, this class still has the D5 gap today.

> **2026-08-11:** 143906 has no cert gate but `required_level=40`. Character level is hardcoded
> 45 while every frame reads level 1 (the `ProgressionXPRefresh` bug), so step 1 doubles as a
> diagnostic: if it slots, the garage checks character level; if it refuses, it checks frame
> level and the `ProgressionXPRefresh` fix becomes a prerequisite for this entry and P6's
> 42009/42013/42023 rows.

1. `createitem 143906`, slot it
2. `npc 1196` for a target dummy, then activate at it by keypress
3. Client log for 15422: does the predicted copy on the target reconcile and end, or linger?
4. `listeffects` with the NPC targeted, and the server log, to see what the server thinks the
   NPC carries

Pass: 15422 applies and ends cleanly on the client. Fail is informative, not bad news: a lingering
target-side prediction means `Entity` means the carrying entity, and the fix's next step is
mirroring effects on *any* entity into the owning initiator's `LocalEffectsController` with
`Entity = <carrier>` — a bigger change than D5h, now with a test to drive it.

## [!] P6: The rest of the table

**First candidate run 2026-08-14 and it failed like P2.** Frontline Medic I (34606 / module
75579, effect 2322): engages on keypress, self-cancels seconds later, tester-observed as
identical to Turret Mode's behaviour. Diagnostic weight is in the duration class — FRAME+CSTATE,
no `serverconfirmed` — which makes the CSTATE check the common denominator across both failures.
Rolled into [NET-26](../ISSUE-REGISTER.md) rather than filed separately. A post-sitting server
read added the other half: chain 228949 started five times (20:59:05–19) and **set no status
effect at all** — unlike Turret, whose chain ran in full — so this failure had no server half to
lean on even in principle; the client-side signature itself was never captured because the
session ran with `f7` off.

Batch entry for the remaining candidates. Work down the table with the shared procedure; most are
one keypress each once the module is slotted. Expected outcome for nearly all of them is the D5h
signature and a checkmark — the value is the sweep itself: after this, "the client mispredicted
it" stops being a plausible write-off anywhere in PIN, because every effect that *could* ghost has
been seen reconciling once.

Keep score here (gate status from the 2026-08-11 audit — runnable rows first):

- [ ] 2322 „Frontsanitäter - I" (34606 / 75579) — runnable now
- [ ] 1875 „Chosen Grunt shotgun Mode" (34408 / 52501) — runnable now
- [ ] 274 + 278 + 279 + 2211 + 7168 (187 / 34461, five effects, one ability) — runnable now
- [ ] 2562 (34773 / 76118) — runnable now
- [ ] 1651 (34146 / 34146) — runnable now
- [ ] 12845 „PvP-Haltefeld" (40482 / 139301) — cert 734 + level 50
- [ ] 12944 „PvP-Heilkugel" (40529 / 139556) — cert 739 + level 50
- [ ] 1064 (31367 / 31367) — cert 741
- [ ] 8924 (38345 / 118792) — cert 741
- [ ] 2681 (34903 / 77072) — cert 690
- [ ] 2783 (30273 / 30273) — cert 7 + level 8
- [ ] 2793 (34918 / 77086) — cert 656 + level 8
- [ ] 8514 (38129 / 118285) — cert 744 + level 12
- [ ] 15435 (42009 / 143899) — level 40 (see P5)
- [ ] 15443 (42013 / 143910) — level 40 (see P5)
- [ ] 15460 (42023 / 143924) — level 40 (see P5)
