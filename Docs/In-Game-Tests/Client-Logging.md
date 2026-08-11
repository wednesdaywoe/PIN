# Client Logging (D5g)

Part of the [in-game test queue](README.md). Written during the
[Charge camera investigation](Charge-Camera.md); it is now the setup step for every entry in the
[Prediction Sweep](Prediction-Sweep.md), so it lives on its own.

## D5g. Read the answer off the client instead of guessing at it

The client is instrumentable and nobody had looked. [D5's status note](Charge-Camera.md) says a
packet capture is the only thing that settles the prediction contract, and that a capture means an
archive from 2016 that we may not be able to get. That's now half wrong: the client can log every
game protocol message it receives, to a file, on demand.

**How the client is configured.** Everything lives in
`~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata/227700/pfx/drive_c/users/steamuser/AppData/Local/Red 5 Studios/Firefall/`.
`settings.con` there is exec'd at startup and takes `SetSave <cvar> "<value>"` lines. The cvar names
and their help text are plain ASCII in `system/bin/FirefallClient.exe`, so `strings` finds them.

There's a console too, but it's gated behind a `.wedge` dev file we don't have and has no default
keybind, so cvars can't be typed at runtime. `settings.con` is the only way in.

Two things to know before trusting an edit to it. The game rewrites the file alphabetically on exit
and silently drops cvars it doesn't recognise, so check your lines are still there before each run.
That drop is also a free existence test: a name that survives a rewrite is a real registered cvar.
And the live log is `console.log`; it only becomes `<timestamp>_last_run.log` at the *next* startup,
so read `console.log` for the run that just ended.

**The one that matters.**

```
SetSave gameproto.logGssMessages "1"      // "Enables logging on every single direct game protocol message."
```

That's the capture, from the receiving end, tagged on the `GSS` log channel. It won't show what the
2016 server sent, but it shows exactly what the client got from PIN and in what order, which is the
half of the contract we've been reconstructing by hand from PIN's own source. Expect a large log and
keep the test session short.

There may also be an ini route. `FirefallClient.exe` reads keys of the form `LogLevel-<channel>` for
channels `GssMessages`, `MatrixMessages`, `FirefallCommands` and `FirefallEvents`, and the section
name looks like `[Debug]`. That's inferred from string adjacency, not confirmed, but it's been added
to `firefall.ini` (backup at `firefall.ini.bak`) since unknown ini keys are ignored either way.
`FirefallCommands` and `FirefallEvents` have no cvar equivalent, so if the section name is right
they're the only way to get at those.

**Two more cvars, one useful, one not.**

`debug.playerCamera` ("Print debug text for the player's camera") is real: it survived a rewrite. It
produces no log lines at all, so it draws on screen. Worth having on while testing, since the stuck
object is a camera, but you have to be looking at the screen to use it.

`statuseffects.showall` ("Show all status effects (including hidden) to the UI") was dropped by the
rewrite, so it isn't reachable this way. No great loss: 15253 has `expose_to_ui=0`, `icon_id=0`, and
its `name_id` 960112 has no row in `dblocalization::LocalizedText`, so it has no name or icon to
show.

**First attempt, 2026-08-10: the cvar never took.** A 40 second run in New Eden produced zero `GSS`
lines, and `gameproto.logGssMessages` was gone from `settings.con` afterwards. `SetSave` on a name
the client doesn't know yet is silently discarded, and `settings.con` is exec'd during early
initialization, before the game protocol layer registers its cvars. Only `debug.playerCamera`
survived, which is why that one worked: it's registered by then.

**The way around it is `bind`.** `bind` is a console command, `settings.con` is a console script, and
a bind only stores a string. The key gets pressed later, in-game, when the cvar does exist. Valid key
names come from the client's own table: `f1`–`f12`, `ctrl`, `shift`, `alt`, `grave`, `tab`, `escape`,
`space`, `uparrow`, `mouse1`–`mouse9`, `kp_*`, `gp1_*`.

The same trick answers the naming question outright. `cvarlist` lists cvars, `grep <match> <command>`
filters another command's output, and `grep.print_to_log 1` sends that output to the log instead of
the console we can't open. So the client will hand over its own authoritative cvar names.

Binds do persist. `SetSave eng.autoSaveConfig "0"` does not stick, so the game still rewrites
`settings.con` on exit, but it re-serialises the binds along with the `SetSave` lines for cvars it
knows. Everything else in the file is dropped, so bare console commands have to be wrapped in a bind
to survive.

**Second attempt, 2026-08-10: binds persisted, still nothing in the log.** f6/f7/f8 produced no
output. Two more things turned up that probably explain it:

```
SetLogLevel <logname> <level>    levels: debug, info, warn, error, nothing
ListLogs                         prints every log channel and its current level
cvarlist                         confirmed, an alias of ListVars
```

No log file in that folder contains a single `DEBUG` line, on any channel, ever. Debug output is
filtered out by default, so a protocol logger writing at debug level would produce exactly what we
saw. The channel is probably `gss`, going by the `GSS` tag next to `gameproto.logGssMessages` in the
binary and the usage text's lowercase examples ("guilib, toolshr, render").

**Third attempt, 2026-08-10: this one worked, and it settles the tooling.** Binds fire (f5 blanked
the FoV box, f6 brought it back), `grep.print_to_log` really does route console output into
`console.log`, and the client answered both questions itself:

- `Unknown command "gameproto.logGssMessages"`. That cvar does not exist. The string in the binary is
  dead. Two runs were spent on it.
- `Unrecognized log name 'gss'`. There's no channel by that name either.

`ListLogs` gave the real list, every channel at `INFO` except `AI` at `DEBUG` and `STEAM` at `ERROR`:

```
ITEMS  IOSYS  SDB    CONSOLE TSANIM TOOLSHR VCS   FX     SNDLOOP SNDACT SOUND
Aptitud  GT   VT     RENDER HAVOK  ENHAVOK ENTROPY HAVOK JSCRIPT GUI    SCRIPT
INPUT  NETWORK NETHTTP ANIMATE AI   PROTO  AUDASST PROTO STEAM  GAME
```

`Aptitud` is the prize, and it's better than the packet capture this section has been chasing. It's
the client's own ability engine, so raising it to debug should log the client's own chain execution:
the predicted copy of 15253, which is the exact thing we've spent seven attempts inferring from the
server side. `PROTO` is the protocol channel, appearing twice, so a name lookup may only reach the
first of the two. `VCS` is the component system that owns `StatusEffectComponentDef`.

The name column is truncated to seven characters, so `Aptitud` is presumably `Aptitude`. Both
spellings are bound; an unrecognised one logs an error and the rest of the line still runs.

```
bind f5 "debug.playerCamera 0"
bind f6 "debug.playerCamera 1"
bind f7 "AlwaysFlushConsole 1; SetLogLevel Aptitude debug; SetLogLevel Aptitud debug; SetLogLevel PROTO debug; SetLogLevel VCS debug"
bind f8 "grep.print_to_log 1; grep s ListLogs"
```

## D5g result: the mechanism, straight from the client

`SetLogLevel Aptitud debug` took (the channel name really is seven characters; `Aptitude` is
rejected) and produced 536 debug lines. `PROTO` accepted the level change on both of its entries and
then logged nothing, so there's no debug output behind that channel.

The whole Charge, client side:

```
00:32  Successfully applied effect 15253
00:32  Successfully applied effect 15252
00:32  Unable to apply status effect 15252: too many stacks (x1)
00:32  Unable to apply status effect 15253: too many stacks (x1)
00:32  Successfully applied effect 15456
00:32  Successfully applied effect 15215
00:32  Successfully removed effect 15252   + Canceled
00:32  Unable to apply status effect 15215: too many stacks (x1)
00:32  Unable to apply status effect 15456: too many stacks (x1)
00:33  applied / removed / Canceled 15216  (twice)
00:33  Successfully removed effect 15215   + Canceled
00:36  Successfully removed effect 15456   + Canceled
```

Two facts fall out, and together they're the whole bug.

**Every effect is applied twice and the second is rejected.** The client predicts the effect at
keypress, the server's replicated copy arrives a moment later, and the client refuses it as a
duplicate because `max_stack_count=1`. That happens to 15252, 15215, 15456 and 15253 alike, so the
double-apply on its own is not the fault.

**15253 is never removed.** It appears exactly twice in the entire log, the apply and the rejected
duplicate, and that's all. Every other effect in the chain gets a matching
`Successfully removed` + `Canceled` pair. What makes it different is where those removals come from:
each one is the client's own predicted copy expiring on its own duration chain. 15253 has
`remove_chain=0` and a duration chain of `RequireCState(living)`, which never goes false while you're
alive, so its predicted copy has no way to end itself. Its only removal is external, and the external
one is aimed at a replicated instance the client rejected and never bound. So nothing removes it, and
the camera stays clamped at 0/0 until you leave the zone.

The apply command is `1593262`, in effect 15252's chain, with `allow_prediction=1` and
`remove_on_rollback=0`. The removal is `1635135` in PIN's `aptgss_agsImpactRemoveEffectCommandDef.json`,
one of the 15 hand-authored entries, commented "guess based on captures". It clears the server's slot
correctly, which is exactly why D5d looked clean: with no prediction there's nothing left behind.

**The Glider does not remove the ghost.** The burst of cancels at 00:40 to 00:44 when the Glider is
boarded covers a dozen effects and 15253 is not among them. It's displacement by a competing
`CustomPlayerCamera`, not removal. That settles the open question the Charge investigation had been
carrying: the ghost stays underneath, and applying 15257 at Charge's end would mask the symptom the
same way without fixing anything.

**Lead worth checking next.** `Lib/AeroMessages/.../Character/Controller/LocalEffectsController.cs`
is GSS controller 6, 64 slots of `LocalEffectsData { EntityId Entity, uint Effect, uint Time }`, and
PIN never writes a single one of them. It's an owner-private effect array, which is the natural place
for a server to drive the effects the owning client predicts, separate from the `StatusEffects_N`
view that every observer sees. That's a hypothesis, not a finding, but it's now cheap to test:
the Aptitude channel shows the client's side directly.

That lead is [D5h](Charge-Camera.md), and it was the bug.

Other cvars found in the same sweep, not yet used: `debug.logDamageDealtEvents` and
`debug.logDamageTakenEvents` (log every damage event the local character deals or takes, a free check
on the damage work), and the `debuglag.*` family, which draws a network diagnostic overlay including
GSS receive counts and clock deltas.
