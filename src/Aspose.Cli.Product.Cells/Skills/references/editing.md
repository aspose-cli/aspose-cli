# Cells edit operations

Batches, `--dry-run`, `--best-effort`, ids, `--in-place`/`--out`, backups and
secrets work as in every product: `aspose-cli docs editing`. The fields, types,
defaults and allowed values of each operation come from the generated schema:

```
aspose-cli schema v2/cells/ops --operation create_chart
```

This page names the operations by task and adds what the schema cannot state:
Cells semantics, ordering rules and recipes.

## Operations by task

| task | operations |
|------|------------|
| Cell data | `set_values` writes a matrix; `set_formula` fills a formula over a range; `clear_range`; `copy_range` |
| Formatting | `format_range` sets only the style fields given; `set_borders` |
| Rows and columns | `insert_rows`, `delete_rows`, `insert_columns`, `delete_columns`, `resize_rows`, `resize_columns`, `merge_cells`, `unmerge_cells`, `group_rows`, `ungroup_rows`, `group_columns`, `ungroup_columns`, `freeze_panes` |
| Sheets | `add_sheet`, `rename_sheet`, `delete_sheet`, `move_sheet`, `set_sheet_visibility`, `set_active_sheet` |
| Workbook look | `set_default_font`, `set_tab_color`, `set_sheet_view` |
| Charts and sparklines | `create_chart`, `update_chart`, `delete_chart`, `add_sparkline` |
| Pivot tables | `create_pivot`, `refresh_pivot` |
| Tables, filtering, sorting | `create_table`, `set_autofilter`, `sort_range`, `remove_duplicates` |
| Input rules | `set_validation`, `clear_validation`, `add_conditional_format`, `clear_conditional_formats` |
| Names, notes, links | `define_name`, `delete_name`, `add_comment`, `edit_comment`, `delete_comment`, `set_hyperlink`, `remove_hyperlink` |
| Pictures | `insert_image` |
| Print and PDF | `set_page_setup`, `set_print_area` |
| Protection | `protect_sheet`, `unprotect_sheet`, `protect_workbook`, `unprotect_workbook` |

## Addressing

- An operation's `sheet` defaults to the active sheet. Name it every time: an
  evaluation save makes its warning sheet the active one.
- Range fields are unqualified A1 on the operation's sheet. Only the fields
  whose schema description says so (`copy_range.from`/`to`,
  `create_pivot.sourceRange`, the chart and sparkline `dataRange`,
  `set_hyperlink.target`) take another sheet. Write the sheet name as it
  appears (`P&L!A1:B9`, `'My Sheet'!A1`); the CLI quotes it for the engine.
- Rows are 1-based numbers and columns are letters, as in A1. Whole rows and
  columns (`A:A`, `1:3`) are not ranges.
- `--set "SHEET!CELL=VALUE"` appends single-cell writes to the same batch,
  after the `--ops` document; a value starting with `=` is a formula.

## Recalculation

The whole workbook calculates once after the complete batch, including
`--set` writes; `--no-recalc` skips it. Auto-fit resizing, `sort_range`,
`remove_duplicates`, `create_pivot` and `refresh_pivot` calculate the batch's
earlier edits first, so they see current formula results. Queries read stored
results and never recalculate: treat an imported workbook or a `--no-recalc`
edit as stale until an edit recalculates it.

Because operations apply in order and calculate at the end, a whole model fits
one batch: a `define_name` early in the batch resolves in formulas set later,
across sheets.

## Batch order

One `edit` per logical change-set, in this order:

1. `set_default_font` first in a new workbook. Column widths are measured in
   the Normal font, so a later font change rescales every width. Arial 10 to
   Calibri 11 keeps column edges pixel-identical; any other font rescales, so
   check widths after it.
2. Values, while ranges are still plain data. Never `sort_range` over live
   formulas: relative references scramble. Sort first, then set formulas.
3. Formulas, then formats, then structure.
4. Charts and pivots, which read the data above.
5. Widths and auto-fits, which measure the formatted results.

Run `--dry-run` first for a large or destructive batch (`delete_sheet`,
`delete_rows`).

## Values and formulas

- Numbers stay numbers: send `1200`, never `"1200"`. A string such as
  `"2026-04-03"` stays text and ignores a date format; write dates as
  `=DATE(2026,4,3)` (`aspose-cli docs cells/workbook-standards`).
- `set_formula` writes the formula of the range's top-left cell and fills it:
  relative references shift per cell, `$` references stay.
- Inserting or deleting rows and columns updates formula references as in
  Excel.

## Charts

    { "op": "create_chart", "sheet": "Data", "type": "column",
      "dataRange": "A1:C5", "at": "E2:L18", "title": "Quarterly Sales" }

- `create_chart` applies a modern look by itself: white plot area, no outer
  border, a bottom legend, slim column and bar gaps, light value-axis
  gridlines and the `#1F4E79`/`#2E75B6`/`#9DC3E6`/`#D9D9D9` series palette. A
  column, bar or area chart whose values are all positive starts its value
  axis at zero. Set `legend`, `axisTitles`, `seriesColors` or `dataLabels`
  only to deviate.
