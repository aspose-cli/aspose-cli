# Troubleshooting

The error envelope is designed for self-correction: `error.code` is
stable, `error.details` carries the valid alternatives, `error.hint` the
most likely fix. Read the hint first; this page adds background.

## Input problems (exit 3)

- **FILE_NOT_FOUND** — relative paths resolve against `--workdir` (or the
  process working directory). Print the resolved path from
  `error.details.path` and list that directory.
- **FILE_LOCKED** — Excel holds files exclusively. Ask the user to close
  the file, then retry the identical command; nothing was written.
- **FILE_CORRUPT** — the content matches no supported spreadsheet
  signature. Check the real file type; renaming a `.docx` to `.xlsx` does
  not make it a workbook. Plain-text data must use a text extension
  (.csv, .tsv, .txt, .json) to be imported as text.
- **INPUT_CHANGED** — the file no longer matches the `--if-match` (or
  `ifMatch`) fingerprint, or changed while it was being read. Re-read it,
  review what changed, and retry with the new `source.fingerprint.sha256`.
- **PASSWORD_REQUIRED / PASSWORD_INVALID** — ask the user for the
  password (never guess); prefer `--password-env VAR` when retrying. The distinction is
  reliable: `_REQUIRED` means none was given, `_INVALID` means the given
  one failed.

## Validation problems (exit 4)

- **SHEET_NOT_FOUND** — `error.details.available` lists every sheet,
  exactly spelled. Sheet names match case-insensitively, as in Excel
  (`data` finds `Data`); results report the stored spelling.
- **RANGE_INVALID** — supported forms: `C5`, `B2:D10`, `Sales!A1:C10`,
  `'My Sheet'!A1:C10`. Whole-row/column specs (`A:A`, `1:3`) are rejected
  by design: give explicit bounds so output stays budgetable.
- **RANGE_TOO_LARGE** — you asked for more cells than `--max-cells`.
  Follow the hint's suggested first window and then the `next` commands,
  or raise `--max-cells` when you truly need everything.
- **OPS_INVALID** — an op failed validation; `error.details.index` is its
  zero-based position, `details.op` its name and `details.reason` the rule
  it broke. A failure found while applying an op keeps its own code (for
  example SHEET_NOT_FOUND) and carries the same `index` and `op` details.
  A JSON shape problem names the field path in `details.reason`, such as
  `unknown field 'style.shiny'` or `'at' must be a whole number`; `details.op`
  is absent when the entry names no known op.
  The batch was atomic: fix that one op and re-run the whole document.

## Output problems (exit 5)

- **OUTPUT_EXISTS** — deliberate safety default. `--overwrite` replaces;
  `--in-place` (on mutating commands) edits the input atomically. An `--out`
  that resolves to the input itself is OPTION_INVALID (exit 2), even with
  `--overwrite`: only `--in-place` replaces the input, with its `--backup`
  and `--if-match` safeguards.
- **OUTPUT_UNWRITABLE** — check directory existence and permissions;
  the CLI creates missing parent directories itself, so this usually
  means an OS-level denial.
- **Workbook open in Excel** — reads usually still work (Excel allows
  shared reads), but `--in-place` fails at the final atomic replace with
  OUTPUT_UNWRITABLE; when the open itself hits the sharing violation you
  get FILE_LOCKED (exit 3) instead. Recovery for both: have the user
  close the file and retry — your backup copy from the safe-editing
  protocol is untouched either way.

## License problems (exit 7)

- **LICENSE_FILE_NOT_FOUND / LICENSE_INVALID** — an explicitly configured
  license is broken; document operations never silently degrade to evaluation.
  Fix the path/file or remove the configuration. `license status` is diagnostic:
  it exits 0 with `products[].mode: "invalid"` and `problem` for a rejected
  source. Do not retry unchanged input in a loop.
- Resolution order: `--license` → product-specific environment variables →
  shared `ASPOSE_LICENSE_B64` / `ASPOSE_LICENSE_PATH` → product-specific and
  shared `.aspose` project files → user config directory. `aspose-cli license
  status` shows which source won.

## Evaluation-mode expectations

Without a license, read-only queries and inspection do not add watermarks;
input, resource and SDK limits still apply. Saved workbooks can gain an
"Evaluation Warning" worksheet; rendered and exported files can be watermarked.
That extra worksheet WILL show up in `inspect` output of files you created
in evaluation mode — it is not a bug, and you should not try to delete it.
Always tell the user their output is watermarked and that a license
removes SDK evaluation restrictions for newly generated output; it does not
clean marks already saved in an existing artifact.

## General moves

- `aspose-cli capabilities --output json` — every verb, format, op and schema
  id this build supports.
- `aspose-cli schema <id>` — the exact JSON Schema of any input or output.
- `--verbose` — emits structured JSONL diagnostics (including timings and
  error codes) on stderr; it does not promise raw stack traces.
- Deterministic output means a repeated command is diff-safe: when in
  doubt, run the read again and compare.

## EVALUATION_LIMIT: text export selected another worksheet

In evaluation mode, CSV, TSV, and Markdown export can write only the first
worksheet. Selecting another sheet with `--sheet` returns `EVALUATION_LIMIT`
(exit 7) before any output is written or replaced. The error details name the
requested sheet and the first sheet. Apply an Aspose.Cells license to export
the requested sheet, or explicitly select the first sheet if that is the data
you intend to export. Do not report a successful export of the requested sheet
when the evaluation SDK would substitute another sheet.