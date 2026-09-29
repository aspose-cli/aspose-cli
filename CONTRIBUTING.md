# Contributing to Aspose CLI

Every change keeps the architecture rules in [AGENTS.md](AGENTS.md). Only collaborators can open
pull requests; this repository does not accept them from outside contributors, so report problems
and proposals through an issue. Report vulnerabilities through [SECURITY.md](SECURITY.md), not
public issues.

## Prerequisites

Windows x64 with:

- a .NET 10 SDK at or above the version in `global.json` (any later 10.0 feature band works);
- PowerShell 7.4 or later (`pwsh`) for the scripts, and Windows PowerShell 5.1, which runs the
  customer installer;
- access to the Playwright CDN, from which `scripts/test.ps1` provisions the pinned Chromium for
  the browser tests.

## Making a change

1. Change the owning Product or shared layer together with its contracts, schemas, Skills and
   tests.
2. After a catalog, identity or dependency change, run `scripts/sync.ps1`. It regenerates
   `eng/generated`, the solution and the lock files from `eng/products.json` (whose row order is
   the display order and whose optional `defaultProduct` owns unprefixed docs topics) and
   `eng/distribution.json` (identity literals, schema base URL and `ASPOSE_CLI_*` variable names,
   read in code through `DistributionInfo`).
