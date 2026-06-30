#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# launch-two-instances.sh
# Run two copies of Oxygen Not Included at once for ONI Together MP testing
# on Linux (native or Proton).
#
# Steam refuses to launch a game twice, but the executable only needs to know
# its Steam AppId (457140). With SteamAppId exported, Steam's single-instance
# lock is bypassed and a second window comes up. (A steam_appid.txt containing
# 457140 next to the binary is the equivalent file-based trick.)
#
# Override paths via env vars:
#   ONI_DIR  game folder (default tries the common Steam locations)
#   ONI_BIN  game binary  (default: $ONI_DIR/OxygenNotIncluded)
# ---------------------------------------------------------------------------
set -euo pipefail

APPID=457140

# Find the game folder: honour ONI_DIR, else try the usual Steam locations.
if [ -z "${ONI_DIR:-}" ]; then
    for d in \
        "$HOME/.steam/steam/steamapps/common/OxygenNotIncluded" \
        "$HOME/.local/share/Steam/steamapps/common/OxygenNotIncluded" \
        "$HOME/.steam/debian-installation/steamapps/common/OxygenNotIncluded"
    do
        [ -d "$d" ] && { ONI_DIR="$d"; break; }
    done
fi
ONI_DIR="${ONI_DIR:-$HOME/.steam/steam/steamapps/common/OxygenNotIncluded}"
ONI_BIN="${ONI_BIN:-$ONI_DIR/OxygenNotIncluded}"

if [ ! -x "$ONI_BIN" ]; then
    echo "[ERROR] game binary not found/executable at: $ONI_BIN" >&2
    echo "Set ONI_DIR (or ONI_BIN) to your install, e.g.:" >&2
    echo "  ONI_DIR=/path/to/OxygenNotIncluded $0" >&2
    exit 1
fi

launch() {  # $1 = label
    local log="/tmp/oni-instance-$1.log"
    echo "Launching instance $1 (log: $log)"
    # nohup so the games survive this script / terminal closing.
    ( cd "$ONI_DIR" && SteamAppId="$APPID" nohup "$ONI_BIN" >"$log" 2>&1 & )
}

launch 1
sleep 2
launch 2

echo
echo "Two instances launching."
echo "To connect them, use the direct-IP (Riptide) transport: host on one window"
echo "and connect the other to 127.0.0.1:7777. Steam P2P shares one account, so it"
echo "can't tell two local instances apart."
