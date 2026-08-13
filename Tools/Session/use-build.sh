#!/usr/bin/env bash
# Swap which GameServer build is installed, for A/B against an older one.
#
#   ./use-build.sh                 list what is installed and what can be swapped in
#   ./use-build.sh latest          rebuild from the tree and install it (same as deploy.sh)
#   ./use-build.sh good            the c659335 build, from before the Damage/Effect work
#   ./use-build.sh current         the 2026-08-11 Damage/Effect build -- see the warning below
#   ./use-build.sh <hash>          any build in builds/, by the hash the banner shows
#
# THE `current` TAG IS A MISNOMER and has already cost a session. It was named when it was the
# newest build; it has not been the newest since 2026-08-11, and running it now silently rolls back
# environmental damage, the invuln command, the DATA-11 weapon-modifier fix and NPC locomotion. The
# name is kept only because test entries written at the time say `use-build.sh current` and those
# entries have to stay reproducible. Use `latest` for the current tree. The listing below always
# prints real dates so no tag has to be trusted.
#
# AFTER SWAPPING, start the servers with `./start-pin.sh --no-build`. Plain `./start-pin.sh` builds
# from source first and would install straight over the build just selected -- correct behaviour for
# every other purpose, and exactly wrong for this one.
set -uo pipefail

# See the note in deploy.sh: following $0 finds the repo copy of this script, not following it finds
# the deployment the servers run from. Both are needed and they are not the same directory.
SCRIPTS=$(cd "$(dirname "$(readlink -f "$0")")" && pwd)
PIN_HOME=${PIN_HOME:-$(cd "$(dirname "$0")" && pwd)}

if [ ! -d "$PIN_HOME/GameServer" ]; then
    echo "!! $PIN_HOME is not a PIN deployment (no GameServer/). Run the symlink in the deployment" >&2
    echo "   directory -- ~/Games/PIN/use-build.sh -- or set PIN_HOME." >&2
    exit 1
fi

cd "$PIN_HOME/GameServer" || exit 1

ARCHIVE=$PIN_HOME/builds
INSTALLED=GameServer.dll

hash_of() { if [ -f "$1" ]; then sha256sum "$1" | cut -c1-12; else echo missing; fi; }
when_of() { date -r "$1" '+%Y-%m-%d %H:%M' 2>/dev/null || echo "unknown"; }

# Tagged builds kept next to the installed one, from before builds/ existed.
tag_path() {
    case "$1" in
        good)    echo "$PIN_HOME/GameServer/GameServer.dll.c659335" ;;
        current) echo "$PIN_HOME/GameServer/GameServer.dll.current" ;;
        *)       echo "" ;;
    esac
}

listing() {
    local installed_hash
    installed_hash=$(hash_of "$INSTALLED")

    echo "installed   $installed_hash   $(when_of "$INSTALLED")"
    if [ -f "$PIN_HOME/BUILD-INFO" ]; then
        local recorded
        recorded=$(sed -n 's|^build.GameServer=||p' "$PIN_HOME/BUILD-INFO")
        if [ "$recorded" = "$installed_hash" ]; then
            echo "            from $(sed -n 's|^commit=||p' "$PIN_HOME/BUILD-INFO") on $(sed -n 's|^branch=||p' "$PIN_HOME/BUILD-INFO")"
        else
            echo "            NOT what deploy.sh installed ($recorded) -- swapped in by hand"
        fi
    fi

    echo
    echo "can swap to:"
    printf '  %-11s %-12s   %s\n' latest "" "rebuild from $(sed -n 's|^repo=||p' "$PIN_HOME/BUILD-INFO" 2>/dev/null || echo 'the tree')"
    for tag in good current; do
        local path note=""
        path=$(tag_path "$tag")
        [ "$tag" = current ] && note="   <- NOT the newest, despite the name"
        [ -f "$path" ] && printf '  %-11s %-12s   %s%s\n' "$tag" "$(hash_of "$path")" "$(when_of "$path")" "$note"
    done
    if [ -d "$ARCHIVE" ]; then
        while IFS= read -r path; do
            [ -n "$path" ] || continue
            printf '  %-11s %-12s   %s\n' "(archived)" "${path##*.}" "$(when_of "$path")"
        done < <(find "$ARCHIVE" -name 'GameServer.dll.*' -printf '%T@ %p\n' 2>/dev/null | sort -rn | cut -d' ' -f2-)
    fi
    echo
    echo "Archived builds are selected by the hash in the middle column."
}

case "${1:-}" in
    "") listing; exit 0 ;;
esac

if pgrep -f "dotnet.*GameServer.dll" >/dev/null; then
    echo "GameServer is still running. Stop it first, then run this again." >&2
    exit 1
fi

if [ "$1" = "latest" ]; then
    # Exported rather than prefixed: deploy.sh would otherwise resolve its own $0 back into the
    # repo and take the source tree for the deployment.
    export PIN_HOME
    exec "$SCRIPTS/deploy.sh"
fi

src=$(tag_path "$1")
[ -n "$src" ] || src=$ARCHIVE/GameServer.dll.$1

if [ ! -f "$src" ]; then
    echo "No build called '$1'." >&2
    echo >&2
    listing >&2
    exit 1
fi

# Keep the outgoing build before overwriting it -- it may be the only copy of whatever a session's
# results were produced against, and deploy.sh only archives on its own path.
mkdir -p "$ARCHIVE"
outgoing=$(hash_of "$INSTALLED")
if [ -f "$INSTALLED" ] && [ ! -f "$ARCHIVE/GameServer.dll.$outgoing" ]; then
    cp -p "$INSTALLED" "$ARCHIVE/GameServer.dll.$outgoing"
    [ -f GameServer.pdb ] && cp -p GameServer.pdb "$ARCHIVE/GameServer.pdb.$outgoing"
fi

cp -p "$src" "$INSTALLED"
pdb=${src/.dll./.pdb.}
[ -f "$pdb" ] && cp -p "$pdb" GameServer.pdb

echo "installed $(hash_of "$INSTALLED") from $(basename "$src")"
echo
echo "Now start with:  ./start-pin.sh --no-build"
echo "(plain ./start-pin.sh rebuilds from source and would overwrite this)"
