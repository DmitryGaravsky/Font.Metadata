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
Push-Location $root
$originalPackages = $env:NUGET_PACKAGES
try {
    $feed = if($PackageDirectory) { [IO.Path]::GetFullPath($PackageDirectory) } else {
        Join-Path $root ("artifacts/packages/$Version/" + [Guid]::NewGuid().ToString('N'))
    }
    if(Test-Path -LiteralPath $feed) { throw 'Package output must be a new directory; never replace different bytes under the same version.' }
    Invoke-DotNet @('pack', 'Font.Metadata/Font.Metadata.csproj', '-c', 'Release',
        '--no-build', '--no-restore', '--output', $feed, "-p:Version=$Version")
    Invoke-DotNet @('pack', 'Font.Metadata.Spdx/Font.Metadata.Spdx.csproj', '-c', 'Release',
        '--no-build', '--no-restore', '--output', $feed, "-p:Version=$Version")
    Invoke-DotNet @('pack', 'Font.Metadata.Tool/Font.Metadata.Tool.csproj', '-c', 'Release',
        '--no-build', '--no-restore', '--output', $feed, "-p:Version=$Version")
    $libraryPackage = Join-Path $feed "Font.Metadata.$version.nupkg"
    $archive = [IO.Compression.ZipFile]::OpenRead($libraryPackage)
    try {
        foreach($entry in @('lib/net10.0/Font.Metadata.dll', 'lib/net10.0/Font.Metadata.xml', 'README.md')) {
            if($null -eq $archive.GetEntry($entry)) { throw ('Missing library package entry: {0}' -f $entry) }
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
    foreach($id in @('Font.Metadata', 'Font.Metadata.Spdx', 'Font.Metadata.Tool')) {
        $package = [IO.Compression.ZipFile]::OpenRead((Join-Path $feed "$id.$Version.nupkg"))
        try {
            $licenseEntry = $package.GetEntry('LICENSE')
            if($null -eq $licenseEntry) { throw ('Missing LICENSE in {0}' -f $id) }
            $reader = [IO.StreamReader]::new($licenseEntry.Open())
            try { $actualLicense = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if($actualLicense -cne $expectedLicense) { throw ('License text mismatch in {0}' -f $id) }
            $reader = [IO.StreamReader]::new($package.GetEntry("$id.nuspec").Open())
            try { [xml] $spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
            if($spec.package.metadata.license.type -cne 'expression' -or
                $spec.package.metadata.license.InnerText -cne 'MIT') {
                throw ('Expected the MIT SPDX license expression in {0}' -f $id)
            }
            if($spec.package.metadata.authors -cne $buildProperties.Project.PropertyGroup.Authors) {
                throw ('Author metadata mismatch in {0}' -f $id)
            }
            if($spec.package.metadata.projectUrl -cne $buildProperties.Project.PropertyGroup.PackageProjectUrl -or
                $spec.package.metadata.repository.url -cne $buildProperties.Project.PropertyGroup.RepositoryUrl -or
                $spec.package.metadata.repository.type -cne $buildProperties.Project.PropertyGroup.RepositoryType) {
                throw ('Project/repository metadata mismatch in {0}' -f $id)
            }
            if($spec.package.metadata.copyright -cne $buildProperties.Project.PropertyGroup.Copyright) {
                throw ('Copyright metadata mismatch in {0}' -f $id)
            }
        } finally { $package.Dispose() }
    }
    $adapter = [IO.Compression.ZipFile]::OpenRead((Join-Path $feed "Font.Metadata.Spdx.$Version.nupkg"))
    try {
        foreach($entry in @('lib/net10.0/Font.Metadata.Spdx.dll', 'lib/net10.0/Font.Metadata.Spdx.xml')) {
            if($null -eq $adapter.GetEntry($entry)) { throw "Missing adapter package entry: $entry" }
        }
        $reader = [IO.StreamReader]::new($adapter.GetEntry('Font.Metadata.Spdx.nuspec').Open())
        try { [xml] $spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $dependencies = @($spec.package.metadata.dependencies.group.dependency)
        [xml] $central = Get-Content -LiteralPath (Join-Path $root 'Directory.Packages.props') -Raw
        $expectedRange = ($central.Project.ItemGroup.PackageVersion | Where-Object Include -EQ 'Spdx.Expressions').Version
        if($dependencies.Count -ne 2 -or
            ($dependencies | Where-Object id -EQ 'Spdx.Expressions').version -cne $expectedRange -or
            @($dependencies | Where-Object id -EQ 'Font.Metadata').Count -ne 1) {
            throw 'Unexpected adapter dependency closure or SPDX version range.'
        }
    } finally { $adapter.Dispose() }
    Write-Host 'Author, repository, MIT license metadata, dependency closure and bundled license text verified for all three packages.'
    # Each run gets an isolated consumer and package cache, avoiding stale packages with the same dev version.
    $run = Join-Path $root ('artifacts/package-smoke/' + [Guid]::NewGuid().ToString('N'))
    $consumer = Join-Path $run 'consumer'
    New-Item -ItemType Directory -Path $consumer -Force | Out-Null
    $env:NUGET_PACKAGES = Join-Path $run 'packages'
    [IO.File]::WriteAllText((Join-Path $run 'Directory.Build.props'), '<Project />')
    [IO.File]::WriteAllText((Join-Path $run 'Directory.Build.targets'), '<Project />')
    [IO.File]::WriteAllText((Join-Path $run 'Directory.Packages.props'), '<Project />')
    $consumerFeed = Join-Path $run 'feed'
    New-Item -ItemType Directory -Path $consumerFeed -Force | Out-Null
    foreach($id in @('Font.Metadata', 'Font.Metadata.Spdx', 'Font.Metadata.Tool')) {
        Copy-Item -LiteralPath (Join-Path $feed "$id.$Version.nupkg") -Destination $consumerFeed
    }
    # Use the exact package selected by restore, including custom global package directories.
    $assets = Get-Content -LiteralPath (Join-Path $root 'Font.Metadata.Spdx/obj/project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
    $spdxVersion = $expectedRange.Trim('[', ']')
    $packageCandidates = @($assets.packageFolders.Keys | ForEach-Object {
        Join-Path $_ "spdx.expressions/$spdxVersion/spdx.expressions.$spdxVersion.nupkg"
    } | Where-Object { Test-Path -LiteralPath $_ })
    if($packageCandidates.Count -ne 1) { throw 'Expected one restored SPDX package matching the central dependency.' }
    $SpdxPackage = $packageCandidates[0]
    Copy-Item -LiteralPath $SpdxPackage -Destination $consumerFeed
    $escapedFeed = [Security.SecurityElement]::Escape($consumerFeed)
    $config = Join-Path $run 'NuGet.Config'
    [IO.File]::WriteAllText($config, "<configuration><packageSources><clear /><add key='local' value='$escapedFeed' /></packageSources></configuration>")
    $project = Join-Path $consumer 'Consumer.csproj'
    [IO.File]::WriteAllText($project, @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include="Font.Metadata.Spdx" Version="[$version]" /></ItemGroup>
</Project>
"@)
    [IO.File]::WriteAllText((Join-Path $consumer 'Program.cs'), @'
using System;
using System.Reflection;
using Fonts.Metadata;
AppContext.SetSwitch("System.IO.Compression.UseStrictValidation", bool.Parse(args[1]));
const string UnexpectedAssemblyMessage = "Unexpected assembly identity.";
const string LibraryVersionMismatchMessage = "Library version does not match the package version.";
const string CompleteReadFailureMessage = "Packaged reader failed the complete metadata contract.";
const string PartialReadFailureMessage = "Packaged reader failed the partial-result contract.";
const string UnexpectedStrictSuccessMessage = "Strict reading unexpectedly accepted incomplete metadata.";
const string LostPartialMetadataMessage = "Strict reading did not preserve partial metadata.";
const string ReaderVerifiedMessage = "Packaged SFNT reading, partial results, diagnostics and strict exceptions verified.";
const string InspectionFacesFailureMessage = "Packaged inspection faces lost availability, identity or immutability.";
const string LicenseAnalysisFailureMessage = "Packaged license analyzer failed its evidence or completeness contract.";
const string MutableHintsMessage = "Packaged license hints expose a mutable collection.";
const string LicenseAnalysisVerifiedMessage = "Packaged license analysis, evidence, immutability and source completeness verified.";
Type[] types = { typeof(FontMetadata), typeof(FontMetadata.Face), typeof(FontMetadata.NameRecord), typeof(FontMetadata.EotHeader),
    typeof(FontLicenseAnalyzer), typeof(FontLicenseAnalysis), typeof(FontLicenseHint), typeof(FontLicenseIdentifierKind) };
foreach(Type type in types) {
    if(type.Assembly.GetName().Name != "Font.Metadata")
        throw new Exception(UnexpectedAssemblyMessage);
    string version = type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion.Split('+')[0];
    if(version != args[0])
        throw new Exception(LibraryVersionMismatchMessage);
    Console.WriteLine(type.FullName);
}
// Independent binary vectors: one readable family name, then a readable name followed by invalid UTF-16.
byte[] valid = Convert.FromHexString("0001000000010010000000006E616D65000000000000001C000000140000000100120003000104090001000200000041");
FontMetadata font = FontMetadataReader.Read(valid);
if(font.Format != FontFormat.Sfnt || font.Faces.Count != 1 || font.Faces[0].FamilyName != "A" ||
    font.Faces[0].NameTableStatus != FontSectionStatus.Complete || font.Faces[0].Os2TableStatus != FontSectionStatus.Missing)
    throw new Exception(CompleteReadFailureMessage);
byte[] damaged = Convert.FromHexString("0001000000010010000000006E616D65000000000000001C0000002200000002001E000300010409000100020000000300010409000D000200020041D800");
FontInspectionResult result = FontMetadataReader.Inspect(damaged);
if(result.Status != FontReadStatus.Partial || result.Faces[0].FamilyName != "A" ||
    result.Faces[0].NameTableStatus != FontSectionStatus.Partial || result.Diagnostics.Count != 1 ||
    result.Diagnostics[0].Code != FontDiagnosticCodes.InvalidNameTable || result.Diagnostics[0].FaceIndex != 0 ||
    result.Diagnostics[0].TableTag != "name")
    throw new Exception(PartialReadFailureMessage);
FontInspectionResult unknown = FontMetadataReader.Inspect(Array.Empty<byte>());
if(unknown.Status != FontReadStatus.NotRecognized || unknown.Metadata != null || unknown.Faces.Count != 0 ||
    !ReferenceEquals(result.Faces[0], result.Metadata.Faces[0]))
    throw new Exception(InspectionFacesFailureMessage);
foreach(FontInspectionResult inspection in new[] { unknown, result }) {
    if(inspection.Faces is System.Collections.IList faces) {
        try {
            faces.Clear();
            throw new Exception(InspectionFacesFailureMessage);
        } catch(NotSupportedException) {
        }
    }
}
try {
    FontMetadataReader.Read(damaged);
    throw new Exception(UnexpectedStrictSuccessMessage);
} catch(FontReadException exception) {
    if(exception.Result.Status != FontReadStatus.Partial || exception.Result.Faces[0].FamilyName != "A")
        throw new Exception(LostPartialMetadataMessage);
}
Console.WriteLine(ReaderVerifiedMessage);
// Independent wire vectors: MIT in name ID 13; then MIT followed by an invalid UTF-16 record.
byte[] licensed = Convert.FromHexString("0001000000010010000000006E616D65000000000000001C00000018000000010012000300010409000D00060000004D00490054");
byte[] partialLicense = Convert.FromHexString("0001000000010010000000006E616D65000000000000001C0000002600000002001E000300010409000D00060000000300010409000E00020006004D00490054D800");
FontMetadata.Face completeFace = FontMetadataReader.Read(licensed).Faces[0];
FontInspectionResult partialInspection = FontMetadataReader.Inspect(partialLicense);
if(partialInspection.Status != FontReadStatus.Partial)
    throw new Exception(LicenseAnalysisFailureMessage);
FontMetadata.Face partialFace = partialInspection.Faces[0];
foreach(FontMetadata.Face face in new[] { completeFace, partialFace }) {
    FontLicenseAnalysis analysis = FontLicenseAnalyzer.Analyze(face);
    if(analysis.IsSourceComplete != ReferenceEquals(face, completeFace) || analysis.Hints.Count != 1)
        throw new Exception(LicenseAnalysisFailureMessage);
    FontLicenseHint hint = analysis.Hints[0];
    if(hint.Identifier != "MIT" || hint.IdentifierKind != FontLicenseIdentifierKind.Spdx || hint.RuleId != "FLA001" ||
        !ReferenceEquals(hint.SourceRecord, face.NameRecords[0]) || hint.SourceRecord.NameId != 13 || hint.SourceRecord.Value != "MIT")
        throw new Exception(LicenseAnalysisFailureMessage);
    if(analysis.Hints is System.Collections.IList list) {
        try {
            list.Clear();
            throw new Exception(MutableHintsMessage);
        } catch(NotSupportedException) {
        }
    }
}
FontLicenseAnalysis emptyAnalysis = FontLicenseAnalyzer.Analyze(font.Faces[0]);
if(!emptyAnalysis.IsSourceComplete || emptyAnalysis.Hints.Count != 0)
    throw new Exception(LicenseAnalysisFailureMessage);
Console.WriteLine(LicenseAnalysisVerifiedMessage);
const string SpdxFailureMessage = "Packaged SPDX adapter lost native results or source evidence.";
var registry = Spdx.Expressions.SpdxLicenseRegistry.Default;
var spdx = FontSpdxAnalyzer.Analyze(completeFace, registry);
if(spdx.Declarations.Count != 1 || spdx.Declarations[0].ExpressionText != "MIT" ||
    spdx.Declarations[0].ParseResult.Status != Spdx.Expressions.SpdxParseStatus.Success ||
    !ReferenceEquals(spdx.Declarations[0].SourceRecord, completeFace.NameRecords[0]) ||
    spdx.Declarations[0].ValidationResult.IdentifierOccurrences[0].CanonicalIdentifier != "MIT" ||
    !spdx.IsSourceComplete || !spdx.IsAnalysisComplete || registry.Identity.LicenseListVersion != "3.29.0")
    throw new Exception(SpdxFailureMessage);
Console.WriteLine(typeof(Spdx.Expressions.SpdxLicenseRegistry).Assembly.Location);

// Web-container consumers compile the additive API and exercise real packaged readers.
const string WebContractFailureMessage = "Packaged web-font metadata contract failed.";
var webLimits = new FontReadLimits {
    MaxTotalDecodedBytes = 128 * 1024 * 1024, MaxMtxPayloadBytes = 64 * 1024 * 1024,
    MaxExpansionRatio = 1000, MaxWoff2Tables = 16384, MaxWoff2TableReferences = 1048576,
    MaxRetainedDecodedBytes = 64 * 1024 * 1024
};
byte[] name = licensed.AsSpan(28).ToArray();
byte[] paddedName = new byte[1024];
name.CopyTo(paddedName, 0);
byte[] woff = MakeWoff(paddedName);
byte[] woff2 = MakeWoff2(name);
byte[] eot = MakeEot(licensed);
foreach(byte[] input in new[] { woff, woff2, eot }) {
    FontMetadata web = FontMetadataReader.Read(input, new FontReadOptions { Limits = webLimits });
    if(!FontMetadataReader.IsFormatSupported(web.Format) || FontLicenseAnalyzer.Analyze(web.Faces[0]).Hints[0].Identifier != "MIT")
        throw new Exception(WebContractFailureMessage);
    if(web.Format == FontFormat.Eot) {
        FontMetadata.EotHeader header = web.Eot;
        if(web.EotHeaderStatus != FontSectionStatus.Complete || header.FormatVersion != 0x00020002 ||
            header.Flags != 0x20 || header.EmbeddingPermissions != 2 || header.FamilyName != "" ||
            header.StyleName != "" || header.FullName != "" || header.Version != "" ||
            header.EudcCodePage != 1252 || header.EudcFlags != 0 || header.EudcFontDataSize != licensed.Length ||
            web.Faces[0].Source != FontFaceSource.EotMain || web.Faces[1].Source != FontFaceSource.EotEudc ||
            web.Faces[1].Index != 1)
            throw new Exception(WebContractFailureMessage);
    }
    else if(web.Eot != null || web.EotHeaderStatus != FontSectionStatus.Missing || web.Faces[0].Source != FontFaceSource.Container)
        throw new Exception(WebContractFailureMessage);
    if(web.Faces is System.Collections.IList list) {
        try {
            list.Clear();
            throw new Exception(WebContractFailureMessage);
        } catch(NotSupportedException) {
        }
    }
}
if(result.Diagnostics[0].TableByteOffset != 32)
    throw new Exception(WebContractFailureMessage);
// Exact trailer validation must be invariant under the host's strict-validation switch.
uint stored = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(woff.AsSpan(52));
Write32(woff, 52, stored - 1);
foreach(int chunkSize in new[] { 1, 7, 4096 }) {
    using(var stream = new ChunkedInput(woff, chunkSize)) {
        FontInspectionResult truncated = FontMetadataReader.Inspect(stream);
        if(truncated.Status == FontReadStatus.Complete || truncated.Metadata.Faces[0].NameTableStatus != FontSectionStatus.Invalid ||
            !System.Linq.Enumerable.Any(truncated.Diagnostics, d => d.Code == FontDiagnosticCodes.InvalidCompressedData))
            throw new Exception(WebContractFailureMessage);
    }
}
Console.WriteLine("Packaged WOFF/WOFF2/EOT, provenance, budgets and strict zlib completion verified.");

static byte[] CompressBytes(byte[] bytes, bool brotli) {
    using(var output = new System.IO.MemoryStream()) {
        using(System.IO.Stream encoder = brotli
            ? new System.IO.Compression.BrotliStream(output, System.IO.Compression.CompressionLevel.Optimal, true)
            : new System.IO.Compression.ZLibStream(output, System.IO.Compression.CompressionLevel.Optimal, true)) {
            encoder.Write(bytes);
        }
        return output.ToArray();
    }
}
static void Write32(byte[] bytes, int offset, uint value) {
    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), value);
}
static byte[] MakeWoff(byte[] table) {
    byte[] compressed = CompressBytes(table, false);
    byte[] bytes = new byte[(64 + compressed.Length + 3) & ~3];
    "wOFF"u8.CopyTo(bytes);
    Write32(bytes, 4, 0x00010000);
    Write32(bytes, 8, (uint)bytes.Length);
    bytes[13] = 1;
    Write32(bytes, 16, (uint)(28 + table.Length));
    "name"u8.CopyTo(bytes.AsSpan(44));
    Write32(bytes, 48, 64);
    Write32(bytes, 52, (uint)compressed.Length);
    Write32(bytes, 56, (uint)table.Length);
    compressed.CopyTo(bytes, 64);
    return bytes;
}
static byte[] MakeWoff2(byte[] table) {
    byte[] compressed = CompressBytes(table, true);
    byte[] bytes = new byte[54 + compressed.Length];
    "wOF2"u8.CopyTo(bytes);
    Write32(bytes, 4, 0x00010000);
    Write32(bytes, 8, (uint)bytes.Length);
    bytes[13] = 1;
    Write32(bytes, 16, (uint)(28 + table.Length));
    Write32(bytes, 20, (uint)compressed.Length);
    bytes[48] = 63;
    "name"u8.CopyTo(bytes.AsSpan(49));
    bytes[53] = (byte)table.Length;
    compressed.CopyTo(bytes, 54);
    return bytes;
}
static byte[] MakeEot(byte[] sfnt) {
    using(var output = new System.IO.MemoryStream()) {
        using(var writer = new System.IO.BinaryWriter(output, System.Text.Encoding.Unicode, true)) {
            writer.Write(new byte[80]);
            for(int index = 0; index < 5; index++) {
                writer.Write((ushort)0);
                writer.Write((ushort)0);
            }
            writer.Write(0x50475342u);
            writer.Write(1252u);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write(0u);
            writer.Write((uint)sfnt.Length);
            writer.Write(sfnt);
            writer.Write(sfnt);
        }
        byte[] bytes = output.ToArray();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)bytes.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)sfnt.Length);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0x00020002);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x20);
        bytes[32] = 2;
        bytes[34] = 0x4c;
        bytes[35] = 0x50;
        return bytes;
    }
}
sealed class ChunkedInput : System.IO.MemoryStream {
    readonly int chunkSize;
    internal ChunkedInput(byte[] input, int chunkSize) : base(input, false) {
        this.chunkSize = chunkSize;
    }
    public override int Read(byte[] buffer, int offset, int count) {
        return base.Read(buffer, offset, Math.Min(count, chunkSize));
    }
    public override int Read(Span<byte> buffer) {
        return base.Read(buffer.Slice(0, Math.Min(buffer.Length, chunkSize)));
    }
}

'@)
    Invoke-DotNet @('restore', $project, '--configfile', $config)
    Invoke-DotNet @('build', $project, '-c', 'Release', '--no-restore')
    foreach($strictValidation in @('false', 'true')) {
        Invoke-DotNet @('run', '--project', $project, '-c', 'Release', '--no-build', '--no-restore', '--', $Version, $strictValidation)
    }
    $dependency = [IO.Compression.ZipFile]::OpenRead($SpdxPackage)
    try {
        $assemblyEntry = $dependency.GetEntry('lib/net10.0/Spdx.Expressions.dll')
        $expectedAssembly = Join-Path $run 'Spdx.Expressions.dll'
        [IO.Compression.ZipFileExtensions]::ExtractToFile($assemblyEntry, $expectedAssembly)
        $expectedHash = (Get-FileHash -LiteralPath $expectedAssembly).Hash
        if((Get-FileHash -LiteralPath (Join-Path $consumer 'bin/Release/net10.0/Spdx.Expressions.dll')).Hash -cne $expectedHash) {
            throw 'The isolated consumer loaded a different SPDX assembly.'
        }
        $toolArchive = [IO.Compression.ZipFile]::OpenRead((Join-Path $feed "Font.Metadata.Tool.$Version.nupkg"))
        try {
            foreach($notice in @('LICENSE', 'THIRD-PARTY-NOTICES.md', 'data/UPSTREAM-README.md', 'data/UPSTREAM-XML-README.md')) {
                $reader = [IO.StreamReader]::new($dependency.GetEntry($notice).Open())
                try { $expected = $reader.ReadToEnd() } finally { $reader.Dispose() }
                $entry = $toolArchive.GetEntry('tools/net10.0/any/notices/Spdx.Expressions/' + [IO.Path]::GetFileName($notice))
                if($null -eq $entry) { throw "Missing bundled SPDX notice: $notice" }
                $reader = [IO.StreamReader]::new($entry.Open())
                try { $actual = $reader.ReadToEnd() } finally { $reader.Dispose() }
                if($actual -cne $expected) { throw "Bundled SPDX notice changed: $notice" }
            }
        } finally { $toolArchive.Dispose() }
    } finally { $dependency.Dispose() }
    $toolPath = Join-Path $run 'tool'
    Invoke-DotNet @('tool', 'install', 'Font.Metadata.Tool', '--version', $version,
        '--tool-path', $toolPath, '--configfile', $config)
    $toolName = if($IsWindows) { 'font-metadata.exe' } else { 'font-metadata' }
    $executable = Join-Path $toolPath $toolName
    $help = & $executable --help
    if($LASTEXITCODE -ne 0 -or ($help -join "\n") -notmatch 'inspect <path>') {
        throw 'Installed tool did not report its current capabilities.'
    }
    $actualVersion = & $executable --version
    if($LASTEXITCODE -ne 0 -or ($actualVersion.Trim() -split '\+')[0] -ne $version) {
        throw ('Installed tool version does not match {0}.' -f $version)
    }
    & (Join-Path $PSScriptRoot 'test-cli-verification.ps1') -Executable $executable -WorkDirectory (Join-Path $run 'cli')
    # Build the release ZIP once, then verify the exact archive that prepare-release copies.
    $payload = Join-Path $run 'portable-payload'
    New-Item -ItemType Directory -Path $payload -Force | Out-Null
    $toolArchive = [IO.Compression.ZipFile]::OpenRead((Join-Path $feed "Font.Metadata.Tool.$Version.nupkg"))
    try {
        foreach($name in @('LICENSE', 'README.md')) {
            $entry = $toolArchive.GetEntry($name)
            if($null -eq $entry) { throw "Tool package is missing $name." }
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $payload $name))
        }
        $prefix = 'tools/net10.0/any/'
        foreach($entry in $toolArchive.Entries) {
            if(-not $entry.FullName.StartsWith($prefix, [StringComparison]::Ordinal) -or -not $entry.Name) { continue }
            $relative = $entry.FullName.Substring($prefix.Length)
            if($relative -eq 'DotnetToolSettings.xml') { continue }
            $destination = [IO.Path]::GetFullPath((Join-Path $payload $relative))
            if(-not $destination.StartsWith($payload + [IO.Path]::DirectorySeparatorChar, [StringComparison]::Ordinal)) {
                throw 'Portable entry escapes its payload.'
            }
            New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination)
        }
    } finally { $toolArchive.Dispose() }
    $usage = "Font.Metadata.Tool $Version`nRequires the .NET 10 runtime.`nRun: dotnet Font.Metadata.Tool.dll --help`n"
    [IO.File]::WriteAllText((Join-Path $payload 'RUNNING.txt'), $usage)
    $portableZip = Join-Path $feed "font-metadata-$Version-portable.zip"
    [IO.Compression.ZipFile]::CreateFromDirectory($payload, $portableZip)
    $portable = Join-Path $run 'portable'
    [IO.Compression.ZipFile]::ExtractToDirectory($portableZip, $portable)
    if((Get-FileHash -LiteralPath (Join-Path $portable 'Spdx.Expressions.dll')).Hash -cne $expectedHash) {
        throw 'Portable SPDX assembly differs from the restored dependency.'
    }
    if([IO.File]::ReadAllText((Join-Path $portable 'LICENSE')) -cne $expectedLicense) {
        throw 'Portable CLI license text mismatch.'
    }
    $entryPoint = Join-Path $portable 'Font.Metadata.Tool.dll'
    $help = & dotnet $entryPoint --help
    if($LASTEXITCODE -ne 0 -or ($help -join "`n") -notmatch 'Usage: font-metadata') { throw 'Portable CLI help failed.' }
    $actual = & dotnet $entryPoint --version
    if($LASTEXITCODE -ne 0 -or ($actual.Trim() -split '\+')[0] -cne $Version) { throw 'Portable CLI version mismatch.' }
    & (Join-Path $PSScriptRoot 'test-cli-verification.ps1') -Executable 'dotnet' -Prefix @($entryPoint) -WorkDirectory (Join-Path $run 'portable-cli')
    Write-Host 'PackageReference consumer, installed dotnet tool and portable checks passed.'
    Write-Host "Verified packages: $feed"
} finally {
    $env:NUGET_PACKAGES = $originalPackages
    Pop-Location
}
