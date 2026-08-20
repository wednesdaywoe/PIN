# Layer 5: Aptitude (Abilities & Effects)

"Aptitude" is Firefall's ability scripting system. Everything an ability does is just a chain of
commands with no hardcoded logic. There's an interpreter and a command library.

Code: [Systems/Aptitude](../../UdpHosts/GameServer/Systems/Aptitude).

## The model

| Concept | Type | Meaning |
|---------|------|---------|
| Ability | SDB `AbilityData` | Names a chain to run |
| Chain | [Chain.cs](../../UdpHosts/GameServer/Systems/Aptitude/Chain.cs) | An ordered list of commands, linked in SDB via `BaseCommandDef.Next` |
| Command | [ICommand.cs](../../UdpHosts/GameServer/Systems/Aptitude/ICommand.cs) | One operation; returns `bool` |
| Effect | [Effect.cs](../../UdpHosts/GameServer/Systems/Aptitude/Effect.cs) | A status effect: up to four chains (apply / duration / update / remove) |
| Context | [Context.cs](../../UdpHosts/GameServer/Systems/Aptitude/Context.cs) | The mutable execution state a chain threads through its commands |

### Chains are boolean expressions

`Chain.Execute` runs commands in order under one of two policies:

- **AndChain** (default): stops at the first `false`. The chain result is `true` only if every
  command succeeded.
- **OrChain**: stops at the first `true`.

This is why requirement/targeting commands return `false` to abort: a chain like
*TargetPBAE → RequireHostile → InflictDamage* stops cleanly when nothing valid is in range.

### Context is the register machine

A `Context` carries:

- `Initiator` (who started it) and `Self` (who the current step is about). These diverge once an
  effect is applied to someone else.
- `Targets` / `FormerTargets`: a stack-ish list that targeting commands push to and damage
  commands consume
- `Register` / `FormerRegister`: a single `float` accumulator. Commands combine their SDB
  parameter with the register through `AbilitySystem.RegistryOp(register, value, op)`, supporting
  assign/add/multiply/subtract/divide/min/max/exponentiate. `NaN` in the register means "unset",
  and returns the incoming value.
- `InitTime`, `InitPosition`: captured at chain start, used for splash origins and for echoing
  the client's activation time back in status-effect fields
- `ExecutionId`: a `Guid` pushed into the Serilog `LogContext`, so every log line from one ability
  activation shares an `ExecutionId`. It's the most useful debugging tool in this system.
- `ExecutionHint`: why this chain is running ([ExecutionHint.cs](../../UdpHosts/GameServer/Systems/Aptitude/ExecutionHint.cs)); also
  suppresses debug logging for the high-frequency duration/update chains.
- `Actives`: commands that registered themselves as "active" so they get `OnApply`/`OnRemove`
  callbacks when the owning effect is applied and removed.

`Context.CopyContext` is used when applying an effect to a target so the target's chains get their
own `Self` without disturbing the caller's.

## Effects and their lifetime

`AbilitySystem.DoApplyEffect(effectId, target, context)`:

1. Copy the context, set `Self = target`.
2. `Factory.LoadEffect` builds the `Effect` (its four chains) from SDB.
3. `target.AddEffect(...)` puts it in one of the entity's 32 slots
   ([BaseAptitudeEntity.cs](../../UdpHosts/GameServer/Entities/BaseAptitudeEntity.cs)) and replicates a
   `StatusEffectData` netfield. Re-applying an already-present effect increments `Stacks` up to
   `MaxStackCount`; exceeding it returns `MaxStacksExceeded` and the apply chain doesn't run.
4. The apply chain executes, then every registered active command gets `OnApply`.

`AbilitySystem.Tick` (every 20ms) walks each aptitude entity's active effects and, for effects
that have a duration chain and are past `UpdateFrequency`:

- runs the duration chain, whose boolean result is the effect's "should I still exist?"
- if `true` and an update chain exists, runs the update chain
- if `false`, `DoRemoveEffect` clears the slot, runs the remove chain, fires `OnRemove`

So duration chains decide expiry and update chains do periodic work. An effect with no duration
chain never expires on its own.

### The status-effect change-time trap

`AddEffect`/`ClearEffect` call `Shard.EntityMan.FlushChanges` immediately rather than waiting for
the periodic flush, and stamp the netfield with a strictly-increasing 16-bit change time
(`NextStatusEffectChangeTime`). A chain that clears a slot and refills it inside one millisecond
would otherwise repeat the change time, and a client treating that field as a sequence number would
drop the second transition, so the stamp is kept strictly increasing per entity. This was written
believing it explained the Charge camera lock; it did not fix it, and the ordering it guarantees has
never been shown to be load-bearing. Keep it as a cheap invariant, not as a diagnosis.

