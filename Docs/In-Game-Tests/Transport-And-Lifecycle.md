# Transport and Lifecycle

Part of the [in-game test queue](README.md). Setup and admin commands: [Session Setup](Session-Setup.md).

## HTTP-only transport (blocks everything needing world entry)

Every advertised URL is plain HTTP, which needs a 1-byte patch to `FirefallClient.exe` removing its
HTTPS-only check on the oracle URL. Background and undo steps:
[Http-Only-Setup.md](../Http-Only-Setup.md). Keep it: it is what made login work. It was also
believed to be the world-entry freeze fix, and T4 disproved that — the freeze was a server bug, and
the Wine WinHTTP warnings that pointed at TLS turn out to appear just as often on healthy runs.

Run T1 and T2 before anything else in the queue — a client that can't reach the world can't run
any other entry.

**"The client froze" is not one bug.** T4 fixed one trigger (a server-side scope leak). The
*remaining* freeze — the one that survived T4, T5 and T6 — was finally caught live in T7 and
localised: the **main/render thread wedges inside a DXVK D3D9 call while holding the game lock
`0459FA8C`**, and everything else (the `RtlpWaitForCriticalSection` timeouts, and in earlier runs the
fault storm from the crash handler that follows) is downstream of that. The WinHTTP/TLS and
thread-affinity theories are both dead. A frozen window is just how this client dies from any fatal
stall under Wine: one thread stops holding a lock, the rest pile up behind it, and Wine prints the
60 s timeout. Read `console.log` first, every time — see [Client Logging](Client-Logging.md) — but
the live `/proc` read in T7 is what actually moved this.

### [x] T1: The servers hand out HTTP and nothing else

No client needed. Catches the half-applied case where only one of the two URL sources was changed,
which leaves the client on HTTPS with no visible error until world entry.

1. Start the servers (`~/Desktop/Firefall servers`, or `cd ~/Games/PIN && ./start-pin.sh`).
2. Wait for `All three servers up.`
3. From any terminal:

```
curl -s -X POST http://localhost:4402/api/v1/oracle/ticket | grep -oE '"(ingame_host|clientapi_host)":"[^"]*"'
curl -s "http://localhost:4400/check?environment=production&build=1973" | grep -oE 'https?://localhost:[0-9]+' | sort -u
```

Pass: the first prints `http://localhost:4403` and `http://localhost:4402`; the second prints only
`http://` URLs (4402, 4403, 4407, 4499). Zero occurrences of `https://` in either.

Fail with `https://` still present: the running server is on the old assemblies. Rebuild and
redeploy both — `WebHost.ClientApi.dll` **and** `WebHost.OperatorApi.dll` — then restart. The
oracle ticket's `operator_override` supersedes the capability response, so changing one file and
not the other silently leaves the client on HTTPS.

**Passed 2026-08-11.** Oracle ticket returns `http://localhost:4403` and `http://localhost:4402`;
the capability response returns 4402, 4403, 4407 and 4499, all `http://`. Zero `https://` in either.

### [x] T2: World entry without the freeze

The point of the whole exercise. Needs the patched client.

1. Confirm the patch is in place, in the game directory:

```
cd ~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Firefall/system/bin
python3 -c "print('%02x' % open('FirefallClient.exe','rb').read()[0x4950af])"
```

   Expect `eb`. If it prints `74` the patch is gone — Steam's "verify integrity of game files"
   reverts it. Re-apply per [Http-Only-Setup.md](../Http-Only-Setup.md).

2. Launch Firefall from Steam, log in, pick a character, enter the world.
3. While it loads, from another terminal:

```
ss -tnp 2>/dev/null | grep -E ':(443|443[0-9][0-9])\b' || echo "no TLS connections -- correct"
```

Pass: you reach the world and can move. No hang at `InitializeWebframe`, and the ss check prints
`no TLS connections`.

Fail, client bounces to the login box or logs `Oracle URL [...] not configured for HTTPS`: the exe
patch didn't take. Re-check step 1.

Fail, still freezes at world entry with no TLS connections: the freeze is not the WinHTTP TLS
deadlock after all. Capture `~/.var/app/com.valvesoftware.Steam/steam-227700.log` and the client's
`console.log` before relaunching — the Proton log is overwritten every launch.

