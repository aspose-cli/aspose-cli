# Contributing to Aspose CLI

Every change keeps the architecture rules in [AGENTS.md](AGENTS.md). Report vulnerabilities
through [SECURITY.md](SECURITY.md), not public issues.

## Prerequisites

Windows x64 with:

- the .NET SDK selected by `global.json`;
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

## Tests

Tests use xUnit v3 with real engines and CLI child processes. `scripts/test.ps1` checks the
prerequisites and generated projections, builds once and runs the test projects side by side at
one of three scopes:

| Scope | Runs | Use |
| --- | --- | --- |
| `Fast` (default) | Every test without a category | While you work; a few minutes |
| `Affected` | `Fast`, plus every test of the projects your change reaches since the merge base with `-Base` (default `master`) | Before a commit |
| `Full` | Every test with a required license, then the [acceptance gates](#acceptance-gates) | Before a release |

A test that takes several seconds by nature carries `[Category(TestCategory.Slow)]`; the
installer and Playwright tests carry `Installer` and `Browser`. The run lists every test without a
category that took longer than 10 seconds: make it faster or mark it. `-NoBuild` reuses a build
and `-HangTimeout` (default `15m`) dumps a test that stays silent that long. Each project's log,
TRX and browser failure traces are written to `artifacts/TestResults/<run-id>/<project>/`. Never
weaken a check or remove a supported operation to make a test pass.

`Affected` runs a test project in full when the change touches it or a project it references,
adds the installer tests when `install.ps1` or `scripts/install-local.ps1` changes, adds nothing
for documentation, and runs everything for any other change, such as `eng/`, `scripts/` or
the shared build files.

Runs are isolated from the developer's machine: they never read `%APPDATA%\aspose-cli`, project
`.aspose` files or `ASPOSE_*` settings; only `ASPOSE_CLI_TEST_*` variables pass through. Run one
test run at a time per Windows account, because App tests share the per-user App endpoint.

A run is licensed only when `ASPOSE_CLI_TEST_LICENSE_PATH` names a license file. Without it,
the cases that need a license are skipped and listed; the `Full` scope requires the license and
fails when a licensed case is skipped.
Other skips name their reason: a missing platform, tool (`pwsh`, `openssl`, `dotnet`) or
symbolic-link privilege. A test marked `[Trait("ProductDefect", "<id>")]` asserts behavior a
confirmed defect still breaks; it fails on purpose and blocks the release until the defect is
fixed. Keep license contents out of logs and fixtures.

```powershell
$env:ASPOSE_CLI_TEST_LICENSE_PATH = 'C:\private\Aspose.Total.lic'
.\scripts\test.ps1 -Configuration Release -Scope Full
```

### Acceptance gates

The SDK acceptance gates under `tests/acceptance` are listed in `eng/acceptance-gates.json`.
`scripts/acceptance.ps1` runs them against the built CLI with licensed SDKs (set
`ASPOSE_LICENSE_PATH` or the variable each gate's README names; the `Full` test scope passes its
test license); `-Plan` only validates the list and the waivers. A failing gate blocks the release unless [KNOWN-ISSUES.md](KNOWN-ISSUES.md)
waives it for the version in `Directory.Build.props`.

## Code conventions

- **JSON input.** Test optional input defaults through the production source-generated
  serializer, including omitted fields and explicit `false`, `0` and `null`. Give new immutable
  input records their scalar defaults as optional constructor parameters, because the generator
  [does not preserve init-only property initializers](https://github.com/dotnet/runtime/issues/84484).
  The SDK's `OperationJsonConverter` owns discriminators, strict fields and duplicate rejection;
  the owning Product owns required fields and semantic validation.
- **Command parameters.** Every string argument and option declares its input role with
  `WithInput` (`InputKind.File`, `InputKind.JsonSource` or `InputKind.None`), and its value
  sources and secret handling on the symbol. The Host reads these declarations, never token
  order, option spelling or file existence; command-tree construction rejects missing or
  conflicting declarations. `GlobalOptionNames` is the one list of reserved global options, and
  analyzer `APCLI008` rejects a product option that reuses one.

## Releases

The version is declared once as `Version` in `Directory.Build.props`, and a release is built only
from the protected tag `v<Version>`. Give every new build a higher version: an update refuses a
different build with the same version.

`scripts/package.ps1 -Configuration Release -RuntimeIdentifier win-x64` needs a clean revision.
Without a mode switch it stages and signs in one run; `-StageOnly` stages the unsigned payload in
`artifacts/publish/win-x64`, `-SignStaged` signs a payload staged from the same revision
(Authenticode, checksums and package signature, an AllSigned install, update and uninstall smoke
test, the archive and its signed `RELEASE-MANIFEST.json`), and `-PrepareOnly` stages an unsigned
development package. The signed manifest binds the source revision and the locked SDK packages;
dependency notices follow the published graph ([notice sources](eng/notices/README.md)).

Signing reads:

| Variable | Value |
| --- | --- |
| `ASPOSE_CLI_RELEASE_SIGNING_KEY` | A passphrase-protected ECDSA P-256 PEM file or an OpenSSL store URI |
| `ASPOSE_CLI_RELEASE_SIGNING_KEY_PASSPHRASE` | Its passphrase |
| `ASPOSE_CLI_OPENSSL_PATH` | OpenSSL 3 |
| `ASPOSE_CLI_AUTHENTICODE_TOOL` | `signtool.exe` |
| `ASPOSE_CLI_AUTHENTICODE_CERTIFICATE_THUMBPRINT` | A certificate in `CurrentUser\My` or `LocalMachine\My`, trusted on the signing machine including Trusted Publishers |
| `ASPOSE_CLI_AUTHENTICODE_TIMESTAMP_SERVER` | An RFC 3161 URL |

The workflows in `.github/workflows` automate this: `ci.yml` runs the `Fast` scope in evaluation
mode (`verify`) and the `Full` scope (`licensed`, environment `licensed-tests`, secret
`ASPOSE_TEST_LICENSE_BASE64`); `release.yml` runs the `Full` scope and `-StageOnly` in `build` (environment `release-build`), then `-SignStaged` in `sign` (environment
`release`, runner `[self-hosted, Windows, X64, aspose-signing]`), the only job that sees the
signing variables and passphrase.
