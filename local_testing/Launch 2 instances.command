#!/usr/bin/env bash
# Double-click in Finder to launch two ONI instances for multiplayer testing.
cd "$(dirname "$0")" || exit 1
./oni-mp-dev.sh launch2
echo
echo "Press Return to close this window."
read -r _
