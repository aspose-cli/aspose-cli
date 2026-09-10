---
name: aspose-cli-cells
description: High-fidelity Excel/spreadsheet processing via the local Aspose CLI without Python or Microsoft Office; use for real workbooks, formula recalculation, native charts and pivots, format-preserving edits, protected files, exact-layout conversion, and file fidelity.
license: Apache-2.0
---

# Aspose Cells CLI

Process spreadsheets with engine-grade fidelity using the local `aspose-cli`
CLI, with no Python or Office dependency. Every command supports
`--output json` and returns a versioned envelope with a `schema` field —
pass it explicitly whenever you parse the result (the default format
depends on whether stdout is a terminal).

## 1. Session start

Run `aspose-cli --version`. If the CLI is missing, ask the user how they want
to install it; contributors can build it from this repository.
Then check the environment once per session:

```
aspose-cli doctor --output json          # license, runtime, output writability
aspose-cli license status --output json  # license source + mode only
```

`aspose-cli doctor` returns a `checks` array (each `ok`/`warn`/`fail`) and a
top-level `ok`; a `license` check of `warn` means evaluation mode —
produced files carry an Aspose evaluation watermark (section 9).

Routing: if the task involves an EXISTING workbook the user cares about,
follow the safe-editing protocol in section 5 before the first mutation.

HTML resource limitation: loading an HTML workbook can request linked resources,
including network URLs, even for inspection. Use trusted HTML inputs; the
current Cells loader does not enforce fully offline resource loading.

## 2. Golden rules

1. NEVER dump a whole sheet. Climb the projection ladder (section 4).
2. Batch all edits into ONE ops document; never run one command per cell.
3. Verify in tiers after ANY write:
   - `read` the changed ranges back — numbers you report come from the
     engine, never from your own arithmetic;
   - if the change touched anything visual (column widths, styles, charts,
     merges, number formats, conditional formats, print setup) OR you are
     delivering a workbook a human will open: `render --all-sheets`, LOOK
     at every PNG with your image-reading tool, self-grade against the
     design checklist (`aspose-cli docs design-system`), then fix and
     re-render until it passes;
   - before delivery, `aspose-cli cells inspect book.xlsx --detail errors` must
     report zero formula errors.
4. Before the FIRST in-place edit of a file you did not create this
   session, make the one-time backup (section 5).
5. Results are deterministic; produced FILES are not all byte-reproducible.
   stdout JSON, text projections (csv, tsv, json, md) and renders (png,
   jpeg, svg) come back identical run to run. Every document container —
   xlsx, xlsm, xlsb, xls, ods, html, mhtml, pdf, xps — embeds run-varying
   bytes (a PDF carries both a random document id and a wall-clock
   timestamp), so two identical `convert --to pdf` runs differ by SHA256.
   Never hash one to detect change: use `cells compare`, or compare the source
   workbook.
6. On any error, read `error.hint` first — it states the most likely fix,
   and `error.details` usually lists the valid alternatives.
7. In evaluation mode, tell the user about the watermark (section 9).
8. Writes to the same file must be serialized; parallel reads are safe.
9. Unsure about a verb, op or field? Ask the CLI (`--help`, `aspose-cli
   capabilities cells edit`, `aspose-cli schema v2/cells/ops`, `aspose-cli docs`) instead
   of guessing from memory.
10. stdout carries exactly one result; diagnostics and warnings go to
    stderr. Parse stdout only.
