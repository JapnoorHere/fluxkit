<#
.SYNOPSIS
    Local end-to-end checks: real OLE drags driven by FluxKit.DragHarness through the real FluxKit selector.

.DESCRIPTION
    Requirements: an unlocked, interactive desktop session with at least 2 virtual desktops,
    FluxKit running with Drag-to-Switch on and default overlay settings (bottom center, standard size).
    The harness moves your mouse for a few seconds per scenario; don't touch the mouse or keyboard meanwhile.
    It returns you to your original desktop afterwards. It never creates or deletes desktops.

    Results: artifacts/e2e/<scenario>.json and artifacts/e2e-results.json.
#>
param(
    [int]$TargetDesktop = 2,
    [ValidateSet('all', 'drop', 'cancel', 'release-on-overlay')]
    [string]$Scenario = 'all'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

if (-not (Get-Process FluxKit -ErrorAction SilentlyContinue)) {
    throw 'FluxKit is not running. Start src/FluxKit.App/bin/.../FluxKit.exe first.'
}

dotnet build "$root/tools/FluxKit.DragHarness" -c Debug -nologo -v:q
dotnet build "$root/tools/FluxKit.DropProbe" -c Debug -nologo -v:q
$harness = Get-ChildItem "$root/tools/FluxKit.DragHarness/bin/Debug" -Recurse -Filter FluxKit.DragHarness.exe | Select-Object -First 1
if (-not $harness) { throw 'DragHarness build output not found.' }

$outDir = Join-Path $root 'artifacts/e2e'
New-Item -ItemType Directory -Force $outDir | Out-Null
$scenarios = if ($Scenario -eq 'all') { @('drop', 'cancel', 'release-on-overlay') } else { @($Scenario) }

$summary = foreach ($s in $scenarios) {
    $out = Join-Path $outDir "$s.json"
    Write-Host "Running scenario '$s' (target desktop $TargetDesktop)..." -ForegroundColor Cyan
    & $harness.FullName --switch fluxkit --target $TargetDesktop --scenario $s --out $out
    $code = $LASTEXITCODE
    [pscustomobject]@{ scenario = $s; exitCode = $code; passed = ($code -eq 0); result = $out; ranAt = (Get-Date).ToString('o') }
    Start-Sleep -Seconds 2
}

$summary | ConvertTo-Json | Set-Content (Join-Path $root 'artifacts/e2e-results.json')
$summary | Format-Table -AutoSize
if ($summary.Where({ -not $_.passed }).Count -gt 0) { exit 1 }
