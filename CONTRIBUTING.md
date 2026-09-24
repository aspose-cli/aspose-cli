# Contributing to Aspose CLI

[AGENTS.md](AGENTS.md) states the architecture invariants any change must keep.
Report sensitive defects through [SECURITY.md](SECURITY.md).

## Prerequisites

Work from this project's root on Windows x64, the only supported platform, with:

- the .NET SDK selected by `global.json`;
- PowerShell 7.4 or later (`pwsh` on PATH) for the scripts, and Windows PowerShell 5.1,
  which runs the customer installer;
- network access to the Playwright CDN, from which `scripts/test.ps1` provisions the pinned
  Chromium for App browser tests.

## Development

1. Update the owning Product with its contracts, schemas, Skills and tests.
2. Run `scripts/sync.ps1` after catalog, identity or dependency changes. It regenerates the
   projections under `eng/generated` and the solution from `eng/products.json` and
   `eng/distribution.json`, and refreshes the lock files; builds and tests use locked restore.
3. Run `scripts/test.ps1 -Configuration Release` and inspect the results at
   `artifacts/TestResults/<run-id>/<project>/results.trx`.
4. Verify publication with `scripts/publish.ps1 -Configuration Release -RuntimeIdentifier win-x64`
   and, for installer changes, `scripts/install-local.ps1`, which builds and installs an
   unsigned development package (`-Update` and `-Uninstall` work as in `install.ps1`).

In `eng/products.json` the row order is the display order, and the optional `defaultProduct`
names the product whose docs topics also resolve without a prefix (`aspose-cli docs editing`)
and which the App uses until a document selects another.
`eng/distribution.json` is the one source of identity literals, the schema base URL and the
`ASPOSE_CLI_*` environment variable names; code reads them through the generated
`DistributionInfo`.

## Tests

Tests use xUnit v3; each test project is an executable run through `dotnet test`.
`scripts/test.ps1` checks the prerequisites and the generated projections, builds the solution
once and runs every test project. `-NoBuild` reuses a build, and `-HangTimeout` (default `15m`)
names a test that stays silent that long and writes a mini dump.

Use real engines and CLI child processes. Browser test failures keep screenshots and traces
beside the project's TRX. Do not weaken checks or remove a supported operation to obtain
passing tests. Test runs are isolated from the developer's machine: they never read
`%APPDATA%\aspose-cli`, project `.aspose` files or `ASPOSE_*` settings; only `ASPOSE_CLI_TEST_*`
variables pass through.

A test skips, with its reason listed after the run, when the machine lacks Windows, a tool on
PATH (`pwsh`, `openssl`, `dotnet`) or the privilege to create file symbolic links (Developer
Mode or elevation).

A test marked `[Trait("ProductDefect", "<id>")]` asserts the desired behavior that a confirmed
defect still breaks. It fails on purpose and is a release blocker until the defect is fixed;
never skip or weaken it.

### Licensed tests

A run is licensed only when `ASPOSE_CLI_TEST_LICENSE_PATH` names a license file; a path to a
missing file fails the run. Without it, `LicensedFact` cases, the in-process Cells engine suite,
the Slides budget cases and one Words case are skipped, and the run reports them.
`-RequireLicense` fails a run that would skip them:

```powershell
$env:ASPOSE_CLI_TEST_LICENSE_PATH = 'C:\private\Aspose.Total.lic'
.\scripts\test.ps1 -Configuration Release -RequireLicense
```

CLI child processes always run in evaluation mode unless a test passes `--license`
explicitly. Keep license contents out of logs and repository fixtures.

App tests share the per-user App control endpoint, so run one test run at a time per Windows
account.

## Acceptance gates