**Passed 2026-08-11 18:24, on the fourth consecutive session — see T4, which is the real test.**
This entry failed in between, and the failure is worth keeping: HTTP-only alone did *not* fix the
freeze, and the run that looked like a pass was one lucky session. The actual cause was the server-side
scope leak in T4. Do not treat a single clean world entry as a pass here; it needs several consecutive
runs against one server process.

<details>
<summary>The intermediate failure, 2026-08-11 17:32</summary>

The 17:31:44 session froze 49 s in, and the Proton log has the original deadlock signature with no
TLS anywhere:

- `RtlpWaitForCriticalSection section 0459FA8C ... wait timed out in thread 0278, blocked by 016c,
  retrying (60 sec)`, twice, 60 s apart. Same critical-section address as the pre-patch capture.
- **Thread `016c` is the WinHTTP worker** — it issued 34 of the 35
  `winhttp:request_query_option unimplemented option 38` fixmes. It then faulted
  `c0000005 addr=(nil) ip=00000000` in a tight loop (383,594 fault lines in 3.6 s) and the process
  was torn down.
- **Option 38 (`WINHTTP_OPTION_SERVER_CERT_CONTEXT`) is still queried 35 times over plain HTTP.**
  The client asks for the server certificate on every request regardless of scheme, so dropping TLS
  never took Wine's unimplemented option-38 path out of play. That is the flaw in the original
  reasoning.
- Client side is clean and proves TLS is not involved: 0 `https://`, 0 `not configured for HTTPS`,
  34 requests all status 200, no non-200 at all.
- It got **further** than the pre-patch failure — past `InitializeWebframe` (00:40, FPS 141) to
  `AUDIO: Zone loading complete event sent` (00:49) — and `GameServer.log` confirms a real world
  entry (`Zone 448 Outpost 17` 17:32:20, `SetStatusEffect` / `ClearStatusEffect` 17:32:24-26).
  `console.log` then stops mid-line with `console.log.alive` left behind.

This ran on **GE-Proton11-5**, so that lever is now tested and does not fix it either — its winhttp
still logs option 38 unimplemented.

**Resolved by T4.** Option 38 turned out to be a red herring: the passing 18:18 session logs it 36
times — more than the failing one's 35 — with zero `RtlpWaitForCriticalSection` and zero
`handle_syscall_fault`. Wine's unimplemented option 38 is harmless noise. The WinHTTP deadlock was
downstream of the server writing to a dead connection, not a Wine bug that needed a Wine fix. The
planned `WINEDEBUG=+winhttp,+seh,+tid` capture is no longer needed.

</details>

<details>
<summary>Superseded: the 16:52 run that passed</summary>

**Passed 2026-08-11 — the freeze is gone.** Reached the world, played, and logged out on purpose.
From that session's `console.log`:

- Zero `https://`, zero `not configured for HTTPS`, and no TLS connection to any `443xx` port.
  Every request is `http://localhost:4402` / `:4499`, each completing in 24-28 ms with status 200.
- The FPS dip is still there but it now **recovers**: 143 -> 13 -> 2 across elapsed 00:10-00:11
  during the world-entry HTTP burst, then 134-143 for the remaining four minutes. That dip is a
  load hitch. The old failure was the same drop with no recovery, the main thread parked forever on
  a critical section held by the faulted WinHTTP worker.
- `InitializeWebframe` — where it used to hang — logs at FPS 143 and passes straight through.
- The log ends with the full D3D teardown (`d3dDevice->Release()` ... `ShaderBuilder.Shutdown()`)
  and there is no `console.log.alive` marker, so the client exited cleanly rather than crashing.

One cosmetic leftover, harmless: `WARN NETHTTP Unable to get proxy options even though they were
requested 87` repeats through the burst (87 = `ERROR_INVALID_PARAMETER`), and
`WINHTTP_ENABLE_SSL_REVERT_IMPERSONATION` fails to set twice at startup. Both are Wine WinHTTP
gaps that the client ignores; neither blocked a request.

(Read in hindsight: those "cosmetic" warnings are the client's own report of the failed option-38
query — 87 is `ERROR_INVALID_PARAMETER` — so they were the visible edge of the live bug, not noise.
39 of them in the passing run, 35 in the failing one.)

</details>

### [x] T4: Second client session against one server run

The freeze is not random — it is the *second* client session against a single server process. Found by
hand 2026-08-11 and matched by every log pair on record: server up 16:51:08, client at 16:52:33 fine,
client at 17:32:20 froze; server up 17:41:51, client at 17:42:53 fine, client at 17:51:16 froze.

