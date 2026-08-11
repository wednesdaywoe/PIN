# Hostility Rules

Part of the [in-game test queue](README.md). Setup, admin commands and the monster type id table:
[Session Setup](Session-Setup.md).

Added in the current working tree:
[HostilityRules](../../UdpHosts/GameServer/Systems/Hostility/HostilityRules.cs),
`SDBUtils.GetFactionStance`, `hostility`. See
[layer 6](../Architecture/06-combat-and-damage.md).

## [ ] H1: Confirm the faction stance encoding (blocks H2-H6)

The highest-value check here; everything else in this section depends on it. Verifies that the
server's loader reads `hostility_stance` as the signed scale `SDBUtils.ToStance` assumes.

1. Start the GameServer against a real `clientdb.sd2`.
2. Fire one shot at anything, which is what first builds the stance table.
3. Back at the source machine:

```
grep -a -A3 "faction stance pairs" ~/Games/PIN/logs/GameServer.log
```

Record the `Stance values in use` and `Faction default stances in use` lists.

- Pass: the stance list contains negative values → `SDBUtils.ToStance` reads the column correctly
  as a signed scale, nothing to change.
- Fail: the follow-up warning `Faction stances hold no negative values` appears → the column is an
  ordinal enum. Cross-faction hostility is disabled until `ToStance` is rewritten for the values
  logged. Bring the two lists back and the mapping can be fixed in one method.

Expect a pass: reading `dbcharacter::FactionRelations` straight out of the retail `clientdb.sd2`
gives `hostility_stance` values across all 89 rows of -2, -1, 0, 1 and 2, and `dbcharacter::Faction`
carries `default_stance` of -1, 0 and 1. Both are signed scales, which is what `ToStance` assumes.
What the run still has to confirm is that the server's loader reads the same column the same way.

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

## [-] H5: NPC damage to the player still lands

1. `npc 1196`
2. Stand in front of it and wait

Pass: player health drops and the hit shows client-side.

Not runnable yet, and won't be until M2. [AIEngine](../../UdpHosts/GameServer/AIEngine.cs) is ticked
every frame from `Shard.Tick` with an empty body, so nothing picks a target or pulls a trigger on an
NPC's behalf. A spawned monster stands still and soaks fire. Confirmed 2026-08-10. Re-run this the
moment an NPC can attack, since it's the only check that exercises `CanDamage` with a monster as the
attacker rather than the target.

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
