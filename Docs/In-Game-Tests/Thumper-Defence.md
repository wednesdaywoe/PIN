---
project: pin
kind: test-stream
title: "Thumper Defence (F1-F6)"
relates:
  - ../TEST-REGISTER.md
---

# Thumper Defence

Part of the [in-game test queue](README.md). Setup, admin commands and the type-id tables:
[Session Setup](Session-Setup.md).

The check on [M7](../streams/m7-encounter-combat.md). The milestone is that a thumper is a fight:
waves spawn while it drills, the machine itself can be destroyed, and the payout depends on how the
defence went. Everything [Thump Placement](Thump-Placement.md) established still holds underneath —
this stream only adds the part where something shoots back.

**Closed 2026-08-15, 6 of 6, in one sitting on the morning the code landed — and it closed
[M7](../streams/m7-encounter-combat.md), the last milestone in the plan.** The sitting ran three
cycles where the doc planned two, because the first defended attempt was *lost*: the deliberate
20-second observation window F3 asked for cost two-thirds of the pool, and wave 4's three sappers
crossed the ring in ~2 seconds (they run 11 m/s) and erased the remaining 1300 health in four —
destroyed at 91%, twenty-six seconds from the finish. The re-run was defended tightly, went
14 kills for 14 spawns, and paid 25 crystite at `completion 1.00, defence 0.63 (1060/4000 health)`.
The tester's wallet read 43, and the log's books balance it to the unit: 25 from the machine plus
18 across both cycles' kill drops. **F5 never had to be run deliberately** — the losing path passed
twice on its own, once on the zone's NPC-owned debug thumper before the tester was even in position
(its own waves killed it, unattended, no crash), and once on the lost first cycle.

The reading that outlived the sitting: **even a near-perfect solo defence pays ~63%**, because
claws land in the seconds between a sapper spawning and dying, four waves over. Full pay is
effectively unreachable alone under current numbers. Options recorded, decision deferred
(tester, 2026-08-15): slower claws, a bigger pool, or a softer payout curve. Two more tuning
observations from the tester's seat: Aranha melee reach reads longer than it should, and the
grenade launcher's AoE is too small for how spread the Aranha stay — they don't clump. None of it
blocks the milestone; it's the first balance data the encounter has ever produced.

What was built, in one paragraph. During THUMPING, four waves of Melded Aranha (528) stand up on a
20m ring around the machine, at 15%, 40%, 65% and 90% progress — 45s, 120s, 195s and 270s into the
5-minute drill. Each wave splits into **sappers**, which march on the machine and ignore return
fire, and **escorts**, which fight whoever they perceive and only chew the machine when nobody is
around: 2+0, then 2+1, 3+1, 3+2, thirteen in total. The thumper now has a physics body and retail's
own health pool (4000–5000 per the calldown def). Destroyed means failure: DESTROYED state, death
ability, a completion event with the destroyed flag, no payout, wreck removed after 6 seconds. A
survived defence pays `completion x defence`, where defence runs linearly from 0.5 at zero health
to 1.0 untouched. Wave deaths route back to the encounter through the M2 death event; kill rewards
pay per kill exactly as in [Kill Rewards](Kill-Rewards.md).

Two things a tester will notice that are physics, not bugs. The machine now blocks shots — your own
rounds stop on it (friendly, so they do no damage), and a monster on the far side is in cover. And
the waves spawn at the thumper's own height, so a thumper on a slope can stand its wave in the
hillside; that is the standing bet every offset spawn makes and the reason deposits live on walked
ground.

