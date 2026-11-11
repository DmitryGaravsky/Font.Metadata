#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string] $Tag, [Parameter(Mandatory)][string] $Directory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$version = & (Join-Path $PSScriptRoot 'get-version.ps1') -Tag $Tag
& (Join-Path $PSScriptRoot 'verify-release-assets.ps1') -Version $version -Directory $Directory
function Invoke-Gh([string[]] $Arguments) {
    $result = & gh @Arguments
    if($LASTEXITCODE -ne 0) { throw "GitHub CLI failed: $($Arguments[0..1] -join ' ')" }
    return $result
}
$prerelease = $version.Contains('-')
$details = & gh release view $Tag --json isDraft,isPrerelease,assets
if($LASTEXITCODE -ne 0) {
    # Creating a draft also fails safely if lookup failed because of auth/network errors or an existing release.
    $arguments = @('release', 'create', $Tag, '--draft', '--verify-tag', '--title', $Tag, '--generate-notes')
    if($prerelease) { $arguments += '--prerelease' }
    Invoke-Gh $arguments | Write-Host
    $details = Invoke-Gh @('release', 'view', $Tag, '--json', 'isDraft,isPrerelease,assets')
}
$release = ($details -join "`n") | ConvertFrom-Json
if($release.isPrerelease -ne $prerelease) { throw 'Existing release has a different prerelease classification.' }
$download = Join-Path ([IO.Path]::GetTempPath()) ('font-metadata-release-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $download | Out-Null
foreach($file in Get-ChildItem -LiteralPath $Directory -File) {
    if($file.Name -cin @($release.assets | ForEach-Object { $_.name })) {
        Invoke-Gh @('release', 'download', $Tag, '--pattern', $file.Name, '--dir', $download) | Write-Host
        $remote = (Get-FileHash -LiteralPath (Join-Path $download $file.Name) -Algorithm SHA256).Hash
        if($remote -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) {
            throw "Existing release asset differs: $($file.Name). Assets will not be overwritten."
        }
    } else {
        if(-not $release.isDraft) { throw "Published release is missing asset: $($file.Name)" }
        Invoke-Gh @('release', 'upload', $Tag, $file.FullName) | Write-Host
    }
}
if($release.isDraft) {
    $arguments = @('release', 'edit', $Tag, '--draft=false')
    if($prerelease) { $arguments += '--latest=false' }
    Invoke-Gh $arguments | Write-Host
}
Write-Host "GitHub release $Tag is published with the verified assets."
