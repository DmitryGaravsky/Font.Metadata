#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string] $Tag)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$version = & (Join-Path $PSScriptRoot 'get-version.ps1') -Tag $Tag
& (Join-Path $PSScriptRoot 'verify.ps1') -Version $version
$run = Join-Path $root ('artifacts/release-work/' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $run 'payload'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
$feed = Join-Path $root "artifacts/packages/$version"
$package = [IO.Compression.ZipFile]::OpenRead((Join-Path $feed "Font.Metadata.Tool.$version.nupkg"))
try {
    $licenseEntry = $package.GetEntry('LICENSE')
    if($null -eq $licenseEntry) { throw 'Tool package is missing LICENSE.' }
    [IO.Compression.ZipFileExtensions]::ExtractToFile($licenseEntry, (Join-Path $payload 'LICENSE'))
    $prefix = 'tools/net10.0/any/'
    foreach($entry in $package.Entries) {
        if(-not $entry.FullName.StartsWith($prefix, [StringComparison]::Ordinal) -or -not $entry.Name) {
            continue
        }
        $relative = $entry.FullName.Substring($prefix.Length)
        if($relative -eq 'DotnetToolSettings.xml') {
            continue
        }
        $destination = [IO.Path]::GetFullPath((Join-Path $payload $relative))
        if(-not $destination.StartsWith($payload + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal)) {
            throw 'Tool package contains a path outside its payload.'
        }
        New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination)
    }
} finally {
    $package.Dispose()
}
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $payload
$usage = "Font.Metadata.Tool $version`nRequires the .NET 10 runtime.`nRun: dotnet Font.Metadata.Tool.dll --help`n"
[IO.File]::WriteAllText((Join-Path $payload 'RUNNING.txt'), $usage)
$zipName = "font-metadata-$version-portable.zip"
$zip = Join-Path $run $zipName
[IO.Compression.ZipFile]::CreateFromDirectory($payload, $zip)
$extracted = Join-Path $run 'extracted'
[IO.Compression.ZipFile]::ExtractToDirectory($zip, $extracted)
$expectedLicense = [IO.File]::ReadAllText((Join-Path $root 'LICENSE'))
if([IO.File]::ReadAllText((Join-Path $extracted 'LICENSE')) -cne $expectedLicense) {
    throw 'Portable CLI license text mismatch.'
}
$entryPoint = Join-Path $extracted 'Font.Metadata.Tool.dll'
$help = & dotnet $entryPoint --help
if($LASTEXITCODE -ne 0 -or ($help -join "`n") -notmatch 'Usage: font-metadata') { throw 'Portable CLI help failed.' }
$actual = & dotnet $entryPoint --version
if($LASTEXITCODE -ne 0 -or ($actual.Trim() -split '\+')[0] -cne $version) { throw 'Portable CLI version mismatch.' }
$output = Join-Path $root "artifacts/release/$version"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$names = @("Font.Metadata.$version.nupkg", "Font.Metadata.Tool.$version.nupkg", $zipName)
foreach($name in $names[0..1]) { Copy-Item -LiteralPath (Join-Path $feed $name) -Destination $output -Force }
Copy-Item -LiteralPath $zip -Destination $output -Force
$checksums = foreach($name in $names) {
    $hash = (Get-FileHash -LiteralPath (Join-Path $output $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $name"
}
[IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS'), ($checksums -join "`n") + "`n")
& (Join-Path $PSScriptRoot 'verify-release-assets.ps1') -Version $version -Directory $output
Write-Host ('Release files prepared and verified in {0}. Nothing was published.' -f $output)
