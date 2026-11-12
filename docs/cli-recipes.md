# CLI recipes

Use `Font.Metadata.Tool` to inspect font files from a terminal, a script or CI. The command is `font-metadata`; the supported containers are TrueType/OpenType, TTC/OTC, WOFF, WOFF2 and EOT. See the [CLI reference](cli.md) for the exact argument and report contracts.

Inspection runs offline without installing fonts or loading a rendering engine. The tool uses the same parser as the library, preserving readable metadata from damaged inputs and linking license hints to their source records.

| Task | Recipe |
| --- | --- |
| Identify fonts and collection faces | [Names and attribution](#inspect-names-and-attribution) |
| Collect declared licenses for review | [License evidence](#collect-license-evidence) |
| Inventory font assets in a repository | [Directory inventory](#inventory-a-directory-with-powershell) |
| Integrate extracted facts into another system | [Read JSON](#read-a-json-report-in-powershell) |
| Investigate incomplete metadata | [Damaged inputs](#inspect-damaged-or-unrecognized-inputs) |
| Check extraction in a build pipeline | [CI](#use-a-pinned-tool-in-ci) |

Replace sample filenames with your files. Shell-neutral command blocks use ordinary arguments; the directory/report recipes require PowerShell 7 and the stdin/CI recipes use Bash. Replace `<version>` and `<feed>` placeholders before running commands.

## Install the tool

Use the .NET 10 SDK for `dotnet tool` installation and restore. Execution requires the .NET 10 runtime, which the SDK includes. The package supports Windows, Linux and macOS and includes the Font.Metadata library.

In the published-package examples, replace `<version>` with an available release version, including its prerelease suffix when applicable. A configured release workflow does not establish that a public package exists. Before publication, use the source-build recipe below.

### Global command

```shell
dotnet tool install --global Font.Metadata.Tool --version <version>
font-metadata --version
font-metadata inspect Example.ttf
```

The .NET global tools directory must be on `PATH`. To change the installed version:

```shell
dotnet tool update --global Font.Metadata.Tool --version <version>
```

### Repository-local command

Run in the consuming repository. Create a manifest only if it does not already have one:

```shell
dotnet new tool-manifest --output .config
dotnet tool install --local Font.Metadata.Tool --version <version>
dotnet tool run font-metadata -- inspect Example.ttf
```

Include `.config/dotnet-tools.json` in version control to share the selected version. On another checkout or in CI, restore it and run from within the manifest's scope:

```shell
dotnet tool restore
dotnet tool run font-metadata -- inspect Example.ttf --format json
```

For a private or local package feed, add `--add-source <feed>` to install/restore, or configure the source in the consuming repository's `NuGet.Config`. A manifest records the package version, not the feed contents; that version must remain available to other machines.

### Build and install from source

From the Font.Metadata repository root, with a fresh tool directory:

```shell
dotnet pack Font.Metadata.Tool/Font.Metadata.Tool.csproj -c Release -o artifacts/local-feed
dotnet tool install --tool-path artifacts/local-tool --add-source artifacts/local-feed Font.Metadata.Tool --version 0.1.0-dev
./artifacts/local-tool/font-metadata --help
./artifacts/local-tool/font-metadata inspect Example.woff2 --format json
```

`0.1.0-dev` is the current default in `Directory.Build.props`. If you override `Version` when packing, use the same version when installing. Use a new version for changed builds you distribute; reinstalling a development version can reuse a cached package. `--add-source` adds a feed without giving it precedence over other feeds.

To use a downloaded `.nupkg`, put it in a directory and pass that directory to `--add-source`. No public package is required. For an installed local tool, use `dotnet tool run font-metadata --` in place of `font-metadata` in the recipes below; for a custom tool directory, use its executable path.

See Microsoft's documentation for [tool installation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install) and [local tool invocation](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-run).

## Inspect names and attribution

```shell
font-metadata inspect Example.ttf
font-metadata inspect One.ttf Two.woff2 --include-name-records
font-metadata inspect Collection.ttc --format json
font-metadata inspect -- "-font.ttf"
```

Plain output shows preferred names, version, attribution, vendor ID, raw embedding flags, table statuses and diagnostics. `--include-name-records` adds all available decoded name records, including duplicates and unknown IDs. Collections retain individual faces and their original indexes. `--` makes subsequent arguments literal filenames.

## Collect license evidence

```shell
font-metadata inspect Example.woff2 --format json --analyze-licenses > font-licenses.json
```

`licenseDescription` and `licenseUrl` are ordinary metadata available without analysis. `--analyze-licenses` adds per-face hints with identifiers, matching rules and complete source records. Add `--include-name-records` to retain all available name records as well.

Inspect `licenseAnalysis.isSourceComplete` and the face's `nameTableStatus` when interpreting evidence. Empty hints do not prove absence of a license, and conflicting hints remain separate. Hints and raw embedding flags do not establish permission to redistribute a font. See [license analysis](licensing.md) for recognition boundaries.

## Inventory a directory with PowerShell

The CLI accepts file paths; the shell must enumerate directories. This PowerShell 7 example gathers common font extensions recursively and writes one report:

```powershell
$extensions = '.ttf', '.otf', '.ttc', '.otc', '.woff', '.woff2', '.eot'
$fontPaths = @(Get-ChildItem -LiteralPath ./fonts -File -Recurse -ErrorAction Stop |
    Where-Object { $_.Extension.ToLowerInvariant() -in $extensions } |
    Sort-Object FullName |
    Select-Object -ExpandProperty FullName)
if($fontPaths.Count -eq 0) {
    throw 'No font files found.'
}
$PSNativeCommandUseErrorActionPreference = $false
font-metadata inspect --format json --analyze-licenses -- @fontPaths > font-report.json
$inspectionExitCode = $LASTEXITCODE
Write-Host "Inspection exit code: $inspectionExitCode"
```

The script handles native exit codes explicitly so that a partial inspection can still be reviewed when PowerShell's error preference is `Stop`. The extension filter belongs to this script: the reader recognizes content at the beginning of each input regardless of its filename. Remove or adapt the filter to inspect other candidates. For very large inventories, split the paths into batches to stay within the operating system's command-line length limit; save each batch as a separate JSON report.

The tool does not expand wildcards, traverse directories, open URLs, extract archives/resources or search for embedded fonts. Extract a font resource to a standalone file or binary stream before inspecting it.

## Read a JSON report in PowerShell

After the directory example, parse the report only when the command produced a complete output document:

```powershell
if($inspectionExitCode -notin 0, 1) {
    throw "Inspection needs attention (exit $inspectionExitCode); review the report and stderr."
}
$report = Get-Content -LiteralPath ./font-report.json -Raw | ConvertFrom-Json
if($report.schemaVersion -ne 1) {
    throw 'Unsupported report schema version.'
}
$report.results | ForEach-Object {
    $result = $_
    if($null -eq $result.metadata) {
        return
    }
    foreach($face in $result.metadata.faces) {
        [pscustomobject]@{
            Path = $result.source.path
            Status = $result.status
            Face = $face.index
            Family = $face.familyName
            Version = $face.version
            LicenseDescription = $face.licenseDescription
            LicenseHints = (@($face.licenseAnalysis.hints | ForEach-Object { $_.identifier }) -join '; ')
        }
    }
} | Format-Table -AutoSize
$report.results | Where-Object status -ne 'complete' |
    Select-Object source, status, diagnostics
```

The second view retains failures with no faces, which the face inventory cannot show. A batch is one JSON object with a `results` array, even when only one file is inspected. Check statuses and diagnostics as well as extracted values. See the [JSON Schema](cli.schema.json).

The `licenseAnalysis` property is present because the preceding recipe requests `--analyze-licenses`. Preserve the original JSON alongside any flattened inventory so that source records, diagnostics and per-face evidence remain available for review or downstream audit/SBOM enrichment. The CLI does not generate an SBOM.

## Inspect damaged or unrecognized inputs

```shell
font-metadata inspect Damaged.ttf Unknown.bin Example.ttf --format json --include-name-records > inspection-report.json
```

This batch continues to the last file even if an earlier input cannot be fully read. A `partial` result can retain verified faces and records; check `nameTableStatus`, `os2TableStatus` and `diagnostics` before using them. An unrecognized input can have `metadata: null`. The exit code is 1 when at least one input is incomplete or unrecognized and no I/O failure occurs.

`complete` describes supported metadata extraction, not full font validation. Unicode coverage, font classification and SPDX expression parsing are not current CLI capabilities.

## Inspect a binary stream

In Bash on Linux or macOS, redirect a file into binary stdin:

```bash
font-metadata inspect - --format json < Example.woff2 > font-report.json
```

`-` must be the only input source. Use a pipe that preserves bytes if another program supplies the font. PowerShell text pipelines such as `Get-Content Example.woff2 | ...` are unsuitable; pass the filename directly when possible.

## Use a pinned tool in CI

Prepare and version the local tool manifest as described above. With the .NET 10 SDK installed and the package feed accessible, a Bash step can restore the tool, save the report and propagate the inspection result:

```bash
dotnet tool restore || exit $?
inspection_exit=0
dotnet tool run font-metadata -- inspect One.ttf Two.woff2 --format json --analyze-licenses > font-report.json || inspection_exit=$?
exit "$inspection_exit"
```

Configure report-artifact upload to run even when the inspection step fails. The explicit capture also works in shells using `set -e`.

| Exit code | Meaning for automation |
| --- | --- |
| 0 | Every input has complete supported metadata; warnings may remain |
| 1 | At least one partial, failed, unsupported or unrecognized input |
| 2 | Invalid arguments; no inspection report |
| 3 | Input I/O or output failure; output failure can truncate the report |
| 130 | Cooperative cancellation; the report may be unfinished |

An individual input failure does not stop subsequent inputs. Input I/O failures are reported in JSON, but code 3 can also mean output failure, so validate the document before consuming it. License hints do not change the exit code: this example checks metadata extraction, not license approval.

## Limit input size

```shell
font-metadata inspect Example.woff2 --max-input-bytes 67108864 --format json
```

This limits each input to 64 MiB; the default is 128 MiB. Other library decoding limits still apply. The option does not impose an exact process-memory ceiling or execution deadline. Ctrl+C requests cooperative cancellation. See [processing untrusted fonts](security.md) and the [CLI reference](cli.md#cancellation-and-memory).
