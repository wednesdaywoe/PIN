# Range Based Damage Decay

Part of the [in-game test queue](README.md). Setup, admin commands and the monster type id table:
[Session Setup](Session-Setup.md).

Added in the current working tree:
[DamageFalloff](../../UdpHosts/GameServer/Systems/ProjectileSim/DamageFalloff.cs),
wired into `ProjectileSim.FireProjectile`, printed by `dbg_weapon`. See
[layer 6](../Architecture/06-combat-and-damage.md).

The curve maths is already covered offline by `DamageFalloffTests` in
[Tests/GameServer.Tests](../../Tests/GameServer.Tests), so these checks are only about whether the
model matches the client and whether the SDB values make it fire at all.

Reading `dbitems::Ammo` and `dbitems::WeaponTemplates` out of the retail `clientdb.sd2` answers the
"does it fire at all" half without a client. `Resolve` enables decay for 120 of the 318 weapon
templates. The 198 it disables break down as 156 whose ammo has `DamageDecay = 0`, 20 with no ammo
row, 19 where the minimum works out to a full round or more, and 3 with non-positive damage per
round, so more than half of any weapon you pick at random is expected to print
`Range decay: disabled` and that on its own means nothing. **Pick a weapon that decays before
concluding anything**, and note that no template is disabled by `DamageDecayRangefrac` alone.

Weapons to test with:

| How to get it | Weapon | Range | Damage | Full damage to | Floor |
|---------------|--------|-------|--------|----------------|-------|
| **switch to the Biotech frame** | BioTech Needler Shotgun, item 87056, template 12135 | 70m | 35 | **7m** | 17.5 at 70m |
| `createitem 20003` | PvE Shotgun, template 1, level 1 | 50m | 40 | **5m** | 2 at 50m |
| `createitem 20005` | Bolt-action Sniper, template 3, level 1 | 215m | 375 | 193.5m | 123.8 at 215m |
| whatever you spawn with | Assault rifle, template 4 family | 150m | 46 | 105m | 15.2 at 150m |

**Use the Biotech frame.** Its default weapon is a shotgun-type that decays, so nothing in this file
needs an item conjured at all — switch frame in the garage and shoot. That matters, because
`createitem` was found broken on 2026-08-11: it flashes a pickup toast and delivers nothing, which
is [I1](Inventory.md). The two `createitem` rows above are unusable until I1 passes and are kept
only because they are the sniper end of the range spread, which the Needler can't reach.

`createitem` puts the weapon in your inventory; equipping it is still a client-side loadout action.

## [x] R1: Read the resolved curve for a few weapons (blocks R2-R4)

Verifies that the server resolves a sane curve from the SDB columns and prints it.

1. `createitem 20003` and `createitem 20005`, then equip each in turn
2. `dbg_weapon` with each of the shotgun, the sniper and your default rifle equipped
3. Record `DamageDecay`, `DamageDecayRangefrac`, `MinDamageFrac`, `MinDamage`, `Range` and the
   sampled curve for each — `dbg_weapon` prints samples at 0, 25, 50, 75, 100 and 125% of max range

- Pass: `DamageDecay` is non-zero on at least some weapons and the samples describe a falloff that
  looks like the weapon (a shotgun dropping off hard and early, a sniper barely at all).
- Fail: every weapon prints `Range decay: disabled`. The gating on `DamageDecay` or the sanity
  checks in `DamageFalloff.Resolve` are reading the columns wrong. Bring the printed inputs back and
  the model can be re-derived from them.

The anchoring worry this entry was written for is already settled offline. Across the whole `Ammo`
table `DamageDecayRangefrac` looks like it clusters at 1.0, but 908 of those 1.0 rows are ammo that
doesn't decay at all and never had the column touched. Restricted to the 308 rows with
`DamageDecay` non-zero it spreads properly — 34 at 0.1, 137 at 0.7, 45 at 0.9 — and the value tracks
the weapon class: shotgun ammo at 0.1, rifle at 0.7, sniper at 0.9. That is the "decay starts here"
reading. The other reading has a shotgun holding full damage for 90% of its range and a sniper for
10%, which is backwards, so `Resolve` does not need inverting. What R1 is still for is confirming
the server's loader reads the same columns and that the printed curve matches.

