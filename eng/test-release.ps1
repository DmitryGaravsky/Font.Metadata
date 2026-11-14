#Requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Variable -Name MockArguments -Option Constant -Value @{
    Operation = 1; UploadPath = 3; DownloadName = 4; DownloadDirectory = 6
}
Set-Variable -Name ExpectedReleaseFileCount -Option Constant -Value 5
Set-Variable -Name MockJsonDepth -Option Constant -Value 4
$getVersion = Join-Path $PSScriptRoot 'get-version.ps1'
foreach($version in @('0.1.0', '1.2.3-preview.1', '1.2.3-rc.0', '10.0.0')) {
    if((& $getVersion -Tag "v$version") -cne $version) { throw 'Valid version rejected.' }
}
foreach($tag in @('1.2.3', 'v', 'v01.2.3', 'v1.2', 'v1.2.3.4', 'v1.2.3+build', 'v1.2.3-RC.1',
    'v1.2.3-01', 'v1.2.3-preview..1', 'v1.2.3/other', "v1.2.3`n")) {
    $rejected = $false
    try { & $getVersion -Tag $tag | Out-Null } catch { $rejected = $true }
    if(-not $rejected) { throw ('Invalid tag accepted: {0}' -f $tag) }
}
# Exercise publication control flow with local fixtures and an in-process GitHub CLI substitute.
$root = Split-Path $PSScriptRoot -Parent
$fixture = Join-Path $root ('artifacts/release-tests/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$version = '1.2.3-preview.1'
foreach($id in @('Font.Metadata', 'Font.Metadata.Spdx', 'Font.Metadata.Tool')) {
    $archive = [IO.Compression.ZipFile]::Open((Join-Path $fixture "$id.$version.nupkg"), 'Create')
    try {
        $writer = [IO.StreamWriter]::new($archive.CreateEntry("$id.nuspec").Open())
        try { $writer.Write("<package><metadata><id>$id</id><version>$version</version></metadata></package>") }
        finally { $writer.Dispose() }
    } finally { $archive.Dispose() }
}
[IO.File]::WriteAllText((Join-Path $fixture "font-metadata-$version-portable.zip"), 'test fixture')
$checksums = Get-ChildItem -LiteralPath $fixture -File | ForEach-Object {
    "$((Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant())  $($_.Name)"
}
[IO.File]::WriteAllText((Join-Path $fixture 'SHA256SUMS'), ($checksums -join "`n") + "`n")
$global:fontMetadataReleaseMock = $null
function gh {
    $global:LASTEXITCODE = 0
    $operation = $args[$MockArguments.Operation]
    $global:fontMetadataReleaseMock.Calls.Add(($args -join ' '))
    switch($operation) {
        'view' {
            if(-not $global:fontMetadataReleaseMock.Exists) {
                $global:LASTEXITCODE = 1
                return
            }
            return (@{ isDraft = $global:fontMetadataReleaseMock.Draft; isPrerelease = $true; assets = @($global:fontMetadataReleaseMock.Assets | ForEach-Object { @{ name = $_ } }) } | ConvertTo-Json -Depth $MockJsonDepth)
        }
        'create' { $global:fontMetadataReleaseMock.Exists = $true; $global:fontMetadataReleaseMock.Draft = $true }
        'upload' {
            if($global:fontMetadataReleaseMock.FailUpload) {
                $global:LASTEXITCODE = 1
                return
            }
            $global:fontMetadataReleaseMock.Assets.Add([IO.Path]::GetFileName($args[$MockArguments.UploadPath]))
        }
        'download' {
            $name = $args[$MockArguments.DownloadName]
            $directory = $args[$MockArguments.DownloadDirectory]
            Copy-Item -LiteralPath (Join-Path $fixture $name) -Destination $directory
            if($global:fontMetadataReleaseMock.Corrupt) { [IO.File]::AppendAllText((Join-Path $directory $name), 'changed') }
        }
        'edit' { $global:fontMetadataReleaseMock.Draft = $false }
        default { throw ('Unexpected GitHub CLI operation: {0}' -f $operation) }
    }
}
function New-Mock {
    $global:fontMetadataReleaseMock = @{
        Exists = $false; Draft = $true; FailUpload = $false; Corrupt = $false
        Assets = [Collections.Generic.List[string]]::new()
        Calls = [Collections.Generic.List[string]]::new()
    }
}
$publisher = Join-Path $PSScriptRoot 'publish-github-release.ps1'
try {
    New-Mock
    & $publisher -Tag "v$version" -Directory $fixture 6>&1 | Out-Null
    if($global:fontMetadataReleaseMock.Draft -or $global:fontMetadataReleaseMock.Assets.Count -ne $ExpectedReleaseFileCount) { throw 'Incomplete release was published.' }
    if(-not ($global:fontMetadataReleaseMock.Calls | Where-Object { $_ -like '*--prerelease*' })) { throw 'Prerelease flag missing.' }
    $global:fontMetadataReleaseMock.Calls.Clear()
    & $publisher -Tag "v$version" -Directory $fixture 6>&1 | Out-Null
    if($global:fontMetadataReleaseMock.Calls | Where-Object { $_ -match '^release (create|upload|edit) ' }) { throw 'Published release was modified on retry.' }
    $global:fontMetadataReleaseMock.Corrupt = $true
    $rejected = $false
    try { & $publisher -Tag "v$version" -Directory $fixture 6>&1 | Out-Null } catch { $rejected = $true }
    if(-not $rejected) { throw 'Different published assets were accepted.' }
    New-Mock
    $global:fontMetadataReleaseMock.FailUpload = $true
    $rejected = $false
    try { & $publisher -Tag "v$version" -Directory $fixture 6>&1 | Out-Null } catch { $rejected = $true }
    if(-not $rejected -or -not $global:fontMetadataReleaseMock.Draft) { throw 'Upload failure did not leave an unpublished draft.' }
    $global:fontMetadataReleaseMock.FailUpload = $false
    & $publisher -Tag "v$version" -Directory $fixture 6>&1 | Out-Null
    if($global:fontMetadataReleaseMock.Draft) { throw 'Draft retry did not finish.' }
    [IO.File]::AppendAllText((Join-Path $fixture "Font.Metadata.$version.nupkg"), 'changed')
    $global:fontMetadataReleaseMock.Calls.Clear()
    $rejected = $false
    try { & $publisher -Tag "v$version" -Directory $fixture 6>&1 | Out-Null } catch { $rejected = $true }
    if(-not $rejected -or $global:fontMetadataReleaseMock.Calls.Count) { throw 'Invalid local assets reached GitHub.' }
} finally {
    Remove-Item Function:\gh
    Remove-Variable -Name fontMetadataReleaseMock -Scope Global
}
Write-Host 'Release tag validation, checksums, draft publication, retries and collision checks passed without network access.'
