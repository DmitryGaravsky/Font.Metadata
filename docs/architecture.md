# Architecture

The repository contains a library, a CLI and two NUnit test projects. It currently establishes packaging and model names; font readers and analyzers are not implemented.

## Repository layout

```text
Font.Metadata/
├── Font.Metadata.slnx
├── Directory.Build.props           compiler settings and development version
├── Directory.Packages.props        central dependency versions
├── global.json                     .NET SDK selection
├── NuGet.Config                    public dependency source
├── Font.Metadata/
│   └── FontMetadata.cs             outer model and nested type declarations
├── Font.Metadata.Tool/
│   └── Program.cs                  help, version and unsupported-command handling
├── Font.Metadata.Tests/
│   └── PublicContractTests.cs       public type identities and accessibility
├── Font.Metadata.Tool.Tests/
│   └── CommandTests.cs             CLI process behavior and output channels
├── eng/
│   ├── Tests.props                 shared test settings and dependencies
│   ├── verify.ps1                  restore, build, tests, formatting and package checks
│   ├── verify-packages.ps1         local packing, consumer and tool installation
│   ├── get-version.ps1             canonical development/release version validation
│   ├── test-release.ps1            offline release-script checks
│   ├── prepare-release.ps1         verified packages and portable CLI archive
│   ├── verify-release-assets.ps1   checksums and package identities
│   └── publish-github-release.ps1  draft upload, collision checks and publication
├── .github/workflows/ci.yml         branch/PR operating-system verification matrix
├── .github/workflows/release.yml    tag verification and package/release publication
└── docs/                           current product and contributor documentation
```

## Dependencies

```text
 Font.Metadata.Tests            Font.Metadata.Tool.Tests
          │                              │
          │ ProjectReference             │ build reference and child process
          ▼                              ▼
 Font.Metadata ◀────────────── Font.Metadata.Tool
                         ProjectReference

 Local library nupkg ──→ isolated PackageReference consumer
 Local tool nupkg ─────→ isolated tool-path installation
```

The library has no application-layer dependencies and no project references. The tool consumes the library and owns console behavior. Tests do not widen the public API.

## Public type organization

```text
namespace Fonts.Metadata
└── FontMetadata
    ├── Face
    ├── NameRecord
    └── EotHeader
```

The model types are sealed and have internal constructors. Their properties and behavior will be introduced with the corresponding implementation rather than populated with placeholder data.

## Build and runtime boundaries

The library, tool and both test projects target net10.0. A stable .NET 10 SDK builds the solution on Windows, Linux and macOS.

All project settings and dependencies are local to this repository. There are no linked sources, project references or feeds from the original consumer tools.

Architectural diagrams use text and UTF box-drawing characters. They must remain readable without a diagram renderer.
