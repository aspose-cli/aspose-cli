# Troubleshooting

Errors are built for self-correction. Read `error.hint` first, then
`error.details`; do not retry unchanged input in a loop. Product error codes
are in the product Skill.

## The error envelope

A failed command writes one JSON envelope to stderr and exits non-zero:

```json
{
  "schema": "https://schemas.aspose.com/aspose-cli/v2/common/error.schema.json",
  "schemaVersion": 2,
  "error": {
    "code": "SHEET_NOT_FOUND",
    "message": "No sheet 'Nope' was found.",
    "details": { "subject": "sheet", "requested": "Nope", "availableCount": 2, "available": ["Data", "Summary"] },
    "hint": "Use one of the names in details.available."
  }
}
```

- `code` is stable; branch on it, never on `message`.
- `details` carries structured context: paths, the valid alternatives, the
  failing operation's `index` and `op`.
- `hint` is the most likely next action; `docs`, when present, names a topic
  for `aspose-cli docs <topic>`.

Warnings do not fail a command. They appear in the result's `warnings` array
with `code`, `message` and `hint`, and on stderr in table output.

## Exit codes

| Exit | Category | Typical codes |
|------|----------|---------------|
| 0 | Success | |
| 1 | Internal defect | `INTERNAL_ERROR` |
| 2 | Usage | `USAGE_ERROR`, `OPTION_INVALID` |
| 3 | Input file | `FILE_NOT_FOUND`, `FILE_LOCKED`, `FILE_CORRUPT`, `PASSWORD_REQUIRED`, `INPUT_CHANGED` |
| 4 | Validation | `OPS_INVALID`, `*_NOT_FOUND`, `PAGE_RANGE_INVALID`, `RENDER_TOO_LARGE` |
| 5 | Output | `OUTPUT_EXISTS`, `OUTPUT_UNWRITABLE`, `OUTPUT_CONFLICT` |
| 6 | Format | `FORMAT_UNSUPPORTED`, `FORMAT_MISMATCH`, `FEATURE_UNSUPPORTED` |
| 7 | License | `LICENSE_FILE_NOT_FOUND`, `LICENSE_INVALID`, `EVALUATION_LIMIT` |
| 8 | Partial | a `--best-effort` failure, a failed review, `OUTPUT_PUBLICATION_PARTIAL` |
| 9 | Timeout | `OPERATION_TIMEOUT` |

`capabilities --output json` lists every error and warning code of this build
with its owner, severity and exit code under `diagnostics`.

## Diagnosis

```powershell
aspose-cli doctor --output json
aspose-cli license status --output json
aspose-cli capabilities --output json
```

- `--verbose` writes structured JSONL diagnostics (timings, error codes) to
  stderr and leaves stdout untouched.
- `aspose-cli schema <id>` prints the exact schema of any input or result.
- `aspose-cli fonts check <file>` explains most rendering and layout
  surprises (`aspose-cli docs verification`).

## Shared codes

**Input (exit 3)**

- `FILE_NOT_FOUND`: relative paths resolve against `--workdir`, or the current
  directory; `details.path` is the resolved path.
- `FILE_LOCKED`: another application holds the file exclusively. Ask the user
  to close it and retry the same command; nothing was written.
- `FILE_ACCESS_DENIED`: the process may not read the file.
- `FILE_CORRUPT`: the content is not a readable document of that type, nor one
  another product reads. Renaming a file does not change its format. A legacy
  binary file (`.doc`, `.xls`, `.ppt`) and an encrypted Office Open XML file
  share one container signature, so a legacy file renamed to `.docx`, `.xlsx`
  or `.pptx` reaches that extension's product and fails there, with
  `FILE_CORRUPT` or with `FORMAT_MISMATCH` naming the products that signature
  could belong to.
- `FILE_TOO_LARGE`, `INPUT_BUDGET_EXCEEDED`: the input exceeds a resource
  budget; `--max-input-bytes` raises the size limit up to its hard maximum.
- `INPUT_CHANGED`: the file no longer matches `--if-match` or `ifMatch`, or
  changed while it was read. Read it again, review what changed, and retry with
  the new `source.fingerprint.sha256`.
- `PASSWORD_REQUIRED`: no password was given. `PASSWORD_INVALID`: the given one
  failed. Ask the user; never guess. Pass it with `--password-env <VAR>` or
  `--password-stdin`.

**Usage and validation (exit 2 and 4)**

