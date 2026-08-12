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

Every template's numbers are tabulated in
[Reference/Weapon-Templates](Reference/Weapon-Templates.md), and the resolved per-weapon figures in
[Reference/Weapons](Reference/Weapons.md).

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

`max_rise = 1` means the climb is a non-event: `rise_per_burst` of 2.5 reads punchy, but it is
hard-capped at 1 and saturates almost at once — the muzzle lifts a little and stops. **The
complaint people remember about this weapon is the bloom, and the bloom is real.** Confirmed
in-game against build 1962.

PIN hands the Dragonfly item **87601, "BR-44 Shaman"** — level 25, required level 20, quality 2 —
from `dbcharacter::CharCreateLoadoutSlots`. A legitimate mid-tier retail rifle, on the same
template as every other one. So the Bio Rifle in PIN is not approximated, not defaulted and not a
dev item. It is what build 1962 shipped.

### Why the bloom bites harder than the numbers suggest

An 8× bloom ratio is only the 80th percentile — the median template blooms 3×, and the FAMAS Burst
Rifle manages 16.7×. Ratio alone does not explain the reputation. Three other things do.

The ramp is **1000 ms at exponent 1**. Linear, and slow. Weapons like the PvE Burst Rifle reach
their ceiling in 120 ms and then sit there; you learn one penalty and shoot around it. The Bio
Rifle degrades continuously for a full second, so accuracy is a moving target the whole time you
hold the trigger. `spread_per_burst` is 0, so this is purely time-based — trigger discipline, not
burst discipline, is what controls it.

Then the movement penalties, which is where it turns nasty:

| State | Effective spread floor |
|-------|------------------------|
| Standing | 1 |
| Moving | 2.25 (`run_minspread_add` 1.25) |
| **Airborne** | **5** (`jump_minspread_add` 4) |
| Ceiling | 8 |

`jump_minspread_add = 4` is **tied for the highest in the game**, and it is on a support frame in a
game built around jetpacks. Of the 53 templates that 20 or more shipping items use, **40 have no
airborne penalty at all** and the median is 0.

So the 1.0 floor is fiction for how the weapon is actually played. Flying — which on a Dragonfly is
most of the time — you start at 5 of a maximum 8 and climb from there. You spend the fight between
62% and 100% of this weapon's worst accuracy. That is the "wild spread bloom", and it is a
deliberate design choice rather than a bug or a broken migration.

Recovery is the one mercy: `ms_spread_return` is 200 ms, so letting go resets you quickly.

## Retuning is a client-side edit

Because handling is read from `clientdb.sd2` and never transmitted, changing how a weapon feels
means editing the client's own database — the server cannot do it. That is within reach rather
than out of it: `MinimalSDB` already round-trips an SDB (`sdb.Read` / `sdb.Write` in prune mode),
so writing modified values back is a tooling problem, not a format problem.

Worth being deliberate about, though. It edits the game install rather than the repo, it changes
what every weapon on that template does at once (214 items share the Bio Rifle's), and a modified
client DB is no longer the reference copy that everything else here is verified against. Keep a
pristine `clientdb.sd2` if you go down this road.

## Two traps

**Internal names are not documentation.** Template 12171 is called *"David's Magic Bio Rifle
Ironsights"* and it is the production template behind 214 shipping weapons named plainly "Bio
Rifle". Red 5 shipped a developer's working title to retail. Judge a template by what references
it, never by what it calls itself.

**Template count is not variety.** Bio weapons span 11 templates but the distribution is heavily
skewed — 237 items on the BioCrossbow template, 229 on the Needler Shotgun, 214 on the Bio Rifle,
then a long tail of one- and two-item templates for NPCs, PvP variants and alt-fires. Finding a
template named after a weapon does not mean players ever held it.
