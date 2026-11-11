# Repository guidance

This is the single source of rules for coding agents working in this repository. User instructions determine the task scope. Tool-specific instruction files, if introduced, should reference this file rather than duplicate its contents.

## Scope and current implementation

The repository contains a buildable foundation: library model type shells, CLI help/version commands, tests, packaging checks and CI. Font parsers, metadata properties and the license analyzer are not implemented yet.

Work in this repository for its authorized tasks. Changes to external consumers, Git staging/commits, remotes and publication require an explicit request. Preserve unrelated concurrent changes.

Keep research, comparisons with original projects and temporary planning outside the repository. Do not embed local workspace paths or research transcripts in product documentation.

## Read by task

| Task | Document |
| --- | --- |
| Current functionality and setup | [README](README.md) |
| Components, dependencies and types | [Architecture](docs/architecture.md) |
| Public type surface | [API](docs/api.md) |
| CLI behavior | [CLI](docs/cli.md) |
| Verification and fixtures | [Testing](docs/testing.md) |
| Hosted builds | [CI](docs/ci.md) |
| Package release preparation | [Release](docs/release.md) |
| Contribution workflow | [Contributing](CONTRIBUTING.md) |

Read only what is relevant to the change. Source code and tests establish implemented behavior; do not treat future design examples as working APIs.

## Architecture and API

- Keep the solution and all four project directories at the repository root. Shared test settings belong in eng/Tests.props; do not introduce src/tests container directories.
- Target net10.0 only for the library, tool and tests. Use a stable .NET 10 SDK; do not introduce older framework targets or compatibility dependencies.
- Keep the library independent of the CLI, external scanners, catalogs and SBOM models.
- Namespace is Fonts.Metadata. Package IDs are Font.Metadata and Font.Metadata.Tool. Metadata models nest one level under FontMetadata: Face, NameRecord and EotHeader.
- Keep operations, options and diagnostics independently usable. Do not nest every type in FontMetadata.
- Public members must serve independent consumers. Keep parsers, buffers, budgets and orchestration internal. Do not widen visibility just for tests; use narrow InternalsVisibleTo access when needed.
- Use immutable results and collections that do not expose mutable backing storage. Snapshot options at operation entry.
- Before introducing an interface, wrapper, DTO, factory or cache, look for an existing contract with the same meaning. A test double alone does not justify an interface.
- Do not add asynchronous/disposable contracts without corresponding I/O or resource ownership.

## Rules for implementing font reading

These are implementation invariants, not claims that readers already exist:

- Read and Inspect must share one parser. Distinguish missing data from unreadable data and preserve independently verified tables, faces and EOT header facts.
- Preserve original indexes and source name records. EOT metadata must not silently replace embedded face facts.
- Recover only through verified ranges. Check sizes, arithmetic and offsets before reading or allocating.
- Keep format constraints separate from resource limits. Account for decoded work across the container, including shared tables, secondary MTX streams and EUDC. Recovery must not reset budgets.
- Observe cancellation during reading and decoding. Do not turn cancellation or I/O exceptions into malformed-font results.
- Catch expected data failures at deliberate boundaries; do not swallow programming errors or OutOfMemoryException.
- Provide stable diagnostic codes and explicit incomplete states. Callers must not have to parse message text.
- Keep unavailable functionality explicit. Do not report empty success merely because a decoder is missing.
- Keep license analysis separate from reading, without I/O or mutation. Preserve each hint's evidence; do not infer ownership, approval or final license conclusions.
- Do not introduce hidden shared mutable state, global caches or implicit global encoding-provider registration.
- Explain non-obvious binary boundaries with short local comments and primary specification references: sizes, offsets, transforms, bit fields, alignment and variable-length integers.

## Coding conventions

- Use nullable reference types and treat compiler warnings as errors.
- Centralize dependency versions in Directory.Packages.props and use NUnit for tests.
- Use narrow visibility, omit private when it is the default, and make stateless helpers static.
- Use block-bodied implementations and compact simple accessors. Keep argument grouping readable.
- Use explicit using blocks for meaningful resource lifetimes; use braces for multiline bodies. Place disposal close to construction and exception filters on continuation lines.
- Follow .editorconfig and .gitattributes; do not inherit unrelated parent-repository settings.
- Write XML documentation for public API contracts. Use ordinary concise comments for implementation decisions.
- Keep meaningful public contracts even if current consumers do not yet use every member. Validate review claims against actual target frameworks before changing accessibility or behavior.

## Verification and documentation

- Run [eng/verify.ps1](eng/verify.ps1) for the complete local pipeline. Focused commands are in testing.md.
- Do not run a full baseline automatically at each task start. Verify changed behavior proportionately and broaden checks when shared behavior or failures justify it.
- Use --no-restore only with current assets; rebuild before --no-build after relevant edits.
- Test observable behavior with independent fixtures. Do not mirror production algorithms in tests.
- Keep publication exclusive to version tag workflows. Prepare and validate release files locally without credentials; never push tags or publish unless explicitly requested.
- For API and packaging changes, test actual nupkg consumers and installed tools, not only ProjectReference builds.
- Keep all repository documentation in English. Product docs describe current functionality; contributor docs describe development rules and checks.
- Use UTF text diagrams, never Mermaid. Update diagrams when type ownership, dependencies or execution flow changes.
- Verify relative links and examples. Report exactly what ran; local success does not imply a GitHub-hosted run passed.
- Inspect the complete change and report the outcome, verification and remaining limitations. Do not stage or commit automatically.
