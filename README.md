# Aspose CLI

Local-first automation of spreadsheets, PDFs, presentations and Word documents for people and
AI agents. `aspose-cli` provides a CLI; documents are processed on your machine. Windows x64 is the supported platform.

The CLI source is MIT-licensed and grants no rights to the commercial Aspose SDKs it runs on, which
have their own [license terms](https://about.aspose.com/legal/eula/); without a license they run
in evaluation mode, and the CLI discloses every evaluation effect on its output.

## Quick start

```powershell
aspose-cli schema
aspose-cli skill list
aspose-cli skill install aspose-cli-platform --host codex
aspose-cli skill install aspose-cli-cells --host codex
```

The executable is the reference for what it supports: `schema` lists every product, command and
format and the id of every document that describes them, such as a command's options, a result's
JSON Schema, an object table, the resource budgets and every diagnostic code with its exit code;
`--help` describes each command. The Skill `aspose-cli-platform` teaches agents the rules every product shares,
and `aspose-cli-cells`, `aspose-cli-pdf`, `aspose-cli-slides` and `aspose-cli-words` teach each
product's workflows; `skill install` supports the `codex`, `claude-code` and `opencode` hosts, in the
project by default or for the user with `--user`.

## What you can rely on

- **Atomic edits.** `edit` applies one batch of operations: the result is staged and published,
  or nothing changes. `--in-place` replaces the input. No other command writes over one of its
  inputs.
- **Read-back.** An edit reports each operation as it reads it from the saved file, opened again.
- **A stable contract.** A command writes one JSON result to stdout or one JSON error to stderr,
  with a stable code and exit code; [Compatibility](#compatibility) says what stays stable.
- **Bounded work.** Inputs, rendering and extraction are held to the budgets that `schema`
  lists.
- **No network access.** A document never causes a network request, and each omitted resource
  is reported ([SECURITY.md](SECURITY.md#boundaries)).
- **Your fonts.** Commands whose output depends on fonts accept `--font-dir` to add local font
  directories to the system fonts.

## Compatibility

From 1.0 the machine contract changes only by adding, except in a major version. The contract
is what a script or agent depends on: commands, options and positional arguments with their
defaults and accepted values; edit operation names and fields; result field names, types and
units; error codes, warning codes and exit codes; schema ids; the environment variables listed
below; the configuration and installation directories; and the Skill names.

A breaking change removes or renames any of these, changes its type or meaning, narrows the
input it accepts, or changes a default or an exit code; it happens only in a major version.
A minor or patch release may add commands, options, operations, optional input fields, result
fields, warning codes, error codes and enumeration values. Text is not part of the contract:
messages, help, Skill prose and the diagnostic output of `--verbose` can
change in any release.

A consumer must ignore result fields, warning codes and error codes it does not know. The result
schemas describe the current build; validate against them ignoring unknown properties.
`schemaVersion` in a result is the contract version: it increases only with a breaking change,
with the major version, never for an addition. It started at 2 and is independent of the CLI
version, so 1.0 publishes `v2` schema ids. The `schema` field names a schema by id; the id is not
a URL to fetch, and `aspose-cli schema <id>` prints that schema offline.

The environment variables in the contract are the license variables `ASPOSE_LICENSE_B64`,
`ASPOSE_LICENSE_PATH` and their per-product forms (the platform Skill's licensing reference), named without the
CLI prefix on purpose so that other Aspose tools can share them; `ASPOSE_CLI_CONFIG_DIR`; and the
variables that a command's `schema` document lists as the source of an option's value (now
`ASPOSE_CLI_MAX_INPUT_BYTES`, for `--max-input-bytes`). Any other `ASPOSE_CLI_*` variable is
internal.

Evaluation mode changes output as the Aspose SDKs decide; the CLI reports each effect with an
`EVALUATION_*` warning, but the evaluation output itself is not covered. 1.0 has no experimental
features: everything it exposes is covered.

## Licensing

```powershell
aspose-cli license install Aspose.Total.lic
aspose-cli license status --output-format json
```

A license comes from `--license`, an environment variable, a `.aspose` directory in the working
directory or the licenses installed for the user; the platform Skill's licensing reference gives the order. An
invalid configured license is an error, not a silent fall back to evaluation.
`--license-mode evaluation` runs one command in evaluation mode without reading any of these
sources, for checking what evaluation output looks like.

## Install

```powershell
powershell -ExecutionPolicy ByPass -c "irm https://github.com/aspose-cli/aspose-cli/releases/latest/download/install.ps1 | iex"
```

The installer downloads the latest [release](https://github.com/aspose-cli/aspose-cli/releases),
verifies it ([SECURITY.md](SECURITY.md#releases)), then installs per user into
`%LOCALAPPDATA%\Aspose\CLI` in one transaction. It adds the directory to the user PATH, installs
the Agent Skills for the Codex, Claude Code and OpenCode setups it finds, and offers to install a
license. Agents that run
commands need nothing more: the Skills teach them the CLI. To install without
downloading, run `install.ps1` from an extracted release archive.

Slides rendering needs the [Microsoft Visual C++ Redistributable (x64)](https://aka.ms/vs/17/release/vc_redist.x64.exe),
which the package does not carry. Without it, Slides commands that need its native drawing library
fail.

Switches apply to `install.ps1` run from an extracted archive, or to the downloaded script, as in
`& ([scriptblock]::Create((irm <install.ps1 URL>))) -SkipPath`:

| Switch | Effect |
| --- | --- |
| `-InstallDirectory <path>` | Install somewhere other than `%LOCALAPPDATA%\Aspose\CLI`. An install directory it creates is open only to you, SYSTEM and the administrators; parent folders it creates can be read by other users but not changed. An existing directory keeps its access rules, with a warning if other accounts can write to the install directory, since they could replace the executable. |
| `-SkipPath`, `-SkipSkills` | Leave PATH or the Skills alone. |
| `-SkillsRoot <path>` | Install the Skills into this directory instead of the detected hosts. |
| `-LicensePath <file>`, `-LicenseProduct <id>` | Install a license, optionally for one product. |
| `-SkipLicensePrompt` | Do not ask for a license. |
| `-Update` | Replace an installation, keeping the choices it was made with. |
| `-Uninstall` [`-RemoveConfiguration`] | Remove the installation, its PATH entry and Skills, and optionally the configuration. |
| `-PackageRoot <path>` | Install from an extracted package elsewhere. |

Configuration lives in `%APPDATA%\aspose-cli`, or in the absolute directory named by
`ASPOSE_CLI_CONFIG_DIR`. To uninstall:

```powershell
powershell -NoProfile -ExecutionPolicy ByPass -File "$env:LOCALAPPDATA\Aspose\CLI\install.ps1" -Uninstall
```

## Development

[CONTRIBUTING.md](CONTRIBUTING.md) covers building, testing and pull requests;
[AGENTS.md](AGENTS.md) holds the architecture rules; [SECURITY.md](SECURITY.md) explains how to
report a vulnerability.