11. An open-ended or vague request ("make me a sales sheet", "show me what
    you can do") gets the FULL deliverable — designed synthetic data, a
    Detail sheet AND a designed Dashboard sheet — never a minimal grid
    (`aspose-cli docs design-system`, Synthetic data quality and Dashboard
    layout).

## 3. What you do NOT need to defend against

Habits from weaker spreadsheet stacks cost time here. Already handled:

- Recalculation is real and automatic after every edit. What `read`
  returns IS the computed value — no stale caches to "refresh", no need
  to re-touch formulas to force an update.
- Ops batches are atomic. Any op fails → the file is untouched; there are
  no half-applied cascades to detect or clean up.
- Document commands do not require a resident daemon or an open/save/close
  lifecycle. App and Preview explicitly start managed background services.
- Inline JSON via `--ops`, `--set` and stdin `-` avoids shell-escaping
  traps by design (one Windows PowerShell caveat: section 11).
- Charts and pivot tables are first-class ops: `update_chart` edits an
  existing chart in place — no delete-and-rebuild, and `delete_chart`
  removes one. `create_chart` applies a modern look by itself (no chart
  border, white plot, bottom legend, light value-axis gridlines) — no
  de-uglification pass needed.
- The cosmetic surface is real: default font, borders, tab colors, sheet
  view, sparklines, conditional-format rules incl. formula/topBottom/
  iconSet — no need to plan around their absence (`aspose-cli docs
  design-system` is the system that uses them).

## 4. Reading: the projection ladder

Step 1 — structure first, never data:

```
aspose-cli cells inspect book.xlsx --output json
aspose-cli cells inspect book.xlsx --preview --output json          # + sample rows
aspose-cli cells inspect book.xlsx --detail names errors --output json
```

- `--detail` sections (repeatable; omitted unless requested). The flag name
  is not the payload key — three of the seven rename, so read from the key,
  not the flag:

  | `--detail` | payload key |
  |------------|-------------|
  | `names` | `workbook.definedNames` |
  | `errors` | `workbook.formulaErrors` — scans every sheet for cells evaluating to `#REF!`, `#DIV/0!`, `#VALUE!`, ... |
  | `validation` | `workbook.validations` (plural) |
  | `fonts` `tables` `charts` `pivots` | `workbook.fonts` `.tables` `.charts` `.pivots` (unchanged) |

- `--preview` samples the first rows of every sheet (`--preview-rows`,
  default 5) — for COLUMN LAYOUT only. It prints display values, so a real
  datetime and the text `"2026-03-02"` both appear as `"2026-03-02"`. Never
  infer a type from a preview: `read` and its `t` are the only type
  authority.
- Before rendering on an unfamiliar machine, `aspose-cli fonts check book.xlsx`
  reports whether each used font is available here and what a render would
  substitute for any that is missing (`allAvailable` is the quick verdict —
  over the font NAMES the workbook declares, not glyph coverage: see
  `aspose-cli docs verification`).
- `aspose-cli fonts list` answers a narrower question than its name suggests: the
  font SOURCES configured on this machine, plus `defaultFont` only when one
  was set explicitly. A stock machine returns `{"sources": []}` — no
  default-font field at all, the table view printing `(engine fallback)` as a
  placeholder, not a font name. It does not enumerate installed fonts and
  never names the fallback a render will actually use, so do not ask it what
  the default font is.

Step 2 — windowed cell data of one sheet:

```
aspose-cli cells query range book.xlsx --sheet Sales --range A1:F50 --output json
```

- `--scope values` (default) | `formulas` (adds `f`) | `styles` (adds
  `styleId` + a deduplicated `styles` pool) | `full`. A cell carrying the
  DEFAULT style has no `styleId` key at all — absent means default, so read
  the field defensively instead of indexing the pool blind.
- Range forms: `C5`, `B2:D10`, `Sales!A1:C10`, `'My Sheet'!A1:C10`. Whole
  rows/columns (`A:A`, `1:3`) are rejected by design — give explicit
  bounds so output stays budgetable.
- Reads are budgeted (`--max-cells`, default 10000). Over-budget default
  reads return a summary plus a ready-to-run `next` command — execute it
  verbatim instead of computing ranges yourself. An explicit `--range` is a
  complete request and never returns `next`.
- Cell types: `string`, `number`, `boolean`, `datetime` (ISO 8601),
  `error` (e.g. `#DIV/0!`), `empty`.

## 5. Editing: one atomic ops batch

Write an ops JSON document, then apply it in one invocation:

```json
{
  "ops": [
    { "id": "sales-row", "op": "set_values", "sheet": "Sales", "range": "A6", "values": [["South", 500, 600]] },
    { "id": "totals", "op": "set_formula", "sheet": "Sales", "range": "E2:E6", "formula": "=SUM(B2:D2)" },
    { "op": "format_range", "sheet": "Sales", "range": "A1:E1",
      "style": { "bold": true, "bg": "#1F4E79", "color": "#FFFFFF" } },
    { "op": "freeze_panes", "sheet": "Sales", "cell": "A2" }
  ]
}
```

```
aspose-cli cells edit book.xlsx --ops ops.json --in-place --backup --verify --output json
```

Quick one-liners need no file — the same grammar, inline:

```
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[{"op":"set_values","sheet":"Sales","range":"B3","values":[[42]]}]}'
aspose-cli cells edit book.xlsx --in-place --set "Sales!B3=42" --set "Sales!G2==E2*F2"
```

- `--ops` takes a path, `-` (stdin), or the ops JSON itself when the value
  starts with `{` or `[`.
  Windows PowerShell strips the inner quotes from inline JSON arguments —
  there, escape them as `\"`, pipe the document via `--ops -`, or prefer
  `--set` (the error hint teaches the same recovery).
- `--set SHEET!CELL=VALUE` sets one cell (repeatable, applied after the
  `--ops` document, same atomic batch): a value starting with `=` is a
  formula, TRUE/FALSE and numbers are typed, anything else is text. Quote
  sheet names that need it: `--set "'My Sheet'!A1=5"`. Ranges and styling
  stay in ops.
- Inside an ops document, `sheet` defaults to the active sheet and range
  fields are unqualified A1 (`B2:D10`); only `copy_range.from/to` and
  `create_pivot.sourceRange` take sheet-qualified references.
- Batches are atomic: if any op fails, the file is untouched and the error
  names the failing op's `index`. Fix that op and retry.
- Each op may carry a unique stable `id`; omitted ids are assigned
  deterministically as `op-0001`, `op-0002`, and so on.
- `info`, `read`, `search`, and `diff` expose SHA-256 source fingerprints.
  Put the current value in `--if-match` or the envelope's `ifMatch` to reject
  an edit when another process changed the workbook.
- The result's op-by-op record is the `applied` array (not `ops`): one entry
  per op in order, each with `id`, `index`, `op`, `status`, product-owned
  `address` when available, and `cellsAffected`; successful writes also expose
  `output.fingerprint.sha256` after real-engine reopen verification.
- `--best-effort` makes the batch partial instead: failing ops stay
  in `applied` with `status: "failed"` and an `error` object, the rest still
  apply, and the command exits 8 (a partial-success signal, not a hard
  error).
- `--dry-run` validates and applies in memory, writing nothing.
- Formulas recalculate automatically after every edit (`--no-recalc` opts out).
- `set_formula` over a range uses Excel fill semantics: relative references
  shift per cell, `$` absolute references stay.
- Formatting only touches the style fields you set; everything else is
  preserved.
- Charts and pivot tables are ops too (`create_chart`, `update_chart`,
  `delete_chart`, `create_pivot`, `refresh_pivot`). `create_chart` plots ONE
  contiguous `dataRange`: a multi-area reference fails `OPS_INVALID` ("a
  range has at most one ':' separator"), so the common "header row + two
  non-adjacent rows" shape (`A1:F1,A3:F4`) needs a contiguous helper block
  first — mirror the wanted rows with formulas (`=A1`, `=A2`, `=A4`) into,
  say, `A7:F9` and chart that. Beyond `title`, both chart ops take `legend`,
  `axisTitles`, `seriesColors` and `dataLabels`, and `create_chart` defaults
  to a modern look on its own (`aspose-cli docs editing`).
- The complete vocabulary — 55 ops, every field — is
  `aspose-cli schema v2/cells/ops`. Semantics and recipes:
  [references/editing.md](references/editing.md), which
  `aspose-cli docs editing` also prints offline (`aspose-cli docs ops` is an alias).

Other mutations:

```
aspose-cli cells create report.xlsx --sheets "Data,Summary"
aspose-cli cells edit report.xlsx --ops data.json --in-place
aspose-cli cells edit model.xlsx --ops '{"ops":[{"op":"recalculate"}]}' --in-place
```

Data files for `write` are JSON arrays of arrays; null clears a cell;
`--start-cell` (default A1) anchors the matrix. Mutating commands write a
sibling `.out` file by default (`book.xlsx` → `book.out.xlsx`); use
`--in-place` to modify the input (atomic: temp file, then move), or
`--out` for an explicit path.

### Safe editing of existing files

For a workbook you did not create this session, use the native safety loop:

```
aspose-cli cells edit book.xlsx --ops ops.json --in-place --backup --verify --output json
```

- `--backup` creates `book.backup.xlsx` before replacement and never
  overwrites it. `backup.created` tells whether this call created it. JSON has
  no timestamp; use the file's normal creation/modified time.
- `--verify` automatically enables that backup for in-place edits. It uses a
  private per-call baseline, so an older stable backup cannot pollute the
  current diff.
- Check `verification.directChanges`, `formulaResultChanges`, `otherChanges`,
  and `formulaErrors`. Open every path in `verification.renders`;
  `visualReviewRequired` means the Agent still needs to look.
- Formula errors or incomplete verification preserve the edited file and exit
  8 with `verification.issues`.
- `--verify` is incompatible with `--dry-run` and `--no-recalc`, but works
  with `--best-effort`.

At the end of a longer session, diff against the stable backup and report it:

```
aspose-cli cells compare book.backup.xlsx book.xlsx --output json
```

   It compares cell values and formula text — and nothing else (`--compare
   values` narrows it to values; those two are the only choices). Styling,
   widths, charts, images and page setup are invisible to it, so a
   formatting-only session correctly reports `identical: true`. It is an
   inventory of value and formula changes, not of everything you did — see
   `aspose-cli docs verification`, Tier 4, before reporting it to the user.
Tell the user the backup path. Restore = copy the backup back over the
   file.

One stable backup per file is enough; repeated `--backup` runs are a no-op.

## 6. Verify before you deliver

Assume there are problems; your job is to find them. Your last command
exiting 0 is not "done" — the first build is almost never right. The tiers:

- **values** — `read` back every range you changed (golden rule 3);
- **visual** — `render` + LOOK whenever anything visual changed or a human
  will open the file;
- **semantic** — `cells inspect --detail errors` reports zero formula errors;
- **session** — the backup diff (section 5) contains only intended changes.

The full protocol with per-tier checklists: `aspose-cli docs verification`.

## 7. Converting and rendering

```
aspose-cli cells convert book.xlsx --to pdf                  # print-accurate
aspose-cli cells convert book.xlsx --to csv --sheet Sales    # csv/tsv/pdf take --sheet
aspose-cli cells render book.xlsx --sheet Sales --range A1:G20 --out check.png
aspose-cli cells render book.xlsx --all-sheets --out check.png   # one PNG per visible sheet
```

Formats: `aspose-cli capabilities --output json` lists everything (convert:
xlsx, xlsm, xlsb, xls, ods, csv, tsv, html, mhtml, pdf, xps, json, md;
render: png, jpeg, svg; `--dpi` default 192, range 24-1200).

`--all-sheets` is the whole-workbook look in one command: every visible
sheet renders to its own file (`check.Data.png`, `check.Summary.png`, …;
`outputs` in the result lists them in order). An empty or unrenderable
sheet is skipped and named in a `SHEETS_SKIPPED` warning — read it.
Hidden sheets never render here; name one explicitly with `--sheet` to
reveal it.

Defaults: `convert` writes the input path with the target extension;
`render` writes the input path with the image extension. For verification
renders, write to a scratch path instead and delete the image after
looking — don't litter the user's directory. `OUTPUT_EXISTS` protects
existing files; pass `--overwrite` deliberately.

## 8. Live preview

```
aspose-cli preview book.xlsx --output json
```

starts a managed background preview and returns immediately with
`id/url/pid/file/view/reused`. It refreshes on every save; the same
product/file/view reuses the existing session.

```
aspose-cli preview status
aspose-cli preview stop <id>
aspose-cli preview stop --all
```

Division of labor: preview is for the HUMAN to look at while you work;
your own checks stay `render` + look, `read`, and `diff`. Session management,
port control, and the change spotlight are documented in
[references/preview.md](references/preview.md), also `aspose-cli docs preview`.

## 9. Licensing and evaluation mode

The CLI is free; without a license the engine runs in evaluation mode:
produced files carry an evaluation watermark and some operations are
limited. Every affected result contains
`warnings: [{ "code": "EVAL_MODE", ... }]`.

You MUST mention the watermark to the user when delivering evaluation-mode
output. Reads are unaffected. A license removes all limits: install it with
`aspose-cli license install Aspose.Cells.lic --product cells`, set
`ASPOSE_CELLS_LICENSE_PATH`, use a shared `ASPOSE_LICENSE_PATH`, or pass
`--license <path>`. `license status` reports Cells and Words independently.

## 10. Errors: exit codes and recovery

Exit codes: 0 ok, 1 internal, 2 usage, 3 input file, 4 validation,
5 output, 6 format, 7 license, 8 partial (`--best-effort`),
9 timeout (`--timeout`). The error envelope on stderr has stable `code`,
`message`, `details` (often the valid alternatives) and `hint`.

| code | do this |
|------|---------|
| FILE_NOT_FOUND | List the directory; fix the path (relative paths resolve against --workdir). |
| FILE_LOCKED | Another process holds the file exclusively; ask the user to close it, retry. |
| OUTPUT_UNWRITABLE | Check directory and permissions — or the target is open in Excel (see note below). |
| PASSWORD_REQUIRED / PASSWORD_INVALID | Ask the user for the password; prefer `--password-env VAR` (below) over `--password`. |
| FILE_TOO_LARGE | Raise `ASPOSE_CLI_MAX_FILE_BYTES` (bytes), or split the workbook. |
| SHEET_NOT_FOUND | Use a name from error.details.available (case-sensitive). |
| RANGE_INVALID / RANGE_TOO_LARGE | Fix the A1 range, or narrow it / follow the hint's suggested window. |
| OPS_INVALID | error.details.index names the failing op; fix that op, retry (nothing was written). |
| OUTPUT_EXISTS | Pass --overwrite, or pick a different --out path. |
| FORMAT_UNSUPPORTED | Use an id from error.details.supported. |
| LICENSE_* | Fix or remove the license configuration; do NOT retry in a loop. |

A workbook open in Excel usually still READS fine (shared read). What
fails is the `--in-place` save — as OUTPUT_UNWRITABLE (exit 5), because
the atomic replace is what gets blocked; FILE_LOCKED (exit 3) appears when
the open itself hits a sharing violation. Recovery for both: ask the user
to close the file, retry.

**Passwords.** Every command that opens a workbook accepts three password
sources, at most one at a time: `--password <value>` (discouraged — visible in
the process list and shell history), `--password-env <VAR>` (the password is
the value of that environment variable) and `--password-stdin` (the first line
of stdin, where stdin is not already the document). Prefer `--password-env`.
`diff` uses `--left-password[-env]` / `--right-password[-env]`.

To **protect a produced file**, the writing verbs (`new`, `write`, `edit`,
`calc`) accept `--encrypt <value>` / `--encrypt-env <VAR>` — prefer the env form.
Only spreadsheet outputs (xlsx, xlsm, xlsb, xls, ods) can be encrypted.

More recovery detail: [references/troubleshooting.md](references/troubleshooting.md).

## 11. Pitfalls

| pitfall | do this instead |
|---------|-----------------|
| Windows PowerShell strips inner double quotes from inline `--ops` JSON | Escape them as `\"`, pipe the document via `--ops -`, or use `--set`. |
| Older Windows PowerShell pipes prefix stdin with a BOM | The CLI absorbs it; if a JSON parse error points at byte 0xEF on an old build, pass a file path instead. |
| Windows PowerShell `>` re-encodes redirected stdout as UTF-16 | Parse stdout directly from the command instead of round-tripping through a file; if you must redirect there, read the file back as UTF-16. |
| Evaluation mode inserts an "Evaluation Warning" sheet | Always name sheets explicitly; never rely on sheet order or index. |
| Sheet names are case-sensitive | Copy the exact spelling from `error.details.available`. |
| Mutating verbs default to writing `book.out.xlsx` | Pass `--in-place` deliberately — with the backup protocol (section 5) for user files. |
| Writing the same file from parallel commands | Never. Serialize writes; only parallel reads are safe. |

## 12. Task routing

What to read before each class of task (`aspose-cli docs` alone lists every
topic):

| task | read first |
|------|------------|
| Editing: the ops vocabulary and recipes | `aspose-cli docs editing` |
| The delivery floor: widths, number formats, validation, print | `aspose-cli docs workbook-standards` |
| Making it look professionally designed: dashboards, colors, charts, KPI cards | `aspose-cli docs design-system` |
| Building a financial model (forecasts, scenarios, valuation) | `aspose-cli docs financial-models` |
| Verifying / QA before delivery | `aspose-cli docs verification` |
| Building it live while the user watches (demo protocol) | `aspose-cli docs design-system`, Live demo protocol; mechanics in `aspose-cli docs preview` |
| Live preview for the user | `aspose-cli docs preview` |
| An error you can't recover from | `aspose-cli docs troubleshooting` |

## 13. Worked examples

- [examples/report-from-csv](examples/report-from-csv/README.md) — CSV to a
  formatted, totaled, chart-bearing workbook and a PDF.
- [examples/recalc-and-verify](examples/recalc-and-verify/README.md) —
  change assumptions, recalculate, read back and visually verify.
- [examples/edit-existing-safely](examples/edit-existing-safely/README.md) —
  back up the user's file, edit in place, and report from a real diff.
- [examples/sales-dashboard](examples/sales-dashboard/README.md) — the
  design-system deliverable for an open-ended ask: messy CSV to a Data
  register + designed Dashboard (KPI cards, sparklines, charts), through
  the eyes loop to a print-ready PDF.

## 14. Visual delivery gate

1. Understand the audience, decision or reporting purpose, viewing context, and requested scope before designing or editing the workbook.
2. For an existing user workbook, preserve formulas, styles, sheets, names, charts, print settings, and unrelated content; change only the requested scope.
3. Run `aspose-cli review <artifact> --out <fresh-review-dir> --output json` for each workbook and exported deliverable, using a fresh output directory for each round. Use the review findings to focus inspection, never as a replacement for opening the output.
4. Actually open every visual artifact produced by review, one by one. Complete coverage means every visible sheet for a new workbook, every changed or affected sheet for a scoped edit, and every page of each exported PDF. Check hierarchy, values and number formats, truncation, row and column sizing, merged cells, borders, alignment, contrast, conditional formats, chart labels and legends, dashboard balance, and print pagination.
5. Fix defects, recalculate, read back, render, and run review again. Stop after at most three visual correction rounds; report remaining defects instead of cycling indefinitely.
6. Do not claim a visual pass when image or document inspection is unavailable, any required sheet/page/artifact was not actually opened, or coverage is incomplete. State exactly what was reviewed and mark the remainder partial or skipped.
7. Report evaluation results separately from licensed results. For every evaluation artifact, disclose `EVAL_MODE`, watermarking, row or feature limits, and do not use it as evidence of licensed fidelity.