Cause: `Shard.MigrateOut` removed the player's character entity and its `Clients` entry, but the player
also sits in `EntityManager._scopedPlayersByEntity[…]` for every *other* entity it had scoped in, and
nothing ever took it back out. `FlushViewChangesToScoped` then kept sending view updates to it, because
it gated on `client.Status` — which is never moved off `Playing` on disconnect — instead of
`CanReceiveGSS`, which also checks `NetClientStatus`. Fixed in `Shard.cs` and `EntityManager.cs`.

Deploy the fix first:

```
cd ~/Github/PIN
OPENSSL_ENABLE_SHA1_SIGNATURES=1 OPENSSL_CONF=~/Games/PIN/openssl-legacy.cnf \
    dotnet build UdpHosts/GameServer/GameServer.csproj -c Release
cp UdpHosts/GameServer/bin/Release/net10.0/GameServer.dll ~/Games/PIN/GameServer/
cp UdpHosts/GameServer/bin/Release/net10.0/GameServer.pdb ~/Games/PIN/GameServer/
```

Then:

1. Start the servers: `~/Games/PIN/start-pin.sh`
2. Launch Firefall from Steam, enter the world, move around, quit to desktop.
3. **Leave the servers running.** Launch Firefall again and enter the world.
4. Stay in for at least 60 s — the failures hit at 12 s and 49 s.
5. Repeat steps 2-3 twice more without restarting the servers, so sessions 3 and 4 are covered too.

Pass: every session reaches the world and stays up, and after each quit the server logs
`RECEIVED CloseConnection` with no exception following it.

Fail, session 2 freezes as before: the scope leak was not the whole story. Copy both logs before
relaunching —

```
cp ~/.var/app/com.valvesoftware.Steam/steam-227700.log /tmp/proton-freeze.log
cp "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata/227700/pfx/drive_c/users/steamuser/AppData/Local/Red 5 Studios/Firefall/console.log" /tmp/client-freeze.log
```

— then check whether the stale player is really gone, by confirming the server sends nothing to the
old socket after `RECEIVED CloseConnection`.

Fail, session 1 now breaks too: the `GetScopedPlayers` snapshot changed scope-in behaviour. Check for
`KeyNotFoundException` disappearing into a silent path.

**Passed 2026-08-11 18:24. Four consecutive sessions on one server run (started 18:06:00, on the
18:02:15 build), all clean:**

| session | connected | closed | in-world | client log ends |
|---|---|---|---|---|
| 1 | 18:06:58 | 18:09:09 | 02:19 | D3D teardown |
| 2 | 18:09:48 | 18:14:27 | 04:46 | D3D teardown |
| 3 | 18:14:49 | 18:17:53 | 03:11 | D3D teardown |
| 4 | 18:18:25 | 18:24:36 | 06:19 | D3D teardown |

Sessions 2-4 are precisely the ones that used to die, and they used to die at 00:12 and 00:49. All
four ended with the full D3D teardown, no `console.log.alive` marker was left behind, every
disconnect logged `RECEIVED CloseConnection`, and `GameServer.log` has no exceptions beyond the
known `.pose file did not contain a File section` ones.

The Proton log for session 4 is the decisive part: **0 `RtlpWaitForCriticalSection`, 0
`handle_syscall_fault`** — and 36 `unimplemented option 38`, one more than the session that froze.
That kills option 38 as the cause and confirms the WinHTTP deadlock was a symptom of the server
pumping view updates at a closed connection. The log is 2.2 MB instead of 270 MB, because 270 MB of
it was the fault storm.

That verdict stands, and it is narrower than it first read. It says the *second-session* freeze is
gone. It does not say the client no longer freezes — see T5.

### [x] T5: The battleframe station is not the trigger

Ran 2026-08-11 22:04, **did not reproduce**, and the negative result is worth more than the test was.

The 21:31 freeze was a **first** session on a **fresh** server, 86 s in-world — the shape T4 rules
out. The Proton log was the usual useless one (266 MB, `RtlpWaitForCriticalSection` on the main
thread, a `handle_syscall_fault` storm). `console.log` stopped here, mid-word, three lines after
opening the battleframe station:

```
01:24 INFO  GUI   Issuing UI HTTP request to URL http://localhost:4402/api/v3/garage_slots/battleframes_for_sale
01:26 ERROR GUI   Could not figure out size and format for texture '' (does it exist?).
01:26 ERROR IOS
```

