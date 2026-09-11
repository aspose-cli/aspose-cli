# Aspose CLI

Open-source, local-first automation for spreadsheets, PDFs, presentations and Word documents.
`aspose-cli` provides a CLI, a loopback browser workspace and a local MCP endpoint.
Document processing runs on your machine. Cells and Words external resources are limited
to verified ordinary local files beneath the input directory, with shared resource budgets.
Omitted resources are reported; MHTML resource completeness requires visual confirmation.
The pinned PDF HTML importer can fetch linked resources outside its callback, so PDF HTML
creation does not yet provide the same isolation guarantee.

The CLI source is Apache-2.0. Commercial Aspose SDK dependencies have their own licensing terms. Licensed and SDK evaluation behavior are supported; evaluation output is disclosed.

This project owns its Host, SDK, analyzers, products, tests and publishing tools.
It has no source, project or package dependency on another CLI project.

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
Each includes executable-specific references and reproducible examples.

## Build from source

Use .NET SDK 10 selected by global.json and PowerShell 7 for development scripts.
The verified self-contained customer target is win-x64.

```powershell
.\scripts\sync.ps1
.\scripts\test.ps1 -Configuration Release
.\scripts\publish.ps1 -Configuration Release
.\scripts\publish.ps1 -Configuration Release -RuntimeIdentifier win-x64
```

eng/products.json is the only product roster. eng/distribution.json owns the fixed application identity.
sync.ps1 regenerates projections and the solution and refreshes lock files. Normal tests and publishes use locked restore.
All source and build inputs must remain inside this project; it also builds from a standalone checkout.
Outputs are ignored under artifacts/ and project-local bin/obj directories.
Each test run writes per-project TRX results to `artifacts/TestResults/<run-id>/<project>/results.trx`;
CI retains these results even when tests fail.

## Install a released build

Verify the release signature and archive checksum with an approved key ring.
Extract the archive, set `ASPOSE_CLI_RELEASE_TRUSTED_KEYS` to the approved key-ring JSON, and run:

```powershell
& "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy AllSigned -File .\install.ps1
```

Customer installation verifies the installer, package signatures and payload hashes.
The default per-user installation is `%LOCALAPPDATA%\Aspose\CLI`.
Configuration is `%APPDATA%\aspose-cli` or the absolute directory selected by `ASPOSE_CLI_CONFIG_DIR`.
The other CLI has different commands, configuration, Skills and MCP registrations.
Managed targets belonging to another application are rejected.

## Local development installation

```powershell
.\scripts\install-local.ps1
```

This builds and transactionally installs an explicitly unsigned development package.
For isolated tests, use package.ps1 -PrepareOnly and invoke install.ps1 with -DevelopmentPackage and a temporary -InstallDirectory.
The development path does not claim customer release trust. User modifications and unrelated files are protected.

## App, Preview and MCP

```powershell
aspose-cli app --welcome
aspose-cli preview document.pdf --open
aspose-cli mcp serve
```

App and Preview use loopback URLs reusable during their lifetime. Host, same-origin, CSRF,
current-user lifecycle controls and bounded uploads remain enforced.
The MCP registration is named `aspose-cli` and points to the matching installed executable.
Its `capabilities` tool is read-only. Its `execute` tool runs bounded, allowlisted
product commands, including document writes; host installation, update, licensing and
service lifecycle mutations are unavailable through it. Command parameters expose
`inputKind`, `valueSource` and `secret` from their declarations. MCP uses the same
command parser as the CLI and retains the server work directory, license source and
input budget; an execute request cannot raise the server input limit.
Two CLI processes still coordinate document publication through neutral operating-system locks.

## File and output contracts

JSON commands return one result on stdout and diagnostics or errors on stderr.
Mutations use safe publication, backups and fingerprints where advertised; extraction is bounded.
Schema URIs identify this application and are available offline through its schema command.
No deprecated command aliases or legacy installer/Skill readers are provided.

## Commercial SDK licensing

Use aspose-cli license status, license install and license remove for commercial SDK licensing.
Existing documented SDK license environment variables retain their meaning.
The source license does not grant SDK rights or remove evaluation restrictions.
See [Aspose EULA](https://about.aspose.com/legal/eula/).

## Release tooling

scripts/package.ps1 -Configuration Release -RuntimeIdentifier win-x64 requires a clean source revision,
OpenSSL, an ECDSA P-256 signing key and a valid Authenticode tool/certificate.
Use `ASPOSE_CLI_RELEASE_SIGNING_KEY`, `ASPOSE_CLI_OPENSSL_PATH`,
`ASPOSE_CLI_AUTHENTICODE_TOOL` and `ASPOSE_CLI_AUTHENTICODE_CERTIFICATE` or their script parameters.
No production signing credentials are generated automatically.
The signed manifest binds the actual SDK package or upstream source provenance.

The independent GitHub workflows run from this directory once it becomes a repository root.
The release workflow requires a configured signing runner and produces artifacts without automatically creating a GitHub release.

See [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md), [AGENTS.md](AGENTS.md) and [LICENSE](LICENSE).

App preferences commit atomically before changing the active preview. If the saved view
cannot be rendered, the App keeps the previous preview and reports that settings were
saved but refresh failed. Saving the same settings retries the refresh.
