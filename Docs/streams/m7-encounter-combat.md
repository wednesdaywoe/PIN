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

## The chains were walked, and the shape answer is a design answer

Run 2026-08-14 with [Tools/ChainWalk](../../Tools/ChainWalk/), which walks every
`apt::BaseCommandDef` chain containing a population or encounter command and reports the shapes.
2,157 chains qualify. Median length is 3 steps, the longest is 19, and none starts at an ambiguous
merge point.

The finding that matters: retail did not script waves inside the chains. Across all 2,157, exactly
2 both spawn something and raise an encounter signal, and 530 of the 552 chains that spawn at all
carry exactly one spawn step. The most common shapes are single verbs: a bare `EncounterSignal`
(292 chains), a bare `DeployableSpawn` (95), a bare `SpawnLoot` (88), a bare `ActivateSpawnTable`
(84), a bare `NPCSpawn` (46). The recurring spawn pattern is `TargetClear > NPCSpawn >
ImpactApplyEffect` (39 chains), a spawn with an arrival effect. So a chain is a one-shot verb, and
the sequencing that turned verbs into an encounter lived in the server-side encounter logic that
never shipped.

That is an endorsement of the architecture PIN already has. `Thumper` is a state machine that
fires abilities at transitions, which is exactly the relationship the retail data shows: the state
machine decides when, the chain does one thing. Waves for this milestone should be built the same
way, with the encounter's own state driving spawn calls, not as a script chain that spawns, waits
and spawns again. No such chain exists in the shipped data to copy.

Two smaller readings from the same run. `StatRequirement > EncounterSignal` appears 43 times, a
signal gated on a stat check, which is the natural shape for a failure path (health hits zero,
signal the encounter). And one 19-step chain is nothing but `ActivateSpawnTable` eighteen more
times, which reads as a zone's population being switched on in one stroke.

The command names come from the server's own
[CommandType.cs](../../UdpHosts/GameServer/Systems/Aptitude/CommandType.cs) enum, because the
client db's `apt::CommandType` leaves the name columns blank on every server-side command. The
walk is linear and doesn't follow `ConditionalBranch` or `Call` jumps; only 48 of the 2,157
chains contain a branch, so the shapes above are read from whole chains, not fragments.

Small once M2 and M3 exist, and almost entirely blocked on them. Worth resisting the urge to start
here, since an encounter with no combat in it is what already exists.

One standing check for wave authoring: every creature type a wave introduces is a creature type
nobody has seen render, and one of the only two so far faces 90° sideways
([CLIENT-3](../gaps/client.md#client-3)). Look at each new monster's facing the first time it
stands in a session — the observation is free then and unrecoverable later.
