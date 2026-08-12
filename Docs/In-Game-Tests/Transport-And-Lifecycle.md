---
project: pin
kind: test-stream
title: "Transport and Lifecycle (T1-T9)"
relates:
  - ../TEST-REGISTER.md
---

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
*remaining* freeze — the one that survived T4, T5 and T6 — was caught live twice and localised, and
each live read moved it. T7 (a DXVK run) put it on the **render thread, holding a game lock, wedged in
the D3D present path**. T8 (a wined3d run) then swapped DXVK out entirely and it **froze identically**,
which exonerates the GPU driver and pushes the wedge one layer down: the render thread is parked in
**`RtlEnterCriticalSection` on the CRT/NT heap lock**, reached from a D3D texture upload
(`wined3d_device_context_update_sub_resource` → `msvcr120` heap → `RtlAllocateHeap`), while it holds
the game lock (`0459FA8C` in T7, `044BFA8C` in T8 — a heap-allocated lock, address shifts per run). An
Awesomium **web-UI worker** piles up on that game lock when a social/squad panel opens, and 60 s later
Wine prints the `RtlpWaitForCriticalSection` timeout. The heap lock in that alloc chain reads **free**,
so the shape is a **lost wakeup in Wine's fsync**, not a held-lock deadlock — which is exactly why it
survives DXVK→wined3d. The WinHTTP/TLS and thread-affinity theories are both dead; everything
downstream (the timeouts, the crash-handler fault storm in old captures) follows from the parked render
thread. Read `console.log` first, every time — see [Client Logging](Client-Logging.md) — but the live
`/proc` reads in T7/T8 are what actually moved this. **T9 closed it:** forcing that fsync path off
(`PROTON_NO_FSYNC=1 PROTON_NO_ESYNC=1`, DXVK) gave four consecutive freeze-free sessions — the
predicted result of a lost wakeup, and now the required client launch config.

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

### [x] T7: The freeze is a render-thread stall holding the game lock, not WinHTTP (DXVK capture)

> **Superseded in part by T8.** This capture localised the freeze to the render thread holding the
> game lock while parked in the D3D9 path, and killed the WinHTTP theory — both still stand. Its
> *conclusion* that DXVK is the culprit did **not**: T8 reproduced the identical freeze on wined3d.
> Read this for the method and the "not WinHTTP" proof; read T8 for where the wedge actually is.


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

**The decisive next test — cheap, one variable** (executed; result in **T8**). Force Wine's own D3D9
path instead of DXVK and soak again: Steam → Firefall → Properties → Launch Options,
`PROTON_USE_WINED3D=1 %command%`. It **still froze** (first run), which is the "independent of DXVK"
branch — the lever is not the driver. See T8.

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
range against the PEB module list (module bases are per-run ASLR — read them from
`/proc/$PID/maps`, which shows each `.dll`/`.exe` at its load address; `FirefallClient.exe` was
`0x79890000` in T7 and `0x79770000` in T8. For the on-disk VA, subtract the base and add `0x400000`).
The T8 walker (TEB scan → ebp chain → raw call-return scan → symbol resolution via `winedump -j
export`) is saved next to that run's logs as `walk.py`.

### [x] T8: The freeze reproduces on wined3d — it is a heap-lock stall, not DXVK

`PROTON_USE_WINED3D=1` from T7, first run: **froze**. The loaded `d3d9.dll` this run was Wine's own
(`GE-Proton11-5/.../wine/i386-windows/d3d9.dll` + `wined3d.dll`, a live `wined3d_cs` thread, zero
DXVK threads — confirmed in `/proc/$PID/maps` and the env `PROTON_USE_WINED3D=1`). Swapping the
entire GPU driver out changed nothing, so **DXVK is exonerated** and the T7 "DXVK stall" conclusion is
dead. Saved: `~/Games/PIN/logs/freezes/260812-wined3d/` (`console.log`, `critsec-timeouts.txt`,
`stackwalk.txt`, `walk.py`).