That reads like a smoking gun and is not one. Repeating it — deploy the station, open it, same
chassis 75774, same visual record 11668 — produced the same three lines and then two more, and the
client carried on for another two minutes and exited cleanly:

```
00:26 ERROR   GUI   Could not figure out size and format for texture '' (does it exist?).
00:26 ERROR  IOSYS  Could not open file .r5tex for read: File not found.
00:26 ERROR RENDER  LoadTexture(!) failed. File not found.
```

So the empty texture is **normal**. `BattleFrameTerminal.lua`'s `PaperdollInit()` calls
`gPaperdollInst.GetTexture()` before the paperdoll has one, gets `""`, and the engine dutifully tries
to open `"" + ".r5tex"`. It happens every time the station is opened, on healthy runs too. The crash
run died in the middle of writing the second of those lines, which is why the log ends there and why
the log lock went down with it — but the error burst is a coincidence of *where*, not *why*.

**What the two runs do establish.**

The signature is real, not background noise: the clean run's Proton log is 1.9 MB with **0**
`RtlpWaitForCriticalSection` and **0** `handle_syscall_fault`, against 266 MB and 2.6 million faults
for the freeze.

And the freeze starts earlier than it looks. Anchoring the Proton clock to the client's on
`ThreadIdealProcessor` / `Thread priority/affinity optimization turned ON` (both at client 00:18,
Proton 630024.588), the first lock timeout at 630109.939 prints 60 s into a wait that began at
**00:43** — 43 s before the main thread stopped at 01:26, while the client was still running at
143 FPS and logging normally. Whatever goes wrong, it is not what the last log line is doing. The
client logs nothing at all between 00:27 and 01:00.

**Lead worth taking next, cheap.** At 00:18 the client logs `Thread priority/affinity optimization
turned ON (main thread core 0, priority 1)` — a scheduling asymmetry that could produce exactly this
kind of long one-sided lock wait under Proton. Run to ground in the exe as T6: the knobs turned out
to be `firefall.ini` keys (not cvars, so the `settings.con` survival test suggested here first is
moot), and "pinning the main thread" was a misreading of the log line — the real default behavior is
in T6's table.

This is a lead, not a diagnosis. Two theories have already died on this bug.

### [x] T6: Soak with the thread scheduling "optimization" turned off

**Ran 2026-08-11, four consecutive sessions. The theory is dead — the third to die.** The override
loaded (all four runs, and the freeze run itself, logged `Thread priority/affinity optimization
turned OFF` — confirmed in each `console.log` at ~00:01–00:16). The fourth session froze anyway,
same Proton signature (`RtlpWaitForCriticalSection` on `0459FA8C`). Turning the eviction and the
priority boost off changes nothing, so the scheduling asymmetry was never the cause. Keep the
override in `firefall.ini` regardless — it's harmless and removes one variable — but stop pursuing
it. The freeze that run was **caught live** and dissected; the real localization is in T7 below. The
setup below is still the correct way to run and confirm the override.

T5's closing lead, chased into the binary instead of guessed at. The 00:18 line comes from one
function in `FirefallClient.exe` (va `0x709900`), and its knobs are **`firefall.ini` keys under
`[Engine]`** — read through the same config object as `[Config] OperatorHost` (both call sites load
`lea ecx,[ebx+0xa6c]`), and OperatorHost provably works from `firefall.ini`, so these keys are
confirmed real, not string-adjacency guesses.

What the disassembly says, and where T5's reading was wrong:

| key                     | default | effect                                                              |
| ----------------------- | ------- | ------------------------------------------------------------------- |
| `setMainThreadAffinity` | false   | when true, pin the main thread to `mainThreadCore`                  |
| `setBgThreadAffinity`   | true    | ban every background thread from `mainThreadCore` (mask `~(1<<n)`)  |
| `mainThreadCore`        | 0       | the core in question; `-1` picks one at runtime                     |
| `mainThreadPriority`    | 1       | `SetThreadPriority` on the main thread, clamped to [-15, 15]        |

So by default the client does **not** pin its main thread. `ON (main thread core 0, priority 1)`
means: every worker thread evicted from core 0, main thread raised to above-normal priority, main
thread free to roam. The asymmetry is the eviction plus the priority boost. `OFF` prints only when
both booleans are false.

The override is live in
`~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Firefall/firefall.ini`
(2026-08-11; unknown keys are ignored, so it cannot break a run even if wrong):

```ini
[Engine]
setMainThreadAffinity = false
setBgThreadAffinity = false
mainThreadPriority = 0
```