- `dataRange` is one contiguous block including headers; a multi-area
  reference is refused. To chart non-adjacent rows, mirror them into a helper
  block with formulas (`aspose-cli docs cells/workbook-standards`, Limits).
- A sheet-qualified `dataRange` puts the chart on a dashboard sheet and its
  data on a data sheet; `at` is always on the operation's sheet. About 8
  columns by 15 rows reads well.
- A bar chart plots the first source row at the bottom: list a ranking's rows
  ascending so it reads largest-first from the top.
- `update_chart` and `delete_chart` address a chart by `index` or `name` as
  `cells inspect --detail charts` lists them. Chart names need not be unique;
  address a shared name by `index`. An update leaves the value axis as it is.
- Chart fonts, axis bounds and category or percentage labels are not
  expressible.

## Pivot tables

    { "op": "create_pivot", "sheet": "Pivot",
      "sourceRange": "Data!A1:D100", "at": "A1",
      "rows": ["Region"], "columns": ["Quarter"],
      "values": [ { "field": "Sales", "numberFormat": "#,##0" },
                  { "field": "Units", "function": "average" } ] }

- Fields are the header names of the source range's first row.
- Put a pivot on its own sheet (`add_sheet` in the same batch) so it cannot
  collide with data. An omitted `name` becomes `PivotTableN`, the first number
  the workbook does not use.
- Give each value field a `numberFormat`; without one the aggregates render
  unformatted.
- Pivots do not follow source changes: add `refresh_pivot` after editing the
  source. Refresh keeps the pivot's formatting and the sheet's column widths.
- `query range` the target area to read the aggregated numbers.

## Conditional formatting

A rule formula is written for the range's top-left cell and shifts per cell
like `set_formula`. Anchoring the column with `$` and leaving the row relative
highlights whole rows from one status column:

```json
{ "ops": [
  { "op": "add_conditional_format", "range": "A2:F100",
    "rule": { "kind": "formula", "value1": "=$F2=\"OVERDUE\"" },
    "style": { "bg": "#FFC7CE" } }
] }
```

Row 2 tests `$F2`, row 3 tests `$F3`. Without the `$` the tested column
drifts right with the rule; with `$F$2` every row tests one cell and the rule
still applies cleanly, answering a different question. `cellValue` bounds
also accept an `=`-led formula: `"value1": "=$B2"` over `A2:A4` flags each
value that exceeds its own row's target in `B`. When two fill rules match one
cell, the later rule's fill wins.

Conditional formats cannot express text-contains, date-period or
above-average rules, rule priority, stop-if-true, or custom icon thresholds.

## Sparklines

    { "op": "add_sparkline", "dataRange": "B2:E10", "location": "F2:F10",
      "type": "line", "color": "#1F4E79" }

The location's cell count decides the fan-out: one sparkline per data row, or
per data column. A sparkline cell must lie inside the sheet's used range to
show in renders and reads; give the location column a header.

## Tables, validation, links and pictures

- `create_table` brings its own filter dropdowns and banding: never add
  `set_autofilter` or a painted header over the same range. An unknown style
  name fails with `STYLE_NOT_FOUND`.
- Validation constrains a person typing in Excel, not the CLI: an ops batch or
  `--set` writes any value into a validated cell and exits 0. Verify your own
  writes.
- `set_hyperlink.target` is a cell or range, sheet-qualified when it lies on
  another sheet; defined names are not accepted.
- `insert_image` refuses an SVG that names a network address, and any SVGZ,
  with `FEATURE_UNSUPPORTED`, because the engine would fetch the address.
  Embed the SVG's images or supply a raster image.
- `protect_sheet` locks every cell with action-level `allow` exceptions; a
  sheet locked except its input cells is not expressible.

## Sheets and view

- `set_active_sheet` saves the sheet a workbook opens on; the browser preview
  honors it. An evaluation save activates its own "Evaluation Warning" sheet
  instead.
- `set_sheet_view` gridlines, zoom and headings affect Excel and the live
  preview, never `render` or PDF output. Draw `set_borders` when a grid must
  appear in a render.
- `set_page_setup` header and footer text fills the center section only.

## Output formats and encryption

`cells edit` writes xlsx, xlsm, xlsb, xls, ods, csv, tsv, html and mhtml; use
`cells convert` for other formats. HTML output embeds its images. Editing an
encrypted workbook keeps its password in xlsx, xlsm, xlsb, xls and ods output;
`--encrypt-env` changes it. Other outputs cannot be encrypted: they drop the
encryption with a `WORKBOOK_ENCRYPTION_REMOVED` warning, and `--encrypt-env`
on them is `OPTION_INVALID`. A single-sheet output of a multi-sheet workbook
reports `SHEETS_DROPPED`. `cells create` and `cells convert` follow the same
rules.

## Built-in verification

`cells edit --verify` compares a private snapshot of the input with the staged
output before publishing and reports `verification` with `directChanges`,
`formulaResultChanges`, `otherChanges`, `formulaErrors` and `issues`. Formula
errors, sheet loss, grid truncation or an incomplete scan keep the edited file
and exit 8 with `verification.ok: false`; a reopen error, budget failure or
cancellation aborts publication. `--verify` needs the final recalculation, so
it cannot accompany `--dry-run` or `--no-recalc`.
