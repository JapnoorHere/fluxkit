<#
.SYNOPSIS
    Publishes a self-contained FluxKit build and zips it as artifacts/FluxKit-<version>-<rid>.zip.
.EXAMPLE
    ./scripts/publish.ps1 -Runtime win-x64
    ./scripts/publish.ps1 -Runtime win-arm64
#>
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$props = Get-Content "$root/Directory.Build.props"
$group = $props.Project.PropertyGroup | Where-Object { $_.VersionPrefix } | Select-Object -First 1
$version = if ($group.VersionSuffix) { "$($group.VersionPrefix)-$($group.VersionSuffix)" } else { $group.VersionPrefix }
$out = Join-Path $root "artifacts/publish/$Runtime"
$zip = Join-Path $root "artifacts/FluxKit-$version-$Runtime.zip"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish "$root/src/FluxKit.App/FluxKit.App.csproj" -c Release -r $Runtime --self-contained `
    -p:WindowsAppSDKSelfContained=true -p:PublishReadyToRun=true -o $out -nologo
if ($LASTEXITCODE -ne 0) { throw "Publish failed ($LASTEXITCODE)." }

Copy-Item "$root/README.md", "$root/LICENSE", "$root/CHANGELOG.md" -Destination $out -ErrorAction SilentlyContinue
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path "$out/*" -DestinationPath $zip
$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host "Created $zip ($size MB)." -ForegroundColor Green
