# Releases

Publishing a GitHub Release starts [release.yml](../.github/workflows/release.yml). The workflow verifies the release commit, publishes the three NuGet packages and attaches five files to the existing GitHub Release. Saving a draft, pushing a version tag or running local preparation does not initiate publication.

The library reads SFNT/TTC, WOFF, WOFF2 and EOT metadata and separately analyzes license evidence from decoded name records. The CLI provides file/stdin inspection with plain/JSON reports and optional license evidence. A release workflow does not imply that a public version already exists.

## Preview readiness

The initial independent validation cycle is complete with documented limitations and a retained reviewed corpus baseline. Validation resumes for the specific preview candidate or relevant product changes; no broader campaign is currently implied. Neither that baseline nor a passing build or prepared archive establishes that review of the release candidate is complete.

Before initiating the first preview publication:

- Complete CLI behavior, schema, installed-package and portable checks for the candidate revision.
- Review independent validation findings for the identified package; distinguish product defects, harness defects, expected limitations and unresolved comparisons.
- Resolve blocking issues involving crashes, loss of verified data, false completion, unsupported license conclusions or uncontrolled resource use. Recheck any changed package and retain its identity and original observations.
- Record the evaluated package version/hash, corpus scope, environment and known coverage gaps. Preserve the reviewed baseline before expanding license analysis.
- Align README examples, release notes and known limitations with the candidate's actual capabilities.
- Confirm publication configuration and explicitly decide to publish.

