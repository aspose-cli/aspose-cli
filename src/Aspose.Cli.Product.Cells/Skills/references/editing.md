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
| Cell data | `set_values` writes a matrix; `set_formula` fills a formula over a range; `clear_range`; `copy_range` within the workbook |
| Other workbooks | `import_range` copies a range from another file; `import_sheet` copies a whole sheet |
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

## Common operations

The field names of these operations are the ones most often guessed wrong:

```json
{ "ops": [
  { "op": "rename_sheet", "sheet": "Sheet1", "to": "Data" },
  { "op": "add_sheet", "name": "Summary", "position": 0 },
  { "op": "move_sheet", "sheet": "Data", "position": 0 },
  { "op": "sort_range", "sheet": "Data", "range": "A1:D100", "hasHeader": true,
    "by": [ { "column": "C", "order": "desc" }, { "column": "A" } ] },
  { "op": "insert_rows", "sheet": "Data", "at": 2, "count": 3 },
  { "op": "resize_rows", "sheet": "Data", "from": 1, "to": 1, "height": 24 },
  { "op": "resize_columns", "sheet": "Data", "from": "A", "to": "D" },
  { "op": "set_sheet_view", "sheet": "Data", "gridlines": false, "zoom": 90 }
] }
```

- `rename_sheet` takes the new name in `to`; `add_sheet` names the new sheet in
  `name`. `position` is the 0-based place in the tab order.
- `sort_range.by` lists keys primary first; `order` is `asc` (default) or
  `desc`.
- Row spans are 1-based numbers and column spans letters, in `from` and an
  optional `to`; `insert_rows` and its kin take `at` and `count`. An omitted
  `height` or `width` auto-fits.
- `set_sheet_view` takes at least one of `gridlines`, `zoom` and `headings`.

## Addressing

- An operation's `sheet` defaults to the active sheet. Name it every time: in
  evaluation mode an active warning sheet gives way to the first other sheet
  (`EVALUATION_SHEET_SKIPPED`), not to the sheet you made active.
- Range fields are unqualified A1 on the operation's sheet. Only the fields
  whose schema description says so (`copy_range.from`/`to`,
  `import_range.from`/`to`, `create_pivot.sourceRange`, the chart and
  sparkline `dataRange`, `set_hyperlink.target`) take another sheet. Write
  the sheet name as it appears (`P&L!A1:B9`, `'My Sheet'!A1`); the CLI quotes
  it for the engine.
- Rows are 1-based numbers and columns are letters, as in A1. Whole rows and
  columns (`A:A`, `1:3`) are not ranges.
- `--set "SHEET!CELL=VALUE"` appends single-cell writes to the same batch,
  after the `--ops` document; a value starting with `=` is a formula.

## Recalculation

The whole workbook calculates once after the complete batch, including
`--set` writes; `--no-recalc` skips it. Auto-fit resizing, `sort_range`,
`remove_duplicates`, `create_pivot` and `refresh_pivot` calculate the batch's
earlier edits first, so they see current formula results. Reads show what
Excel shows on opening: a workbook that asks to be calculated when opened (as
openpyxl, pandas and many exporters write them, with no stored results) is
calculated first, and `FORMULAS_CALCULATED_ON_OPEN` says how many results differ
from the stored ones. Any other workbook reads its stored results, so treat a
`--no-recalc` edit as stale until an edit recalculates it.

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

## Combining workbooks

`copy_range` works inside one workbook; a `[book.xlsx]Sheet!A1` source is not
a sheet name. Bring data from another file with `import_range` or
`import_sheet`: `path` names the source file (any format `cells` reads,
including CSV and TSV), relative to the working directory, and the source is
only read. Merge several sources in one batch, then formulas over the merged
cells:

```json
{ "ops": [
  { "op": "import_range", "sheet": "Report", "path": "eu_raw.xlsx",
    "from": "Totals!A2:D8", "to": "F2" },
  { "op": "import_sheet", "path": "us_raw.csv", "name": "US", "position": 1 },
  { "op": "set_formula", "sheet": "Report", "range": "J2:J8", "formula": "=SUM(F2:I2)" }
] }
```

- `import_range.from` without a sheet lies on the source's first sheet.
  `content` `values` (default) writes values, with formulas replaced by their
  results, and number formats; `all` keeps formulas as written and formatting,
  like `copy_range`.
