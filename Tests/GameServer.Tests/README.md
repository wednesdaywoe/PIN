# GameServer.Tests

Offline coverage for the parts of the server that are pure functions over their inputs. xUnit, no
`clientdb.sd2`, no client, no shard.

```
dotnet test PIN.sln
dotnet test Tests/GameServer.Tests/GameServer.Tests.csproj
```

## What belongs here

Logic that can be reached without standing up a `Shard`. In practice that means the maths: damage
curves, register operations, the spread PRNG, guid packing, and the parts of the hostility rules that
answer before the faction table is consulted.

A lot of the server's interesting behaviour is currently welded to entities and tick loops and can't
be reached from here. Lifting a calculation out into something callable is usually the first half of
writing the test, and is worth doing when the calculation is subtle rather than for its own sake.

## What these tests can and can't tell you

Several of the models they cover are reverse-engineering guesses: the range decay curve, the splash
curve, the faction stance encoding. A test pins what the code was meant to do. Whether that matches
what Firefall actually did is a question only the client can answer, and those checks live in
[Docs/In-Game-Tests](../../Docs/In-Game-Tests/README.md).

The split is worth keeping deliberate. When an in-game check fails, the tests are what tell you the
model is wrong rather than the implementation of it.

`SpreadTests.ReproducesACapturedClientShot` is the exception that goes further: its inputs and
expected direction were captured from the real client, so it does check our PRNG against Firefall's
and not just against itself.
