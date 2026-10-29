<#
.SYNOPSIS
    Runs the automated unit tests (no desktop interaction). Use -Coverage for a coverlet report.
.EXAMPLE
    ./scripts/test.ps1 -Coverage
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',
    [switch]$Coverage
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$results = Join-Path $root 'artifacts/test-results'

$testArgs = @("$root/FluxKit.slnx", '-c', $Configuration, '--nologo', '--filter', 'Category!=Integration', '--results-directory', $results)
if ($Coverage) { $testArgs += @('--collect', 'XPlat Code Coverage') }

dotnet test @testArgs
if ($LASTEXITCODE -ne 0) { throw "Tests failed ($LASTEXITCODE)." }
Write-Host "All unit tests passed." -ForegroundColor Green
if ($Coverage) { Write-Host "Coverage files: $results" }
