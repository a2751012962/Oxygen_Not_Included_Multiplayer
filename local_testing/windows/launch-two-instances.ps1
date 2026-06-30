<#
.SYNOPSIS
    Run two copies of Oxygen Not Included at once for ONI Together MP testing.
.DESCRIPTION
    Steam refuses to launch a game twice, but the executable only needs to know
    its Steam AppId (457140). With $env:SteamAppId set, Steam's single-instance
    lock is bypassed and a second window comes up. Start-Process inherits this
    process's environment, so both launched games see the AppId.
.PARAMETER OniDir
    The OxygenNotIncluded game folder. Defaults to the usual Steam location.
.PARAMETER AppId
    Steam AppId for Oxygen Not Included (457140 — you shouldn't need to change it).
.EXAMPLE
    .\launch-two-instances.ps1
.EXAMPLE
    .\launch-two-instances.ps1 -OniDir 'D:\Steam\steamapps\common\OxygenNotIncluded'
#>
param(
    [string]$OniDir = "C:\Program Files (x86)\Steam\steamapps\common\OxygenNotIncluded",
    [int]$AppId = 457140
)

$exe = Join-Path $OniDir 'OxygenNotIncluded.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Error "OxygenNotIncluded.exe not found at '$exe'. Pass -OniDir '<your game folder>'."
    exit 1
}

$env:SteamAppId = "$AppId"

Write-Host "Launching instance 1..."
Start-Process -FilePath $exe -WorkingDirectory $OniDir
Start-Sleep -Seconds 2
Write-Host "Launching instance 2..."
Start-Process -FilePath $exe -WorkingDirectory $OniDir

Write-Host ""
Write-Host "Two instances launching."
Write-Host "To connect them, use the direct-IP (Riptide) transport: host on one window"
Write-Host "and connect the other to 127.0.0.1:7777. Steam P2P shares one account, so it"
Write-Host "can't tell two local instances apart."
