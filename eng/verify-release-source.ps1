#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Tag, [Parameter(Mandatory)][string]$Commit)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Set-StrictMode -Version Latest
$version = & "$PSScriptRoot/get-version.ps1" -Tag $Tag
if ($Commit -cnotmatch '\A[0-9a-f]{40}\z') {
    throw 'Expected the full commit SHA from the release event.'
}
function Read-GitHub([string]$Path) {
    $json = & gh api "repos/DmitryGaravsky/Font.Metadata/$Path" --method GET
    if ($LASTEXITCODE -ne 0) {
        throw "Cannot read release source: $Path"
    }
    return (($json -join [char]10) | ConvertFrom-Json)
}
# Resolve the explicit tag ref, including annotated tags, rather than a branch with the same name.
$source = Read-GitHub "commits/refs/tags/$Tag"
if ($source.sha -cne $Commit) {
    throw 'The release tag no longer points to the event commit.'
}
$comparison = Read-GitHub "compare/$Commit...main"
if ($comparison.status -cnotin @('ahead', 'identical') -or $comparison.merge_base_commit.sha -cne $Commit) {
    throw 'The release commit must belong to main.'
}
$release = Read-GitHub "releases/tags/$Tag"
if ($release.tag_name -cne $Tag -or $release.draft -or $release.prerelease -ne $version.Contains('-')) {
    throw 'Expected a published release with a matching tag and prerelease classification.'
}
if ($release.immutable) {
    $names = @($release.assets | ForEach-Object { $_.name })
    $required = @("Font.Metadata.$version.nupkg", "Font.Metadata.Spdx.$version.nupkg",
        "Font.Metadata.Tool.$version.nupkg", "font-metadata-$version-portable.zip", 'SHA256SUMS')
    if (@($required | Where-Object { $_ -cnotin $names }).Count -gt 0) {
        throw 'An immutable release cannot receive missing files. Disable immutable releases before creating a new UI release.'
    }
}
return $release
