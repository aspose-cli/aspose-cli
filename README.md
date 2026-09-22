# Aspose CLI

Open-source, local-first automation for spreadsheets, PDFs, presentations and Word documents.
`aspose-cli` provides a CLI, a loopback browser workspace and a local MCP endpoint.
Document processing runs on your machine.

The CLI source is Apache-2.0. Commercial Aspose SDK dependencies have their own licensing terms.
Licensed and SDK evaluation behavior are supported; evaluation output is disclosed.

## Capabilities and Skills

```powershell
aspose-cli capabilities --output json
aspose-cli doctor --output json
aspose-cli skill list
aspose-cli docs cells/editing
aspose-cli skill install aspose-cli-cells --host codex --scope project
```

The executable is authoritative for formats, operations, budgets and limitations.
Skills are `aspose-cli-cells`, `aspose-cli-pdf`, `aspose-cli-slides` and `aspose-cli-words`.
Each includes references, reproducible examples and, for Slides and Words, a default
design template that Markdown content is authored into.

## Build from source

Use .NET SDK 10 selected by global.json and PowerShell 7 for development scripts.
The verified self-contained customer target is win-x64.

```powershell
.\scripts\sync.ps1
.\scripts\test.ps1 -Configuration Release
.\scripts\publish.ps1 -Configuration Release
.\scripts\publish.ps1 -Configuration Release -RuntimeIdentifier win-x64
```

`sync.ps1` regenerates projections and the solution and refreshes lock files; tests and
publishes use locked restore. Build outputs stay under `artifacts/` and project-local
`bin`/`obj`. See [CONTRIBUTING.md](CONTRIBUTING.md) to work on the project.

## Install a released build

Verify the release signature and archive checksum with an approved key ring.
Extract the archive, set `ASPOSE_CLI_RELEASE_TRUSTED_KEYS` to the approved key-ring JSON, and run:

```powershell
& "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy AllSigned -File .\install.ps1
```

The installer verifies itself, the package signatures and the payload hashes, then installs
per user into `%LOCALAPPDATA%\Aspose\CLI`. Configuration is `%APPDATA%\aspose-cli` or the
absolute directory selected by `ASPOSE_CLI_CONFIG_DIR`. Optional MCP registration runs only after
the installation transaction is finalized.

For a local development build, `.\scripts\install-local.ps1` builds and transactionally installs
an explicitly unsigned package. It does not claim customer release trust.

## Working with documents

- Commands that change a document apply one atomic batch of operations and publish the result
  safely: the output is staged, reopened and only then replaces its target, with an optional
  backup. `--if-match` rejects a file that changed since it was read.
- Cells and Words `edit --verify` report semantic evidence (cell changes, formula errors,
  field, revision and protection state) before publication.
- `aspose-cli review <file>` writes a static evidence directory with an image of every sheet,
  slide or page and layout findings. It is the visual check for every product.
- In JSON mode a successful command writes one result to stdout; a failure writes one error
  envelope to stderr. `--verbose` adds structured diagnostics.
- Cells and Words read external resources only from ordinary local files beneath the input
  directory. The pinned PDF HTML importer can fetch linked resources itself, so create PDFs
  from trusted HTML only.

## App, Preview and MCP

```powershell
aspose-cli app --welcome
aspose-cli preview document.pdf --open
aspose-cli mcp serve
```

App and Preview are loopback browser views for a human; they enforce exact Host, same-origin,
CSRF and current-user controls. The MCP registration is named `aspose-cli`: its `capabilities`
tool is read-only, and its `execute` tool runs bounded, allowlisted product commands with the
same parser as the CLI. Installation, update, licensing and service lifecycle commands are not
available through MCP.

## Commercial SDK licensing

```powershell
aspose-cli license install Aspose.Total.lic
aspose-cli license status --output json
```

`license status` reports each product in `products[]` with its effective source and a
`licensed`, `evaluation`, `invalid` or `not-applicable` mode. Document operations reject an
invalid configured license instead of falling back to evaluation. Human-readable output prints
a compact license notice on stderr; `--quiet` suppresses it. The source license does not grant
SDK rights or remove evaluation restrictions. See [Aspose EULA](https://about.aspose.com/legal/eula/).

## Release tooling

`scripts/package.ps1 -Configuration Release -RuntimeIdentifier win-x64` requires a clean source
revision, OpenSSL, an ECDSA P-256 signing key and a valid Authenticode tool and certificate,
supplied through `ASPOSE_CLI_RELEASE_SIGNING_KEY`, `ASPOSE_CLI_OPENSSL_PATH`,
`ASPOSE_CLI_AUTHENTICODE_TOOL` and `ASPOSE_CLI_AUTHENTICODE_CERTIFICATE`. The signed manifest
binds the source revision and the locked SDK package identities and hashes.

See [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md), [AGENTS.md](AGENTS.md) and [LICENSE](LICENSE).
