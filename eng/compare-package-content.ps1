#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ExpectedPackage, [Parameter(Mandatory)][string]$ActualPackage)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Read-Entries([IO.Compression.ZipArchive]$Archive) {
    $entries = [Collections.Generic.Dictionary[string, IO.Compression.ZipArchiveEntry]]::new([StringComparer]::Ordinal)
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $Archive.Entries) {
        if (-not $names.Add($entry.FullName)) {
            throw 'Duplicate or case-colliding package entries are not allowed.'
        }
        # NuGet.org adds/replaces the root repository signature without changing package payloads.
        # https://learn.microsoft.com/en-us/nuget/reference/signed-packages-reference
        if ($entry.FullName -ceq '.signature.p7s') {
            continue
        }
        $entries.Add($entry.FullName, $entry)
    }
    return ,$entries
}
$expected = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ExpectedPackage).ProviderPath)
try {
    $actual = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ActualPackage).ProviderPath)
    try {
        $expectedEntries = Read-Entries $expected
        $actualEntries = Read-Entries $actual
        if ($expectedEntries.Count -ne $actualEntries.Count) {
            throw 'Published package file set differs from the verified artifact.'
        }
        foreach ($name in $expectedEntries.Keys) {
            if (-not $actualEntries.ContainsKey($name) -or $expectedEntries[$name].Length -ne $actualEntries[$name].Length) {
                throw "Published package entry differs: $name"
            }
            $expectedStream = $expectedEntries[$name].Open()
            try { $expectedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($expectedStream)) }
            finally { $expectedStream.Dispose() }
            $actualStream = $actualEntries[$name].Open()
            try { $actualHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($actualStream)) }
            finally { $actualStream.Dispose() }
            if ($expectedHash -cne $actualHash) {
                throw "Published package bytes differ: $name"
            }
        }
    }
    finally { $actual.Dispose() }
}
finally { $expected.Dispose() }
Write-Host 'Published package payload matches the verified artifact (repository signature excluded).'
