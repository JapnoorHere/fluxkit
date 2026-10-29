<#
.SYNOPSIS
    Builds FluxKit and starts it. Quits a running copy first, because a running copy locks its files.
.EXAMPLE
    ./scripts/run.ps1            # opens the FluxKit window
    ./scripts/run.ps1 -Background  # starts in the tray only
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Background
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

Get-Process FluxKit -ErrorAction SilentlyContinue | Stop-Process -Force
& "$PSScriptRoot/build.ps1" -Configuration $Configuration

# The solution build puts the app under bin/<platform>/<configuration>.
$exe = Get-ChildItem "$root/src/FluxKit.App/bin/x64/$Configuration" -Recurse -Filter FluxKit.exe | Select-Object -First 1
if (-not $exe) { throw "FluxKit.exe wasn't found after the build." }

$arguments = if ($Background) { @('--background') } else { @() }
Start-Process $exe.FullName -ArgumentList $arguments
Write-Host "Started $($exe.FullName)" -ForegroundColor Green
