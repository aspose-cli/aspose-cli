---
name: aspose-cli-cells
description: High-fidelity Excel/spreadsheet processing via the local Aspose CLI without Python or Microsoft Office; use for real workbooks, formula recalculation, native charts and pivots, format-preserving edits, protected files, exact-layout conversion, and file fidelity.
license: Apache-2.0
---

# Aspose Cells CLI

Process spreadsheets with engine-grade fidelity using the local `aspose-cli`,
with no Python or Office dependency. Pass `--output json` when parsing results;
each result names its `schema`.

## 1. Session start

Run `aspose-cli --version`; if the CLI is missing, ask the user how to install
it. Then check the environment once per session:

```
aspose-cli doctor --output json          # license, runtime, output writability
aspose-cli license status --output json  # per-product source and mode
```

A `license` check of `warn` means some product is in evaluation mode; read the
`cells` entry.

## 2. Golden rules

1. Never dump a whole sheet. Climb the projection ladder (section 3).
2. Batch all edits into ONE ops document; never run one command per cell.
3. Numbers you report come from the engine (`query range`), never from your
   own arithmetic.
4. Before the first in-place edit of a file you did not create this session,
   use `--in-place --backup --verify` (section 4).
5. Verify before delivery (section 5); a clean exit code is not "done".
6. On any error, read `error.hint` first; `error.details` usually lists the
   valid alternatives.
7. Serialize writes to the same file; parallel reads are safe.
8. Unsure about a verb, op or field? Ask the CLI (`--help`,
   `aspose-cli capabilities cells edit`, `aspose-cli schema v2/cells/ops`,
   `aspose-cli docs`) instead of guessing.
9. An open-ended request ("make me a sales sheet") gets the full deliverable:
   designed synthetic data, a Detail sheet and a designed Dashboard sheet
   (`aspose-cli docs design-system`).

## 3. Reading: the projection ladder

Structure first, never data:

```
aspose-cli cells inspect book.xlsx --output json
aspose-cli cells inspect book.xlsx --preview --output json          # + sample rows
aspose-cli cells inspect book.xlsx --detail names errors --output json
```

- `--detail errors` fills `workbook.formulaErrors`, `names` fills
  `workbook.definedNames`, `validation` fills `workbook.validations`; `fonts`,
  `tables`, `charts` and `pivots` keep their names.
- `--preview` shows display values for column layout only; `query range` and
  its `t` field are the only type authority.

Then a window of one sheet:

```
aspose-cli cells query range book.xlsx --sheet Sales --range A1:F50 --output json
```

- `--scope values` (default), `formulas` (adds `f`), `styles` or `full`.
- Whole rows or columns (`A:A`) are rejected; give explicit bounds.
- Over-budget reads return a summary and a ready-to-run `next` command;
  execute it verbatim.
- Cell types: `string`, `number`, `boolean`, `datetime` (ISO 8601), `error`,
  `empty`.

## 4. Editing: one atomic ops batch

```json
{
  "ops": [
    { "op": "set_values", "sheet": "Sales", "range": "A6", "values": [["South", 500, 600]] },
    { "op": "set_formula", "sheet": "Sales", "range": "E2:E6", "formula": "=SUM(B2:D2)" },
    { "op": "format_range", "sheet": "Sales", "range": "A1:E1",
      "style": { "bold": true, "bg": "#1F4E79", "color": "#FFFFFF" } },
    { "op": "freeze_panes", "sheet": "Sales", "cell": "A2" }
  ]
}
```

```
aspose-cli cells edit book.xlsx --ops ops.json --in-place --backup --verify --output json
aspose-cli cells edit book.xlsx --in-place --set "Sales!B3=42" --set "Sales!G2==E2*F2"
```

- `--ops` takes a path, `-` (stdin) or inline JSON. Without `--in-place` or
  `--out`, the result goes to `book.out.xlsx`.
- Batches are atomic: if any op fails, nothing is written and the error names
  the op `index`. `--best-effort` keeps successful ops and exits 8 when any
  op fails; `--dry-run` validates in memory and writes nothing.
- Formulas recalculate once after the whole batch; `--no-recalc` opts out.
  Auto-fit, sort, duplicate-removal and pivot ops calculate earlier edits first.
- `set_formula` over a range uses Excel fill semantics; formatting ops touch
  only the style fields you set.
- Charts and pivots are ops (`create_chart`, `update_chart`, `delete_chart`,
  `create_pivot`, `refresh_pivot`). `create_chart` plots one contiguous
  `dataRange` and applies a modern look by itself.
