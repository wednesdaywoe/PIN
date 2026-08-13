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

## Retail's encounter scripts are readable, with the parameters blanked

Found on 2026-08-13 while establishing that spawn placements are unrecoverable
([DATA-15](../gaps/data.md#data-15)); the detail is under [DATA-5](../gaps/data.md#data-5). Firefall
populated a zone by running aptitude chains, the same engine abilities use, and the chains shipped:
`apt::BaseCommandDef` holds 143,498 steps, each naming a command type and the next step. 18,025 of
them call a server-side command whose parameter table never reached the client, including 297 NPC
spawns, 252 spawn-table activations and 884 encounter signals.

That is worth more here than anywhere else in the project. This milestone has to invent what a wave
looks like, and it doesn't have to invent it from nothing: the skeleton says how many spawn steps a
real encounter used, in what order, and which signals fired between them. Only what each step
spawned is missing.

Nothing needs writing yet. The thing to do before designing waves from scratch is walk the chains
that call `agsEncounterSignalCommandDef` and `agsActivateSpawnTableCommandDef` and read the shape.

Small once M2 and M3 exist, and almost entirely blocked on them. Worth resisting the urge to start
here, since an encounter with no combat in it is what already exists.