**Same signature, one layer deeper.** The Proton log timed out on `044BFA8C` (heap-allocated lock,
different address than T7's `0459FA8C` — same *shape*):

```
044bfa8c:  DebugInfo=0xffffffff  LockCount=1  RecursionCount=1  OwningThread=0x178   ← the render thread
Proton log:  RtlpWaitForCriticalSection section 044BFA8C … timed out in thread 024c, blocked by 0178
```

Walking owner `0x178`'s live stack (`/proc/$PID/mem`, innermost → outermost) resolves to:

```
RtlEnterCriticalSection+0x19         ← parked here, on the CRT/NT heap lock
 ← msvcr120 heap (RtlAllocateHeap+0x20 / RtlFreeHeap+0x19)
 ← wined3d_device_context_update_sub_resource+0x3ac      ← a texture / VT-tile upload
 ← d3d9  (IDirect3DDevice9 present/update, internal)
```

So the render thread, uploading a texture subresource during streaming, called the CRT allocator and
never came back out of `RtlEnterCriticalSection` — while holding game lock `044BFA8C`. The one waiter,
wine tid `0x24c`, is an **Awesomium** (web-UI) worker blocked entering that same game lock;
`console.log` cuts off mid-line at `00:19 RAM:1445MB FPS:5` in a burst of UI HTTP calls
(`squad_builder/lfp`, `garage_slots`, `armies/members`) — i.e. a **social/squad panel opening** is
what made the UI contend with the render thread for `044BFA8C`.

**Lost wakeup, not deadlock.** The heap `CRITICAL_SECTION` in that alloc chain (`0x00150078`,
`DebugInfo` → ntdll data) reads **free** (`LockCount=0xffffffff`, `Owner=0`), yet `0x178` sleeps in
`RtlEnterCriticalSection` on it. A waiter parked on a free lock is the fingerprint of a **lost futex
wakeup in Wine's fsync** critical-section path — which also explains why it is driver-independent
(fsync sits below both DXVK and wined3d) and intermittent (it is a race).

**The decisive next test — cheap, one variable.** Take fsync/esync out of the picture (revert to DXVK
first; it is exonerated and faster). Steam → Firefall → Properties → Launch Options:

```
PROTON_NO_FSYNC=1 PROTON_NO_ESYNC=1 %command%
```

- If the freeze **vanishes**: confirmed fsync lost-wakeup — ship with fsync/esync off, or move to a
  Proton whose fsync has the fix. Done.
- If it **still freezes**: it is a real lock-ordering problem in the game on `044BFA8C` (render thread
  holds it across a present-time allocation; the UI worker needs it) — then the lever is reducing that
  hold, e.g. cutting UI-triggered churn while streaming, not the driver or the sync layer.

Run it the same way as T6 (four-session bar; save any freeze, don't kill a live hang — walk it first).

**How the freeze ends, and a better artifact.** The T8 client sat frozen ~5 min, then a single thread
took a `SIGSEGV` and the OS crash handler (ABRT) auto-saved a full core to
`/var/spool/abrt/ccpp-*-<pid>/`. That core says the death is a **double fault inside Wine's ntdll**:
the guest hit an access violation (`0xC0000005`, a near-null **read of `0x1fba`** at win32
`ntdll.dll+0x4773b`, an internal fn), and Wine faulted *delivering* it — near-null **read of `0x14c0`**
in unix `ntdll.so setup_raise_exception+0xb1`. One thread crashed; the other 207 were parked (130 in
`futex`, 77 in a restarting syscall) — i.e. the whole process was wedged, confirming the hang
independently. This is the process's *death*, downstream of the *freeze*, and it's not proven to share
a root cause — but every terminal event is inside ntdll (sync + exception delivery), the exact layer
`PROTON_NO_FSYNC/ESYNC` swaps out. Practical upshot: **ABRT captures a full core for every
freeze-crash** — a better artifact than a live `/proc` read. Read just the notes cheaply without
inflating 500 MB: `zstd -dc coredump.zst | head -c 120M > core.head; eu-readelf -n core.head` (ELF
notes are at the start; the first `PRSTATUS` is the crashing thread, `SIGINFO` has the fault address).
Full detail: `~/Games/PIN/logs/freezes/260812-wined3d/CRASH-SUMMARY.md`.

### [x] T9: Disabling Wine fsync/esync fixes the freeze — confirmed lost-wakeup

The decisive test from T8, run. Config: reverted to DXVK (exonerated in T8, and faster), fsync and
esync forced off. Steam → Firefall → Properties → Launch Options — the *only* change from the config
that reliably froze:

```
PROTON_NO_FSYNC=1 PROTON_NO_ESYNC=1 %command%
```

Then soak, exercising the correlated trigger (open the social/squad panel during streaming, which is
what made the UI worker contend for the render thread's lock in T8).

**Result — passed 2026-08-12: four consecutive sessions, zero freezes.** The freeze had been hitting
within 1–4 runs (T6 froze on the 4th, T7 and T8 on the 1st), so four clean in a row clears the
four-session bar against that rate.

Why this one counts where WinHTTP (T5), thread affinity (T6), and DXVK (T7) did not: it is not
"changed something, got lucky." T8 localised the render thread parked in `RtlEnterCriticalSection` on
a **free** heap lock — the fingerprint of a lost futex wakeup — and predicted that taking Wine's fsync
path out would fix it. It did, on the first config tried. Prediction plus the bar is much stronger
evidence than either alone.

This is an intermittent race, so "four clean" is strong evidence, not a mathematical proof — but the
mechanism is identified and the fix is mechanistically correct: fsync sits below both DXVK and
wined3d, which is exactly why the freeze was driver-independent (T7/T8) and intermittent.

**Shipping config:** `PROTON_NO_FSYNC=1 PROTON_NO_ESYNC=1 %command%` on DXVK is now the required
client launch line — see [Http-Only-Setup.md](../Http-Only-Setup.md#client-launch-options). The
alternative fix, if the small fsync speed cost ever matters, is a Proton whose fsync carries the
wakeup fix; that is not worth chasing while this holds.

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
