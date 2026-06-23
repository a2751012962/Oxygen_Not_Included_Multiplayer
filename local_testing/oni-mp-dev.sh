#!/usr/bin/env bash
#
# oni-mp-dev.sh — macOS local-testing helper for ONI Together (multiplayer mod)
#
# What it does:
#   * installs the local mod package into ONI's mods/local folder
#   * launches Oxygen Not Included directly (bypassing Steam) so TWO copies
#     can run side by side for multiplayer testing
#
# The trick (the whole reason this script exists):
#   Steam normally refuses to start a second copy of a game. ONI's executable
#   only needs to know which Steam AppId it is. We give it that in two ways
#   (either is enough — we do both for good measure):
#       1. the SteamAppId=457140 environment variable, or
#       2. a `steam_appid.txt` file containing 457140 in the working directory
#          (same idea as the boot.config debug flag in the README).
#   457140 is the Steam AppId for Oxygen Not Included.
#   With that set we run the game binary inside the .app bundle directly, which
#   also sidesteps macOS's "one .app instance" rule, so two windows come up.
#
# Usage:
#   ./oni-mp-dev.sh <command> [args]
#
# Commands:
#   paths                 Detect & print every path this script uses; flag problems.
#   setup [--force]       Scaffold Directory.Build.props.user from detected paths + restore tools.
#   build [--release]     Compile the mod (auto-deploys to your ModFolder as ONI_Together_dev).
#   install               Copy the prebuilt mod/oni_mp package into ONI's mods/local folder.
#   dev [--release]       build + launch2 (compile from source, then run two instances).
#   appid [--remove]      Write (or remove) steam_appid.txt in the game folder(s).
#   launch [--bg]         Launch ONE instance (foreground unless --bg).
#   launch2               Launch TWO instances (both backgrounded) for MP testing.
#   clone-instance <dir>  Duplicate the game install to <dir> and remember it as
#                         the 2nd instance (mirrors the public / public_testing
#                         two-folder setup). Useful if one .app refuses to run twice.
#   stop                  Stop instances launched by this script.
#   config                Show where overridable paths are cached.
#   help                  Show this help.
#
# Overridable paths (env var OR local_testing/.generated/config.env):
#   ONI_APP        Path to OxygenNotIncluded.app (1st / host instance)
#   ONI_APP_2      Path to a 2nd OxygenNotIncluded.app (optional 2nd instance)
#   ONI_MODS_DIR   ONI mods folder (the one that contains local/ and dev/)
#
set -euo pipefail

# --- locate ourselves & the repo -------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
GEN_DIR="$SCRIPT_DIR/.generated"
CONFIG="$GEN_DIR/config.env"
PIDS_FILE="$GEN_DIR/instances.pids"
STEAM_APP_ID=457140
MOD_PACKAGE="$REPO_ROOT/mod/oni_mp"   # the committed local mod package
MOD_FOLDER_NAME="oni_mp"              # folder name under mods/local/

mkdir -p "$GEN_DIR"

# --- defaults for macOS -----------------------------------------------------
DEFAULT_STEAM_COMMON="$HOME/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded"
DEFAULT_APP="$DEFAULT_STEAM_COMMON/OxygenNotIncluded.app"

# Capture caller-supplied env BEFORE sourcing the cache so env always wins.
_env_app="${ONI_APP:-}"
_env_app2="${ONI_APP_2:-}"
_env_mods="${ONI_MODS_DIR:-}"
# shellcheck disable=SC1090
[ -f "$CONFIG" ] && . "$CONFIG"

ONI_APP="${_env_app:-${ONI_APP:-$DEFAULT_APP}}"
ONI_APP_2="${_env_app2:-${ONI_APP_2:-}}"
ONI_MODS_DIR="${_env_mods:-${ONI_MODS_DIR:-}}"

# --- pretty output (colors only when writing to a terminal) -----------------
if [ -t 1 ]; then
    c_blue=$'\033[34m'; c_green=$'\033[32m'; c_yellow=$'\033[33m'; c_red=$'\033[31m'; c_dim=$'\033[2m'; c_off=$'\033[0m'
else
    c_blue=''; c_green=''; c_yellow=''; c_red=''; c_dim=''; c_off=''
fi
info()  { printf '%s==>%s %s\n' "$c_blue"  "$c_off" "$*"; }
ok()    { printf '%s ok %s %s\n' "$c_green" "$c_off" "$*"; }
warn()  { printf '%swarn%s %s\n' "$c_yellow" "$c_off" "$*" >&2; }
err()   { printf '%sERR %s %s\n' "$c_red"   "$c_off" "$*" >&2; }
die()   { err "$*"; exit 1; }

# --- helpers ----------------------------------------------------------------

