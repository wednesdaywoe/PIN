# EffectSweep

Answers one question about a `clientdb.sd2`: which status effects can the client predict and then
never get rid of on its own?

The question exists because of effect 15253 (Charge's camera lock,
`Docs/In-Game-Tests/Charge-Camera.md` D5).
The client predicts effects at keypress when the apply command has `allow_prediction=1`, and a
predicted copy ends one of two ways: its duration chain expires client-side, or an external
removal reconciles it through `LocalEffectsController`. PIN didn't write that controller until
2026-08-10, so every predicted effect without a self-terminating duration was a permanent
client-side ghost. This tool finds the whole class so each member can be re-tested against the
fix — the test entries live in `Docs/In-Game-Tests/Prediction-Sweep.md`.

## How it decides

The walk mirrors the client's predictor rather than the server's executor. From every
`apt::AbilityData` root chain it follows `Next` links and every chain-valued command parameter,
skipping `env=server` commands outright (the client never runs them — which is also why an
external `ImpactRemoveEffect` can't save a prediction). An `ImpactApplyEffect` or
`ImpactToggleEffect` with `allow_prediction=1` records its effect and recurses into that effect's
own four chains, since a predicted copy runs those client-side too — 15252's apply chain is what
predicts 15253.

An effect stays in the report when nothing in its duration chain is time-based
(`TimeDuration`-style commands self-expire and heal the prediction). The report tags what the
duration chain waits on instead:

| Tag | The predicted copy lives until |
|-----|-------------------------------|
| `SERVERCONFIRMED` | the server says so — `apttf_serverconfirmed_scf`, the reconciliation channel itself |
| `FRAME` | a battleframe switch |
| `RESPAWN` | death |
| `CSTATE` | a character-state check flips, which for `living` is never (15253's shape) |
| `NONE` | nothing; there is no duration chain at all |

Effects only reachable from abilities with no `AbilityModule` are reported separately: no player
can slot them, so no client ever predicts them.

## Running it

```
OPENSSL_ENABLE_SHA1_SIGNATURES=1 dotnet build Tools/EffectSweep/EffectSweep.csproj
cd Tools/EffectSweep && dotnet bin/Debug/net10.0/EffectSweep.dll
```

`config.json` (copy `config.example.json`) points `input` at the retail `clientdb.sd2` — the
original, not a pruned one. Output is `candidates.md` and `candidates.json` in the working
directory, one entry per effect with its duration chain spelled out, the apply commands with
their to-self/to-target flags, and per ability one `AbilityModule` item id, which is the
`createitem` id that puts the ability in reach of a real predicted keypress.

The tool validates itself on the known case: if 15253 doesn't fall out of the walk under ability
35366, it prints `VALIDATION FAILED` and exits 3.

## Blind spot

Client-env command types have no loaded def table server-side, so sub-chains referenced by their
parameters are invisible to the walk (the run prints how many such commands it skipped). A
predicted apply hiding behind a client-only logic command would be missed. Every apply reachable
through `both`-env commands — which is where gameplay effects live — is covered.