- `import_sheet` takes the source sheet from the operation's `sheet`, the
  source's first sheet when omitted, and names the new sheet after it unless
  `name` is given. A name the workbook already uses is refused; `position` is
  0-based, appended when omitted. The sheet keeps its charts, tables, pivots,
  comments, validation and conditional formats.
- Imported formulas keep their sheet names: a reference to another source
  sheet points at the sheet of that name in this workbook, or becomes `#REF!`.
  Import the referenced sheets first, or import `values`. Defined names that
  imported formulas use come along unless this workbook defines them;
  `import_sheet` refuses a source whose workbook-level name this workbook
  defines differently.
- A reference to a third workbook keeps its link and is not read through it, so
  recalculation uses the values the link cached. A link that caches no values
  shows `#REF!` in the source but reads as empty, usually 0, once imported;
  the import warns `EXTERNAL_LINK_CACHE_MISSING` with those cells, and
  `--verify` reports it as an issue. Replace them with `set_formula` or
  `set_values`.
- A formula you write that reads another workbook
  (`='C:\data\[fx.xlsx]Rates'!$B$2`) creates a link without cached values,
  since the CLI never opens the linked file: it stays `#REF!`, which `--verify`
  reports, until Excel updates the link. A link to a file in the folder the
  workbook was opened from is stored by file name alone
  (`='[fx.xlsx]Rates'!$B$2`), relative to the output's folder; the edit warns
  `EXTERNAL_LINK_RELATIVE`, so keep the linked file beside the output. To use
  the linked values, bring them in with `import_range` instead.
- An encrypted source needs `passwordEnv`. Sources open once per batch; the
  edited file can be a source, read as it is on disk before the edit.

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
- Captions follow `captions`: `en` keeps the engine's English ones (`Sum of X`,
  `Grand Total`, `Data`); `zh` writes Excel's Simplified Chinese ones
  (`求和项:X`, `计数项:X`, `平均值项:X`, `最大值项:X`, `最小值项:X`, `总计`,
  `行标签`, `列标签`, `值`). The default `auto` picks `zh` when a row, column
  or value field name contains a Han character, so other pivots keep the
  English captions. Refresh keeps them.
- A value field's `label` names it in either language, such as
  `{ "field": "不含税净额", "label": "净额合计" }`. A label that repeats a source
  header or another value field's caption, ignoring case, is refused, as Excel
  refuses it.
- With two or more value fields, the grand-total row of each value field reads
  `Total <caption>` in every language; no pivot caption holds that word.
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
- Protection guides Excel, not the CLI: an edit changes a protected sheet or a
  protected workbook structure without its password and warns
  `PROTECTION_NOT_ENFORCED`. `cells inspect` reports `protected` and
  `passwordProtected` per sheet and `structureProtected` for the workbook.
  Confirm such a change is authorized, or run `unprotect_sheet` with its
  `passwordEnv` first; unprotecting checks the password. Protecting again
  what is protected warns too: `protect_sheet` replaces the allowed actions but
  keeps a sheet's existing password, and `protect_workbook` fails on a
  structure protected with a password; unprotect first to change a password.

## Sheets and view

- `set_active_sheet` saves the sheet a workbook opens on; the browser preview
  honors it. An evaluation save activates its own "Evaluation Warning" sheet
  instead (`EVALUATION_SHEET_ADDED`).
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

`cells edit --verify` compares a temporary snapshot of the input with the staged
output before publishing and reports `verification` with `directChanges`,
`formulaResultChanges`, `otherChanges`, `formulaErrors` and `issues`. Formula
errors, sheet loss, grid truncation, imported formulas whose results changed
(`EXTERNAL_LINK_CACHE_MISSING`) or an incomplete scan keep the edited file
and exit 8 with `verification.ok: false`. Issue codes: `FORMULA_ERRORS` (the
edited workbook has formula errors; `location` is the cell when there is one),
`DIFF_TRUNCATED` (more than 1000 changed cells, so the change lists are
incomplete), `LIST_TRUNCATED` (`formulaErrors` holds only the first 1000), or
the code of a completeness warning such as `SHEETS_DROPPED`; `capabilities`
lists every code. A formula error whose input cell had the same formula and
the same error carries `preexisting: true`; it still fails verification, so
fix it or tell the user it predates the edit. A reopen error, budget failure or
cancellation aborts publication. `--verify` needs the final recalculation, so
it cannot accompany `--dry-run` or `--no-recalc`.
