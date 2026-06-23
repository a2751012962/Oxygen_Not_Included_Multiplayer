#!/usr/bin/env bash
# Double-click in Finder to install the local mod package into ONI's mods/local.
cd "$(dirname "$0")" || exit 1
./oni-mp-dev.sh install
echo
echo "Press Return to close this window."
read -r _
