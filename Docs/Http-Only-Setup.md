# HTTP-Only Setup (Wine/Proton)

The client under Proton freezes on world entry. The cause is not the game: Wine's WinHTTP
deadlocks its own critical section during the burst of concurrent TLS handshakes the client
fires when it enters the world. Every Proton branch tried (Experimental, 9, GE) hits it.
Importing the ASP.NET dev cert fixed *login*, but the deadlock is downstream of the cert.

The fix is to take TLS out of the picture: serve every client-facing API over plain HTTP.
Kestrel already binds both — `config/appsettings.json` has each host on `443xx` and `4xxx`
— so the server side is a URL change, not a new listener.

The obstacle is one check in the client.

## The client-side check

`FirefallClient.exe` validates the scheme of the URLs it gets from the operator/oracle
handshake and refuses anything that isn't HTTPS:

```
Oracle URL [%s] not configured for HTTPS (request must be secure)
```

That string lives at VA `0x1b52120` and is referenced from exactly one place. The gate:

```
0x00895cab  83 7f 0c 01    cmp dword [edi+0xC], 1     ; 1 == scheme is https
0x00895caf  74 4a          je   0x00895cfb            ; ok -> build the request
                           ; fall through -> format the error, log it, abort
```

Patching the `je` to an unconditional `jmp` skips the error for every scheme.

**File offset `0x4950af`: `74` -> `EB`.** (VA `0x895caf`; `.text` RVA `0x1000`, raw `0x400`.)

This does not force plain HTTP; it only stops the client rejecting it. The actual TLS
decision is made separately, from the same field, at the request-building site:

```
lea   eax, [ecx+0xC]
xor   esi, esi
cmp   dword [eax], 1
mov   eax, 0x800000        ; WINHTTP_FLAG_SECURE
cmove esi, eax             ; secure flag only when scheme == 1
...
push  esi                  ; -> dwFlags
call  WinHttpOpenRequest
```

With an `http://` URL the field is 0, so `dwFlags` is 0 and the request goes out in the
clear. An `https://` URL still works exactly as before — the patch removes a refusal, it
does not change behaviour for HTTPS URLs.

### Applying it

The client is at
`~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Firefall/system/bin/`.

```
cd ~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Firefall/system/bin
cp -n FirefallClient.exe FirefallClient.exe.prehttp.bak
python3 -c "
p='FirefallClient.exe'; OFF=0x4950af
d=bytearray(open(p,'rb').read())
assert d[OFF]==0x74, 'unexpected byte %02x -- wrong build, do not patch' % d[OFF]
d[OFF]=0xEB
open(p,'wb').write(d)
print('patched')
"
```

To undo: `cp FirefallClient.exe.prehttp.bak FirefallClient.exe`.

`FirefallClient copy.exe` in the same directory is the pristine pre-DRM-crack backup; the
active exe carries the 5-byte DRM patch at `0x2ead58`, `0x30682d`, `0x1250eb3`. Do not
restore from `copy` unless you also want the DRM check back.

Steam's "verify integrity of game files" replaces the exe and reverts both patches.

## The server-side URLs

Two places advertise host URLs to the client, and they have to agree. HTTP ports are the
HTTPS port minus 40000 (`44302` -> `4402`), except CatchAll (`44399` -> `4499`).

- `WebHosts/WebHost.OperatorApi/Capability/CapabilityRepository.cs` — the capability
  response from first contact; all 11 hosts.
- `WebHosts/WebHost.ClientApi/Oracle/OracleController.cs` — `operator_override`, which
  supersedes the capability response for `ingame_host` and `clientapi_host`. Changing only
  one of the two files leaves the client on HTTPS.

`firefall.ini` already points `OperatorHost` at `localhost:4400` (HTTP), and the asset and
VT paths were already `http://localhost:4401`.

`PIN_DISABLE_HTTPS_REDIRECT=1` must stay set in `start-pin.sh`. With `UseHttpsRedirection()`
on, every one of these calls gets a 307 the client won't follow.

## Deploying and verifying

```
cd ~/Github/PIN
dotnet build WebHosts/WebHostManager/WebHostManager.csproj -c Release
cp WebHosts/WebHost.ClientApi/bin/Release/net10.0/WebHost.ClientApi.dll     ~/Games/PIN/WebHostManager/
cp WebHosts/WebHost.OperatorApi/bin/Release/net10.0/WebHost.OperatorApi.dll ~/Games/PIN/WebHostManager/
```

`~/Games/PIN/WebHostManager/config/appsettings.json` is tuned locally (`DevMode: true`,
Kestrel debug logging) and deliberately differs from the repo copy. Don't overwrite it.

Restart the servers, then confirm both endpoints hand out `http://`:

```
curl -s -X POST http://localhost:4402/api/v1/oracle/ticket | grep -o 'http[s]*://localhost:[0-9]*'
curl -s "http://localhost:4400/check?environment=production&build=1973" | grep -o 'http[s]*://localhost:[0-9]*'
```

Every URL in both responses should read `http://`. If any still says `https://`, the old
DLL is still loaded — the servers were not restarted.

Then launch the client and enter the world. The freeze is gone if you reach the world with
no hang; `ss -tnp | grep 443` should show no TLS connections from the client during entry.
