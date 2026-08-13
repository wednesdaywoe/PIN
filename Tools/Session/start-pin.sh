#!/usr/bin/env bash
# Starts the three PIN servers. Built from source against net10.0, so these are
# portable IL and run natively on Linux -- no Wine, no Proton prefix.
#
# Flatpak Steam runs with shared=network, so it has no network namespace of its
# own. Firefall under Proton reaches these on plain host loopback.
#
#   ./start-pin.sh              build from source, install, then run -- the default, because
#                               testing a stale build has cost us sessions more than once
#   ./start-pin.sh --no-build   run whatever is installed, unchanged. For A/B against an archived
#                               build (see use-build.sh); says so loudly if the tree has moved on
#
# Either way the build being run is announced before launch and written into the head of every log,
# so a transcript can always be traced back to the binary that produced it.
set -uo pipefail

# See the note in deploy.sh: following $0 finds the repo copy of this script, not following it finds
# the deployment the servers run from. Both are needed and they are not the same directory. Every
# path below is relative to the deployment, so that is what we cd to.
SCRIPTS=$(cd "$(dirname "$(readlink -f "$0")")" && pwd)
PIN_HOME=${PIN_HOME:-$(cd "$(dirname "$0")" && pwd)}
REPO=${PIN_REPO:-$(cd "$SCRIPTS/../.." && pwd)}

if [ ! -d "$PIN_HOME/GameServer" ]; then
    echo "!! $PIN_HOME is not a PIN deployment (no GameServer/). Run the symlink in the deployment" >&2
    echo "   directory -- ~/Games/PIN/start-pin.sh -- or set PIN_HOME." >&2
    exit 1
fi

cd "$PIN_HOME" || exit 1

SERVERS=(WebHostManager MatrixServer GameServer)

BUILD=1
case "${1:-}" in
    --no-build) BUILD=0 ;;
    -h|--help)  sed -n '2,14p' "$0"; exit 0 ;;
    "")         ;;
    *)          echo "usage: $0 [--no-build]" >&2; exit 1 ;;
esac

# Leave this at 1. It disables UseHttpsRedirection(), which now has to stay off
# because every URL the server hands the client is plain HTTP: firefall.ini
# points OperatorHost at localhost:4400, and CapabilityRepository plus the
# oracle ticket's operator_override advertise the 44xx HTTP ports. With the
# redirect on, those calls and every retry get a 307 the client never follows
# (libcurl needs FOLLOWLOCATION, and a 307 on POST means re-POSTing), so login
# spins forever.
#
# The all-HTTP setup exists to keep Wine's WinHTTP out of TLS entirely -- it
# deadlocks its own critical section on the concurrent handshakes at world entry
# and hangs the client. It depends on a 1-byte patch to FirefallClient.exe that
# removes the "Oracle URL [...] not configured for HTTPS" check. Without that
# patch the client rejects these URLs and world entry dies instead.
# See Docs/Http-Only-Setup.md in the repo for the patch and how to undo it.
export PIN_DISABLE_HTTPS_REDIRECT=1

# Only matters if something is switched back to HTTPS -- nothing should be
# handshaking now. Firefall negotiates TLS 1.0. PIN already asks Kestrel for it,
# but Fedora's crypto policy sets MinProtocol=TLSv1.2 in OpenSSL and the
# handshake is refused under the app ("unsupported protocol"), before any
# certificate is exchanged. This profile applies to these three processes only
# -- no system-wide update-crypto-policies, nothing else on the machine is
# affected.
export OPENSSL_CONF="$SCRIPTS/openssl-legacy.cnf"

if ! command -v dotnet >/dev/null 2>&1; then
    echo "dotnet not found. Install with: sudo dnf install dotnet-sdk-10.0" >&2
    exit 1
fi

# Refuse to start on top of a previous run. A server left over from a half-dead
# shutdown still holds UDP 25000/25001 or TCP 4400-4411, so the new one throws
# an unhandled SocketException on bind and dies -- but only AFTER the log was
# truncated below, leaving two processes writing to one file at different
# offsets. That is how you get a MatrixServer.log whose first line reads
# "Unhandled exception. System.Net.Sockets.So[13:07:52 INF] Assigning SocketID".
# Stop here instead and say what to kill. This also has to precede the deploy:
# installing over a DLL a live server has open is its own confusing crash.
stale=$(pgrep -f "dotnet (WebHostManager|MatrixServer|GameServer)\.dll" || true)
if [ -n "$stale" ]; then
    echo "!! PIN servers are already running -- refusing to start a second set." >&2
    ps -o pid,etime,args -p "$(echo "$stale" | paste -sd, -)" >&2
    echo >&2
    echo "   Stop them:   kill $(echo "$stale" | paste -sd' ' -)" >&2
    echo "   If stuck:    kill -9 $(echo "$stale" | paste -sd' ' -)" >&2
    exit 1
