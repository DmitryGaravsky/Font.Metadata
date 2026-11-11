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
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}
Push-Location $root
$originalPackages = $env:NUGET_PACKAGES
try {
    $feed = Join-Path $root "artifacts/packages/$Version"
    Invoke-DotNet @('pack', 'Font.Metadata/Font.Metadata.csproj', '-c', 'Release',
        '--no-build', '--no-restore', '--output', $feed, "-p:Version=$Version")
    Invoke-DotNet @('pack', 'Font.Metadata.Tool/Font.Metadata.Tool.csproj', '-c', 'Release',
        '--no-build', '--no-restore', '--output', $feed, "-p:Version=$Version")
    $libraryPackage = Join-Path $feed "Font.Metadata.$version.nupkg"
    $archive = [IO.Compression.ZipFile]::OpenRead($libraryPackage)
    try {
        foreach($entry in @('lib/net10.0/Font.Metadata.dll', 'lib/net10.0/Font.Metadata.xml', 'README.md')) {
            if($null -eq $archive.GetEntry($entry)) { throw "Missing library package entry: $entry" }
        }
        if(@($archive.Entries | Where-Object {
            $_.FullName -like 'lib/*' -and $_.FullName -notlike 'lib/net10.0/*'
        }).Count -ne 0) {
            throw 'The library package must target net10.0 only.'
        }
        if(@($archive.Entries | Where-Object FullName -Like '*Font.Metadata.Tool*').Count -ne 0) {
            throw 'The library package must not contain the CLI.'
        }
    } finally {
        $archive.Dispose()
    }
    $expectedLicense = [IO.File]::ReadAllText((Join-Path $root 'LICENSE'))
    [xml] $buildProperties = Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw
    foreach($id in @('Font.Metadata', 'Font.Metadata.Tool')) {
        $package = [IO.Compression.ZipFile]::OpenRead((Join-Path $feed "$id.$Version.nupkg"))
        try {
            $licenseEntry = $package.GetEntry('LICENSE')
            if($null -eq $licenseEntry) { throw "Missing LICENSE in $id" }
            $reader = [IO.StreamReader]::new($licenseEntry.Open())
            try { $actualLicense = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if($actualLicense -cne $expectedLicense) { throw "License text mismatch in $id" }
            $reader = [IO.StreamReader]::new($package.GetEntry("$id.nuspec").Open())
            try { [xml] $spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if($spec.package.metadata.license.type -cne 'expression' -or
                $spec.package.metadata.license.InnerText -cne 'MIT') {
                throw "Expected the MIT SPDX license expression in $id"
            }
            if($spec.package.metadata.authors -cne $buildProperties.Project.PropertyGroup.Authors) {
                throw "Author metadata mismatch in $id"
            }
            if($spec.package.metadata.projectUrl -cne $buildProperties.Project.PropertyGroup.PackageProjectUrl -or
                $spec.package.metadata.repository.url -cne $buildProperties.Project.PropertyGroup.RepositoryUrl -or
                $spec.package.metadata.repository.type -cne $buildProperties.Project.PropertyGroup.RepositoryType) {
                throw "Project/repository metadata mismatch in $id"
            }
            if($spec.package.metadata.copyright -cne $buildProperties.Project.PropertyGroup.Copyright) {
                throw "Copyright metadata mismatch in $id"
            }
        } finally { $package.Dispose() }
    }
    Write-Host 'Author, repository, MIT license metadata and bundled license text verified for both packages.'
    # Each run gets an isolated consumer and package cache, avoiding stale packages with the same dev version.
    $run = Join-Path $root ('artifacts/package-smoke/' + [Guid]::NewGuid().ToString('N'))
    $consumer = Join-Path $run 'consumer'
    New-Item -ItemType Directory -Path $consumer -Force | Out-Null
    $env:NUGET_PACKAGES = Join-Path $run 'packages'
    [IO.File]::WriteAllText((Join-Path $run 'Directory.Build.props'), '<Project />')
    [IO.File]::WriteAllText((Join-Path $run 'Directory.Build.targets'), '<Project />')
    [IO.File]::WriteAllText((Join-Path $run 'Directory.Packages.props'), '<Project />')
    $escapedFeed = [Security.SecurityElement]::Escape($feed)
    $config = Join-Path $run 'NuGet.Config'
    [IO.File]::WriteAllText($config, "<configuration><packageSources><clear /><add key='local' value='$escapedFeed' /></packageSources></configuration>")
    $project = Join-Path $consumer 'Consumer.csproj'
    [IO.File]::WriteAllText($project, @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include="Font.Metadata" Version="$version" /></ItemGroup>
</Project>
"@)
    [IO.File]::WriteAllText((Join-Path $consumer 'Program.cs'), @'
using System;
using System.Reflection;
using Fonts.Metadata;
Type[] types = { typeof(FontMetadata), typeof(FontMetadata.Face), typeof(FontMetadata.NameRecord), typeof(FontMetadata.EotHeader) };
foreach(Type type in types) {
    if(type.Assembly.GetName().Name != "Font.Metadata")
        throw new Exception("Unexpected assembly identity.");
    string version = type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion.Split('+')[0];
    if(version != args[0])
        throw new Exception("Library version does not match the package version.");
    Console.WriteLine(type.FullName);
}
'@)
    Invoke-DotNet @('restore', $project, '--configfile', $config)
    Invoke-DotNet @('build', $project, '-c', 'Release', '--no-restore')
    Invoke-DotNet @('run', '--project', $project, '-c', 'Release', '--no-build', '--no-restore', '--', $Version)
    $toolPath = Join-Path $run 'tool'
    Invoke-DotNet @('tool', 'install', 'Font.Metadata.Tool', '--version', $version,
        '--tool-path', $toolPath, '--configfile', $config)
    $toolName = if($IsWindows) { 'font-metadata.exe' } else { 'font-metadata' }
    $executable = Join-Path $toolPath $toolName
    $help = & $executable --help
    if($LASTEXITCODE -ne 0 -or ($help -join "\n") -notmatch 'not implemented') {
        throw 'Installed tool did not report its scaffold capabilities.'
    }
    $actualVersion = & $executable --version
    if($LASTEXITCODE -ne 0 -or ($actualVersion.Trim() -split '\+')[0] -ne $version) {
        throw "Installed tool version does not match $version."
    }
    Write-Host 'PackageReference consumer and installed dotnet tool checks passed.'
} finally {
    $env:NUGET_PACKAGES = $originalPackages
    Pop-Location
}
