# Font.Metadata

Font.Metadata is a .NET font metadata library with a companion command-line tool. It is a personal project by [Dmitry Garavsky](https://github.com/DmitryGaravsky/).

The current development version provides the package structure and public model type names. **Font parsing and license analysis are not implemented yet.** It does not currently read TTF, OpenType, WOFF or EOT files.

## Available functionality

- Library package: Font.Metadata.
- Namespace: Fonts.Metadata.
- Public model types: FontMetadata, FontMetadata.Face, FontMetadata.NameRecord and FontMetadata.EotHeader.
- Tool package: Font.Metadata.Tool; command: font-metadata.
- CLI commands: --help and --version.

The model types currently have no metadata properties or public constructors. They establish the API names without returning fabricated font data. The inspect command reports that inspection is unavailable and exits unsuccessfully.

## Requirements

| Component | Target |
| --- | --- |
| Library | .NET 10 |
| CLI | .NET 10 |
| Build | .NET 10 SDK and PowerShell 7 |
| Tests | .NET 10 on Windows, Linux and macOS |

The library and tool require .NET 10. The build uses a stable .NET 10 SDK selected by global.json.

## Build and verify

```powershell
pwsh -File eng/verify.ps1
```

This restores dependencies, builds the solution, runs the available NUnit tests, checks C# whitespace, creates local development packages and verifies both a PackageReference consumer and an installed dotnet tool.

Run the CLI from source:

```shell
dotnet run --project Font.Metadata.Tool -- --help
dotnet run --project Font.Metadata.Tool -- --version
```

Tagged releases publish the library and dotnet tool to NuGet.org and attach a portable CLI archive to GitHub Releases. Publication requires the one-time setup in [Releases](docs/release.md). Local verification does not publish anything or establish that a public version is available.

## License

Copyright (c) 2026 Dmitry Garavsky. Font.Metadata and Font.Metadata.Tool are licensed under the [MIT License](LICENSE).

## Documentation

- [Current API](docs/api.md)
- [Command-line behavior](docs/cli.md)
- [Architecture](docs/architecture.md)
- [Testing](docs/testing.md)
- [Continuous integration](docs/ci.md)
- [Release preparation](docs/release.md)
- [Contributing](CONTRIBUTING.md)
