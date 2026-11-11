# Command-line tool

Package: Font.Metadata.Tool. Command: font-metadata. Runtime: .NET 10.

## Supported commands

| Invocation | stdout | stderr | Exit code |
| --- | --- | --- | --- |
| No arguments, --help or -h | Usage and current limitations | Empty | 0 |
| --version | Tool informational version | Empty | 0 |
| inspect followed by any arguments | Empty | Inspection is not implemented | 1 |
| Other arguments | Empty | Unknown arguments | 2 |

The tool does not read font files yet. Plain and JSON font reports are unavailable. A failed inspect invocation does not emit an empty success report or classify the input as malformed.

## Running from source

```shell
dotnet run --project Font.Metadata.Tool -- --help
dotnet run --project Font.Metadata.Tool -- --version
```

## Release distribution

After a version has been published, install it with `dotnet tool install --global Font.Metadata.Tool --version <version>`. The .NET 10 SDK is required for tool installation.

Alternatively, download `font-metadata-<version>-portable.zip` from GitHub Releases, extract it and run:

```shell
dotnet Font.Metadata.Tool.dll --help
dotnet Font.Metadata.Tool.dll --version
```

The same ZIP works on Windows, Linux and macOS with the .NET 10 runtime installed. It does not bundle a runtime or a native executable. Each release includes SHA256SUMS for its downloadable files.

## Local installation

After running eng/verify.ps1, use the generated development package:

```shell
dotnet tool install Font.Metadata.Tool --version 0.1.0-dev --tool-path ./artifacts/manual-tool --add-source ./artifacts/packages/0.1.0-dev
./artifacts/manual-tool/font-metadata --help
```

On Windows, the installed executable is font-metadata.exe. The automated package verification uses an isolated local-only feed and package cache; it does not install or update a global tool.
