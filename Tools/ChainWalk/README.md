# ChainWalk

Reads the shape of retail's aptitude scripts out of `apt::BaseCommandDef`. Every step in that
table carries only an id, a command type and a pointer to the next step, and the parameter tables
for server-side commands never shipped, so a chain reads as structure with blank leaves. This tool
finds every chain containing a population or encounter command (spawns, spawn-table activations,
encounter signals, loot drops), walks each one end to end, and reports the shapes.

```
ChainWalk <clientdb.sd2> [maxExamples]
```

Run it against the original `clientdb.sd2`, not a pruned one. Command names come from
[CommandType.cs](../../UdpHosts/GameServer/Systems/Aptitude/CommandType.cs), parsed at startup,
because the db's own `apt::CommandType` leaves the name columns blank on every server-side
command.

It reports, in order: how many chains touch the population commands and how long they run, which
commands appear alongside them, the most common whole-chain shapes, whether any chain both spawns
and signals, the longest chains, and how many spawn steps a single chain carries.

Two limits worth knowing. The walk is linear: `ConditionalBranch` and `Call` jumps aren't
followed, which understates the length of the few chains that use them (48 of 2157 contain a
branch). And a step with several predecessors stops the backwalk at the merge rather than picking
a side; the report says how many chains start at one (zero, in the 1962 db).

The finding this produced is written up in
[streams/m7-encounter-combat.md](../../Docs/streams/m7-encounter-combat.md): retail kept wave
sequencing out of the chains entirely. Chains are one-shot verbs, and the orchestration lived in
the server-side encounter logic that never shipped.
