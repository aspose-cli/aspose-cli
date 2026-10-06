---
name: aspose-cli-cells
description: High-fidelity Excel/spreadsheet processing via the local Aspose CLI without Python or Microsoft Office; use for real workbooks, formula recalculation, native charts and pivots, format-preserving edits, protected files, exact-layout conversion, and file fidelity.
license: Apache-2.0
---

# Aspose Cells CLI

Read, edit, verify and deliver spreadsheets with the `aspose-cli cells`
commands. Session start, the shared golden rules and the routes to the
shared topics are in `aspose-cli docs overview`; this Skill adds what is
specific to workbooks.

## Workbook rules

1. Read the numbers you report with `cells query range`.
2. If a number can be computed from other cells, write a formula; the engine
   recalculates after every edit.
3. Name the sheet on every read, render and operation (`--sheet`, `"sheet"`);
   sheet names match case-insensitively.
4. An open-ended request ("make me a sales sheet") gets the full deliverable:
   realistic data, a Detail sheet and a designed Dashboard sheet
   (`aspose-cli docs cells/design-system`).

## Reading

Structure first, then windows of one sheet:

```
aspose-cli cells inspect book.xlsx --output compact
aspose-cli cells inspect book.xlsx --preview --detail names errors --output json
aspose-cli cells query range book.xlsx --sheet Sales --range A1:F50 --output json
aspose-cli cells query search book.xlsx --pattern "Total" --output json
```

- `--detail` adds `names` (`workbook.definedNames`), `errors`
  (`workbook.formulaErrors`), `validation` (`workbook.validations`),
  `layout` (`workbook.layouts`: frozen panes, outline groups, AutoFilter,
  print area and titles, page setup), `fonts`, `tables`, `charts` and `pivots`.
- `--preview` shows display values for layout only. `query range` and its `t`
  field (`string`, `number`, `boolean`, `datetime`, `error`, `empty`) are the
  type authority.
- `--scope values` (default), `formulas` (adds `f`), `styles` (a
  deduplicated style pool) or `full`. Style fields read back under the names
  `format_range` writes.
- Give explicit bounds; `A:A` is refused. A read without `--range` of a sheet
  larger than `--max-cells` returns a summary and a `window.next` command for
  the first page; an explicit `--range` larger than that fails with
  `RANGE_TOO_LARGE`, and its hint is that command. Follow each `window.next`.
- CSV and TSV inputs are read as UTF-8 with invariant number and date formats.
  A file that needs anything else is refused rather than misread:
  `INPUT_ENCODING_INVALID` for other encodings (typical of Chinese ERP exports),
  `FORMAT_AMBIGUOUS` for decimal commas (`1.234,56`). Import it once with
  `cells convert data.csv --to xlsx --encoding gb18030` or `--culture de-DE`, as
  the hint says, and work on the workbook.
- `cells inspect` and `cells convert` warn `TEXT_TABLE_LAYOUT` when a CSV or
  TSV is not a plain table from row 1: title or query-condition rows before the
  header, empty rows inside the table, or a total row (`合计`, `Total`) at its
  end. Take the header row and data rows from the message, not from row 1.

## Editing

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
aspose-cli schema v2/cells/ops --operation format_range
```

- `set_formula` over a range uses Excel fill semantics.
- `copy_range` copies within the workbook; data from another file comes in
  with `import_range` or `import_sheet` (`aspose-cli docs cells/editing`,
  Combining workbooks).
- `--verify` compares the staged output with the input before publishing and
  reports cell changes and formula errors (exit 8 on findings). Use
  `--in-place --backup --verify` for the first edit of a file you did not
  create, and diff against `book.backup.xlsx` at the end.
- Operations by task, ordering rules, recipes and the field names most often
  guessed wrong: `aspose-cli docs cells/editing`.

## Verifying

| tier | check |
|------|-------|
| values | `cells query range` every changed range |
| visual | `aspose-cli review book.xlsx --out <new-dir> --output json`, then open every sheet image |
| widths | `cells render --sheet S --range A1:G20` |
| semantic | `cells inspect --detail errors` reports no formula errors; `cells query search` finds no placeholders |
| session | `cells compare book.backup.xlsx book.xlsx` lists only intended value and formula changes |

Tiers and render facts: `aspose-cli docs cells/verification`.

## Converting and rendering

```
aspose-cli cells convert book.xlsx --to pdf
aspose-cli cells convert book.xlsx --to csv --sheet Sales
aspose-cli cells render book.xlsx --all-sheets --out book.png
```

`cells convert book.xlsx --to xltx` saves an Excel template (xltm keeps macros);
new workbooks made from it in Excel start with its sheets, styles and protection.
`cells convert` takes `--sheet` for csv, tsv, md and pdf. CSV and TSV output is
UTF-8 without a byte order mark; add `--bom` when a person will open the file in
Excel, which otherwise misreads non-English text. A PDF that splits a
chart across pages warns `CHART_SPLIT_ACROSS_PAGES` (review reports
`CELLS_CHART_SPLIT_ACROSS_PAGES`); fit the sheet with `set_page_setup`
(`fitToWidth` 1, `fitToHeight` 0, or landscape) first. `cells render`
writes png, jpeg or svg, taken from `--to` or the `--out` extension.
`aspose-cli capabilities cells` lists every format.

## Evaluation mode

Warning sheets, data-export limits and their fixes:
`aspose-cli docs cells/troubleshooting`.

## Pitfalls

| pitfall | do this instead |
|---------|-----------------|
| Inline `--ops` JSON in PowerShell | Write the ops to a file, or use `--set` for single cells; why: `aspose-cli docs troubleshooting`, Windows PowerShell |
| A workbook open in Excel | The in-place save fails with `OUTPUT_UNWRITABLE`; ask the user to close it |
| `set_values` with `"2026-04-03"` | Stored as text; write `=DATE(2026,4,3)` |
| A bigger font on a title row | The row keeps its height; auto-fit it with `resize_rows` and no `height` |
| Text written into a new workbook | Columns keep the default width, so long text is cut off by the next cell; size them with `resize_columns` (no `width` auto-fits) |
| `cells compare` after a formatting session | `identical: true` is correct: it compares values and formula text only |

## Routing

| task | read |
|------|------|
| Operations by task, recalculation and recipes | `aspose-cli docs cells/editing` |
| The delivery floor: widths, number formats, validation, print | `aspose-cli docs cells/workbook-standards` |
| A designed workbook: dashboards, colors, charts, KPI cards | `aspose-cli docs cells/design-system` |
| A financial model | `aspose-cli docs cells/financial-models` |
| Workbook verification tiers and render facts | `aspose-cli docs cells/verification` |
| What the workbook preview shows | `aspose-cli docs cells/preview` |
| Cells error codes, warnings and evaluation effects | `aspose-cli docs cells/troubleshooting` |

Worked examples:
[report from CSV](examples/report-from-csv/README.md),
[recalculate and verify](examples/recalc-and-verify/README.md),
[edit an existing file safely](examples/edit-existing-safely/README.md),
[sales dashboard](examples/sales-dashboard/README.md).
