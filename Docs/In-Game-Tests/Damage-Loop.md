# Damage Loop

Part of the [in-game test queue](README.md). Setup, admin commands and the monster type id table:
[Session Setup](Session-Setup.md).

These landed in commits `9768cf4`, `31f0ff3`, `4a8075e` but predate the pause, so their in-game
state is unknown. Prune whatever you've already seen working.

D5 was the fourth entry in this batch; it grew into an investigation of its own and lives in
[Charge Camera](Charge-Camera.md).

## [ ] D1: Weapon damage loop end to end

1. `npc 1196`, kill it
2. Let a hostile source kill you, or shoot yourself off a height with `float` off
3. Move after respawning

Pass: projectile hits damage characters, death fires, respawn puts the player at the nearest
uncaptured outpost and movement input is accepted again afterwards.

## [x] D2: Headshot and crit

1. `npc 1196`
2. Land one body shot and one headshot on it
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -10`

Pass: `hit.DamageMod` and `weapon.HeadshotMult` visibly change the number; the `Critical` damage
flag reaches the client on headshots and crit materials. With the 46-damage assault rifle the
expected pair is 39 body and 58 head.

## [ ] D3: `InflictDamage` splash falloff

1. `npc 1196 0 0 0`, `npc 1196 3 0 0`, `npc 1196 6 0 0` — three targets at increasing distance from
   one splash centre
2. Fire the splash ability centred on the first
3. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -20`

Pass: damage scales down linearly from `Pointblankrange` to the splash range, and the target caught
by both the direct hit and the splash is only damaged once.

## [ ] D4: `SetWeaponDamage` restores on effect end

1. `npc 1196`, shoot it once, note the damage
2. Apply the ability that overrides weapon damage, shoot again
3. `listeffects` to confirm it's active, then wait for it to expire
4. Shoot a third time
5. `grep -a "damage from" ~/Games/PIN/logs/GameServer.log | tail -10`

Pass: the third number matches the first, so the `OnRemove` restore path in the active-command
pattern actually ran.