`MinDamageFrac` has 24 rows above 1.0 (values of 2, 2.2, 8 and 10). Those clamp to a full round and
disable decay, which is where the 19 disabled-by-minimum templates come from. It's the one column
whose units look wrong, so if a weapon that ought to decay prints `disabled`, check this first.

**Passed 2026-08-11**, off the BioTech Needler Shotgun and the server log rather than `dbg_weapon` —
80 `Damage falloff` lines carry the resolved curve on every shot, which is what the printed samples
were a substitute for. Inputs and result:

| | |
|---|---|
| Weapon | item 87056, template 12135, "BioTech Needler Shotgun" |
| `Range` | 70 |
| `DamagePerRound` | 35, `MinDamage` 0, `HeadshotMult` 1.5 |
| Ammo 1417 | `DamageDecay` 1, `DamageDecayRangefrac` **0.1**, `MinDamageFrac` **0.5** |
| Resolved | full damage to **7m**, down to **17.5** at **70m** |

7 is 70 × 0.1 and 17.5 is 35 × 0.5, so `Resolve` reads both columns as fractions of range and of a
round, in the units the offline reading above predicted. The falloff between the two is exact linear
interpolation — every logged value matches `35 - (d-7)/63 × 17.5` to five decimal places.

This is also the live confirmation of the anchoring worry this entry was written for: 0.1 is where
decay *starts*, not where full damage ends. A shotgun holding full damage for the first tenth of its
range is the right shape, and the inverted reading would have had it hold to 63m.

The two `createitem` weapons were not read, because `createitem` is broken ([I1](Inventory.md)).
Nothing depends on them — the sniper end of the spread would only re-confirm a curve that is now
confirmed.

## [x] R2: Damage actually drops with distance

Verifies that the resolved curve is applied to a real shot, at the distances the curve claims.

Use the shotgun. Decay starts at 5m there, so all three distances fit in a space you can pace out;
an assault rifle needs a target more than 105m away before a single number moves.

1. `createitem 20003`, equip it, then `dbg_weapon` and note `Range decay: full to Xm`
2. `npc 1196 0 0 0` — substitute a flat spot you can back away from in a straight line
3. `float` to toggle `cheat_float` on if you need height or distance the terrain won't give you
4. Shoot it from contact range, from just past X, and from as far as it stays hittable
5. `float` again to turn it off
6. Back at the source machine, pair each shot's distance with its damage:

```
grep -aE "HitHandler|damage from" ~/Games/PIN/logs/GameServer.log | tail -40
```

The `T` value in each `HitHandler` line is that shot's distance in metres, and the `took N damage`
line right after it is what landed. That pairing is the measurement — no pacing needed.

**The client gives you no distance readout, so don't try to hit a distance.** Stand at contact
range, then shoot-step-back-shoot-step-back in a straight line until the target dies or stops being
hittable, and let the log say afterwards where each shot was taken from. Walking the whole curve
this way costs one magazine and produces more points than aiming at three specific distances would.
The one thing to do deliberately is pause and fire several shots wherever the floating number
changes, so the step is pinned on both sides.

If you do need a number in the moment, aim at the target and type `target` — it prints the distance
along with the entity id, which makes it a rangefinder.

Pass: the numbers fall off past X and match the `dbg_weapon` samples at those distances. The
`Damage falloff for {Weapon} at {Distance}m` lines need `--loglevel debug` and only appear once a
shot lands past the full damage range, so their absence is only meaningful if `T` exceeded X.

Attempted 2026-08-10, inconclusive rather than failed. The session landed 122 hits, the longest at
83m, all with a 46-damage assault rifle whose decay doesn't begin until 105m. Damage came out at a
flat 39 body / 58 head throughout and the log has no `Damage falloff` line, which is what a correct
implementation does inside the full damage range. Nothing about the model was exercised.

**Passed 2026-08-11** on the second attempt, with the Biotech frame's Needler Shotgun — decay starts
at 7m there, so the whole curve fits in a space you can walk. Pairing each `HitHandler` distance
with the `took N damage` line after it:

| Distance | Curve says | Applied |
|----------|-----------|---------|
| 2.1m | — (inside the full damage band, no falloff line at all) | 35 |
| 8.25m | 34.65 | 35 |
| 9.79m | 34.23 | 34 |
| 16.91m | 32.25 | 32 |
| 26.31m | 29.64 | 30 |
| 35.33m | 27.13 | — |

