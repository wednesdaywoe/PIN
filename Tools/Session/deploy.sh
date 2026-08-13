#!/usr/bin/env bash
# Build the three PIN servers from source and install them where start-pin.sh runs them.
#
#   ./deploy.sh              build and install all three servers
#   ./deploy.sh --quiet      same, but print only warnings and the one-line result
#
# This exists because deploying used to be a hand-typed `cp` of GameServer.dll. Forget it and the
# session tests yesterday's server while reading today's test entry -- the result gets written down
# against code that was never running. start-pin.sh calls this by default so the normal path cannot
# skip it, and a failed build aborts rather than falling through to the stale binary.
#
# WHAT IS DELIBERATELY NOT COPIED: the local configs. GameServer.dll.config carries this machine's
# paths to clientdb.sd2 and the asset DB, where the repo's copy points at a Windows Steam install;
# WebHostManager/config/appsettings.json has DevMode on locally and off in the repo. Overwriting
# either produces a server that fails in ways that look like a code bug. They are excluded from the
# sync and their upstream templates are hash-tracked instead, so a genuine change to a template gets
# reported rather than silently applied or silently missed.
#
# The sync does not delete. Stale files left in a deploy directory are inert (nothing loads a DLL
# the synced deps.json does not name), and --delete in a directory holding the build archive is a
# foot-gun that outweighs the tidiness.
set -uo pipefail

# This script lives in the repo (Tools/Session) and is symlinked into the deployment directory, so
# it has two homes and needs both, and the difference is which way $0 gets resolved. Following the
# symlink finds the repo copy -- where the source and the openssl profile are. NOT following it
# finds the deployment -- where the servers actually run. Getting these the wrong way round either
# installs the build on top of the source tree or looks for GameServer/ inside the repo. Override
# either with PIN_HOME / PIN_REPO; a script calling a sibling must pass PIN_HOME through, since the
# sibling would otherwise resolve its own $0 straight back into the repo.
SCRIPTS=$(cd "$(dirname "$(readlink -f "$0")")" && pwd)
PIN_HOME=${PIN_HOME:-$(cd "$(dirname "$0")" && pwd)}
REPO=${PIN_REPO:-$(cd "$SCRIPTS/../.." && pwd)}
FRAMEWORK=net10.0
CONFIGURATION=Release
ARCHIVE=$PIN_HOME/builds
ARCHIVE_KEEP=10
INFO=$PIN_HOME/BUILD-INFO

# name : project directory under the repo : deploy directory under here
SERVERS=(
    "GameServer:UdpHosts/GameServer:GameServer"
    "MatrixServer:UdpHosts/MatrixServer:MatrixServer"
    "WebHostManager:WebHosts/WebHostManager:WebHostManager"
)

QUIET=0
case "${1:-}" in
    --quiet) QUIET=1 ;;
    "")      ;;
    *)       echo "usage: $0 [--quiet]" >&2; exit 1 ;;
esac

say() { [ "$QUIET" -eq 1 ] || echo "$@"; }
die() { echo "!! $*" >&2; exit 1; }

# Same width the server hashes itself with (GameServer/BuildInfo.cs), so the banner, the log line
# and BUILD-INFO are all directly comparable by eye.
hash_of() {
    [ -f "$1" ] || { echo "missing"; return; }
    sha256sum "$1" | cut -c1-12
}

# Local configs the sync must leave alone, per server.
preserved_paths() {
    case "$1" in
        GameServer)     echo "GameServer.dll.config" ;;
        WebHostManager) echo "config/appsettings.json config/appsettings.Development.json" ;;
        *)              echo "" ;;
    esac
}

[ -d "$PIN_HOME/GameServer" ] || die "$PIN_HOME is not a PIN deployment (no GameServer/). Run the
   symlink in the deployment directory -- ~/Games/PIN/deploy.sh -- or set PIN_HOME."
[ -d "$REPO/.git" ] || die "no repo at $REPO -- set PIN_REPO to where PIN is checked out."
command -v dotnet  >/dev/null 2>&1 || die "dotnet not found. Install with: sudo dnf install dotnet-sdk-10.0"
command -v rsync   >/dev/null 2>&1 || die "rsync not found. Install with: sudo dnf install rsync"

# Installing over a server that has the file open produces a half-written DLL and a confusing crash.
running=$(pgrep -f "dotnet (WebHostManager|MatrixServer|GameServer)\.dll" || true)
if [ -n "$running" ]; then
    echo "!! PIN servers are running -- stop them before deploying." >&2
    ps -o pid,etime,args -p "$(echo "$running" | paste -sd, -)" >&2
    exit 1
fi

# Fedora's crypto policy rejects SHA-1, which is what Bepu's strong-name signing uses; without this
# the BepuPhysics projects fail to sign and the build dies. Same profile start-pin.sh runs under.
export OPENSSL_ENABLE_SHA1_SIGNATURES=1
export OPENSSL_CONF="$SCRIPTS/openssl-legacy.cnf"

