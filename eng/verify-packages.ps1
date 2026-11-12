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
    foreach($id in @('Font.Metadata', 'Font.Metadata.Tool')) {
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
AppContext.SetSwitch("System.IO.Compression.UseStrictValidation", bool.Parse(args[1]));
const string UnexpectedAssemblyMessage = "Unexpected assembly identity.";
const string LibraryVersionMismatchMessage = "Library version does not match the package version.";
const string CompleteReadFailureMessage = "Packaged reader failed the complete metadata contract.";
const string PartialReadFailureMessage = "Packaged reader failed the partial-result contract.";
const string UnexpectedStrictSuccessMessage = "Strict reading unexpectedly accepted incomplete metadata.";
const string LostPartialMetadataMessage = "Strict reading did not preserve partial metadata.";
const string ReaderVerifiedMessage = "Packaged SFNT reading, partial results, diagnostics and strict exceptions verified.";
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
if(result.Status != FontReadStatus.Partial || result.Metadata.Faces[0].FamilyName != "A" ||
    result.Metadata.Faces[0].NameTableStatus != FontSectionStatus.Partial || result.Diagnostics.Count != 1 ||
    result.Diagnostics[0].Code != FontDiagnosticCodes.InvalidNameTable || result.Diagnostics[0].FaceIndex != 0 ||
    result.Diagnostics[0].TableTag != "name")
    throw new Exception(PartialReadFailureMessage);
try {
    FontMetadataReader.Read(damaged);
    throw new Exception(UnexpectedStrictSuccessMessage);
} catch(FontReadException exception) {
    if(exception.Result.Status != FontReadStatus.Partial || exception.Result.Metadata.Faces[0].FamilyName != "A")
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
FontMetadata.Face partialFace = partialInspection.Metadata.Faces[0];
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
    Write-Host 'PackageReference consumer and installed dotnet tool checks passed.'
} finally {
    $env:NUGET_PACKAGES = $originalPackages
    Pop-Location
}
