@echo off
rem ---------------------------------------------------------------------------
rem launch-two-instances.bat
rem Run two copies of Oxygen Not Included at once for ONI Together MP testing.
rem
rem Steam refuses to launch a game twice, but the executable only needs to know
rem its Steam AppId (457140). With SteamAppId set in the environment, Steam's
rem single-instance lock is bypassed and a second window comes right up.
rem (A steam_appid.txt containing 457140 next to the .exe is an equivalent trick,
rem  but writing into Program Files needs admin, so we just set the env var.)
rem
rem Override the game folder by setting ONI_DIR before running, e.g.:
rem    set "ONI_DIR=D:\Steam\steamapps\common\OxygenNotIncluded"
rem    launch-two-instances.bat
rem ---------------------------------------------------------------------------

if "%ONI_DIR%"=="" set "ONI_DIR=C:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded"

if not exist "%ONI_DIR%\OxygenNotIncluded.exe" (
  echo [ERROR] OxygenNotIncluded.exe not found in "%ONI_DIR%".
  echo Set ONI_DIR to your game folder first, e.g.:
  echo     set "ONI_DIR=D:\Steam\steamapps\common\OxygenNotIncluded"
  pause
  exit /b 1
)

set "SteamAppId=457140"
echo Launching instance 1...
start "ONI 1" /D "%ONI_DIR%" "%ONI_DIR%\OxygenNotIncluded.exe"
timeout /t 2 /nobreak >nul
echo Launching instance 2...
start "ONI 2" /D "%ONI_DIR%" "%ONI_DIR%\OxygenNotIncluded.exe"

echo.
echo Two instances launching.
echo To connect them, use the direct-IP (Riptide) transport: host on one window
echo and connect the other to 127.0.0.1:7777. Steam P2P shares one account, so it
echo can't tell two local instances apart.
