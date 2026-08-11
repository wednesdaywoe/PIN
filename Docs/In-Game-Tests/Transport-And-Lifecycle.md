# Transport and Lifecycle

Part of the [in-game test queue](README.md). Setup and admin commands: [Session Setup](Session-Setup.md).

## HTTP-only transport (blocks everything needing world entry)

The client froze on world entry because Wine's WinHTTP deadlocks its own critical section during
the concurrent TLS handshakes there. The fix takes TLS out entirely: every advertised URL is now
plain HTTP, which needs a 1-byte patch to `FirefallClient.exe` removing its HTTPS-only check on
the oracle URL. Background and undo steps: [Http-Only-Setup.md](../Http-Only-Setup.md).

Run T1 and T2 before anything else in the queue — a client that can't reach the world can't run
any other entry.

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
