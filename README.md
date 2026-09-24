# Aspose CLI

Open-source, local-first automation for spreadsheets, PDFs, presentations and Word documents.
`aspose-cli` provides a CLI, a loopback browser workspace and a local MCP endpoint.
Document processing runs on your machine. Windows x64 is the supported platform.

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

The executable is authoritative for formats, operations, budgets and limitations:
`capabilities` lists every command, each product's operation vocabulary with its maximum
batch size, the resource budgets and every diagnostic code with its exit code. `schema`
prints the JSON Schemas and `docs` the bundled references.
Skills are `aspose-cli-cells`, `aspose-cli-pdf`, `aspose-cli-slides` and `aspose-cli-words`;
each includes references and reproducible examples. `skill install` supports the `codex`,
`claude-code` and `opencode` hosts at project or user scope.

## Working with documents

- `edit` applies one batch of operations atomically: the output is staged and validated,
  then published, or nothing changes. `--in-place` rewrites the input, `--backup` keeps a
  copy of it first, `--if-match` rejects a file that changed since it was read, and
  `--dry-run` applies the batch without writing. Only `--in-place` replaces the input:
  `edit`, `convert`, `render` and `pdf sign` reject an `--out` that resolves to the input file.
- Cells and Words `edit --verify` report semantic evidence (cell changes, formula errors,
  field, revision and protection state) before publication.
- `aspose-cli review <file>` writes a static evidence directory with an image of every sheet,
  slide or page and layout findings. It is the visual check for every product.
- `review`, `preview` and `fonts check` accept `--font-dir`, repeatable, to search local font
  directories in addition to the system fonts; relative paths resolve against `--workdir`.
- In JSON mode a successful command writes one result to stdout; a failure writes one error
  envelope to stderr. `--verbose` adds structured diagnostics.
- One input is admitted up to `--max-input-bytes` (default 1 GiB, at most 4 GiB); the other
  budgets are listed by `capabilities`.
- A document never causes a network request unless you opt in. External resources are read
  only from ordinary local files beneath the input directory, and each omitted resource is
  disclosed with `REMOTE_RESOURCES_BLOCKED`. The PDF HTML and Markdown importers and SVG images
  for PDF and Cells fetch before any policy can stop them (see [KNOWN-ISSUES.md](KNOWN-ISSUES.md)),
  so input that names a network address or contains script is refused with
  `FEATURE_UNSUPPORTED`. For trusted HTML, `pdf create --from-html --allow-network-resources`
  lets the importer fetch and lists every address in `NETWORK_RESOURCES_REQUESTED`; combine it
  with `--timeout`. `words convert page.html --to pdf` makes no request. Markdown for PDF may
  reference only ordinary files beneath its own directory.

## App, Preview and MCP

```powershell
aspose-cli app --welcome
aspose-cli preview document.pdf --open
aspose-cli mcp serve
```

App and Preview are loopback browser views for a human on `http://127.0.0.1:<port>`; they
enforce the exact Host `127.0.0.1:<port>`, same-origin, CSRF and current-user controls. The MCP
registration is named `aspose-cli`: its `capabilities` tool is read-only, and its `execute` tool
runs bounded product commands and the read-only host commands `doctor`, `schema`, `docs`,
`fonts list`, `fonts check`, `license status`, `skill list`, `preview status` and `app status`
with the same parser as the CLI. `review`, installation, update, license changes and service
lifecycle commands are not available through MCP.

## Commercial SDK licensing

```powershell
aspose-cli license install Aspose.Total.lic
aspose-cli license status --output json
```

`license status` reports each product in `products[]` with its effective source and mode.
A license is taken from `--license`, then `ASPOSE_<PRODUCT>_LICENSE_B64` or
`ASPOSE_<PRODUCT>_LICENSE_PATH`, then `ASPOSE_LICENSE_B64` or `ASPOSE_LICENSE_PATH`, then
`.aspose/licenses/<product>.lic` or `.aspose/license.lic` in the working directory, then the
licenses installed for the user.
Document operations reject an invalid configured license instead of falling back to
evaluation. Human-readable output prints a compact license notice on stderr; `--quiet`
suppresses it. The source license does not grant SDK rights or remove evaluation
restrictions. See [Aspose EULA](https://about.aspose.com/legal/eula/).

## Install a released build

The release archive holds the Authenticode-signed payload, its `SHA256SUMS` and detached
package signature, the CLI `LICENSE` and the original dependency notices under `notices/`.
Save the approved key ring,
`{"keys":[{"keyId":"<SHA-256 of the public key>","publicKeyPem":"<PEM>"}]}`, set
`ASPOSE_CLI_RELEASE_TRUSTED_KEYS` to the path of that file, extract the archive and run:

```powershell
& "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy AllSigned -File .\install.ps1
```

The installer verifies its own signature, the package signature and the payload hashes, then
installs per user into `%LOCALAPPDATA%\Aspose\CLI` in one transaction. By default it adds the
directory to the user PATH, installs the Skills for the Codex, Claude Code and OpenCode homes it
finds, asks for an optional license and registers the MCP server with each of those hosts whose
CLI is on PATH. Existing user-owned registrations are preserved, and a host that cannot be
registered only produces a warning.

| Switch | Effect |
| --- | --- |
| `-InstallDirectory <path>` | Install somewhere other than `%LOCALAPPDATA%\Aspose\CLI`. |
| `-SkipPath`, `-SkipSkills`, `-SkipMcp` | Leave PATH, Skills or MCP registration alone. |
| `-SkillsRoot <path>` | Install the Skills into this directory instead of the detected hosts. |
| `-LicensePath <file>`, `-LicenseProduct <id>` | Install a license, optionally for one product. |
| `-SkipLicensePrompt` | Do not ask for a license. |
| `-Update` | Replace an existing installation, replaying the choices it was made with. |
| `-Uninstall` [`-RemoveConfiguration`] | Remove the installation, its PATH entry, Skills and MCP registrations, and optionally the configuration. |
| `-PackageRoot <path>` | Install from an extracted package elsewhere. |
| `-DevelopmentPackage` | Accept an unsigned development package; no release trust is claimed. |

Configuration lives in `%APPDATA%\aspose-cli`, or in the absolute directory named by
`ASPOSE_CLI_CONFIG_DIR`. To uninstall:

```powershell
powershell -NoProfile -ExecutionPolicy AllSigned -File "$env:LOCALAPPDATA\Aspose\CLI\install.ps1" -Uninstall
```

## Update

`aspose-cli update check <feed>` and `aspose-cli update install <feed>` read a local
`RELEASE-MANIFEST.json` or an HTTPS manifest URL, verify it against the same key ring and
never run in the background. An update installs only a higher semantic version, or the
identical build again; a downgrade, or a different build with the same version, is refused.
`update install` hands off to the release's installer after the CLI exits; the outcome is
recorded in `%TEMP%\aspose-cli-<user-hash>\updates\status-<hash>.json` with its log beside it,
and a later `update` command reports a failed or unfinished run as `UPDATE_FAILED` or
`UPDATE_IN_PROGRESS`.

## Build from source

See [CONTRIBUTING.md](CONTRIBUTING.md) to build, test, package and release the CLI;
`.\scripts\install-local.ps1` installs a local unsigned development build.
See also [SECURITY.md](SECURITY.md), [KNOWN-ISSUES.md](KNOWN-ISSUES.md), [AGENTS.md](AGENTS.md)
and [LICENSE](LICENSE).