# Resolve the real executable inside an .app bundle (reads CFBundleExecutable,
# falls back to the lone file in Contents/MacOS).
app_binary() {
    local app="$1" exe
    [ -d "$app" ] || { echo ""; return 0; }
    exe="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleExecutable' "$app/Contents/Info.plist" 2>/dev/null || true)"
    if [ -n "$exe" ] && [ -x "$app/Contents/MacOS/$exe" ]; then
        echo "$app/Contents/MacOS/$exe"; return 0
    fi
    # fallback: first executable file in Contents/MacOS
    # (|| true guards against pipefail/SIGPIPE aborting the script under set -e)
    /usr/bin/find "$app/Contents/MacOS" -maxdepth 1 -type f -perm +111 2>/dev/null | head -1 || true
}

# Best-effort detection of ONI's mods folder on macOS.
detect_mods_dir() {
    [ -n "$ONI_MODS_DIR" ] && { echo "$ONI_MODS_DIR"; return 0; }
    local c
    for c in \
        "$HOME/Library/Application Support/Klei/OxygenNotIncluded/mods" \
        "$HOME/Documents/Klei/OxygenNotIncluded/mods" \
        "$HOME/Library/Application Support/Klei/Oxygen Not Included/mods"
    do
        [ -d "$c" ] && { echo "$c"; return 0; }
    done
    # nothing exists yet — return the conventional macOS location so we can create it
    echo "$HOME/Library/Application Support/Klei/OxygenNotIncluded/mods"
}

save_config() {
    cat > "$CONFIG" <<EOF
# Generated by oni-mp-dev.sh — edit to override detected paths.
ONI_APP="$ONI_APP"
ONI_APP_2="$ONI_APP_2"
ONI_MODS_DIR="$(detect_mods_dir)"
EOF
    ok "cached paths -> ${CONFIG/#$HOME/\~}"
}

# --- commands ---------------------------------------------------------------

cmd_paths() {
    local app1_bin app2_bin mods
    app1_bin="$(app_binary "$ONI_APP")"
    mods="$(detect_mods_dir)"

    info "Repo root         : ${REPO_ROOT/#$HOME/\~}"
    info "Mod package       : ${MOD_PACKAGE/#$HOME/\~}"
    [ -f "$MOD_PACKAGE/mod.yaml" ] && ok "mod package looks valid (mod.yaml present)" \
        || warn "mod package mod.yaml not found — did you build the mod?"

    echo
    info "Instance 1 (.app) : ${ONI_APP/#$HOME/\~}"
    if [ -n "$app1_bin" ]; then ok "binary: ${app1_bin/#$HOME/\~}"
    else err "OxygenNotIncluded.app not found — set ONI_APP (see: $0 config)"; fi

    if [ -n "$ONI_APP_2" ]; then
        app2_bin="$(app_binary "$ONI_APP_2")"
        info "Instance 2 (.app) : ${ONI_APP_2/#$HOME/\~}"
        if [ -n "$app2_bin" ]; then ok "binary: ${app2_bin/#$HOME/\~}"
        else err "2nd .app not found at ONI_APP_2"; fi
    else
        info "Instance 2 (.app) : ${c_dim}(none set — launch2 will run the 1st app twice)${c_off}"
    fi

    echo
    info "ONI mods folder   : ${mods/#$HOME/\~}"
    [ -d "$mods" ] && ok "exists" || warn "does not exist yet (install will create it)"
    local installed="$mods/local/$MOD_FOLDER_NAME"
    [ -d "$installed" ] && ok "mod installed at local/$MOD_FOLDER_NAME" \
        || info "mod not yet installed (run: $0 install)"
}

cmd_install() {
    [ -f "$MOD_PACKAGE/mod.yaml" ] || die "no mod package at $MOD_PACKAGE (build the mod first)"
    local mods dest
    mods="$(detect_mods_dir)"
    dest="$mods/local/$MOD_FOLDER_NAME"
    info "Installing local test package"
    mkdir -p "$mods/local"
    rm -rf "$dest"
    cp -R "$MOD_PACKAGE" "$dest"
    ok "installed -> ${dest/#$HOME/\~}"
    info "Enable it in-game under Mods, then restart ONI."
    info "Both instances share this folder, so one install covers both."
}

# Write/remove steam_appid.txt next to the game binary (its working dir).
_appid_for_app() {
    local app="$1" action="$2" bin macos
    bin="$(app_binary "$app")"; [ -n "$bin" ] || { warn "skip (no binary): $app"; return 0; }
    macos="$(dirname "$bin")"
    if [ "$action" = remove ]; then
        rm -f "$macos/steam_appid.txt" && ok "removed steam_appid.txt from ${macos/#$HOME/\~}"
    else
        printf '%s' "$STEAM_APP_ID" > "$macos/steam_appid.txt" \
            || die "cannot write steam_appid.txt to ${macos/#$HOME/\~} (permission?)"
        ok "wrote steam_appid.txt ($STEAM_APP_ID) -> ${macos/#$HOME/\~}"
    fi
}

