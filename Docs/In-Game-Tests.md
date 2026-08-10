# In-Game Test Queue

A running log of checks for me to run with a real client, written down as work
lands so nothing accumulates unverified. Work through it at the game machine, record the result
inline, and delete entries once they've passed and the behaviour is covered elsewhere.

Status markers: `[ ]` not run, `[x]` passed, `[!]` failed (leave it in with what happened),
`[-]` skipped or not reproducible.

Adding an entry: what changed, the steps, and what counts as a pass. If a check needs a specific
world state (two clients, a particular NPC), say so. That's usually what makes an entry sit unrun.

## Session setup

```
dotnet build PIN.sln                     # 0 errors expected
dotnet test PIN.sln                      # all green before leaving the desk
WebHostManager                           # ports 4400-4411 / 44300-44311
MatrixServer                             # UDP 25000
GameServer                               # UDP 25001
```

Remember to watch the GameServer log throughout. Two things are silent failures worth grepping for after any
session:

- `HandlePacket Caught`: a controller handler threw and was swallowed
- `Unrecognized MsgID`: the client sent something we don't handle

---

## Hostility rules

Added in the current working tree: [HostilityRules](../UdpHosts/GameServer/Systems/Hostility/HostilityRules.cs),
`SDBUtils.GetFactionStance`, `/hostility`. See [layer 6](Architecture/06-combat-and-damage.md).

### [ ] H1: Confirm the faction stance encoding (blocks H2-H6)

The highest-value check here; everything else in this section depends on it.

1. Start the GameServer against a real `clientdb.sd2`.
2. Fire one shot at anything, which is what first builds the stance table.
3. Find the log line `Loaded N faction stance pairs from M relations`.

Record the `Stance values in use` and `Faction default stances in use` lists.

- Pass: the stance list contains negative values → `SDBUtils.ToStance` reads the column correctly
  as a signed scale, nothing to change.
- Fail: the follow-up warning `Faction stances hold no negative values` appears → the column is an
  ordinal enum. Cross-faction hostility is disabled until `ToStance` is rewritten for the values
  logged. Bring the two lists back and the mapping can be fixed in one method.

### [ ] H2: `/hostility` against a hostile NPC

`/spawncharacter` a monster, aim at it, `/hostility`.

Pass: both faction ids print and differ, stance is `Hostile` (or `Neutral` if H1 failed) and
`can damage: True`.

### [ ] H3: `/hostility` against yourself

`/target me`, then `/hostility`.

Pass: same faction both sides, stance `Friendly`, `can damage: False` in both directions.

### [ ] H4: Player damage to NPCs still lands

Shoot a spawned monster to death.

Pass: health drops, headshots read higher than body shots, the kill registers, and the corpse
despawns about 30s later. This is the regression H1 guards against. If shots register but health
never moves, H1 failed and the stance mapping is inverted.

### [ ] H5: NPC damage to the player still lands

Let a hostile NPC shoot back.

Pass: player health drops and the hit shows client-side.

### [ ] H6: Same-faction NPCs no longer hurt each other

Needs two NPCs of the same faction and a splash source. Spawn a group, then hit the middle of it
with a splash ability.

Pass: NPCs of a faction hostile to you take splash; NPCs sharing a faction with the splash's owner
don't. Previously every character in radius took damage.

### [ ] H7: Player versus player is still blocked

Needs two clients. Shoot the other player.

Pass: no damage. This used to come from an `IsPlayerControlled` check and now comes from both
characters being faction 1, so it's worth confirming the replacement actually holds.

---

## Range based damage decay

Added in the current working tree: [DamageFalloff](../UdpHosts/GameServer/Systems/ProjectileSim/DamageFalloff.cs),
wired into `ProjectileSim.FireProjectile`, printed by `/dbg_weapon`. See
[layer 6](Architecture/06-combat-and-damage.md).

The curve maths is already covered offline by `DamageFalloffTests` in
[Tests/GameServer.Tests](../Tests/GameServer.Tests), so these checks are only about whether the model
matches the client and whether the SDB values make it fire at all.

### [ ] R1: Read the resolved curve for a few weapons (blocks R2-R4)

`/dbg_weapon` with a rifle equipped, then repeat for a shotgun, an SMG and a sniper, the weapons
whose real falloff differs most.

Record `DamageDecay`, `DamageDecayRangefrac`, `MinDamageFrac`, `MinDamage`, `Range` and the sampled
curve for each.

- Pass: `DamageDecay` is non-zero on at least some weapons and the samples describe a falloff that
  looks like the weapon (a shotgun dropping off hard and early, a sniper barely at all).
- Fail: every weapon prints `Range decay: disabled`. The gating on `DamageDecay` or the sanity
  checks in `DamageFalloff.Resolve` are reading the columns wrong. Bring the printed inputs back and
  the model can be re-derived from them.

Watch for `DamageDecayRangefrac` anchoring the wrong end. If the values are small (0.1-0.3) then
decay starting there and running to `Range` is right; if they cluster near 1.0 the column more
likely marks where decay *ends*, and `Resolve` needs inverting.

### [ ] R2: Damage actually drops with distance

Shoot the same NPC body part from point blank, from mid range and from as far as it stays hittable.

Pass: the numbers fall off, and they match the `/dbg_weapon` samples at those distances. Compare
against the `Damage falloff for {Weapon} at {Distance}m` log lines, which need `--loglevel debug`
and only appear once a shot lands past the full damage range.

### [ ] R3: Point blank damage is unchanged

Shoot an NPC at contact range with several weapons.

Pass: identical damage to before this change. Decay must not touch anything inside the full damage
range. If close-range numbers moved, `FullDamageRange` resolved to roughly 0 and the anchoring in
R1 is wrong.

### [ ] R4: Server numbers agree with the client

Compare a damage number the client renders against the same shot's server-side value.

Pass: they agree. A mismatch here is the real test of the model, since the client computes its own
expectation from the same SDB columns. This is the check that would confirm or kill the curve
outright.

### [ ] R5: Buffed weapon damage still decays proportionally

Apply an ability that raises weapon damage, then fire at long range.

Pass: long-range damage rises with the buff rather than decaying back to the unbuffed floor.
`Resolve` reads both minimums as fractions of a full round specifically so this holds.

---

## Carried over from the damage work

These landed in commits `9768cf4`, `31f0ff3`, `4a8075e` but predate the pause, so their in-game
state is unknown. Prune whatever you've already seen working.

### [ ] D1: Weapon damage loop end to end

Pass: projectile hits damage characters, death fires, respawn puts the player at the nearest
uncaptured outpost and movement input is accepted again afterwards.

### [ ] D2: Headshot and crit

Pass: `hit.DamageMod` and `weapon.HeadshotMult` visibly change the number; the `Critical` damage
flag reaches the client on headshots and crit materials.

### [ ] D3: `InflictDamage` splash falloff

Pass: damage scales down linearly from `Pointblankrange` to the splash range, and a target caught by
both the direct hit and the splash is only damaged once.

### [ ] D4: `SetWeaponDamage` restores on effect end

Apply an ability that overrides weapon damage, let it expire.

Pass: damage returns to the base value, so the `OnRemove` restore path in the active-command
pattern actually ran.

### [ ] D5: Charge camera lockup (fix in `573d370`)

Pass: camera control returns after Charge ends, including when the ability is interrupted or the
target dies mid-charge.

---

## Known to need the client, not yet scheduled

Not tests so much as things that can't be checked offline at all, kept here so they're not mistaken
for done:

- Whether any new netfield shape or message layout is accepted by the client
- Client prediction agreement on spread, movement and effect timing
- Anything about what the client renders
