# Local multiplayer testing on macOS

Tooling to run **two copies of Oxygen Not Included at once** on a single Mac so
you can test the **ONI Together** multiplayer mod (host + client) without a
second machine or a second Steam account window.

> **Why this is needed:** Steam refuses to launch a game twice, and macOS
> refuses to open the same `.app` twice. The game executable itself doesn't
> care — it only needs to know its Steam **AppId (`457140`)**. Give it that and
> run the binary inside the `.app` directly, and a second window comes right up.
>
> We supply the AppId two ways (either alone is enough; the script does both):
> 1. the `SteamAppId=457140` environment variable, **or**
> 2. a `steam_appid.txt` file containing `457140` in the working directory —
>    the same "drop a file next to the game" idea as the `boot.config` debug
>    flags in the main README.

---

## TL;DR

```bash
cd local_testing
chmod +x oni-mp-dev.sh *.command     # first time only

./oni-mp-dev.sh paths                # check it found your game + mods folder
./oni-mp-dev.sh install              # install the mod into mods/local
./oni-mp-dev.sh launch2              # boot two instances
```

Or, from Finder, double-click **`Check setup.command`**,
**`Install mod (local).command`**, then **`Launch 2 instances.command`**.

---

## Requirements

- Oxygen Not Included installed via Steam, and the **Steam client running and
  logged in** (the game still talks to Steam for its API; it just doesn't go
  through Steam's launcher).
- A built mod package at `../mod/oni_mp/` (already committed in this repo). To
  refresh it, rebuild `ONI_MP.dll` and drop it into `../mod/oni_mp/` — see the
  main README's *Setup* section.

## Commands

| Command | What it does |
|---|---|
| `./oni-mp-dev.sh paths` | Detect & print every path used; flags anything missing. Run this first. |
| `./oni-mp-dev.sh install` | Copy `../mod/oni_mp` → `…/Klei/OxygenNotIncluded/mods/local/oni_mp`. |
| `./oni-mp-dev.sh appid` | Write `steam_appid.txt` (`457140`) next to the game binary. (Optional — `launch` already sets the env var.) |
| `./oni-mp-dev.sh appid --remove` | Remove those `steam_appid.txt` files. |
| `./oni-mp-dev.sh launch` | Launch ONE instance in the foreground (add `--bg` to background it). |
| `./oni-mp-dev.sh launch2` | Launch TWO instances, backgrounded, for host+client testing. |
| `./oni-mp-dev.sh clone-instance <dir>` | Duplicate the whole game install to `<dir>` and remember it as the 2nd instance. |
| `./oni-mp-dev.sh stop` | Stop instances this script started. |
| `./oni-mp-dev.sh config` | Show/locate the cached, overridable paths. |

## If the paths are wrong

The script auto-detects the standard Steam and Klei locations. Override any of
them with environment variables (they're cached in `.generated/config.env`):

```bash
ONI_APP="/path/to/OxygenNotIncluded.app" ./oni-mp-dev.sh paths
ONI_MODS_DIR="$HOME/Library/Application Support/Klei/OxygenNotIncluded/mods" ./oni-mp-dev.sh install
```

Defaults it looks for:
- Game: `~/Library/Application Support/Steam/steamapps/common/OxygenNotIncluded/OxygenNotIncluded.app`
- Mods: `~/Library/Application Support/Klei/OxygenNotIncluded/mods`

## One install or two?

The mod lives in your per-user `mods/local` folder, **not** inside the game
install, so **one `install` covers both instances** no matter how they're
launched.

`launch2` runs the *same* `.app` twice by default. If macOS or the game won't
allow that, make a second copy and let the script use it:

```bash
./oni-mp-dev.sh clone-instance "$HOME/ONI-test/second"
./oni-mp-dev.sh launch2
```

This mirrors the proven two-folder setup (one folder on Steam's `public`
branch, the other on `public_testing`) — handy for testing across game
branches, not just for two windows.

## Connecting the two instances

Two local instances are signed into the **same Steam account**, so Steam's
P2P transport can't tell them apart. For real host↔client testing use the
**direct-IP (Riptide) transport**:

1. In instance **A**, host a session on the Riptide / direct-connect transport.
2. In instance **B**, connect to `127.0.0.1` on the host's port (default `7777`).

Transport, IP and port live in
`../ONI_Together_DedicatedServer/testing/dedicated_server_config/multiplayer_settings.json`
(`"Transport"`, `"Ip"`, `"Port"`), and the in-game debug menu lets you pick the
host transport. A standalone dedicated server is also available under
`../ONI_Together_DedicatedServer/` if you'd rather host headless and connect
both windows as clients.

## Logs & cleanup

- Per-instance output: `local_testing/.generated/instance-1.log`, `instance-2.log`
- ONI's own player log (macOS): `~/Library/Logs/Klei/Oxygen Not Included/Player.log`
- `.generated/` is git-ignored. `./oni-mp-dev.sh stop` ends what the script started.

## Other platforms

This folder targets macOS. The same trick works elsewhere:
- **Windows:** a `.bat`/shortcut that sets `SteamAppId=457140` then runs
  `OxygenNotIncluded.exe`, or a `steam_appid.txt` in the game folder.
- **Linux/Proton:** export `SteamAppId=457140` before launching the binary, or
  drop the `steam_appid.txt` file beside it.
