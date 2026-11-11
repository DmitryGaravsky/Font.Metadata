#Requires -Version 7.0
[CmdletBinding()]
param([string] $Version)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$Version = & (Join-Path $PSScriptRoot 'get-version.ps1') -Version $Version
function Invoke-DotNet([string[]] $Arguments) {
    & dotnet @Arguments
    if($LASTEXITCODE -ne 0) {
        throw ('dotnet {0} failed with exit code {1}.' -f ($Arguments -join ' '), $LASTEXITCODE)
    }
}
Push-Location $root
try {
    & (Join-Path $PSScriptRoot 'test-release.ps1')
    Invoke-DotNet @('restore', 'Font.Metadata.slnx', '--configfile', 'NuGet.Config', "-p:Version=$Version")
    Invoke-DotNet @('build', 'Font.Metadata.slnx', '-c', 'Release', '--no-restore', "-p:Version=$Version")
    Invoke-DotNet @('test', 'Font.Metadata.Tests/Font.Metadata.Tests.csproj',
        '-c', 'Release', '--no-build', '--no-restore', "-p:Version=$Version",
        '--logger', 'trx;LogFilePrefix=library', '--results-directory', 'artifacts/test-results/library')
    Invoke-DotNet @('test', 'Font.Metadata.Tool.Tests/Font.Metadata.Tool.Tests.csproj',
        '-c', 'Release', '--no-build', '--no-restore', "-p:Version=$Version",
        '--logger', 'trx;LogFilePrefix=tool', '--results-directory', 'artifacts/test-results/tool')
    Invoke-DotNet @('format', 'whitespace', 'Font.Metadata.slnx', '--verify-no-changes', '--no-restore')
    & (Join-Path $PSScriptRoot 'verify-packages.ps1') -Version $Version
} finally {
    Pop-Location
}
