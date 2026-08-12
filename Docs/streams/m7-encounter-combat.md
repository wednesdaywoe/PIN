---
project: pin
kind: stream
title: "M7: An Encounter That Plays"
relates:
  - ../PROGRESS.md
---

# M7: An Encounter That Plays

The encounter framework is real and three encounters use it, but none of them involve combat.
`Thumper` is a timed state machine that advances on interactions and fires abilities at each
transition. Nothing spawns to attack it and nothing checks whether the players are still alive.

With M2 done this becomes wiring rather than new systems: spawn waves on the state transitions
that already exist, subscribe to the death event, and let the encounter fail as well as succeed.

| Work | Where |
|------|-------|
| Spawn waves keyed to encounter state | [Thumper.cs](../../UdpHosts/GameServer/Systems/Encounters/Encounters/Thumper.cs), [EncounterManager.cs](../../UdpHosts/GameServer/Systems/Encounters/EncounterManager.cs) |
| Route deaths back to the owning encounter | `EncounterComponent`, the M2 death event |
| A failure path, not just `OnSuccess` | [BaseEncounter.cs](../../UdpHosts/GameServer/Systems/Encounters/BaseEncounter.cs) |
| Scale the payout to how well the defence went, on top of whatever M4 says the ground holds | as above |

Exit: call down a thumper, defend it against waves that get harder, and either extract with the
resources or lose them.

Small once M2 and M3 exist, and almost entirely blocked on them. Worth resisting the urge to start
here, since an encounter with no combat in it is what already exists.
