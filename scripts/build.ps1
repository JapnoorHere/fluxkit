<#
.SYNOPSIS
    Builds the whole FluxKit solution (app, libraries, tools, tests).
.PARAMETER Verify
    Also checks that every file matches .editorconfig (dotnet format). Fails if anything needs formatting.
.EXAMPLE
    ./scripts/build.ps1 -Configuration Release -Verify
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Verify
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

dotnet build "$root/FluxKit.slnx" -c $Configuration -nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }

if ($Verify) {
    dotnet format "$root/FluxKit.slnx" --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Some files don't match .editorconfig. Run 'dotnet format FluxKit.slnx' to fix them." }
    Write-Host "Formatting is clean." -ForegroundColor Green
}

Write-Host "Build succeeded ($Configuration)." -ForegroundColor Green