1. Start the servers (`~/Desktop/Firefall servers`, or `cd ~/Games/PIN && ./start-pin.sh`).
2. Launch Firefall from Steam and log in.
3. At the login screen, confirm the override engaged — this half of the test is deterministic and
   takes one run:

```bash
grep "Thread priority/affinity" "$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata/227700/pfx/drive_c/users/steamuser/AppData/Local/Red 5 Studios/Firefall/console.log"
```

Expected: `Thread priority/affinity optimization turned OFF`. If it still says `ON (...)`, the
section or key casing is wrong — stop and fix that before spending any soak sessions, or the soak
measures nothing.

4. Then soak. The freeze is intermittent with no identified trigger, so this half is statistical:
   play normal sessions — world entry, the battleframe station, the T4 second-session shape. One
   freeze with `OFF` confirmed in that run's log kills the theory. Clean sessions only build
   confidence; T4 needed four in a row before its fix was believed, use the same bar before drawing
   any conclusion here. (This is exactly what happened: four sessions, `OFF` every time, froze on the
   fourth — see the T6 header and T7.)
5. If a freeze does happen, save the evidence before relaunching (the next start overwrites
   `console.log`), following the existing naming in `~/Games/PIN/logs/freezes/`:

```bash
STAMP=$(date +%y%m%d-%H%M)
CLIENTDIR="$HOME/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/compatdata/227700/pfx/drive_c/users/steamuser/AppData/Local/Red 5 Studios/Firefall"
cp "$CLIENTDIR/console.log" ~/Games/PIN/logs/freezes/$STAMP-console.log
cp ~/Games/PIN/logs/GameServer.log ~/Games/PIN/logs/freezes/$STAMP-GameServer.log
tail -c 20000000 ~/.var/app/com.valvesoftware.Steam/steam-227700.log > ~/Games/PIN/logs/freezes/$STAMP-proton-tail.log
```

6. **Do not relaunch yet if the frozen window is still up.** A hung client is the best evidence
   this bug produces — every past capture was a post-mortem log. See T7 for what to pull off the
   live process (`/proc/<pid>/task`, the critical-section dump, the thread walk). The `260811-2237`
   freeze was dissected this way.

### [x] T7: The freeze is a DXVK/D3D9 stall on the main thread, not WinHTTP

The T6 fourth-session freeze was caught **live** — the client left hung on screen instead of killed
— and read out of `/proc` before relaunch. It relocates the bug entirely. Saved:
`~/Games/PIN/logs/freezes/260811-2237-*` (`console.log`, `GameServer.log`, `proton-full.log`,
`thread-analysis.txt`).

**What the live process showed.** The critical section the Proton log always names, `0459FA8C`, is a
real Win32 `RTL_CRITICAL_SECTION`. Its owner field settles the question the logs never could:

```
0x459fa8c:  DebugInfo=0xffffffff  LockCount=1  RecursionCount=1  OwningThread=0x164  Sem=0  Spin=0
```

`OwningThread = 0x164` is the **main thread**. It holds the lock, one waiter queued. And the main
thread is itself parked — syscall 449 (`futex_waitv`, Wine's fsync path) — with an innermost call
chain of `d3d9.dll+0x92c5f` ← `d3d9.dll+0x205a6e` ← … ← the game's main-frame function (returns to
`FirefallClient.exe` va `0x703641`, right after its `call 0x70aba0`). That `d3d9.dll` is **DXVK
v3.0.2** (the file exports `DxvkInst`; the process has live `dxvk-submit`/`dxvk-cs`/`dxvk-queue`
threads). The single waiter, wine tid `0x278`, is a game worker blocked in `RtlpWaitForCriticalSection`
trying to enter that same lock (`FirefallClient.exe` `0xbbe9f6` ← `0xbbd4db` ← `0x12c09e1`).

So the shape is: **the main/render thread wedges inside a DXVK D3D9 call while holding a game lock; a
worker piles up behind that lock; 60 s later Wine prints the timeout.** The critical-section message
is a *downstream* symptom of a graphics-path stall, not a lock bug in the game.

**This kills the WinHTTP theory outright.** This capture has **zero** `handle_syscall_fault` and
**zero** `https` — the 266 MB / 2.6-million-fault storm from earlier captures was the post-freeze
crash handler, not the cause. WinHTTP option-38 still logs 35 times, faulting on nothing. The freeze
happens with the network path completely quiet. (See the superseded
[client-crash memory note](../../MEMORY.md) for that trail.)

