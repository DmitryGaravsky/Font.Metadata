# Contributing

Use [AGENTS.md](AGENTS.md) for repository-wide coding rules and [Architecture](docs/architecture.md) for the implemented structure.

## Development setup

Install a stable .NET 10 SDK and PowerShell 7. All projects target net10.0 on Windows, Linux and macOS.

```powershell
pwsh -File eng/verify.ps1
```

The script restores the exact SPDX dependency and build/test packages from nuget.org using the repository configuration; see [dependency verification](docs/spdx.md#build-and-verification). It builds and tests the projects, checks C# whitespace, packs local development artifacts and verifies consumption of those exact artifacts.

See [Testing](docs/testing.md) for focused commands and [CI](docs/ci.md) for the hosted matrix.

## Making a change

- Keep changes focused on the requested behavior and review the whole diff.
- Keep public documentation in English and describe implemented functionality accurately.
- Update API documentation, the affected UTF diagrams and meaningful tests together.
- Use independent fixtures with documented provenance for binary parsers.
- Do not duplicate production parsing or license-matching algorithms in tests.
- Keep research, hypothesis checks, experiments, ideas, reports, feature proposals and temporary plans outside this repository. Do not copy them into the repository or introduce dependencies, links or reader directions to unpublished local materials. Document relevant reviewed findings self-containedly, without machine-specific paths or external research filenames.
- Do not stage, commit, publish or change external consumer repositories unless explicitly requested.

## Verification

Run checks appropriate to the change. Documentation-only changes need link, example and consistency checks. Parser changes need focused valid/malformed, bounds, limits and cancellation tests. API/TFM changes need compatibility and package checks; CLI changes need process tests.

Avoid unconditional full baseline runs at every task start. Use --no-restore only with current assets and --no-build only after building the relevant changes.

## Formatting and documentation

Follow .editorconfig and .gitattributes. Source and documentation use UTF-8 and LF. Architectural diagrams use UTF text graphics in fenced text blocks; Mermaid is not used.

Document public APIs with XML comments covering ownership, errors and limits. Keep internal comments concise and explain non-obvious format boundaries with specification references.

Use relative links inside the repository. Contributor documentation may describe procedures and required checks; product documentation must not present unimplemented features as available.

Keep one owner for each detailed description: README for the capability/status overview, Formats for supported data and selection, API/Diagnostics for contracts, CLI plus its schema for reports, Architecture for the model/principles, and Testing for validation evidence. Link to the owner instead of maintaining competing specifications. Source and tests must substantiate implemented behavior; plans and a passing build alone cannot do so.

For a documentation review, check local links and anchors, compare examples with the current public surface, and distinguish implemented behavior, measured evidence and deferred work. If a change affects executable examples, run the relevant existing consumer or CLI check. Record exactly which checks ran. GitHub About is external presentation metadata; reconcile it during release review using the implemented README scope, without assuming a repository edit updates it.
