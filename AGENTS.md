# Repository guidance

This is the single source of rules for coding agents working in this repository. User instructions determine the task scope. Tool-specific instruction files, if introduced, should reference this file rather than duplicate its contents.

## Scope and current implementation

The library implements SFNT/TTC, WOFF, WOFF2 and EOT metadata reading with partial results, per-table statuses and diagnostics. The repository also contains CLI file/stdin inspection with plain/JSON reports and optional license evidence, tests, packaging checks and CI. Separate license analysis of decoded name records is implemented. WOFF2 collections and EOT main/EUDC payloads use shared budgets and preserve independent metadata. Embedded search remains deferred.

Work directly in this repository for its authorized tasks. Changes to external consumers require an explicit request. Preserve unrelated concurrent changes.

## Git ownership

Agents may read and inspect the repository using Git, including status, diff, log, show, blame, ls-files and other read-only analysis operations. Editing project files and running build/test checks are allowed within the authorized task. The user performs operations that change Git state or remote repository state after reviewing the changes. Do not stage, commit, create or switch branches, manage worktrees, merge, rebase, stash, reset, clean, create or delete tags, change remotes, fetch, pull or push. Do not modify Git metadata directly or use wrappers or repository-management APIs to bypass this rule. At the end of each reasonably complete implementation task, include a suggested commit title in the final response without waiting for a separate request. This rule applies unless the user explicitly changes it.

Keep research, comparisons with original projects and temporary planning outside the repository. Do not embed local workspace paths or research transcripts in product documentation.

## Read by task

| Task | Document |
| --- | --- |
| Current functionality and setup | [README](README.md) |
| Components, dependencies and types | [Architecture](docs/architecture.md) |
| Public type surface | [API](docs/api.md) |
| License evidence and recognition boundaries | [Licensing](docs/licensing.md) |
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
- Group library sources by responsibility: Reading for operations/options/limits/exceptions, Reading/Internal for parser implementation, Inspection for outcomes/statuses, Diagnostics for diagnostic contracts, and Licensing for evidence analysis (with internal matching helpers under Licensing/Internal). Keep FontMetadata and FontFormat at the library root. Folder names do not change namespaces: public types stay in Fonts.Metadata and parser helpers in Fonts.Metadata.Internal.
- Keep operations, options and diagnostics independently usable. Do not nest every type in FontMetadata.
- Public members must serve independent consumers. Keep parsers, buffers, budgets and orchestration internal. Do not widen visibility just for tests; use narrow InternalsVisibleTo access when needed.
- Use immutable results and collections that do not expose mutable backing storage. Use immutable init-only options, or snapshot any mutable options at operation entry.
- Before introducing an interface, wrapper, DTO, factory or cache, look for an existing contract with the same meaning. A test double alone does not justify an interface.
- Do not add asynchronous/disposable contracts without corresponding I/O or resource ownership.

## Rules for implementing font reading

These invariants apply to implemented and future readers:

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
- Use block-bodied implementations. Put method and constructor bodies on separate lines, including one-statement helpers and test doubles; keep only simple property accessors compact. Keep argument grouping readable.
- Put if and loop bodies on the next line. Short closing else and finally branches may stay on one line when readable, for example `else protectedRanges.Add(range);` or `finally { stream.Dispose(); }`. Keep return, continue, break and throw on separate lines.
- Use named local variables for intermediate results instead of nesting parsing, snapshot creation and result construction in a single expression.
- Default absent collections on the receiving model where useful. Use ReadOnlyCollection<T>.Empty and ReadOnlyDictionary<TKey, TValue>.Empty instead of wrapping empty arrays or allocating empty dictionaries; preserve immutable result collections.
- Express four-byte textual signatures with UTF-8 literals and explicit big-endian conversion when this is clearer than hexadecimal values. Keep genuinely numeric versions and independent test vectors numeric.
- Use explicit using blocks for meaningful resource lifetimes; use braces for multiline bodies. Place disposal close to construction and exception filters on continuation lines.
- Follow .editorconfig and .gitattributes; do not inherit unrelated parent-repository settings.
- Write XML documentation for public API contracts. Use ordinary concise comments for implementation decisions.
- Replace binary-format magic numbers with named constants for signatures, versions, field offsets, sizes and flags. Keep ordinary loop/index literals where their meaning is clear.
- Explain non-obvious binary parsing and recovery decisions with short comments and links to primary format specifications; do not narrate obvious statements.
- Extract complex predicates and branching into focused helpers with meaningful names when this improves readability.
- In C#, keep diagnostic message text in named constants rather than embedding message literals in parsing/control-flow code. In PowerShell, keep messages inline; do not introduce message constants or lookup tables. Retain readable conditions and multiline control flow in scripts.
- Keep test fixtures independent of production format constants. Name repeated layout values in test helpers; preserve explicit boundary cases, expected values and fixed binary vectors as independent test evidence.
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
- At the end of a major implementation phase, review the PR description against the final scope, behavior and validation results. If it is outdated, propose a revised description for the user to apply.
