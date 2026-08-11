# Weapon handling in build 1962

How a gun *feels* — muzzle climb, bloom, camera kick — is decided on the client, out of the
client's own `clientdb.sd2`. The server picks the weapon; the client does the rest.

No GSS message in V66 carries a recoil, rise or spread value. The only two that mention spread at
all are `Character/Command/SetNoSpreadFlag` and the `CombatController` view, and neither transmits
a curve. So a weapon that feels wrong in PIN is almost never PIN sending bad numbers — it is PIN
equipping a different item, or the client reading numbers that were always like that.

## Where handling actually lives

In `dbitems::WeaponTemplates`, not on the item. The relevant fields:

| Group | Fields |
|-------|--------|
| Climb | `rise_per_burst`, `min_rise_per_burst`, `max_rise`, `rise_ramp_time`, `rise_ramp_exponent`, `ms_rise_return_delay`, `overmax_rise_permanent_frac` |
| Horizontal | `slide_per_burst`, `min_slide_per_burst`, `max_slide`, `slide_ramp_exponent`, `slide_permanent_frac` |
| Bloom | `min_spread`, `max_spread`, `starting_spread`, `spread_per_burst`, `spread_ramp_time`, `spread_ramp_exponent`, `ms_spread_return` |
| Camera | `cam_recoil_base`, `cam_recoil_shake`, `cam_recoil_recover_ms` |
| Jitter | `initial_jitter`, `max_jitter`, `jitter_ramp_time` |

`dbitems::WeaponTemplateModifiers` can override any of them per item, as
`(base + modifier) * multiplier` — the arithmetic
[SDBUtils](../../UdpHosts/GameServer/StaticDB/SDBUtils.cs) already implements. A modifier of 0 with
a multiplier of 1 is a no-op, which is most of them.

## Worked example: the Dragonfly's Bio Rifle

Every Bio Rifle in the game — all **214** items — resolves to weapon template **12171**, and after
modifiers are applied their effective handling is *identical*:

| | |
|--|--|
| `rise_per_burst` | 2.5 |
| `min_rise_per_burst` | 2 |
| **`max_rise`** | **1** |
| `rise_ramp_time` | 900 ms, exponent 2.1 |
| `min_spread` → `max_spread` | 1 → 8 over 1000 ms |

210 of the 214 carry modifier rows and **not one of them changes recoil**. There is no gun-feel
progression here: a level 1 Bio Rifle and a level 45 Bio Rifle climb exactly the same.

`max_rise = 1` is the number that matters. A `rise_per_burst` of 2.5 reads as punchy, but the climb
is hard-capped at 1 and saturates almost at once — the muzzle lifts a little and stops. Bloom is
where this weapon actually punishes you: spread grows eightfold over a second of sustained fire.

PIN hands the Dragonfly item **87601, "BR-44 Shaman"** — level 25, required level 20, quality 2 —
from `dbcharacter::CharCreateLoadoutSlots`. A legitimate mid-tier retail rifle, on the same
template as every other one.

So the Bio Rifle in PIN is not approximated, not defaulted and not a dev item. It is what build
1962 shipped.

## Two traps

**Internal names are not documentation.** Template 12171 is called *"David's Magic Bio Rifle
Ironsights"* and it is the production template behind 214 shipping weapons named plainly "Bio
Rifle". Red 5 shipped a developer's working title to retail. Judge a template by what references
it, never by what it calls itself.

**Template count is not variety.** Bio weapons span 11 templates but the distribution is heavily
skewed — 237 items on the BioCrossbow template, 229 on the Needler Shotgun, 214 on the Bio Rifle,
then a long tail of one- and two-item templates for NPCs, PvP variants and alt-fires. Finding a
template named after a weapon does not mean players ever held it.