`AddEffect` stamps the netfield `Time` with `Context.InitTime`, falling back to `Shard.CurrentTime`
for effects applied outside a chain, and sends `Stacks` in `Stack` where it used to send 0. Both
fields exist so the client can match an effect it predicted itself against the one the server
replicates, and getting that match wrong is the leading explanation for Charge's stuck camera
(D5d/D5e in [In-Game-Tests](../In-Game-Tests/Charge-Camera.html)).

Echoing `InitTime` was tried and reverted twice before, on the grounds that it's the client's clock
and shard time is something else. That was wrong. `Shard.CurrentTime` is unix epoch milliseconds
truncated to uint32, which is the same clock the client stamps activations with, and the "~23000"
that looked like a mismatch was a `CurrentShortTime`, the low 16 bits, held up against a full uint.
The real caveat is different and still stands: an effect applied later in a chain inherits the
activation time rather than its own, because `ImpactApplyEffectCommand` copies `InitTime` down. For a
client reconciling a whole predicted activation that's arguably what it wants, but it hasn't been
shown.

## The command library

[Systems/Aptitude/Commands](../../UdpHosts/GameServer/Systems/Aptitude/Commands), grouped by
domain: `Damage`, `Movement`, `Effect`, `Target`, `Requirement`, `Register`, `Logic`, `Calldown`,
`Deployable`, `Encounter`, `NPC`, `Interaction`, `Hostility`, `Modifier`, `Impact`, `Self`, …

Roughly 121 implemented commands, plus 197 stubs in `Todo/` subfolders. A stub parses its SDB
definition and returns `true`:

```csharp
public bool Execute(Context context)
{
    return true;
}
```

They exist so an unimplemented step doesn't abort a whole chain.

### Wiring a command

[Factory.cs](../../UdpHosts/GameServer/Systems/Aptitude/Factory.cs) maps `CommandType` → concrete
command in one large switch (~100 live cases, ~233 commented out). Commands whose environment is
`client` short-circuit to `CustomNOOPCommand` because the server doesn't need them.

Implementing a `Todo` command therefore takes three steps:

1. Fill in `Execute` (and `OnApply`/`OnRemove` if it needs to survive as an active).
2. Uncomment its `case` in `Factory.LoadCommand`.
3. Move the file out of the `Todo/` folder and fix its namespace.

If the command's definition table isn't in SDB, it lives in
[StaticDB/CustomData](../../UdpHosts/GameServer/StaticDB/CustomData) as JSON and is read through
`CustomDBInterface` instead of `SDBInterface`; see [layer 8](08-static-data.md).

### Server-environment commands run on invented parameters

`clientdb.sd2` ships the client's own command tables. An `env=server` command appears in a chain by
id and type with no parameters at all, because the client was never given them. Every `aptgss_`
command is in that state, so its behaviour is whatever someone hand-authored in `CustomData`.

Most of it isn't authored. `agsImpactRemoveEffectCommandDef.json` has 3812 entries and 3797 carry
nothing but an id and a comment, which means they parse, execute, and remove nothing. The 15 filled-in
ones were reconstructed from captures a bug at a time, and say so: Charge's is commented "guess based
on captures". This is worth knowing before concluding that a chain does what it looks like it does. A
removal that never happens looks identical in the log to a chain that had no removal in it.

Charge's own windup is an example. Effect 15252's apply chain ends with `aptgss_impactremeffectcmd_ire`
1593257, which is one of the blanks, so nobody knows what the real server removed there.

## Entry points

| Entry point | Trigger |
|-------------|---------|
| `HandleActivateAbility` | Client `ActivateAbility` via `Character/CombatController` |
| `HandleLocalProximityAbilitySuccess` | Client reports a proximity command fired |
| `DoApplyEffect` / `DoRemoveEffect` | Chains applying effects to targets; also the `applyeffect` / `removeeffect` admin commands |
| `AbilitySystem.Tick` | Duration and update chains |

Calldown requests (vehicles, deployables, thumpers) are a two-phase handshake: the client sends a
request that `AbilitySystem` parks in a dictionary, and the chain's calldown command later consumes
it with `TryConsume…CalldownRequest`. An unconsumed request is discarded when a new one arrives.

`HandleTargetAbility`, `HandleDeactivateAbility`, and `HandleActivateConsumable` still throw
`NotImplementedException`; ability deactivation is currently handled in
`Controllers/Character/CombatController.DeactivateAbility` by clearing the effects the ability
applied.
