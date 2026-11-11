#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string] $Version, [Parameter(Mandatory)][string] $Directory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Version = & (Join-Path $PSScriptRoot 'get-version.ps1') -Version $Version
$names = @("Font.Metadata.$Version.nupkg", "Font.Metadata.Tool.$Version.nupkg", "font-metadata-$Version-portable.zip")
$files = @(Get-ChildItem -LiteralPath $Directory -Force)
$expectedFiles = $names + 'SHA256SUMS'
$unexpectedFiles = @($files | Where-Object { $_.PSIsContainer -or $_.Name -cnotin $expectedFiles })
if($files.Count -ne $expectedFiles.Count -or $unexpectedFiles.Count -ne 0) {
    throw 'Expected exactly two packages, one portable ZIP and SHA256SUMS.'
}
$lines = @(Get-Content -LiteralPath (Join-Path $Directory 'SHA256SUMS'))
if($lines.Count -ne $names.Count) { throw 'Invalid checksum manifest.' }
foreach($name in $names) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $Directory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if("$hash  $name" -cnotin $lines) { throw ('Checksum mismatch for {0}' -f $name) }
}
foreach($id in @('Font.Metadata', 'Font.Metadata.Tool')) {
    $archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $Directory "$id.$Version.nupkg"))
    try {
        $entry = $archive.GetEntry("$id.nuspec")
        if($null -eq $entry) { throw ('Missing nuspec for {0}' -f $id) }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml] $spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if($spec.package.metadata.id -cne $id -or $spec.package.metadata.version -cne $Version) {
            throw ('Unexpected package identity for {0}' -f $id)
        }
    } finally { $archive.Dispose() }
}
Write-Host 'Release file names, checksums and package identities verified.'
