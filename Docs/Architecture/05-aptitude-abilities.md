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
(`NextStatusEffectChangeTime`). The client dedupes on that field, so two changes to the same slot
in the same millisecond means it drops the second one. That's what the Charge camera lock was: the
remove-then-reapply chain reused a slot within a millisecond, the client ignored the follow-up, and
the aim lock never released. Watch for it whenever a chain removes an effect and immediately
applies another.

`AddEffect` also echoes `Context.InitTime` (the client's activation time) into the netfield rather
than server time, because the client has already locally predicted the apply and will keep two
copies if they don't match.

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

## Entry points

| Entry point | Trigger |
|-------------|---------|
| `HandleActivateAbility` | Client `ActivateAbility` via `Character/CombatController` |
| `HandleLocalProximityAbilitySuccess` | Client reports a proximity command fired |
| `DoApplyEffect` / `DoRemoveEffect` | Chains applying effects to targets; also admin `/effect` commands |
| `AbilitySystem.Tick` | Duration and update chains |

Calldown requests (vehicles, deployables, thumpers) are a two-phase handshake: the client sends a
request that `AbilitySystem` parks in a dictionary, and the chain's calldown command later consumes
it with `TryConsume…CalldownRequest`. An unconsumed request is discarded when a new one arrives.

`HandleTargetAbility`, `HandleDeactivateAbility`, and `HandleActivateConsumable` still throw
`NotImplementedException`; ability deactivation is currently handled in
`Controllers/Character/CombatController.DeactivateAbility` by clearing the effects the ability
applied.
