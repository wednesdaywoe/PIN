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

## Built 2026-08-15, structured the way the chain walk said

The whole milestone is wiring, as predicted, and the wave design follows the research pass
directly: the thumper's own state machine decides when, and every spawn is a one-shot verb.
Four waves of Melded Aranha stand up on a 20m ring at 15/40/65/90% of drilling progress
(2+0, 2+1, 3+1, 3+2), each split into **sappers** that march on the machine ignoring return fire
and **escorts** that fight what they perceive. Single-species deliberately, and 528 specifically so
no unchecked model renders (the CLIENT-3 rule above stands for whoever varies the waves).

**The reason given for single-species at the time was wrong, though the decision survives it.** It
was written as "Aranha and Chosen are mutually hostile and a mixed wave fights itself, which is
N14's lesson". Checked against `dbcharacter::FactionRelations` on 2026-08-16: 528 is faction 6
("melding"), 1196 is faction 2 ("chosen"), and that pair reads `hostility_stance=2,
hostility_bidirectional=1` — allied, both ways. Zone 448's three standing groups mix them for
exactly that reason. N14's lesson was real but it was about a different set of three creatures.
What still holds is the model rule: 528 and 1196 are the only two creatures ever confirmed to
render correctly, so varying the waves is a CLIENT-3 risk whatever the factions say.

What it took, by the work table:

- **The thumper is a thing that can die.** `ThumperEntity` now implements `IDamageable` with
  retail's own pool (`Health` on the calldown def shipped 4000–5000 on all 61 rows), gets a physics
  body from the beacon's `PosefileId` collision asset, shows its health in the view, and on zero
  transitions to DESTROYED, runs the beacon's death ability, and tells its encounter through a new
  `IDestructionHandler`. Damageable from touchdown to liftoff; a LEAVING thumper has already won.
- **The AI can attack a non-character.** Target selection, sightline, combat and movement all
  resolved targets as `CharacterEntity`; they now resolve `IDamageable`. An NPC can carry an
  *objective* (`CharacterEntity.ObjectiveId` + `ObjectiveFirst`), which comes in beside the threat
  table rather than through it, because threat only accumulates on characters. Sappers put the
  objective first; escorts use it as a fallback. Structures are aimed at 0.7m rather than chest
  height, because a missing collision asset falls back to a 0.9m sphere at the base and a
  chest-height shot clears it.
- **Deaths route to the owning encounter.** `EncounterManager` subscribes to M2's
  `CharacterDiedEvent` and dispatches to a new `IDeathHandler` when the victim's
  `EncounterComponent` carries the new `Event.Death` flag — the same addressing interactions
  already use. `KillRewardSim` is untouched; the encounter is a second subscriber.
- **Failure exists.** `Thumper.OnFailure` sends the completion event with `Destroyed = 1` and
  nothing aboard, despawns the wave, and leaves the wreck 6 seconds to be seen. Both exits are
  guarded so success and failure can't both fire.
- **The payout reads the defence.** Yield is now `completion × defence`, where defence runs
  linearly from 0.5 at zero health to 1.0 untouched (`Thumper.DefenceMultiplier`, unit-tested).
  The curve and the wave schedule are invented in the DATA-10 sense; the health pool is not.

What no offline test can settle is sized in the new test stream,
[Thumper-Defence (F1–F6)](../../Game Testing/Thumper-Defence.html): whether the beacon's collision
asset loads in the deployment (F3's `fallback shape` grep), and whether Aranha claws against a
4000-point pool make the failure path reachable at all (F3/F5 record the DPS reading that tunes
the schedule).

Exit unchanged and now testable end to end: call one down, defend it against waves that get
harder, and either extract with the resources or lose them.

## Closed 2026-08-15, 6 of 6, the same morning

[Thumper-Defence F1–F6](../../Game Testing/Thumper-Defence.html) all passed in one sitting, and the
exit condition happened in both directions: the tester *lost* the first defended cycle at 91% (the
F3 observation window plus wave 4's four-second kill of the remaining pool), then won the re-run
14 kills for 14 spawns at `completion 1.00, defence 0.63`, ledger balanced to the unit. Both
offline unknowns resolved: the collision asset loads, and the waves are lethal. The sitting's
lasting product is the first balance reading — 49 per claw at ~1 hit/s/sapper against 4000 means
a flawless solo defence keeps ~63% — recorded in the F-stream with the melee-reach and
grenade-AoE observations, all deferred to a design pass. This closed the last milestone in
[PROGRESS](../PROGRESS.md).