3. Run `scripts/test.ps1 -Configuration Release` while you work and
   `scripts/test.ps1 -Configuration Release -Scope Affected` before you commit (see [Tests](#tests)).
4. For publishing or installer changes, check `scripts/publish.ps1 -Configuration Release
   -RuntimeIdentifier win-x64` and `scripts/install-local.ps1`, which installs an unsigned
   development build (`-Update` and `-Uninstall` work as in `install.ps1`).
5. Open a pull request as described below.

## Pull requests

Every change reaches `master` through a pull request that `CI / verify` passes. The
`Pull request` workflow checks the branch name and the title.

- **One concern per pull request**, split by responsibility rather than by file, with its tests,
  schemas, Skills and docs in the same change. Keep a mechanical refactor apart from a behavior
  change. Aim for about 400 changed lines, not counting generated files and snapshots, and
  split anything near 1,000.
- **Branch:** `<type>/<kebab-case-summary>` from the latest `master`, for example
  `fix/backup-disclosure`. GitHub's own `revert-<number>-<branch>` branches are accepted too.
- **Title:** `<type>(<scope>): <summary>`, imperative, starting in lower case, no final period,
  at most 65 characters; GitHub appends ` (#N)` when it becomes the squash commit. Types: `feat`,
  `fix`, `refactor`, `perf`, `test`, `docs`, `build`, `ci`, `chore`, `revert`. The scope is
  optional: `sdk`, `host`, `cli`, `app`, `cells`, `pdf`, `slides`, `words`, `skills`,
  `install`, `release`, `deps`. Retitle a GitHub-generated revert as
  `revert: <original summary>`.
- **Description:** the template's summary (what and why), test plan (commands actually run) and
  contract checklist. Leave out how the change was produced.
- **Push when step 3 passes;** every push runs CI again. Resolve conflicts on the branch by
  rebasing on `master`; take lock files and `eng/generated` from `master` and rerun
  `scripts/sync.ps1` rather than merging them by hand.
- **Merge** by squash, with the title as the whole commit message. Commits inside a branch are
  not kept, so their messages only need to be short.

## Tests

Tests use xUnit v3 with real engines and CLI child processes. `scripts/test.ps1` checks the
prerequisites and generated projections, builds once and runs the test projects side by side at
one of four scopes:

| Scope | Runs | Use |
| --- | --- | --- |
| `Fast` (default) | Every test without a category | While you work; a few minutes |
| `Changed` | Only the test projects a change reaches, plus the architecture tests | Pull-request CI |
| `Affected` | `Fast`, plus every test of the projects your change reaches since the merge base with `-Base` (default `master`) | Before a commit |
| `Full` | Every test, with a required license | Before a release and after an SDK update |

A test that takes several seconds by nature carries `[Category(TestCategory.Slow)]`; the
installer and Playwright tests carry `Installer` and `Browser`. The run lists every test without a
category that took longer than 10 seconds: make it faster or mark it. `-NoBuild` reuses a build
and `-HangTimeout` (default `15m`) dumps a test that stays silent that long. Each project's log,
TRX and browser failure traces are written to `artifacts/TestResults/<run-id>/<project>/`. Never
weaken a check or remove a supported operation to make a test pass.

Help and capabilities output is pinned by snapshots in
`tests/Aspose.Cli.Platform.Tests/Integration/Snapshots`; after an intended change, rerun
`CliContractTests` with `ASPOSE_CLI_TEST_UPDATE_SNAPSHOTS=1` and review the regenerated files in
the diff.

`Affected` runs a test project in full when the change touches it or a project it references,
adds the installer tests when `install.ps1` or `scripts/install-local.ps1` changes, adds nothing
for documentation, and runs everything for any other change, such as `eng/`, `scripts/` or
the shared build files.

`Changed` runs the tests without a category of the test projects the change since the merge
base with `-Base` reaches, by the same project rule, and always `tests/Aspose.Cli.Tests`.
Documentation and repository metadata (`*.md`, `.github/`, `LICENSE*`, `.gitignore`,
`.gitattributes`, `.editorconfig`) reach nothing else, and any other change outside the
projects runs every project. The other projects are listed as skipped. Pull-request CI runs
`Changed` against the target branch; pushes to `master` run `Fast`.

Runs are isolated from the developer's machine: they never read `%APPDATA%\aspose-cli`, project
`.aspose` files or `ASPOSE_*` settings; only `ASPOSE_CLI_TEST_*` variables pass through. Run one
test run at a time per Windows account, because App tests share the per-user App endpoint.

A run is licensed only when `ASPOSE_CLI_TEST_LICENSE_PATH` names a license file. Without it,
the cases that need a license are skipped and listed; the `Full` scope requires the license and
fails when a licensed case is skipped.
Other skips name their reason: a missing platform, tool (`pwsh`, `openssl`, `dotnet`) or
symbolic-link privilege. Keep license contents out of logs and fixtures.

```powershell
$env:ASPOSE_CLI_TEST_LICENSE_PATH = 'C:\private\Aspose.Total.lic'
.\scripts\test.ps1 -Configuration Release -Scope Full
```

### Known SDK issues

Each issue in [KNOWN-ISSUES.md](KNOWN-ISSUES.md) is reproduced with the SDK alone by a test in its
product's `<Product>KnownIssueTests` class that calls `KnownIssue.Reproduces` with the issue's id.
The test passes while the pinned SDK still has the defect. `KnownIssueCatalogTests` keeps the
file true: every issue has one reproduction, source code that names its id, and a heading with
the SDK version `eng/products.json` pins.

To update an SDK, change its version in `eng/products.json`, run `scripts/sync.ps1` and the
`Full` scope. For each reproduction that now fails, the SDK fixed the defect: search its id and
delete the issue's section, the code that names it and the test. Then update the version in the
issue headings.

## Code conventions

- **JSON input.** Test optional input defaults through the production source-generated
  serializer, including omitted fields and explicit `false`, `0` and `null`. The serializer
  [does not preserve init-only property initializers](https://github.com/dotnet/runtime/issues/84484),
  so an input record outside an operation vocabulary takes its scalar defaults as optional
  constructor parameters.
- **Operation contracts.** An operation is declared once, as a record with
  `[Operation("name")]` under the product's `[OperationVocabulary]` base record. To add one,
  write the record in the product's contract file with its `[JsonSerializable]` line in the
  ops JSON context there, the handler method the engine's `I{Base}Handler` interface then
  requires, and its documentation and tests. The record states the contract: `required`
  members, initializers for defaults, `[InputPath]` and `[SecretEnv]` members, constraint
  attributes such as `[Minimum]`, `[PageRange]` or the record rules `[ExactlyOneOf]`,
  `[AtLeastOneOf]`, `[DependentRequired]`, `[PresentWhen]` and, on nested records,
  `[MinProperties]` for every rule JSON Schema can state, and a `Validated()` override for the
  rest, stated in the record's summary. An array constraint applies at its `Depth`; any
  other constraint also reaches the items of lists and maps. The operation generator builds
  the catalog and the handler dispatch, and analyzer `APCLI012` rejects an incomplete
  contract. `OperationJsonConverter` owns the discriminator, strict fields and duplicate
  rejection and writes omitted defaults; the catalog enforces the constraints and writes the
  ops schema, whose committed copy the product contract tests keep current (rewrite it with
  `ASPOSE_CLI_TEST_UPDATE_SNAPSHOTS=1`). Three deliberate gaps remain between the schema and
  the parser: an integer member accepts a whole number written as `1.0` only as far as the
  serializer does, the schema's `integer` does not state the CLR type's range, and a value
  kind read by a parser, such as an A1 range, publishes a pattern that admits some values
  the parser rejects.
- **Missing targets.** A code for a sheet, slide, bookmark or other target the document does
  not contain is declared with `ErrorCode.NotFound`, and its errors are built only with
  `CliErrors.NotFound` (named targets, listing the available names and the closest ones) or
  `CliErrors.NotFoundAt` (numbered targets, stating the count); `CliException` rejects a
  not-found code without those details. A code needed by more than one product is declared
  once in the SDK's `ErrorCodes`. A name that several targets share is refused, never resolved
  to the first match.
- **Command parameters.** Every string argument and option declares its input role with
  `WithInput` (`InputKind.File`, `InputKind.JsonSource` or `InputKind.None`), and its value
  sources and secret handling on the symbol. The Host reads these declarations, never token
  order, option spelling or file existence; command-tree construction rejects missing or
  conflicting declarations. `GlobalOptionNames` is the one list of reserved global options and
  `StandardOptionNames` the one list of options the command template owns; analyzer `APCLI008`
  rejects a product option that reuses either. Products build commands with
  `StandardCommand` or `BoundedEditCommand`, which run through the host pipeline; analyzer
  `APCLI011` rejects a product that references the Host seam `StandardOptions`.

## Releases

The version is declared once as `Version` in `Directory.Build.props`, and a release is built only
from the protected tag `v<Version>`. Give every new build a higher version: an update refuses a
different build with the same version.

`scripts/package.ps1 -Configuration Release -RuntimeIdentifier win-x64` needs a clean revision.
It publishes the payload, writes its `SHA256SUMS`, runs an install, update and uninstall smoke
test, and writes the release assets to `artifacts/release/win-x64`: the archive, `install.ps1`,
`RELEASE-MANIFEST.json` (the archive's name, size and SHA-256, and the source revision) and
`SHA256SUMS`. `-PrepareOnly` stages a development package instead. Dependency notices follow the
published graph ([notice sources](eng/notices/README.md)).

No license reaches the workflows in `.github/workflows`, so they run the SDKs in evaluation
mode. `ci.yml` runs the `Changed` scope for a pull request, and `package.ps1` too when the pull
request changes a packaging input (`install.ps1`, `scripts/`, `eng/`, the build properties,
`global.json` or the launcher project). Pushes to `master` run the `Fast` scope, so a test a
pull request skipped runs when it merges; a manual run adds `package.ps1`, to rehearse a release
before its tag. A newer push to a pull request cancels its running check. `release.yml`, on a protected
version tag, runs the `Fast` scope and `package.ps1` and publishes the assets as a GitHub
release of `aspose-cli/aspose-cli`, the repository named by `releaseRepository` in
`eng/distribution.json`. Before pushing the tag, run the `Full` scope locally with a license.

The workflows rely on these repository settings:

- **Actions permissions** allow only actions created by GitHub and require them to be pinned to
  a full-length commit SHA, so every `uses:` names a commit, with its tag in a comment.
- **Workflow permissions** give `GITHUB_TOKEN` read access by default; `release.yml` asks for
  `contents: write` in its one job.
- **A tag ruleset** protects `v*`. `release.yml` runs only when `github.ref_protected` is true,
  so a tag pushed without it builds nothing.
- **Pull requests** allow only squash merging, with the pull request title as the default
  commit message, and head branches are deleted after merging.
- **A branch ruleset** on `master` requires a pull request and the `verify` and `conventions`
  checks, requires linear history and blocks force pushes and deletion.