No new creature types: every wave member is 528, one of the two models ever seen rendering, so this
stream triggers no [CLIENT-3](../gaps/client.md#client-3) facing check. That is deliberate. The
first stream that puts a new monster id in a wave inherits the check.

The log lines this stream reads:

```
grep -a "wave" ~/Games/PIN/logs/GameServer.log
grep -a "Thumper .* took .* damage" ~/Games/PIN/logs/GameServer.log
grep -a "wave member" ~/Games/PIN/logs/GameServer.log
grep -a "was destroyed by" ~/Games/PIN/logs/GameServer.log
grep -a "mined node type" ~/Games/PIN/logs/GameServer.log
grep -a "released" ~/Games/PIN/logs/GameServer.log
grep -a "fallback shape" ~/Games/PIN/logs/GameServer.log
```

The last one is F3's diagnostic: the thumper's collision shape comes from the beacon's
`PosefileId` asset (189562 or 250491), and if the deployment can't load it the physics engine
substitutes a 0.9m sphere at the base. NPCs aim at 0.7m on structures for exactly that case, so the
fight works either way, but the line says which body the machine actually has.

A defended run is still the full 7.5-minute cycle. Plan the sitting around two full cycles: one
defended (F1–F4, F6 all read from it), one abandoned (F5).

## [x] F1: A defended thumper pays, scaled by its health

The milestone's exit condition.

**Passed 2026-08-15, on the second cycle.** The first was destroyed at 91% (see F5). The re-run:
all four waves killed on sight, 14 for 14, collect at COMPLETED, and the payout line read
`completion 1.00, defence 0.63 (1060/4000 health, 14 attacker(s) down): 25 total` at distance
fraction 0.56 of deposit 1. The tester's on-screen 43 crystite is the session's whole ledger —
25 from the machine, 18 from kills — matched to the unit. The defence factor is honest and the
number every future tuning pass argues with.

1. Log in to zone 448. Walk to deposit 1's center, `199.8 315.7 401.3` (Basin Head Crystite).
2. `thumper` — the default def 766269. Confirm the calldown lands and starts its cycle.
3. Defend it: kill every wave as it arrives, staying near the machine. `invuln` first if dying to
   escorts would end the sitting — F4 wants at least one honest engagement first.
4. When it reaches COMPLETED, collect it (interact, hold).
5. `grep -a "mined node type" ~/Games/PIN/logs/GameServer.log` — the line now carries
   `defence 0.XX (H/M health, K attacker(s) down)`. Expect defence near 1.00 if the sappers were
   killed promptly, and the payout to match [Thump Placement](Thump-Placement.md)'s S3 range scaled
   by that factor.

Pass: the thumper survives, pays, and the log states completion, defence, health and kills in one
line. Fail, no waves ever spawn: check `grep -a "wave"` — if the spawn lines are absent the
encounter never reached its progress thresholds (early collection skips remaining waves by design);
if the lines are there but nothing stood up, that is the spawn path and N-stream territory.

## [x] F2: The waves arrive on schedule and walk in

Read from F1's cycle, no extra setup.

**Passed 2026-08-15, on both cycles, to the second.** First cycle: THUMPING began 09:06:00 and the
spawn lines read 09:06:45, 09:08:01, 09:09:17, 09:10:30 — 45/121/197/270 seconds in, against a
45/120/195/270 schedule. Second cycle repeated the offsets. Counts escalated 2+0, 2+1, 3+1, 3+2 as
authored, and the members walked their ring straight to their targets. Nothing stood in terrain,
but deposit 1 is flat ground and proves nothing about slopes.

1. `grep -a "wave" ~/Games/PIN/logs/GameServer.log` — expect four lines:
   `wave 1 of 4 at 15%: 2 sapper(s) and 0 escort(s)` through `wave 4 of 4 at 90%: 3 sapper(s) and
   2 escort(s)`, roughly 45s, 120s, 195s and 270s after THUMPING begins.
2. On screen: monsters appear ~20m out and close on the machine. The sappers should walk past you
   to reach it.

Pass: four waves, escalating counts, at the logged progress marks. Watch for members standing in
terrain on sloped ground — record it if seen, it bounds where waves can be trusted.

## [x] F3: Sappers hurt the machine

The entry that proves the collision body, which is the piece no offline test can touch.

**Passed 2026-08-15, and the observation window it asked for is what lost the first cycle.** The
collision asset loaded — no `fallback shape` line for the thumper's body; the fallback lines in
the log are all `assetId 0`, an unrelated pre-existing case — and the damage stream was immediate:
**49 per claw, roughly one hit per second per sapper**, so two sappers are ~100/s and three ~150/s
against a 4000-point pool. The requested ~20 seconds of watching cost ~2000 health on its own.
Health-bar movement on screen matched the log. The DPS reading is the sitting's main product:
unopposed, one sapper kills the machine in 80 seconds, wave 4 alone in under 10.

1. During F1's cycle, let one wave reach the machine unopposed for ~20 seconds before killing it.
2. `grep -a "Thumper .* took .* damage" ~/Games/PIN/logs/GameServer.log` — expect a stream of
   debug lines with falling health.
3. Check the machine's health bar on screen fell to match (the view carries health percent).
4. `grep -a "fallback shape" ~/Games/PIN/logs/GameServer.log` — record whether the pose asset
   loaded. Not a pass/fail condition, but the answer decides whether the aim-low compromise is
   carrying the fight.

Pass: server log shows damage landing and the client shows the same wound. Fail, no damage lines at
all while sappers visibly claw the machine: their rounds are missing the body — the fallback-shape
grep plus the NPC hold-fire lines (`holding fire on ... OutOfRange / NoLineOfSight`) say which half
broke. Record the damage rate either way: nothing offline can size Aranha claws against a
4000-point pool, and this number is what tunes the wave schedule next.

## [x] F4: Sappers ignore you, escorts don't

**Passed 2026-08-15, tester-reported — the one entry only eyes can close.** Sappers ignored being
shot mid-walk, and kept ignoring the tester even when Charged clean through a group of them, which
is a stronger provocation than the entry asked for. Escorts hunted the tester like any NPC-Combat
monster. Two observations came out of the fighting itself, both deferred tuning rather than
defects: the grenade launcher's small AoE never caught more than one Aranha because they don't
group tightly enough, and Aranha melee reach reads longer than it should (DATA-10 territory — the
5m claw plus the 4m standoff means contact starts at range).

1. During wave 2 or later (first wave with an escort), shoot a sapper without killing it. It should
   keep walking to the machine and never turn on you.
2. Stand inside ~25m of an escort. It should notice, close and attack you, exactly like an
   [NPC Combat](NPC-Combat.md) monster.
3. Kill the escort, then leave the last sapper alone with the machine and step 30m back: with
   nobody perceived it keeps chewing.

Pass: the two roles are visibly different on screen. This is the invented half of the design — the
split is what makes defending a decision instead of a shooting gallery — so note anything that
reads wrong, not just anything broken.

## [x] F5: Losing the thumper costs the load

The failure path, on its own cycle.

**Passed 2026-08-15 twice, and never by running the entry as written.** First, unattended: the
zone's NPC-owned debug thumper spawned its own wave at 09:02:23 — before the tester was in
position — and its two sappers killed it by 09:03:16. `destroyed ... at 32% with 0 attacker(s)
down`, survivors released, death ability ran, no crash, no payout. That is G5's unattended-timer
role inherited by M7: **the debug thumper now loses its cycle every session**, because nobody
defends it — a changed zone behaviour to expect in future logs, not a defect. Second, live: the
tester's first cycle died at 91% with 9 attackers down (see F3 for why), five survivors released,
no payout anywhere in the log. The client-side presentation of the destroyed state was not
separately reported — the tester's attention was on the fight — so the wreck's on-screen reading
rides along unconfirmed; everything server-side is exact.

1. `thumper` on deposit 1 again. Do not defend it. Stay 30m+ away so escorts idle on the machine
   too.
2. Wait. If the waves can kill a 4000-point machine, it dies somewhere in the back half of the
   cycle; the health bar and the damage grep track it down.
3. At zero: expect the DESTROYED state on screen (the client has its own art for it), the beacon's
   death ability firing, and the wreck disappearing about 6 seconds later. The client should treat
   the run as failed — the completion event goes out with `Destroyed = 1` and zero quantity.
4. `grep -a "was destroyed by" ~/Games/PIN/logs/GameServer.log` — expect the encounter's line with
   progress and kills. `grep -a "released"` — surviving members despawn with the encounter.
5. Confirm nothing was paid: no new `paying ... resource` line for this cycle.

Pass: the machine dies, the client shows a failed run, nothing pays, the swarm disperses. Fail, the
cycle completes with the machine barely scratched: that is not a defect in the path, it is the DPS
reading from F3 saying the waves are undersized — record the final health and file it against the
wave schedule rather than the code.

## [x] F6: Wave deaths are the encounter's business

Read from F1's cycle.

**Passed 2026-08-15.** Every kill logged `wave member <id> died to <tester>, N standing, K down`
and the standing count tracked the fight in real time across 23 routed deaths (9 on the lost
cycle, 14 on the won one). Kill loot was untouched by the second subscriber: 528s paid their usual
2-crystite rolls and Melded residue, one item roll logged `not paid — item drops are unbuilt`, all
exactly the [Kill Rewards](Kill-Rewards.md) behaviour.

1. `grep -a "wave member" ~/Games/PIN/logs/GameServer.log` — every kill should log
   `wave member <id> died to <you>, N standing, K down`, and the standing count should track the
   fight.
2. Cross-check one kill against [Kill Rewards](Kill-Rewards.md): the same death should also roll
   loot (`grep -a "Kill of monster type 528"`), because the encounter routing is a second
   subscriber, not a replacement.

Pass: the encounter counts its own dead and the kill payout is untouched. This entry is also the
first live proof of the death-routing plumbing (`EncounterComponent.Event.Death`), which M7 built
for every future encounter, not just this one.