**It correlates with heavy streaming.** In every T6 run `console.log` shows RAM climbing 320 MB →
~1050 MB and FPS collapsing 143 → ~24 across the New Eden zone load, and the hang lands in that
window. DXVK is doing resource work (VT tiles, zone assets) when the main thread stops.

**The decisive next test — cheap, one variable.** Force Wine's own D3D9→D3D11/GL path instead of
DXVK and soak again:

- Steam → Firefall → Properties → Launch Options, add `PROTON_USE_WINED3D=1 %command%`.
- If the freeze **vanishes**: it is DXVK-specific — then bisect DXVK (Proton-GE ships a given DXVK;
  try stock Proton 9/10, or drop a newer/older `d3d9.dll` + `dxvk.conf` into the prefix, e.g.
  `dxvk.maxFrameLatency = 1`, `d3d9.deferSurfaceCreation = True`).
- If it **still freezes** on wined3d: the stall is the game's own render thread serialising on
  `0459FA8C` under Wine's D3D scheduling, independent of DXVK — then the lever is the game's
  renderer, not the driver (the `Offset renderer` path at EXE `0x70a4c0` /
  `"Offset renderer requested application shutdown"` is the thing to read next).

Either branch is progress, and this is the first test in the whole investigation aimed at the layer
the evidence actually implicates. Run it the same way as T6 (four-session bar; save any freeze).

Reproducing the live read, for next time a client hangs (don't kill it first):

```bash
PID=$(pgrep -f 'S:\\steamapps\\common\\Firefall.*FirefallClient.exe' | head -1)   # the wine-side pid
# critical-section owner (address from the Proton log's RtlpWaitForCriticalSection line):
gdb -q -p "$PID" -batch -ex 'x/6wx 0x0459FA8C'    # word[3] = OwningThread (wine tid)
# thread states — a wedged main thread sits in futex syscall 449/240:
for t in /proc/$PID/task/*; do echo "$(basename $t) $(cat $t/comm) $(cut -d' ' -f1 $t/syscall 2>/dev/null)"; done
```

Mapping a wine tid (like `0x164`) to a host thread and walking its PE stack is scripted in
`thread-analysis.txt`'s companion work; the short version is: scan `/proc/$PID/mem` for the TEB
whose self-pointer matches, take its `StackBase`/`StackLimit`, and resolve return addresses on that
range against the PEB module list (`FirefallClient.exe` base `0x79890000` → subtract and add
`0x400000` for the on-disk VA).

## Shutdown

### [x] T3: Shutdown leaves nothing behind

`start-pin.sh` used to send SIGTERM once and then `wait` forever, so a server that didn't act on it
kept the window open; closing the window by hand orphaned the servers still holding UDP 25000/25001
and TCP 4400-4411. The next run then died on bind, after truncating the log — which is how
`MatrixServer.log` ended up starting with
`Unhandled exception. System.Net.Sockets.So[13:07:52 INF] Assigning SocketID`.

1. Start the servers and wait for `All three servers up.`
2. Press Ctrl+C in that window.
3. Then:

```
pgrep -af "dotnet (WebHostManager|MatrixServer|GameServer)\.dll" || echo "  no servers"
ss -tln | grep -cE ":(440[0-9]|441[01])\b"
ss -uln | grep -cE ":(25000|25001)\b"
```

Pass: the script prints `==> All servers stopped.`, `pgrep` prints `no servers`, and both `ss`
counts are `0`.

Fail: note which pids survived and whether the script printed
`pid NNNN ignored SIGTERM after 5s -- SIGKILL`. That line means the escalation did its job and the
server genuinely ignores SIGTERM; no line plus survivors means the trap never ran.

Also verify the guard: with the servers running, start the script a second time. It must refuse
with `PIN servers are already running` and exit 1 rather than starting a set that dies on bind.

**Passed 2026-08-11**, though not by a literal Ctrl+C — SIGINT and SIGHUP were sent to the script's
pid from another terminal, which is the same delivery Ctrl+C and closing the window produce. Both
paths stopped all three servers plus the `tail`, released every TCP and UDP port, and printed
`==> All servers stopped.`; the duplicate-run guard refused with exit 1. Nothing needed the SIGKILL
escalation — the servers act on plain SIGTERM in under 3s, which is why a survivor means the trap
never ran rather than that a server ignored the signal.