cmd_appid() {
    local action=write
    [ "${1:-}" = "--remove" ] && action=remove
    _appid_for_app "$ONI_APP" "$action"
    [ -n "$ONI_APP_2" ] && _appid_for_app "$ONI_APP_2" "$action"
}

# Launch one instance. $1 = label, $2 = .app path, $3 = "fg"|"bg"
_launch_one() {
    local label="$1" app="$2" mode="$3" bin macos log
    bin="$(app_binary "$app")"
    [ -n "$bin" ] || die "cannot find game binary in: $app"
    macos="$(dirname "$bin")"
    log="$GEN_DIR/instance-$label.log"
    info "Launching instance $label"
    printf '   app : %s\n' "${app/#$HOME/\~}"
    printf '   log : %s\n' "${log/#$HOME/\~}"
    # cd into the binary dir so a steam_appid.txt there is picked up, and also
    # export SteamAppId so it works even without the file.
    if [ "$mode" = bg ]; then
        # nohup + disown so the instances keep running after this script (or a
        # double-clicked .command window) exits — otherwise SIGHUP kills them.
        ( cd "$macos" && SteamAppId="$STEAM_APP_ID" exec nohup "$bin" >"$log" 2>&1 ) &
        local pid=$!
        disown 2>/dev/null || true
        echo "$pid" >> "$PIDS_FILE"
        ok "started (pid $pid)"
    else
        ( cd "$macos" && SteamAppId="$STEAM_APP_ID" exec "$bin" )
    fi
}

cmd_launch() {
    local mode=fg
    [ "${1:-}" = "--bg" ] && mode=bg
    _launch_one "1" "$ONI_APP" "$mode"
}

cmd_launch2() {
    : > "$PIDS_FILE"
    local app2="${ONI_APP_2:-$ONI_APP}"
    if [ "$app2" = "$ONI_APP" ]; then
        warn "Both instances use the SAME install. If the 2nd refuses to start,"
        warn "run '$0 clone-instance <dir>' to make a separate copy (see README)."
    fi
    _launch_one "1" "$ONI_APP" bg
    sleep 2
    _launch_one "2" "$app2" bg
    echo
    ok "Two instances launching. Logs: ${GEN_DIR/#$HOME/\~}/instance-*.log"
    info "Stop them with: $0 stop"
    info "To actually connect them, see 'Connecting the instances' in the README"
    info "(use the direct-IP / Riptide transport — Steam P2P shares one account)."
}

cmd_clone_instance() {
    local dest="${1:-}"
    [ -n "$dest" ] || die "usage: $0 clone-instance <destination-folder-or-.app>"
    [ -d "$ONI_APP" ] || die "source .app not found: $ONI_APP"
    # Allow passing a folder; place a copy of the .app inside it.
    case "$dest" in
        *.app) : ;;
        *) dest="${dest%/}/OxygenNotIncluded.app" ;;
    esac
    info "Cloning install (this copies the whole game — may take a minute)"
    printf '   from: %s\n' "${ONI_APP/#$HOME/\~}"
    printf '   to  : %s\n' "${dest/#$HOME/\~}"
    mkdir -p "$(dirname "$dest")"
    # ditto preserves the app bundle correctly on macOS.
    if command -v ditto >/dev/null 2>&1; then ditto "$ONI_APP" "$dest"; else cp -R "$ONI_APP" "$dest"; fi
    ONI_APP_2="$dest"
    save_config
    ok "2nd instance set to ${dest/#$HOME/\~}"
    info "Tip: point this copy at the 'public' branch and your main install at"
    info "'public_testing' (or vice-versa) to test across game branches."
}

cmd_stop() {
    [ -f "$PIDS_FILE" ] || { info "no tracked instances"; return 0; }
    local pid stopped=0 i
    while read -r pid; do
        [ -n "$pid" ] || continue
        kill -0 "$pid" 2>/dev/null || continue        # already gone
        kill "$pid" 2>/dev/null || true               # ask nicely (SIGTERM)
        i=0
        while kill -0 "$pid" 2>/dev/null && [ "$i" -lt 10 ]; do sleep 0.3; i=$((i + 1)); done
        kill -0 "$pid" 2>/dev/null && kill -9 "$pid" 2>/dev/null || true  # force if still up
        ok "stopped pid $pid"; stopped=1
    done < "$PIDS_FILE"
    : > "$PIDS_FILE"
    [ "$stopped" = 1 ] || info "nothing was running"
}