Applied damage is the curve rounded to nearest. The 2.1m shots produce no `Damage falloff` line,
which is the correct behaviour inside the full damage range and the same absence that made the
2026-08-10 attempt unreadable — it only means something once a shot lands past X.

## [x] R3: Point blank damage is unchanged

Verifies that decay doesn't touch anything inside the full damage range.

1. `npc 1196`
2. Shoot it at contact range with each weapon you have
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: identical damage to before this change. If close-range numbers moved, `FullDamageRange`
resolved to roughly 0 and the anchoring in R1 is wrong.

Passed 2026-08-10: an assault rifle held 46 base, 39 on a body hit and 58 on a headshot, unchanged
from 0m out to 83m, which is the whole of its full damage band up to where the shots stopped.

## [x] R4: The client draws the server's number, and doesn't argue with it

This entry was written as "the real test of the model, since the client computes its own expectation
from the same SDB columns". **That premise is wrong for PIN**, and finding that out is most of what
running it was worth. `CharacterEntity.TakeDamage` hands the applied amount to
[DamageEvents.Describe](../../UdpHosts/GameServer/Systems/Combat/DamageEvents.cs), which puts it in
`DealtHit.DamageData.DamageValue` and sends it to the attacker. The floating number over the target
*is* the server's number. It cannot disagree, so no run of this entry can confirm or kill the curve.

What it does establish, and what the 2026-08-11 run did establish: `DealtHit` reaches the client and
renders correctly, and the client isn't quietly substituting a local prediction that disagrees. That
is worth having — it just isn't a check on the model.

1. Do R2 again with the client's floating damage numbers visible
2. Note the client's number and the distance for one shot past the full damage range
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -5` for the same shot

Pass: the floating number matches the server's `took N damage` for the same shot.

**Passed 2026-08-11.** Backing away one shot at a time until the floating number read 30, then
holding there: the server logged 23.5m and 30 damage for those shots, which is the curve
(`35 - 16.5/63 × 17.5 = 30.4`) rounded. Then out to 41.8m and 25, still matching.

That run also settled the order of operations for free, which no entry had asked for. Headshots in
the same session read 46 at 23.6m, 44 at 27.0m, 40 at 37.1m and 38 at 41.8m — each one the *undecayed
precision* curve value times 1.5, rounded once at the end. Rounding the body number first and then
multiplying predicts 45, 43.5, 40.5 and 37.5, and gets three of the four wrong. So decay applies to
the full-precision damage per round, `HeadshotMult` scales what's left, and the single rounding
happens last, exactly as
[layer 6](../Architecture/06-combat-and-damage.md) describes it.

### What remains genuinely unverified

Not the two anchors — R1 confirmed those come straight out of `Range × DamageDecayRangefrac` and
`DamagePerRound × MinDamageFrac`. What no test here can reach is the **shape between them**.
`Resolve` interpolates linearly; Red 5's server may not have. The only ground truth for that is the
466 `TookHit` damage values in the 2016 recording, and pairing one with a distance means
reconstructing both entities' positions from the `MovementView` and `ConfirmedPoseUpdate` traffic
around it — see [Capture Replay](Capture-Replay.md).

Deliberately not queued. The [restoration charter](../Restoration.md) doesn't treat fidelity as a
constraint, the curve is anchored correctly at both ends, and a different interpolation would move
mid-range damage by a couple of points. Worth doing if the shape ever starts mattering to how
something feels; not worth the archaeology now.

## [ ] R5: Buffed weapon damage still decays proportionally

Verifies that both minimums are read as fractions of a round rather than absolute points.

R1 confirmed the reading offline for one weapon — 17.5 is 35 × `MinDamageFrac`, not 0.5 damage — so
what's left here is whether it still holds when the base moves under it.

1. Do R2 once unbuffed and write down the long-range number
2. Apply an ability that raises weapon damage, then fire at the same distance before it expires
3. `grep -a "Damage falloff" ~/Games/PIN/logs/GameServer.log | tail -5` to see both `Base` and
   `MinDamage` move together

Pass: long-range damage rises with the buff rather than decaying back to the unbuffed floor.
`Resolve` reads both minimums as fractions of a full round specifically so this holds.