`eng/acceptance-gates.json` lists the SDK acceptance gates under `tests/acceptance`, currently
`PDF-MOVE-BOOKMARK` and `SLD-003`. `scripts/acceptance.ps1` runs them against the built CLI and
SDK assemblies with licensed SDKs (set `ASPOSE_LICENSE_PATH` or the product license variable
each gate's README names); `-Plan` only validates the gate list and waivers. A failing gate
blocks the release unless [KNOWN-ISSUES.md](KNOWN-ISSUES.md) waives it for the version in
`Directory.Build.props` under "Release gate waivers".

## Code conventions

### JSON input defaults

Verify optional input defaults through the production source-generated serializer, including
omitted fields and explicit `false`, `0` and `null`. CLR construction alone does not test the
wire contract. For new immutable input records, use optional constructor parameters for scalar
defaults: the current .NET generator
[does not preserve init-only property initializers](https://github.com/dotnet/runtime/issues/84484).
Keep required-field and semantic validation in the owning product. The SDK's
`OperationJsonConverter` owns discriminator ordering, strict fields and duplicate rejection;
product converters supply only the operation registry and wire defaults.

### Command parameter semantics

Every string argument and string option, including arrays, must declare its input role at
construction with `WithInput`: `InputKind.File` for document files, `InputKind.JsonSource` for
file/inline/stdin JSON, or `InputKind.None` for ordinary values, output paths, directories and
separately owned configuration or credentials. Numeric and boolean values default to `None`.
Shared option factories own their declarations.

Value sources and secret handling are also declared on the symbol. The Host consumes the actual
parser result and these declarations; it must not infer roles from token order, option spelling
or the existence of a same-named file. Command-tree construction rejects missing string
declarations, conflicting declarations and invalid type/source combinations.
`GlobalOptionNames` is the one list of host-reserved global options; analyzer `APCLI008`
rejects a product option that reuses one.

## Releases

The CLI version is declared once as `Version` in `Directory.Build.props`. A release is built
only from the protected tag `v<Version>`; publish every new build under a higher version,
because an update refuses a different build with the same version.

`scripts/package.ps1 -Configuration Release -RuntimeIdentifier win-x64` requires a clean source
revision. It has three modes, besides staging and signing in one run:

- `-StageOnly` publishes and stages the unsigned payload in `artifacts/publish/win-x64` without
  reading signing input;
- `-SignStaged` signs a payload staged from the same revision: Authenticode, package checksums
  and signature, an AllSigned install, update and uninstall smoke test, the archive and its
  signed `RELEASE-MANIFEST.json`;
- `-PrepareOnly` stages an unsigned local development package.

Signing reads `ASPOSE_CLI_RELEASE_SIGNING_KEY` (a passphrase-protected ECDSA P-256 PEM file or
an OpenSSL store URI) with `ASPOSE_CLI_RELEASE_SIGNING_KEY_PASSPHRASE`,
`ASPOSE_CLI_OPENSSL_PATH` (OpenSSL 3), `ASPOSE_CLI_AUTHENTICODE_TOOL` (signtool.exe),
`ASPOSE_CLI_AUTHENTICODE_CERTIFICATE_THUMBPRINT` (a certificate in `CurrentUser\My` or
`LocalMachine\My`, trusted on the signing machine including Trusted Publishers) and
`ASPOSE_CLI_AUTHENTICODE_TIMESTAMP_SERVER` (an RFC 3161 URL). The signed manifest binds the
source revision and the locked SDK package identities and hashes. Notice collection follows
the published dependency graph, including the self-contained .NET runtime; see
[notice sources](eng/notices/README.md).

CI (`.github/workflows/ci.yml`) runs the `verify` job in evaluation mode and the `licensed` job
in the `licensed-tests` environment with the `ASPOSE_TEST_LICENSE_BASE64` secret and
`-RequireLicense`. The release workflow (`.github/workflows/release.yml`) runs the `build` job in the
`release-build` environment (licensed tests, acceptance gates, `-StageOnly`) and then the `sign`
job (`-SignStaged`) in the `release` environment on a
`[self-hosted, Windows, X64, aspose-signing]` runner; only `sign` sees the signing variables and
the passphrase secret.