say "==> Building $CONFIGURATION from $REPO"
for entry in "${SERVERS[@]}"; do
    IFS=: read -r name project _ <<<"$entry"
    output=$(cd "$REPO" && dotnet build "$project" -c "$CONFIGURATION" --nologo -v q 2>&1)
    # shellcheck disable=SC2181
    if [ $? -ne 0 ]; then
        echo "$output" >&2
        die "$name failed to build. Nothing was installed; the previous build is still in place."
    fi
    say "    $name ok"
done

# Keep the outgoing GameServer.dll before it is overwritten. It is the only copy of whatever was
# last tested, and once a session has produced results against it that binary is evidence.
mkdir -p "$ARCHIVE"
outgoing=$PIN_HOME/GameServer/GameServer.dll
if [ -f "$outgoing" ]; then
    outgoing_hash=$(hash_of "$outgoing")
    incoming_hash=$(hash_of "$REPO/UdpHosts/GameServer/bin/$CONFIGURATION/$FRAMEWORK/GameServer.dll")
    if [ "$outgoing_hash" != "$incoming_hash" ] && [ -z "$(find "$ARCHIVE" -name "GameServer.dll.$outgoing_hash" -print -quit)" ]; then
        cp -p "$outgoing" "$ARCHIVE/GameServer.dll.$outgoing_hash"
        [ -f "$PIN_HOME/GameServer/GameServer.pdb" ] && cp -p "$PIN_HOME/GameServer/GameServer.pdb" "$ARCHIVE/GameServer.pdb.$outgoing_hash"
        say "    archived the outgoing build as builds/GameServer.dll.$outgoing_hash"
    fi
    # Bounded history: keep the newest few, drop the rest.
    mapfile -t stale < <(find "$ARCHIVE" -name 'GameServer.dll.*' -printf '%T@ %p\n' | sort -rn | tail -n +$((ARCHIVE_KEEP + 1)) | cut -d' ' -f2-)
    for old in "${stale[@]:-}"; do
        [ -n "$old" ] && rm -f "$old" "${old/.dll./.pdb.}"
    done
fi

say "==> Installing into $PIN_HOME"
declare -A dll_hash
declare -A template_hash
declare -A template_source
declare -A template_local
for entry in "${SERVERS[@]}"; do
    IFS=: read -r name project target <<<"$entry"
    source_dir=$REPO/$project/bin/$CONFIGURATION/$FRAMEWORK
    target_dir=$PIN_HOME/$target

    [ -d "$source_dir" ] || die "$name built but $source_dir is missing."
    mkdir -p "$target_dir"

    excludes=()
    for path in $(preserved_paths "$name"); do
        excludes+=(--exclude="/$path")
        template=$source_dir/$path
        if [ -f "$template" ]; then
            template_hash["$name/$path"]=$(hash_of "$template")
            template_source["$name/$path"]=$template
            template_local["$name/$path"]=$target_dir/$path
        fi
    done

    rsync -a "${excludes[@]}" "$source_dir/" "$target_dir/" \
        || die "$name failed to install into $target_dir."

    dll_hash[$name]=$(hash_of "$target_dir/$name.dll")
    say "    $name ${dll_hash[$name]}"
done

# A template whose hash moved means the repo changed a setting the local config does not know about
# -- a new key, a changed default. The local file is still the one that runs; this only says look.
if [ -f "$INFO" ]; then
    for key in "${!template_hash[@]}"; do
        was=$(sed -n "s|^template\.$key=||p" "$INFO")
        if [ -n "$was" ] && [ "$was" != "${template_hash[$key]}" ]; then
            echo "!! $key changed in the repo since the last deploy. Your local copy is kept and is" >&2
            echo "   still what runs, but it may be missing a new setting. Compare them:" >&2
            echo "   diff ${template_source[$key]} ${template_local[$key]}" >&2
        fi
    done
fi

# BUILD-INFO is what start-pin.sh reads to describe the deployment. The DLL hashes in it are what
# make the description checkable rather than merely claimed: recompute them and they either match
# what is on disk or something replaced a file behind the pipeline's back.
{
    echo "deployed=$(date -Iseconds)"
    echo "repo=$REPO"
    echo "branch=$(git -C "$REPO" rev-parse --abbrev-ref HEAD 2>/dev/null || echo unknown)"
    echo "commit=$(git -C "$REPO" rev-parse --short=7 HEAD 2>/dev/null || echo unknown)"
    echo "subject=$(git -C "$REPO" log -1 --pretty=%s 2>/dev/null || echo unknown)"
    echo "dirty=$(git -C "$REPO" status --porcelain 2>/dev/null | wc -l)"
    for name in "${!dll_hash[@]}"; do
        echo "build.$name=${dll_hash[$name]}"
    done
    for key in "${!template_hash[@]}"; do
        echo "template.$key=${template_hash[$key]}"
    done
} > "$INFO"

commit=$(sed -n 's/^commit=//p' "$INFO")
dirty=$(sed -n 's/^dirty=//p' "$INFO")
echo "==> Deployed ${dll_hash[GameServer]} from $commit$([ "$dirty" -gt 0 ] && echo " +$dirty uncommitted")"