These are maintainer review criteria, not additional checks automatically enforced by the release scripts. [Initial validation results](testing.md#independent-validation) describe the evaluated development package and coverage limits.

The integrated adapter/tool uses the published Spdx.Expressions 1.0.0 package. Release verification restores that exact dependency from nuget.org and checks its packaged DLL and notices. Dependency availability does not replace the remaining release-readiness checks. Unicode coverage and exhaustive fuzzing remain deferred. Its documented scope and remaining limitations must be clear. See [Testing](testing.md#verification-layers) and [Processing untrusted fonts](security.md).

## Version policy

- Stable tags: `v1.0.0`.
- Prerelease tags: `v0.1.0-preview.1` or `v1.0.0-rc.1`.
- Use three numeric components without leading zeros and lowercase prerelease identifiers. Build metadata (`+...`), four-component versions and numeric prerelease identifiers with leading zeros are rejected.
- The tag supplies the version for all three packages and assemblies. Ordinary builds use the version in Directory.Build.props; release builds override it without editing source files.
- Never move a published tag or reuse a package version for changed code. Fixes require a new version.

## Release flow

```text
Publish release ──→ Validate tag, commit and main ancestry
                         │
                         ▼
             Verify tagged commit on three OSes
             ├── Build, NUnit and offline release-script tests
             ├── PackageReference consumer and installed tool
             └── Portable ZIP extraction and execution
                         │
                         ▼
             Preserve verified Linux release files
                         │
                         ▼
             NuGet Trusted Publishing (environment: nuget)
             ├── Font.Metadata
             ├── Font.Metadata.Spdx
             └── Font.Metadata.Tool
                         │
                         ▼
             Existing GitHub Release ──→ Upload missing files ──→ Verify downloads
```

All jobs use the tagged commit. Publication waits for the entire verification matrix. Publishing jobs download the same retained artifact; they do not rebuild. SHA256SUMS and package identities are checked before publication. Release runs for the same tag are serialized and do not cancel an active publication.

GitHub Release assets:

| File | Purpose |
| --- | --- |
| Font.Metadata.&lt;version&gt;.nupkg | Library package |
| Font.Metadata.Spdx.&lt;version&gt;.nupkg | Optional SPDX adapter |
| Font.Metadata.Tool.&lt;version&gt;.nupkg | Installable dotnet tool package |
| font-metadata-&lt;version&gt;-portable.zip | CLI files for Windows, Linux and macOS; requires the .NET 10 runtime |
| SHA256SUMS | SHA-256 checksums of the four files above |

The ZIP uses the tool package's managed payload and includes RUNNING.txt and LICENSE. Run `dotnet Font.Metadata.Tool.dll --help` after extraction. It does not contain a bundled runtime. The GitHub prerelease checkbox must match the tag suffix. The workflow preserves the release title, description and latest-release selection.

## One-time setup

The repository and all three packages use the [MIT License](../LICENSE). All three packages declare the SPDX expression `MIT` and include LICENSE; the portable CLI ZIP includes the same license text. Copyright metadata is shared in Directory.Build.props.

This is a personal project by [Dmitry Garavsky](https://github.com/DmitryGaravsky/). All three packages identify Dmitry Garavsky as author and copyright holder. Package project and repository URLs point to [Font.Metadata on GitHub](https://github.com/DmitryGaravsky/Font.Metadata).

All three packages will be published under the personal NuGet account [DmitryGaravsky](https://www.nuget.org/profiles/DmitryGaravsky). Before the first public release, confirm all three package IDs are available or owned by this account and that package metadata uses the correct repository URL. Package author metadata identifies Dmitry Garavsky; NuGet ownership is managed by the account.

1. Confirm that release.yml is present in the candidate revision and GitHub Actions is enabled. Restrict actions to checkout, setup-dotnet, upload-artifact, download-artifact and NuGet/login, and require full commit SHAs. Disable immutable releases in the repository release settings before publishing a release with an empty asset list. The workflow attaches verified files after the release becomes public.
2. Create a GitHub Actions environment named **nuget**. In its deployment rules, allow tags matching `v*`. Set DmitryGaravsky as the required reviewer, allow self-review and disable administrator bypass.
3. In the **nuget** environment, add the configuration variable **NUGET_USER** with the exact value **DmitryGaravsky**. It is a variable, not an API-key secret. Select the personal NuGet account **DmitryGaravsky** as the policy owner on NuGet.org.
4. On NuGet.org, open **Trusted Publishing** and create a policy with the following values:

   | Policy field | Value |
   | --- | --- |
   | Policy owner | `DmitryGaravsky` (personal NuGet account) |
   | Repository owner | `DmitryGaravsky` |
   | Repository | `Font.Metadata` |
   | Workflow file | `release.yml` (filename only) |
   | Environment | `nuget` |

5. Confirm the account and policy have the required package permissions. Confirm all three package IDs are covered by the policy. Protect main and tags with the required Ubuntu and Windows checks. Use a reviewed, signed commit on main as the release target.

The NuGet job alone requests `id-token: write`. `NuGet/login@v1` exchanges GitHub's OIDC token for a short-lived key immediately before pushing. No persistent NUGET_API_KEY secret is needed. GitHub Release publication uses the built-in GITHUB_TOKEN with `contents: write` in its own job; no personal access token is required.

See [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing) and the [NuGet login action](https://github.com/NuGet/login) for account and organization requirements. Actual authentication cannot be established by a local dry run.

## Prepare locally

```powershell
pwsh -File eng/prepare-release.ps1 -Tag v0.1.0-preview.1
```

This performs the checks and writes `artifacts/release/0.1.0-preview.1/`. It does not create a tag, contact publishing endpoints or publish files. Restore may download build dependencies from nuget.org. A local Git tag is not required.

## Initiate a release

1. Merge the reviewed workflow through a pull request and confirm that CI passed for the chosen main commit.
2. In **Releases** select **Draft a new release**. Choose a new version tag targeting that commit, or select an existing tag on it. Enter the title and release notes, set **This is a pre-release** for a prerelease suffix and leave the asset list empty.
3. Select **Publish release** when ready. The release becomes public immediately. Follow the **Release** workflow and approve the deployment to the **nuget** environment after verification.
4. Confirm that the three packages, portable ZIP and SHA256SUMS appear on the release. Allow time for NuGet indexing.

Once the workflow finishes and NuGet indexing completes:

```shell
dotnet add package Font.Metadata --version 0.1.0-preview.1
dotnet tool install --global Font.Metadata.Tool --version 0.1.0-preview.1
```

## Recover from a failed release

Use **Re-run failed jobs** on the original Actions run. Verified artifacts are retained for 30 days. Successful verification jobs and their artifacts can then be reused. Re-running all jobs may conflict with an already uploaded immutable artifact and can produce different ZIP bytes; it is not the recovery path after publication has started.

- Before authentication, the workflow checks all existing release assets and all three package IDs. It verifies NuGet package entries against the retained Linux artifacts, ignoring only the NuGet repository signature. A conflicting file stops the run.
- On retry, matching packages are skipped and missing packages are pushed in dependency order. The GitHub job checks existing files, uploads missing files and verifies the uploaded bytes. The release text stays under the maintainer's control.
- A public release can temporarily have no files or only some files if verification or publication fails. Use the original run's retained artifacts to recover. A successful NuGet push persists if a later job fails.

Local tests exercise these paths with GitHub and NuGet substitutes. An actual release run is required to verify hosted OIDC authentication and publication.
