#Requires -Version 7.0
[CmdletBinding()]
param([string] $Version, [string] $Tag)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if($Version -and $Tag) { throw 'Specify Version or Tag, not both.' }
if($Tag) {
    if(-not $Tag.StartsWith('v', [StringComparison]::Ordinal)) { throw 'Release tags must start with v.' }
    $Version = $Tag.Substring(1)
} elseif(-not $Version) {
    [xml] $properties = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Directory.Build.props') -Raw
    $Version = [string] $properties.Project.PropertyGroup.Version
}
# Canonical NuGet/SemVer subset: lowercase prerelease labels, no build metadata or numeric leading zeros.
$number = '(0|[1-9][0-9]*)'
$identifier = '(0|[1-9][0-9]*|[0-9]*[a-z-][0-9a-z-]*)'
if($Version -cnotmatch "\A$number\.$number\.$number(-$identifier(\.$identifier)*)?\z") {
    throw "Invalid version: $Version. Use major.minor.patch[-prerelease] with lowercase labels."
}
return $Version