- `inspect`, `query range`, `query search` and `compare` report SHA-256
  fingerprints; pass one as `--if-match` to reject a concurrently changed file
  (a mismatch fails with INPUT_CHANGED, exit 3).
- The result's `applied` array has one entry per op with `status`,
  `itemsAffected` and `targets`.

Full vocabulary and recipes: `aspose-cli docs editing` and
`aspose-cli schema v2/cells/ops`.

### Editing a user's file

`--backup` (only with `--in-place`) creates `book.backup.xlsx` once and never
overwrites it. `--verify` compares the staged output with a
private pre-edit snapshot before publishing and reports `directChanges`,
`formulaResultChanges`, `otherChanges` and `formulaErrors`. Formula errors or
incomplete checks keep the edited file and exit 8 with `verification.issues`.
`--verify` cannot accompany `--dry-run` or `--no-recalc`.

At the end of the session, diff against the stable backup and report it:

```
aspose-cli cells compare book.backup.xlsx book.xlsx --output json
```

It compares cell values and formula text only; styling, widths and charts are
invisible to it. Tell the user the backup path.

## 5. Verify before delivery

| tier | check |
|------|-------|
| values | `query range` every changed range; numbers come from the engine |
| semantic | `cells inspect --detail errors` reports no formula errors; `cells query search` finds no TBD/TODO placeholders |
| visual | `aspose-cli review book.xlsx --out <new-dir> --output json`, then open every sheet image it lists |
| widths | `cells render --sheet S --range A1:G20` for truncation; only range renders match Excel's column widths |
| session | the backup diff contains only intended changes |

Fix, then review again into a fresh directory; stop after three rounds and
report what remains. Never claim a visual pass for sheets you did not open.
Full protocol: `aspose-cli docs verification`.

## 6. Converting and rendering

```
aspose-cli cells convert book.xlsx --to pdf                  # print-accurate
aspose-cli cells convert book.xlsx --to csv --sheet Sales    # csv/tsv/md/pdf take --sheet
aspose-cli cells render book.xlsx --all-sheets --out book.png   # one PNG per visible sheet
```

`aspose-cli capabilities --output json` lists every format. Existing files are
protected; pass `--overwrite` deliberately.

## 7. Licensing and evaluation mode

Without a license, produced files carry an evaluation watermark and an extra
"Evaluation Warning" sheet, results carry `EVAL_MODE`, and CSV, TSV and
Markdown exports are limited to the first worksheet. Tell the user when you
deliver evaluation output. Install a license with
`aspose-cli license install Aspose.Cells.lic --product cells`; details in
`aspose-cli docs licensing`.

## 8. Errors and pitfalls

Exit codes: 0 ok, 1 internal, 2 usage, 3 input file, 4 validation, 5 output,
6 format, 7 license, 8 partial, 9 timeout. Recovery for every code:
`aspose-cli docs troubleshooting`.

| pitfall | do this instead |
|---------|-----------------|
| Windows PowerShell strips inner quotes from inline `--ops` JSON | Escape them as `\"`, pipe via `--ops -`, or use `--set` |
| Windows PowerShell `>` re-encodes stdout as UTF-16 | Parse stdout directly |
| A workbook open in Excel | Reads work; the in-place save fails with OUTPUT_UNWRITABLE — ask the user to close it |
| Evaluation adds a sheet and makes it the active one | Always pass `--sheet`; names match case-insensitively; take them from `error.details.available` and never rely on sheet order |
| Passwords | Prefer `--password-env VAR`; protect outputs with `--encrypt-env` |

## 9. Task routing

| task | read first |
|------|------------|
| The ops vocabulary and recipes | `aspose-cli docs editing` |
| The delivery floor: widths, number formats, validation, print | `aspose-cli docs workbook-standards` |
| A professionally designed workbook: dashboards, colors, charts, KPI cards | `aspose-cli docs design-system` |
| A financial model | `aspose-cli docs financial-models` |
| Verification and QA | `aspose-cli docs verification` |
| Live preview for the user | `aspose-cli docs preview` |
| An error you cannot recover from | `aspose-cli docs troubleshooting` |

Worked examples:
[report from CSV](examples/report-from-csv/README.md),
[recalculate and verify](examples/recalc-and-verify/README.md),
[edit an existing file safely](examples/edit-existing-safely/README.md),
[sales dashboard](examples/sales-dashboard/README.md).
