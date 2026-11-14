#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string] $Tag)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$version = & (Join-Path $PSScriptRoot 'get-version.ps1') -Tag $Tag
$run = Join-Path $root ('artifacts/release-work/' + [Guid]::NewGuid().ToString('N'))
$feed = Join-Path $run 'packages'
& (Join-Path $PSScriptRoot 'verify.ps1') -Version $version -PackageDirectory $feed
$zipName = "font-metadata-$version-portable.zip"
$output = Join-Path $root "artifacts/release/$version"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$names = @("Font.Metadata.$version.nupkg", "Font.Metadata.Spdx.$version.nupkg", "Font.Metadata.Tool.$version.nupkg", $zipName)
foreach($name in $names) { Copy-Item -LiteralPath (Join-Path $feed $name) -Destination $output -Force }
$checksums = foreach($name in $names) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS'), ($checksums -join "`n") + "`n")
& (Join-Path $PSScriptRoot 'verify-release-assets.ps1') -Version $version -Directory $output
Write-Host ('Release files prepared and verified in {0}. Nothing was published.' -f $output)