cmd_config() {
    info "Cached config: ${CONFIG/#$HOME/\~}"
    [ -f "$CONFIG" ] && cat "$CONFIG" || info "(none yet — run '$0 paths' or set ONI_APP/ONI_MODS_DIR)"
    echo
    info "Resolved now:"
    local mods; mods="$(detect_mods_dir)"
    printf '   ONI_APP      = %s\n' "${ONI_APP/#$HOME/\~}"
    printf '   ONI_APP_2    = %s\n' "${ONI_APP_2:-<none>}"
    printf '   ONI_MODS_DIR = %s\n' "${mods/#$HOME/\~}"
}

# Scaffold the (git-ignored) Directory.Build.props.user the mod build needs,
# using the detected game + mods paths, then restore the dotnet tools.
cmd_setup() {
    local force=""
    [ "${1:-}" = "--force" ] && force=1
    local props="$REPO_ROOT/Directory.Build.props.user"
    if [ -f "$props" ] && [ -z "$force" ]; then
        ok "Directory.Build.props.user already exists — leaving it (use --force to overwrite)."
    else
        local managed="$ONI_APP/Contents/Resources/Data/Managed"
        local modfolder; modfolder="$(detect_mods_dir)/dev"
        [ -f "$managed/Assembly-CSharp.dll" ] \
            || warn "game libs not found at ${managed/#$HOME/\~} — fix GameLibsFolder in the file if wrong."
        cat > "$props" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<!-- Generated by local_testing/oni-mp-dev.sh setup. Edit if a path is wrong. -->
<Project>
	<PropertyGroup>
		<GameLibsFolder>$managed</GameLibsFolder>
		<ModFolder>$modfolder</ModFolder>
	</PropertyGroup>
</Project>
EOF
        ok "wrote ${props/#$HOME/\~}"
        info "  GameLibsFolder = ${managed/#$HOME/\~}"
        info "  ModFolder      = ${modfolder/#$HOME/\~}"
    fi
    if command -v dotnet >/dev/null 2>&1; then
        info "Restoring dotnet tools (publicizer/refasmer)…"
        ( cd "$REPO_ROOT" && dotnet tool restore ) && ok "tools restored"
    else
        warn "dotnet SDK not found — install the .NET 8 SDK to build (see the main README)."
    fi
}

# Compile the mod. The repo's MSBuild targets auto-deploy it to your ModFolder.
cmd_build() {
    local cfg=Debug
    [ "${1:-}" = "--release" ] && cfg=Release
    command -v dotnet >/dev/null 2>&1 \
        || die "dotnet SDK not found (need the .NET 8 SDK). Run '$0 setup' or see the main README."
    [ -f "$REPO_ROOT/Directory.Build.props.user" ] \
        || die "Directory.Build.props.user missing — run '$0 setup' first (the build needs your game-libs path)."
    info "Restoring dotnet tools…"; ( cd "$REPO_ROOT" && dotnet tool restore ) >/dev/null
    info "Building ONI_Together ($cfg). The first build also publicizes the game DLLs — can take a few minutes."
    ( cd "$REPO_ROOT" && dotnet build ONI_Together/ONI_Together.csproj -c "$cfg" )
    ok "Build complete — auto-deployed to your ModFolder as 'ONI_Together_dev'."
    warn "That dev mod is SEPARATE from '$0 install' (mods/local/oni_mp); enable only ONE in-game to avoid a duplicate-mod conflict."
}

# Full from-source workflow: compile, then launch two instances.
cmd_dev() {
    cmd_build "$@"
    echo
    cmd_launch2
}

cmd_help() {
    # Print the leading comment block (between the shebang and the first
    # non-comment line), stripping the leading "# ". Robust to edits above.
    awk 'NR==1 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "${BASH_SOURCE[0]}"
}

# --- dispatch ---------------------------------------------------------------
main() {
    local cmd="${1:-help}"; shift || true
    case "$cmd" in
        paths|doctor)      cmd_paths "$@" ;;
        setup)             cmd_setup "$@" ;;
        build)             cmd_build "$@" ;;
        dev)               cmd_dev "$@" ;;
        install)           cmd_install "$@" ;;
        appid)             cmd_appid "$@" ;;
        launch|launch1)    cmd_launch "$@" ;;
        launch2|two)       cmd_launch2 "$@" ;;
        clone-instance)    cmd_clone_instance "$@" ;;
        stop)              cmd_stop "$@" ;;
        config)            cmd_config "$@" ;;
        help|-h|--help)    cmd_help ;;
        *) err "unknown command: $cmd"; echo; cmd_help; exit 2 ;;
    esac
}
main "$@"
