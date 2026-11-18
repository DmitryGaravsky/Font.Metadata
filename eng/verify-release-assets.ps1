#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string] $Version, [Parameter(Mandatory)][string] $Directory, [string] $Commit)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$Version = & (Join-Path $PSScriptRoot 'get-version.ps1') -Version $Version
$Directory = (Resolve-Path -LiteralPath $Directory).ProviderPath
$names = @("Font.Metadata.$Version.nupkg", "Font.Metadata.Spdx.$Version.nupkg", "Font.Metadata.Tool.$Version.nupkg", "font-metadata-$Version-portable.zip")
$files = @(Get-ChildItem -LiteralPath $Directory -Force)
$expectedFiles = $names + 'SHA256SUMS'
$unexpectedFiles = @($files | Where-Object { $_.PSIsContainer -or ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $_.Name -cnotin $expectedFiles })
if($files.Count -ne $expectedFiles.Count -or $unexpectedFiles.Count -ne 0) {
    throw 'Expected exactly three packages, one portable ZIP and SHA256SUMS.'
}
$lines = @(Get-Content -LiteralPath (Join-Path $Directory 'SHA256SUMS'))
if($lines.Count -ne $names.Count) { throw 'Invalid checksum manifest.' }
foreach($name in $names) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $Directory $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if("$hash  $name" -cnotin $lines) { throw ('Checksum mismatch for {0}' -f $name) }
}
foreach($id in @('Font.Metadata', 'Font.Metadata.Spdx', 'Font.Metadata.Tool')) {
    $archive = [IO.Compression.ZipFile]::OpenRead((Join-Path $Directory "$id.$Version.nupkg"))
    try {
        $entry = $archive.GetEntry("$id.nuspec")
        if(@($archive.Entries | Where-Object { $_.FullName.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase) }).Count -ne 1 -or $null -eq $entry) { throw ('Missing nuspec for {0}' -f $id) }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml] $spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if($spec.package.metadata.id -cne $id -or $spec.package.metadata.version -cne $Version) {
            throw ('Unexpected package identity for {0}' -f $id)
        }
        $repository = $spec.package.metadata.repository
        if($PSBoundParameters.ContainsKey('Commit') -and
            ($Commit -cnotmatch '\A[0-9a-f]{40}\z' -or $repository.commit -cne $Commit)) {
            throw "Package repository commit differs for $id."
        }
        if($repository.type -cne 'git' -or $repository.url -cne 'https://github.com/DmitryGaravsky/Font.Metadata') {
            throw "Incorrect repository metadata for $id."
        }
        foreach($requiredEntry in @('LICENSE','README.md')) {
            if($null -eq $archive.GetEntry($requiredEntry)) { throw "Missing release package entry: $requiredEntry" }
        }
    } finally { $archive.Dispose() }
}
Write-Host 'Release file names, checksums and package identities verified.'
