# Contributing

Use [AGENTS.md](AGENTS.md) for repository-wide coding rules and [Architecture](docs/architecture.md) for the implemented structure.

## Development setup

Install a stable .NET 10 SDK and PowerShell 7. All projects target net10.0 on Windows, Linux and macOS.

```powershell
pwsh -File eng/verify.ps1
```

The script restores from nuget.org using the repository configuration. It builds and tests the projects, checks C# whitespace, packs local development artifacts and verifies consumption of those exact artifacts.

See [Testing](docs/testing.md) for focused commands and [CI](docs/ci.md) for the hosted matrix.

## Making a change

- Keep changes focused on the requested behavior and review the whole diff.
- Keep public documentation in English and describe implemented functionality accurately.
- Update API documentation, the affected UTF diagrams and meaningful tests together.
- Use independent fixtures with documented provenance for binary parsers.
- Do not duplicate production parsing or license-matching algorithms in tests.
- Keep research, source-project comparisons and temporary planning material outside this repository. Do not add machine-specific paths to public docs.
- Do not stage, commit, publish or change external consumer repositories unless explicitly requested.

## Verification

Run checks appropriate to the change. Documentation-only changes need link, example and consistency checks. Parser changes need focused valid/malformed, bounds, limits and cancellation tests. API/TFM changes need compatibility and package checks; CLI changes need process tests.

Avoid unconditional full baseline runs at every task start. Use --no-restore only with current assets and --no-build only after building the relevant changes.

## Formatting and documentation

Follow .editorconfig and .gitattributes. Source and documentation use UTF-8 and LF. Architectural diagrams use UTF text graphics in fenced text blocks; Mermaid is not used.

Document public APIs with XML comments covering ownership, errors and limits. Keep internal comments concise and explain non-obvious format boundaries with specification references.

Use relative links inside the repository. Contributor documentation may describe procedures and required checks; product documentation must not present unimplemented features as available.
