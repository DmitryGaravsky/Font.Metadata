#Requires -Version 7.0
[CmdletBinding()]
param([string] $Version, [string] $PackageDirectory)
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
$savedEnvironment = @{}
foreach($entry in @{
    DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'; DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'; DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'; DOTNET_NOLOGO = '1'
}.GetEnumerator()) {
    $savedEnvironment[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
    [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
}
$restoreConfig = Join-Path $root 'NuGet.Config'
Push-Location $root
try {
    & (Join-Path $PSScriptRoot 'test-release.ps1')
    Invoke-DotNet @('restore', 'Font.Metadata.slnx', '--configfile', $restoreConfig, "-p:Version=$Version")
    Invoke-DotNet @('build', 'Font.Metadata.slnx', '-c', 'Release', '--no-restore', "-p:Version=$Version")
    Invoke-DotNet @('test', 'Font.Metadata.Tests/Font.Metadata.Tests.csproj',
        '-c', 'Release', '--no-build', '--no-restore', "-p:Version=$Version",
        '--logger', 'trx;LogFilePrefix=library', '--results-directory', 'artifacts/test-results/library')
    Invoke-DotNet @('test', 'Font.Metadata.Tool.Tests/Font.Metadata.Tool.Tests.csproj',
        '-c', 'Release', '--no-build', '--no-restore', "-p:Version=$Version",
        '--logger', 'trx;LogFilePrefix=tool', '--results-directory', 'artifacts/test-results/tool')
    Invoke-DotNet @('test', 'Font.Metadata.Spdx.Tests/Font.Metadata.Spdx.Tests.csproj',
        '-c', 'Release', '--no-build', '--no-restore', "-p:Version=$Version",
        '--logger', 'trx;LogFilePrefix=spdx', '--results-directory', 'artifacts/test-results/spdx')
    Invoke-DotNet @('format', 'whitespace', 'Font.Metadata.slnx', '--verify-no-changes', '--no-restore')
    & (Join-Path $PSScriptRoot 'verify-packages.ps1') -Version $Version -PackageDirectory $PackageDirectory
} finally {
    Pop-Location
    foreach($entry in $savedEnvironment.GetEnumerator()) {
        # PowerShell converts a plain null argument to an empty string; preserve absence explicitly.
        $value = if($null -eq $entry.Value) { [System.Management.Automation.Language.NullString]::Value } else { $entry.Value }
        [Environment]::SetEnvironmentVariable($entry.Key, $value, 'Process')
    }
}