fi

# Build from source and install, so that what runs is what is in the tree. A
# failed build stops here rather than falling through to the binary already
# installed: quietly running stale code is the exact failure this prevents.
if [ "$BUILD" -eq 1 ]; then
    # Exported, not prefixed: deploy.sh would otherwise resolve its own $0 back into the repo and
    # take the source tree for the deployment.
    export PIN_HOME
    "$SCRIPTS/deploy.sh" || exit 1
fi

# ---------------------------------------------------------------------------
# Announce the build. Three independent readings of one number have to agree:
# what deploy.sh recorded in BUILD-INFO, what is hashed off disk right now, and
# -- once it is up -- what GameServer hashes itself as and logs on its first
# line (UdpHosts/GameServer/BuildInfo.cs). Agreement means the build that was
# built is the build that was installed is the build that is running. A
# disagreement means something replaced a file behind the pipeline's back, and
# is worth more than the session it would otherwise ruin.
# ---------------------------------------------------------------------------
info()    { sed -n "s|^$1=||p" BUILD-INFO 2>/dev/null; }
hash_of() { if [ -f "$1" ]; then sha256sum "$1" | cut -c1-12; else echo missing; fi; }

banner=()
problems=()

if [ ! -f BUILD-INFO ]; then
    banner+=("build      unidentified -- no BUILD-INFO")
    problems+=("This deployment was not installed by deploy.sh, so nothing here knows what it is."
               "Run ./deploy.sh, or start without --no-build.")
else
    game_hash=$(hash_of GameServer/GameServer.dll)
    dirty=$(info dirty)
    written=$(date -r GameServer/GameServer.dll '+%Y-%m-%d %H:%M:%S' 2>/dev/null || echo unknown)

    banner+=("build      $game_hash  (GameServer.dll, written $written)")

    # The commit recorded in BUILD-INFO describes what deploy.sh installed. If the file on disk is
    # no longer that one, printing the commit beside it would put a lie in the line most likely to
    # be believed -- so say the source is unknown, which is the truth about a hand-placed binary.
    if [ "$game_hash" = "$(info build.GameServer)" ]; then
        banner+=("source     $(info commit) on $(info branch)  \"$(info subject)\"$([ "${dirty:-0}" -gt 0 ] && echo "  +$dirty uncommitted")")
        banner+=("installed  $(info deployed)")
    else
        banner+=("source     unknown -- this is not the build deploy.sh installed")
    fi

    # Every server, not just GameServer: a MatrixServer left behind from an
    # older deploy is just as capable of producing a result nobody can explain.
    for name in "${SERVERS[@]}"; do
        recorded=$(info "build.$name")
        actual=$(hash_of "$name/$name.dll")
        if [ -n "$recorded" ] && [ "$recorded" != "$actual" ]; then
            problems+=("$name.dll is $actual but BUILD-INFO records $recorded -- it was replaced by hand.")
            match=$(find builds -name "*.dll.$actual" -printf '%f\n' 2>/dev/null | head -1)
            [ -n "$match" ] && problems+=("  It matches the archived build $match.")
        fi
    done

    # --no-build is the deliberate A/B path, so a difference from the tree is
    # not an error -- but it must not be silent, because the reason this mode
    # exists is also the reason it gets left on by accident.
    if [ "$BUILD" -eq 0 ]; then
        repo_dll=$REPO/UdpHosts/GameServer/bin/Release/net10.0/GameServer.dll
        repo_hash=$(hash_of "$repo_dll")
        if [ "$repo_hash" != "missing" ] && [ "$repo_hash" != "$game_hash" ]; then
            problems+=("--no-build: the repo holds a different GameServer.dll ($repo_hash)."
                       "  You are NOT testing the current tree. Restart without --no-build for that.")
        fi
    fi
fi

if [ -t 1 ]; then bold=$(tput bold); red=$(tput setaf 1); dim=$(tput dim); off=$(tput sgr0)
else bold=""; red=""; dim=""; off=""; fi

echo "$dim────────────────────────────────────────────────────────────────$off"
for line in "${banner[@]}"; do echo "  $bold$line$off"; done
for line in "${problems[@]}"; do echo "  $red!! $line$off"; done
echo "$dim────────────────────────────────────────────────────────────────$off"

