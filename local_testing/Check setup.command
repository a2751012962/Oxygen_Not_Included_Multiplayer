#!/usr/bin/env bash
# Double-click in Finder to verify all paths are detected correctly.
cd "$(dirname "$0")" || exit 1
./oni-mp-dev.sh paths
echo
echo "Press Return to close this window."
read -r _
