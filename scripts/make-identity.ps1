<#
.SYNOPSIS
    Manages FluxKit's identity package (ADR-014), which lets FluxKit read Windows notifications.
.DESCRIPTION
    -Register   Development: registers packaging/Identity/AppxManifest.xml unsigned (needs Developer Mode), pointing it
                at the built app folder. Per user, no admin rights.
    -Pack       Release: packs the manifest into FluxKit.Identity.msix and signs it with your certificate. Ship the .msix
                in the app's Identity folder; FluxKit registers it itself when the user turns notifications on.
    -Unregister Removes the identity package for the current user.
.EXAMPLE
    ./scripts/make-identity.ps1 -Register
    ./scripts/make-identity.ps1 -Pack -Certificate .\fluxkit.pfx -Password (Read-Host -AsSecureString)
#>
param(
    [switch]$Register,
    [switch]$Pack,
    [switch]$Unregister,
    [string]$Certificate,
    [securestring]$Password,
    [string]$Configuration = 'Debug'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$manifestDir = Join-Path $root 'packaging/Identity'
$appDir = Join-Path $root "src/FluxKit.App/bin/$Configuration/net10.0-windows10.0.26100.0/win-x64"

function Find-SdkTool([string]$name) {
    $tool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Recurse -Filter $name -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } | Sort-Object FullName | Select-Object -Last 1
    if (-not $tool) { throw "$name was not found. Install the Windows 10/11 SDK." }
    $tool.FullName
}

if ($Unregister) {
    Get-AppxPackage -Name 'FluxKit.Identity' | Remove-AppxPackage
    Write-Host 'Identity package removed.' -ForegroundColor Green
}

if ($Register) {
    if (-not (Test-Path $appDir)) { throw "Build FluxKit first ($appDir is missing)." }
    Add-AppxPackage -Register (Join-Path $manifestDir 'AppxManifest.xml') -ExternalLocation $appDir
    Write-Host "Identity registered for $appDir. Restart FluxKit to use it." -ForegroundColor Green
}

if ($Pack) {
    if (-not $Certificate) { throw 'Pass -Certificate with the .pfx used to sign releases.' }
    $out = Join-Path $root 'artifacts/identity'
    New-Item -ItemType Directory -Force $out | Out-Null
    $msix = Join-Path $out 'FluxKit.Identity.msix'
    & (Find-SdkTool 'makeappx.exe') pack /d $manifestDir /p $msix /nv /o
    if ($LASTEXITCODE -ne 0) { throw 'MakeAppx failed.' }
    $plain = [Runtime.InteropServices.Marshal]::PtrToStringUni([Runtime.InteropServices.Marshal]::SecureStringToGlobalAllocUnicode($Password))
    & (Find-SdkTool 'signtool.exe') sign /fd SHA256 /f $Certificate /p $plain $msix
    if ($LASTEXITCODE -ne 0) { throw 'SignTool failed.' }
    Write-Host "Signed identity package: $msix (the certificate's subject must match the manifest's Publisher)." -ForegroundColor Green
}