# config/appsettings.json binds HTTPS on 44300-44399 alongside HTTP. Firefall
# only speaks HTTP, but Kestrel faults at startup without a usable cert.
if ! dotnet dev-certs https --check >/dev/null 2>&1; then
    echo "==> Generating ASP.NET Core dev certificate"
    dotnet dev-certs https >/dev/null 2>&1 \
        || echo "warning: dev-certs failed; HTTPS endpoints may not bind" >&2
fi

# The servers run `HandleCommand(Console.ReadLine())` in a loop and dereference
# the result without a null check (Shared.Udp/PacketServer.cs:54). Any stdin
# that reports EOF -- </dev/null, or a backgrounded job -- kills them instantly
# with a NullReferenceException. A FIFO held open read-write never signals EOF,
# so ReadLine just blocks forever, which is what we want.
CTL=$(mktemp -u --tmpdir pin-ctl.XXXXXX)
mkfifo -m 600 "$CTL" || exit 1
exec 3<>"$CTL"
rm -f "$CTL"

mkdir -p logs
pids=()

# SIGTERM, then SIGKILL anything still alive 5s later. The escalation is the
# whole point: a server that does not act on SIGTERM used to leave the script
# parked in `wait` forever, so the window never closed, and closing it by hand
# sent SIGHUP to the script only -- reparenting the servers to init still
# holding their ports. HUP is trapped for the same reason: closing the terminal
# has to run this, not bypass it.
cleanup() {
    trap - EXIT INT TERM HUP
    echo
    echo "==> Stopping servers"

    for pid in "${pids[@]}"; do
        kill -TERM "$pid" 2>/dev/null
    done

    for _ in $(seq 50); do
        alive=0
        for pid in "${pids[@]}"; do
            kill -0 "$pid" 2>/dev/null && alive=1
        done
        [ "$alive" -eq 0 ] && break
        sleep 0.1
    done

    for pid in "${pids[@]}"; do
        if kill -0 "$pid" 2>/dev/null; then
            echo "   pid $pid ignored SIGTERM after 5s -- SIGKILL" >&2
            kill -KILL "$pid" 2>/dev/null
        fi
    done

    [ -n "${tail_pid:-}" ] && kill -TERM "$tail_pid" 2>/dev/null
    wait 2>/dev/null
    exec 3>&-

    left=$(pgrep -f "dotnet (WebHostManager|MatrixServer|GameServer)\.dll" || true)
    if [ -n "$left" ]; then
        echo "!! still running after cleanup: $(echo "$left" | tr '\n' ' ')" >&2
    else
        echo "==> All servers stopped."
    fi
}
trap cleanup EXIT INT TERM HUP

# Each server lives in its own directory for two reasons: WebHostManager
# resolves config/ against Directory.GetCurrentDirectory(), and Serilog's
# config scanner tries to Assembly.Load any Serilog.* dll it finds in the
# folder -- a shared publish dir makes one server's dependency crash another,
# since it is absent from that server's own deps.json.
for name in "${SERVERS[@]}"; do
    echo "==> Starting $name"
    # Seed the log with the build banner rather than truncating to empty, so a
    # transcript read back weeks later still names the binary that wrote it --
    # which is the whole point, since the DLL by then has been overwritten.
    # Note the append below: `>` here would truncate the banner straight back off.
    {
        printf '%s\n' "${banner[@]}"
        printf '!! %s\n' "${problems[@]:-}"
    } | sed '/^!! $/d' > "logs/$name.log"
    ( cd "$name" && exec dotnet "$name.dll" ) <&3 >>"logs/$name.log" 2>&1 &
    pids+=($!)
done

# GameServer is slowest to come up; it parses a 32 MB static DB first.
for _ in $(seq 60); do
    grep -qs "Listening on" logs/GameServer.log && break
    sleep 0.5
done

failed=0
for i in "${!SERVERS[@]}"; do
    if ! kill -0 "${pids[$i]}" 2>/dev/null; then
        echo "!! ${SERVERS[$i]} exited on startup -- see logs/${SERVERS[$i]}.log" >&2
        failed=1
    fi
done

if [ "$failed" -eq 0 ]; then
    echo
    # Repeated as the last thing before you go to the client, because by now the
    # banner above has scrolled off behind the startup chatter.
    echo "${bold}All three servers up on build ${banner[0]#build      }${off}"
    echo "Launch Firefall from Steam."
fi
echo "Ctrl+C to shut down. Following logs (also in $(pwd)/logs/):"
echo

# Backgrounded and waited on, rather than run in the foreground, so cleanup can
# kill it by pid. `kill <script>` from another terminal otherwise leaves tail
# behind holding the window open.
tail -n +1 -F logs/*.log &
tail_pid=$!
wait "$tail_pid"
