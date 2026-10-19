---
name: aspose-cli-cells
description: High-fidelity Excel/spreadsheet processing via the local Aspose CLI without Python or Microsoft Office; use for real workbooks, formula recalculation, native charts and pivots, format-preserving edits, protected files, exact-layout conversion, and file fidelity.
license: MIT
---

# Aspose Cells

Use `aspose-cli cells` for workbooks: XLSX, XLSM, XLSB, XLS, ODS, CSV and TSV.
The shared rules (session start, reading windows, operation batches, secrets,
review, licensing, errors) are in `aspose-cli docs overview`; this Skill adds
what is specific to workbooks. `aspose-cli capabilities cells` lists commands,
formats, operations and codes; `aspose-cli schema v2/cells/ops --operation <op>`
gives an operation's fields. A table in a Word, PDF or slide file is theirs.

## Workflow

1. **Inspect, then read windows of one sheet.**

   ```
   aspose-cli cells inspect book.xlsx --output-format compact
   aspose-cli cells inspect book.xlsx --sample --detail names errors --output-format json
   aspose-cli cells read book.xlsx --sheet Sales --range A1:F50 --output-format json
   aspose-cli cells search book.xlsx --pattern "Total" --output-format json
   ```

   `--sample` shows display values for layout only; take types (`t`) from
   `cells read`, whose `--content` adds formulas (`f`) or a style pool.

2. **Edit in one batch** (batches and outcomes: `aspose-cli docs editing`):

   ```json
   {
     "ops": [
       { "op": "set_values", "sheet": "Sales", "range": "A6", "values": [["South", 500, 600]] },
       { "op": "set_formula", "sheet": "Sales", "range": "E2:E6", "formula": "=SUM(B2:D2)" },
       { "op": "format_range", "sheet": "Sales", "range": "A1:E1", "style": { "bold": true } },
       { "op": "freeze_panes", "sheet": "Sales", "cell": "A2" }
     ]
   }
   ```

   ```
   aspose-cli cells edit book.xlsx --ops ops.json --in-place --backup --verify --output-format json
   aspose-cli cells edit book.xlsx --in-place --set "Sales!B3=42" --set "Sales!G2==E2*F2"
   ```

   The engine recalculates the workbook after the batch. `--verify` reports
   cell changes and formula errors before publishing (exit 8 on findings);
   add it to the first in-place edit of a file you did not create, and report
   the totals `verification.formulaResultChanges` lists. Operations by task,
   batch order and recipes: `aspose-cli docs cells/editing`.

3. **Verify** (`aspose-cli docs cells/verification`):

   | tier | check |
   |------|-------|
   | values | `cells read` every changed range |
   | visual | `aspose-cli review book.xlsx --out <new-dir> --output-format json`, then open every sheet image; judge widths from `cells render --range Sales!A1:G20` |
   | semantic | `cells inspect --detail errors` reports no formula errors; `cells search` finds no placeholders |
   | session | `cells compare book.backup.xlsx book.xlsx` lists only intended value and formula changes |

4. **Convert and render.**

   ```
   aspose-cli cells convert book.xlsx --to pdf
   aspose-cli cells convert book.xlsx --to csv --sheets 2 --bom
   aspose-cli cells render book.xlsx --sheets all --out book.png
   ```

   Add `--bom` to a CSV or TSV a person opens in Excel, or it misreads
   non-English text. What an output drops: `aspose-cli docs cells/troubleshooting`.

## Workbook rules

- Name the sheet every time: `--sheet` on `cells read` and `cells search`,
  `--range Sales!A1:G20` on `cells render`, `"sheet"` in an operation.
  `--sheets` on `cells convert` and `cells render` takes positions from
  `cells inspect` (`2`, `1-3`, `all`), never names.
- If a number can be computed from other cells, write a formula: `set_formula`
  fills a range like Excel; `set_values` stores every string as text.
- An open-ended request ("make me a sales sheet") gets the full deliverable:
  realistic data, a Detail sheet and a designed Dashboard sheet
  (`aspose-cli docs cells/design-system`).
- A CSV or TSV in another encoding than UTF-8 (`INPUT_ENCODING_INVALID`) or
  with decimal commas (`FORMAT_AMBIGUOUS`) is refused: import it once with
  `cells convert data.csv --to xlsx` and the `--encoding` or `--culture` the
  hint names. `TEXT_TABLE_LAYOUT` means the table does not start at row 1;
  take its rows from the message (`aspose-cli docs cells/troubleshooting`).

## Common mistakes

| mistake | do this instead |
|---------|-----------------|
| Inline `--ops` JSON in PowerShell | Write the ops to a file, or use `--set` for single cells (`aspose-cli docs troubleshooting`, Windows PowerShell) |
| A whole-column range such as `A:A` | Give explicit bounds such as `A1:A500`; whole rows and columns are refused |
| A date written as `"2026-04-03"` or `"=DATE(2026,4,3)"` in `set_values` | Both are stored as text; write `=DATE(2026,4,3)` with `set_formula` (or `--set "Sales!B2==DATE(2026,4,3)"`), then give the cells a date `numberFormat` such as `yyyy-mm-dd`: `cells read` reports `"t": "datetime"` only once the format is set |
| `sort_range` over live formulas | Relative references scramble; sort the values first, then set the formulas |
| `copy_range` from another file | It copies within the workbook; use `import_range` or `import_sheet` |
| A bigger font on a title row | The row keeps its height; auto-fit it with `resize_rows` and no `height` |
| Text written into a new workbook | Columns keep the default width, so long text is cut off by the next cell; size them with `resize_columns` (no `widthChars` auto-fits) |
| A workbook open in Excel | The in-place save fails with `OUTPUT_UNWRITABLE`; ask the user to close it |
| `cells compare` after a formatting session | `identical: true` is correct: it compares values and formula text only |

## References

| topic | read it when |
|-------|--------------|
| `aspose-cli docs cells/editing` | Choosing operations by task, batch order, charts, pivots, tables, protection, `--verify` |
| `aspose-cli docs cells/workbook-standards` | Delivering any workbook: widths, number formats, validation, print |
| `aspose-cli docs cells/design-system` | A designed workbook: dashboards, colors, charts, KPI cards |
| `aspose-cli docs cells/financial-models` | Building a formula-driven model |
| `aspose-cli docs cells/verification` | Verifying: the four tiers, render facts, `cells compare` |
| `aspose-cli docs cells/preview` | Showing a workbook in the live viewer |
| `aspose-cli docs cells/troubleshooting` | A Cells error or warning, CSV imports, evaluation-mode warning sheets |

Examples: [report from CSV](examples/report-from-csv/README.md),
[recalculate and verify](examples/recalc-and-verify/README.md),
[edit an existing file safely](examples/edit-existing-safely/README.md),
[sales dashboard](examples/sales-dashboard/README.md).
