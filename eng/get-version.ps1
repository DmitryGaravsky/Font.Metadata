#Requires -Version 7.0
[CmdletBinding()]
param([string] $Version, [string] $Tag)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Variable -Name ReleaseTagPrefix -Option Constant -Value 'v'
if($Version -and $Tag) { throw 'Specify Version or Tag, not both.' }
if($Tag) {
    if(-not $Tag.StartsWith($ReleaseTagPrefix, [StringComparison]::Ordinal)) { throw 'Release tags must start with v.' }
    $Version = $Tag.Substring($ReleaseTagPrefix.Length)
} elseif(-not $Version) {
    [xml] $properties = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Directory.Build.props') -Raw
    $Version = [string] $properties.Project.PropertyGroup.Version
}
# https://semver.org/#spec-item-9
# Canonical NuGet/SemVer subset: lowercase prerelease labels, no build metadata or numeric leading zeros.
$number = '(0|[1-9][0-9]*)'
$identifier = '(0|[1-9][0-9]*|[0-9]*[a-z-][0-9a-z-]*)'
if($Version -cnotmatch "\A$number\.$number\.$number(-$identifier(\.$identifier)*)?\z") {
    throw ('Invalid version: {0}. Use major.minor.patch[-prerelease] with lowercase labels.' -f $Version)
}
return $Version
