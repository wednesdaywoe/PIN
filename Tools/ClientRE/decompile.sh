#!/usr/bin/env bash
# Decompile FirefallClient.exe functions from the analysed Ghidra project, without opening the GUI.
#
#   Tools/ClientRE/decompile.sh 0xbc0ec0                      one function, by any address inside it
#   Tools/ClientRE/decompile.sh apt::ActiveInitiationCommand  every function in a class's vtable
#
# Needs the project made once by `decompile.sh --analyse` (about 15 minutes on 32 cores). GHIDRA_HOME and
# GHIDRA_PROJECTS default to this machine's install under ~/Apps.
set -euo pipefail

GHIDRA_HOME=${GHIDRA_HOME:-$HOME/Apps/ghidra_12.1.4_PUBLIC}
GHIDRA_PROJECTS=${GHIDRA_PROJECTS:-$HOME/Apps/ghidra-projects}
CLIENT=${FIREFALL_CLIENT:-/mnt/games-1tb/SteamLibrary/steamapps/common/Firefall/system/bin/FirefallClient.exe}
SCRIPTS=$(cd "$(dirname "$(readlink -f "$0")")" && pwd)
export GHIDRA_HEADLESS_MAXMEM=${GHIDRA_HEADLESS_MAXMEM:-16G}

if [[ ${1:-} == --analyse ]]; then
    exec "$GHIDRA_HOME/support/analyzeHeadless" "$GHIDRA_PROJECTS" Firefall -import "$CLIENT" -analysisTimeoutPerFile 14400
fi

if [[ $# -eq 0 ]]; then
    sed -n '2,8p' "$0"
    exit 1
fi

# -readOnly leaves the project untouched, so runs can't race each other into a locked or half-saved state.
# The script's println lines come through Ghidra's logger, prefixed and suffixed; the decompiled C after each
# one comes through bare. Everything else Ghidra logs is dropped.
"$GHIDRA_HOME/support/analyzeHeadless" "$GHIDRA_PROJECTS" Firefall -process FirefallClient.exe -noanalysis -readOnly \
    -scriptPath "$SCRIPTS" -postScript Decompile.java "$@" 2>/dev/null |
    awk '/^INFO  Decompile.java> / { sub(/^INFO  Decompile.java> /, ""); sub(/ \(GhidraScript\) *$/, ""); print; body = 1; next }
         /^(INFO|WARN|ERROR|DEBUG) / { body = 0; next }
         /^ \(GhidraScript\) *$/ { next }
         body'
