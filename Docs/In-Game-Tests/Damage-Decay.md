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

Weapons to test with, and the exact ids to conjure them:

| Command | Weapon | Range | Damage | Full damage to | Floor |
|---------|--------|-------|--------|----------------|-------|
| `createitem 20003` | PvE Shotgun, template 1, level 1 | 50m | 40 | **5m** | 2 at 50m |
| `createitem 20005` | Bolt-action Sniper, template 3, level 1 | 215m | 375 | 193.5m | 123.8 at 215m |
| whatever you spawn with | Assault rifle, template 4 family | 150m | 46 | 105m | 15.2 at 150m |

`createitem` puts the weapon in your inventory; equipping it is still a client-side loadout action.

## [ ] R1: Read the resolved curve for a few weapons (blocks R2-R4)

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

## [ ] R2: Damage actually drops with distance

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

Pass: the numbers fall off past X and match the `dbg_weapon` samples at those distances. The
`Damage falloff for {Weapon} at {Distance}m` lines need `--loglevel debug` and only appear once a
shot lands past the full damage range, so their absence is only meaningful if `T` exceeded X.

Attempted 2026-08-10, inconclusive rather than failed. The session landed 122 hits, the longest at
83m, all with a 46-damage assault rifle whose decay doesn't begin until 105m. Damage came out at a
flat 39 body / 58 head throughout and the log has no `Damage falloff` line, which is what a correct
implementation does inside the full damage range. Nothing about the model was exercised.

## [x] R3: Point blank damage is unchanged

Verifies that decay doesn't touch anything inside the full damage range.

1. `npc 1196`
2. Shoot it at contact range with each weapon you have
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: identical damage to before this change. If close-range numbers moved, `FullDamageRange`
resolved to roughly 0 and the anchoring in R1 is wrong.

Passed 2026-08-10: an assault rifle held 46 base, 39 on a body hit and 58 on a headshot, unchanged
from 0m out to 83m, which is the whole of its full damage band up to where the shots stopped.

## [ ] R4: Server numbers agree with the client

The real test of the model, since the client computes its own expectation from the same SDB columns.

1. Do R2 again with the client's floating damage numbers visible
2. Note the client's number and the distance for one shot past the full damage range
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -5` for the same shot

Pass: they agree. A mismatch here would confirm or kill the curve outright.

Live-server damage numbers to check the curve against can also be read out of the 2016 recording —
see [Capture Replay](Capture-Replay.md).

## [ ] R5: Buffed weapon damage still decays proportionally

Verifies that both minimums are read as fractions of a round rather than absolute points.

1. Do R2 once unbuffed and write down the long-range number
2. Apply an ability that raises weapon damage, then fire at the same distance before it expires
3. `grep -a "Damage falloff" ~/Games/PIN/logs/GameServer.log | tail -5` to see both `Base` and
   `MinDamage` move together

Pass: long-range damage rises with the buff rather than decaying back to the unbuffed floor.
`Resolve` reads both minimums as fractions of a full round specifically so this holds.
