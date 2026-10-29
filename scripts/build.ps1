<#
.SYNOPSIS
    Builds the whole FluxKit solution (app, libraries, tools, tests).
.EXAMPLE
    ./scripts/build.ps1 -Configuration Release
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

dotnet build "$root/FluxKit.slnx" -c $Configuration -nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }
Write-Host "Build succeeded ($Configuration)." -ForegroundColor Green
