#Requires -Version 7.0
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Tag, [Parameter(Mandatory)][string]$Commit,
    [Parameter(Mandatory)][string]$Directory, [switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
Set-StrictMode -Version Latest
$version = & "$PSScriptRoot/get-version.ps1" -Tag $Tag
& "$PSScriptRoot/verify-release-assets.ps1" -Version $version -Directory $Directory -Commit $Commit
function Invoke-Gh([string[]]$Arguments) {
    & gh @Arguments --repo DmitryGaravsky/Font.Metadata
    if ($LASTEXITCODE -ne 0) {
        throw "GitHub CLI failed: $($Arguments[0]) $($Arguments[1])"
    }
}
$release = & "$PSScriptRoot/verify-release-source.ps1" -Tag $Tag -Commit $Commit
$files = @(Get-ChildItem -LiteralPath $Directory -File)
$assetNames = @($release.assets | ForEach-Object { $_.name })
if (@($assetNames | Where-Object { $_ -cnotin $files.Name }).Count -ne 0 -or
    @($assetNames | Select-Object -Unique).Count -ne $assetNames.Count) {
    throw 'Existing release contains unexpected or duplicate assets; nothing will be overwritten.'
}
$download = Join-Path ([IO.Path]::GetTempPath()) ('font-release-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($download) | Out-Null
# Validate all existing assets before uploading any missing ones. Keep the user's release text intact.
foreach ($file in $files) {
    if ($file.Name -cin $assetNames) {
        Invoke-Gh @('release', 'download', $Tag, '--pattern', $file.Name, '--dir', $download) | Out-Null
        if ((Get-FileHash -LiteralPath (Join-Path $download $file.Name)).Hash -cne (Get-FileHash -LiteralPath $file.FullName).Hash) {
            throw "Existing release asset differs: $($file.Name). Assets will not be overwritten."
        }
    }
}
if ($CheckOnly) {
    Write-Host 'GitHub release inputs verified without changes.'
    return
}
$current = & "$PSScriptRoot/verify-release-source.ps1" -Tag $Tag -Commit $Commit
if ($current.id -ne $release.id) {
    throw 'The GitHub release was replaced during verification.'
}
foreach ($file in $files) {
    if ($file.Name -cnotin $assetNames) {
        Invoke-Gh @('release', 'upload', $Tag, $file.FullName) | Out-Null
    }
}
# Re-read and download the result: upload success alone is not evidence of matching remote bytes.
& "$PSCommandPath" -Tag $Tag -Commit $Commit -Directory $Directory -CheckOnly
$final = & "$PSScriptRoot/verify-release-source.ps1" -Tag $Tag -Commit $Commit
$finalNames = @($final.assets | ForEach-Object { $_.name })
if ($final.id -ne $release.id -or @($files | Where-Object { $_.Name -cnotin $finalNames }).Count -ne 0) {
    throw 'The GitHub release was replaced or lost an asset during publication.'
}
Write-Host "GitHub release $Tag has the verified assets."