- `OPTION_INVALID`: an option value is invalid here; `details.option` names
  it. An `--out` that resolves to the input is refused: use `--in-place`.
- `OPS_INVALID`: `details.index`, `details.op` and `details.reason` locate the
  failing operation (`aspose-cli docs editing`).
- `*_NOT_FOUND`: `details.available` and `details.suggestions` list what
  exists (`aspose-cli docs editing`). `PAGE_NOT_FOUND` reports
  `details.availableCount`.
- `PAGE_RANGE_INVALID`: use 1-based ranges such as `1-3,7,9-`.
- `RENDER_TOO_LARGE`: the image would have too many pixels; lower `--dpi` or
  render less.
- `RENDER_FAILED`: the engine failed while rasterizing; the input can still be
  valid. Reduce the selection or check fonts.

**Output (exit 5)**

- `OUTPUT_EXISTS`: pass `--overwrite` deliberately or choose another path.
  Commands that write several files refuse the whole run when one exists.
  Review evidence directories are never overwritten.
- `OUTPUT_UNWRITABLE`: the operating system refused the write. A file open in
  another application usually reads fine but fails the final in-place replace;
  ask the user to close it. The input and any backup are untouched.
- `OUTPUT_CONFLICT`: the output changed while the command ran and was not
  overwritten.

**Format (exit 6)**

- `FORMAT_UNSUPPORTED`: `capabilities` lists the formats each product loads,
  converts and renders.
- `FORMAT_MISMATCH`: the extension disagrees with the content, such as a Word
  document renamed to `.pdf`; check the real file type. `details.path` names the
  file and `details.detected` the product the content looks like, whose command
  a product command's hint names: use it, rename the file, or pass
  `--product <id>` to `review` or `preview`, which then lets that engine read it.
  A damaged file of a format the command reads, such as a PDF given to
  `words convert`, stays `FILE_CORRUPT`.
- `FORMAT_AMBIGUOUS`: the input can be read more than one way. Several products
  recognize it (pass `--product`), or a text input's numbers depend on its
  culture (the hint names the option that reads it).
- `FEATURE_UNSUPPORTED`: this build cannot perform the request on this input;
  the message names what.

**Timeout (exit 9)**

- `OPERATION_TIMEOUT`: `--timeout <seconds>` expired. Supervised work was
  stopped and staged outputs were recovered before the exit.

## Shared warnings

- `EVAL_MODE`, `EVAL_INPUT_TRUNCATED`: evaluation mode; disclose it
  (`aspose-cli docs licensing`).
- `BACKUP_PREDATES_EDIT`: an in-place edit kept an existing backup that holds an
  earlier version than the file it replaced; the replaced version has no backup.
- `LIST_TRUNCATED`: a list in the result was capped; its hint names the command
  that reads the rest.
- `LOSSY_CONVERSION`: the target format cannot hold everything the source
  had; tell the user what was lost.
- `REMOTE_RESOURCES_BLOCKED`: an external resource was left out of the output;
  review the incomplete result.
- `SIGNATURE_INVALIDATED`: the change invalidated a digital signature; re-sign
  the reviewed output.

## Windows PowerShell

How PowerShell hands an `--ops` document to `aspose-cli` depends on its version
and on `$PSNativeCommandArgumentPassing`:

| shell | inline `'{"ops":...}'` | inline with `\"` | piped to `--ops -` | ops file |
|-------|------------------------|------------------|--------------------|----------|
| Windows PowerShell 5.1 | quotes stripped, `OPS_INVALID` | `USAGE_ERROR` or `OPS_INVALID` | non-ASCII text becomes `?`, no error | works |
| PowerShell 7.3+, `Windows` (default) or `Standard` | works | `OPS_INVALID` | works | works |
| PowerShell 7, `Legacy` | quotes stripped, `OPS_INVALID` | works | works | works |

- Write the ops document to a file and pass its path: that works in every
  shell, in UTF-8 with or without a byte order mark and in the UTF-16 that
  `Out-File` and `>` write in Windows PowerShell 5.1. Never escape quotes as
  `\"`; whether that helps depends on the shell.
- Windows PowerShell 5.1 pipes text to a native command as ASCII
  (`$OutputEncoding`), so `--ops -` silently turns Chinese and other non-ASCII
  text into `?`.
- Windows PowerShell 5.1's `>` redirection re-encodes stdout as UTF-16; parse
  stdout directly or use PowerShell 7.
