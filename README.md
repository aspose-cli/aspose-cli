# Aspose CLI

Local-first automation of spreadsheets, PDFs, presentations and Word documents for people and
AI agents. `aspose-cli` provides a CLI, a loopback browser workspace and a local MCP endpoint;
documents are processed on your machine. Windows x64 is the supported platform.

The CLI source is Apache-2.0 and grants no rights to the commercial Aspose SDKs it runs on, which
have their own [license terms](https://about.aspose.com/legal/eula/); without a license they run
in evaluation mode, and the CLI discloses every evaluation effect on its output.

## Quick start

```powershell
aspose-cli capabilities --summary --output json
aspose-cli doctor --output json
aspose-cli skill list
aspose-cli docs cells/editing
aspose-cli skill install aspose-cli-platform --host codex --scope project
aspose-cli skill install aspose-cli-cells --host codex --scope project
```

The executable is the reference for what it supports: `capabilities` lists every command,
each product's operations and batch limits, the resource budgets and every diagnostic code with
its exit code; `--help` describes each command, `schema` prints the JSON Schemas and `docs` the
bundled guides. The Skill `aspose-cli-platform` teaches agents the rules every product shares,
and `aspose-cli-cells`, `aspose-cli-pdf`, `aspose-cli-slides` and `aspose-cli-words` teach each
product's workflows; `skill install` supports the `codex`, `claude-code` and `opencode` hosts at
project or user scope.

## What you can rely on

- **Atomic edits.** `edit` applies one batch of operations: the result is staged, validated and
  published, or nothing changes. `--in-place` replaces the input (`--backup` keeps a copy first),
  `--if-match` refuses a file that changed since it was read, and `--dry-run` writes nothing.
  No other command writes over one of its inputs.
- **Verification.** `aspose-cli review <file>` writes an evidence directory with an image of
  every sheet, slide or page and its layout findings; Cells, PDF and Words `edit --verify` report
  semantic evidence before publication.
- **A stable contract.** In JSON mode a command writes one result to stdout or one error to
  stderr, with a stable code and exit code.
- **Bounded work.** Inputs, rendering and extraction are held to the budgets that `capabilities`
  lists; `--timeout` sets a deadline for the whole command.
- **No network access.** A document never causes a network request unless you opt in, and each
  omitted resource is reported ([SECURITY.md](SECURITY.md#boundaries)).
- **Your fonts.** Commands whose output depends on fonts accept `--font-dir` to add local font
  directories to the system fonts.

## App, Preview and MCP

```powershell
aspose-cli app --welcome
aspose-cli preview document.pdf --open
aspose-cli mcp serve
```

App and Preview are browser views for a person, served only on `127.0.0.1`. The MCP server
`aspose-cli` offers the tools `capabilities` and `execute`. [SECURITY.md](SECURITY.md) describes
what each may do.

## Licensing

```powershell
aspose-cli license install Aspose.Total.lic
aspose-cli license status --output json
```

A license is taken from `--license`, then `ASPOSE_<PRODUCT>_LICENSE_B64` or
`ASPOSE_<PRODUCT>_LICENSE_PATH`, then `ASPOSE_LICENSE_B64` or `ASPOSE_LICENSE_PATH`, then
`.aspose/licenses/<product>.lic` or `.aspose/license.lic` in the working directory, then the
licenses installed for the user. An invalid configured license is an error, not a silent fall
back to evaluation. `--license-mode evaluation` runs one command in evaluation mode without
reading any of these sources, for checking what evaluation output looks like.

## Install

```powershell
powershell -ExecutionPolicy ByPass -c "irm https://github.com/aspose-cli/aspose-cli/releases/latest/download/install.ps1 | iex"
```

The installer downloads the latest [release](https://github.com/aspose-cli/aspose-cli/releases),
verifies it ([SECURITY.md](SECURITY.md#releases)), then installs per user into
`%LOCALAPPDATA%\Aspose\CLI` in one transaction. It adds the directory to the user PATH, installs the Agent Skills for the
Codex, Claude Code and OpenCode setups it finds, and offers to install a license. Agents that run
commands need nothing more: the Skills teach them the CLI. It changes no agent's MCP
configuration unless you ask with `-Mcp`, which also registers the `aspose-cli mcp serve` server
with those hosts (a host it cannot register produces only a warning). To install without
downloading, run `install.ps1` from an extracted release archive.

Switches apply to `install.ps1` run from an extracted archive, or to the downloaded script, as in
`& ([scriptblock]::Create((irm <install.ps1 URL>))) -Mcp`:

| Switch | Effect |
| --- | --- |
| `-InstallDirectory <path>` | Install somewhere other than `%LOCALAPPDATA%\Aspose\CLI`. |
| `-SkipPath`, `-SkipSkills` | Leave PATH or the Skills alone. |
| `-Mcp` | Also register the MCP server with the detected agent hosts. |
| `-SkillsRoot <path>` | Install the Skills into this directory instead of the detected hosts. |
| `-LicensePath <file>`, `-LicenseProduct <id>` | Install a license, optionally for one product. |
| `-SkipLicensePrompt` | Do not ask for a license. |
| `-Update` | Replace an installation, keeping the choices it was made with. |
| `-Uninstall` [`-RemoveConfiguration`] | Remove the installation, its PATH entry, Skills and MCP registrations, and optionally the configuration. |
| `-PackageRoot <path>` | Install from an extracted package elsewhere. |

Configuration lives in `%APPDATA%\aspose-cli`, or in the absolute directory named by
`ASPOSE_CLI_CONFIG_DIR`. To uninstall:

```powershell
powershell -NoProfile -ExecutionPolicy ByPass -File "$env:LOCALAPPDATA\Aspose\CLI\install.ps1" -Uninstall
```

`aspose-cli update check` and `aspose-cli update install` read the latest release, or the
`RELEASE-MANIFEST.json` path or HTTPS URL you name, and verify it the same way. An update runs
the release's installer after the CLI exits; a later `update` command reports a failed or
unfinished run.

## Development

[CONTRIBUTING.md](CONTRIBUTING.md) covers building, testing and releasing;
[AGENTS.md](AGENTS.md) holds the architecture rules; [SECURITY.md](SECURITY.md) explains how to
report a vulnerability.
