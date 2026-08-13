---
project: pin
kind: test-stream
title: "Hostility Rules (H1-H7)"
relates:
  - ../TEST-REGISTER.md
---

# Hostility Rules

Part of the [in-game test queue](README.md). Setup, admin commands and the monster type id table:
[Session Setup](Session-Setup.md).

Added in the current working tree:
[HostilityRules](../../UdpHosts/GameServer/Systems/Hostility/HostilityRules.cs),
`SDBUtils.GetFactionStance`, `hostility`. See
[layer 6](../Architecture/06-combat-and-damage.md).

## [x] H1: Confirm the faction stance encoding (blocks H2-H6)

The highest-value check here; everything else in this section depends on it. Verifies that the
server's loader reads `hostility_stance` as the signed scale `SDBUtils.ToStance` assumes.

1. Start the GameServer against a real `clientdb.sd2`.
2. `npc 1196` — spawns a Chosen Fiend, faction 2, at your feet
3. `target`, then `hostility`

   **It has to be a cross-faction pair, and "fire one shot at anything" is not enough.** The table
   is a `Lazy<>` built on the first call to `SDBUtils.GetFactionStance`, and
   [HostilityRules.GetStance](../../UdpHosts/GameServer/Systems/Hostility/HostilityRules.cs#L26-L38)
   returns early — before reaching it — whenever the two entities share a team or a faction. Every
   player character is faction 1, so shooting scenery, another player, or nothing at all never
   builds the table and the grep below comes back empty with the server working correctly. Any
   cross-faction call does it; `hostility` is the cheapest because it needs no shot, and it's the
   same two commands as H2.

4. Back at the source machine:

```
grep -a -A3 "faction stance pairs" ~/Games/PIN/logs/GameServer.log
```

Record the `Stance values in use` and `Faction default stances in use` lists.

An empty grep means step 3 didn't reach a cross-faction pair — check that the `npc` actually spawned
and that `target` picked it up, then run `hostility` again. It does not mean the load failed; a
genuine load failure logs `No faction relations loaded` at startup instead.

- Pass: the stance list contains negative values → `SDBUtils.ToStance` reads the column correctly
  as a signed scale, nothing to change.
- Fail: the follow-up warning `Faction stances hold no negative values` appears → the column is an
  ordinal enum. Cross-faction hostility is disabled until `ToStance` is rewritten for the values
  logged. Bring the two lists back and the mapping can be fixed in one method.

Expect a pass: reading `dbcharacter::FactionRelations` straight out of the retail `clientdb.sd2`
gives `hostility_stance` values across all 89 rows of -2, -1, 0, 1 and 2, and `dbcharacter::Faction`
carries `default_stance` of -1, 0 and 1. Both are signed scales, which is what `ToStance` assumes.
What the run still has to confirm is that the server's loader reads the same column the same way.

**Passed 2026-08-11, off H2 rather than the grep above.** `GetFactionStance` returns `Neutral`
unconditionally when the column isn't signed, so H2 printing `Stance Hostile` for a faction 1 versus
faction 2 pair can only happen when the loader read a negative value — which is the whole question.
The stance lists were never captured, because the session that answered it predates the corrected
step 3 and the log has since been truncated. Grep them off the next session if the exact values are
ever wanted; nothing depends on them now.

The consequence: `ToStance` needed no change, and the `!Signed` fallback in
[SDBUtils.GetFactionStance](../../UdpHosts/GameServer/StaticDB/SDBUtils.cs) — which forced every
cross-faction pair to Neutral while the encoding was in doubt — has been removed. The warning that
fires when the values aren't signed stays, as the tripwire for a db that doesn't look like retail.

## [x] H2: `hostility` against a hostile NPC

Verifies that a cross-faction pair resolves to Hostile and is damageable.

1. `npc 1196` — spawns a Chosen Fiend, faction 2, at your feet
2. Aim at it, or `target` with no argument to ray-cast onto it
3. `hostility`

Pass: both faction ids print and differ, stance is `Hostile` (or `Neutral` if H1 failed) and
`can damage: True`.

## [x] H3: `hostility` against yourself

Verifies that the same-faction short circuit reports Friendly and blocks damage both ways.

1. `target me`
2. `hostility`
3. `clear` when done, so later checks don't inherit the target

Pass: same faction both sides, stance `Friendly`, `can damage: False` in both directions.

## [x] H4: Player damage to NPCs still lands

The regression H1 guards against.

1. `npc 1196`
2. Shoot it to death, landing at least one headshot
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: health drops, headshots read higher than body shots, the kill registers, and the corpse
despawns about 30s later. If shots register but health never moves, H1 failed and the stance mapping
is inverted.

## [x] H5: NPC damage to the player still lands

1. `npc 1196`
2. Stand in front of it and wait

Pass: player health drops and the hit shows client-side.

Unblocked as of the M2 attack pass, having sat `[-]` since 2026-08-10 on an empty `AIEngine.Tick`
that meant a spawned monster stood still and soaked fire. An NPC now picks a target and fires a
weapon at it. Run alongside [N2](NPC-Combat.md), which is the same scenario looked at from the other
side: N2 asks whether the attack pass works, this asks whether `CanDamage` holds up with a monster as
the attacker rather than the target, which nothing else checks.

**Passed 2026-08-12.** `CanDamage` holds with the monster as attacker, and the log shows the hits
landing one at a time — `Fallback took 1 damage from CharacterEntity (...), 1 of it on shields, 2642
shields and 19192 health left`, then 2641, then 2640.

**The character looked invulnerable and isn't.** Standing in front of a Chosen Fiend, nothing
visible happens, and the arithmetic is why: its rifle does 1 damage a round at about 11 dps, against
3000 shields ([DATA-1](../gaps/data.md#data-1)'s stand-in) and 19192 health. That is roughly four
and a half minutes to strip the shields and half an hour to finish the job. Use `2342` (125 a hit)
if the point is to watch something die.

Walking into a melding wall or deep water doing nothing is a different thing entirely, and not a
damage-routing failure: **there is no environmental damage in the server at all.**
`MeldingBubbleEntity` carries a position and a radius and nothing that hurts anyone,
`dbvisualrecords::WaterDesc` parses `DrowningPercent` and `DrowningCharStatusEffectId` that nothing
reads, and every `TakeDamage` call in the codebase comes from a projectile or an `InflictDamage`
aptitude command. Recorded under [Deferred](../PROGRESS.md#deferred).

## [x] H6: Same-faction NPCs no longer hurt each other

Verifies that splash respects faction, which is what changed.

1. `npc 1196 0 0 0`, `npc 1196 2 0 0`, `npc 290 4 0 0` — two chosen and one accord, spawned two
   metres apart. Substitute your own coordinates; any three points in a line work.
2. Fire a splash ability into the middle of the group
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: the two faction-2 NPCs take splash and the faction-1 one doesn't. Previously every character in
radius took damage.

## [ ] H7: Player versus player is still blocked

Needs two clients.

1. Both players log in
2. `target <the other player's entity id>` then `hostility`, to see the pair before shooting
3. Shoot the other player

Pass: no damage, and `hostility` prints faction 1 on both sides with `can damage: False`. This used
to come from an `IsPlayerControlled` check and now comes from both characters being faction 1, so
it's worth confirming the replacement actually holds.
