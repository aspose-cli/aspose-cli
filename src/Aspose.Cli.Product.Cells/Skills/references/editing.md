# Cells edit operations

Batches, `--dry-run`, `--best-effort`, ids, `--in-place`/`--out`, backups and
secrets work as in every product: `aspose-cli docs editing`. The fields, types,
defaults and allowed values of each operation come from the generated schema:

```
aspose-cli schema v2/cells/ops --operation insert_chart
```

This page names the operations by task and adds what the schema cannot state:
Cells semantics, ordering rules and recipes.

## Operations by task

`aspose-cli capabilities cells edit` lists the operations this build accepts.

| task | operations |
|------|------------|
| Cell data | `set_values` writes a matrix; `set_formula` fills a formula over a range; `clear_range`; `copy_range` within the workbook; `replace_text` replaces text in text values, never in formulas or numbers |
| Other workbooks | `import_range` copies a range from another file; `import_sheet` copies a whole sheet |
| Formatting | `format_range` sets only the style fields given; `set_borders` |
| Rows and columns | `insert_rows`, `delete_rows`, `insert_columns`, `delete_columns`, `resize_rows`, `resize_columns`, `merge_cells`, `unmerge_cells`, `group_rows`, `ungroup_rows`, `group_columns`, `ungroup_columns`, `freeze_panes`, `unfreeze_panes` |
| Sheets | `insert_sheet`, `rename_sheet`, `delete_sheet`, `move_sheet`, `set_sheet_visibility`, `set_active_sheet` |
| Workbook look | `set_default_font`, `set_tab_color`, `clear_tab_color`, `set_sheet_view` |
| Charts and sparklines | `insert_chart`, `set_chart`, `delete_chart`, `add_sparkline`; `clear_range` with `what` `all` removes the sparklines in its range |
| Pivot tables | `insert_pivot`, `refresh_pivot` |
| Tables, filtering, sorting | `define_table`, `resize_table`, `set_autofilter`, `clear_autofilter`, `sort_range`, `delete_duplicates` |
| Input rules | `set_validation`, `clear_validation`, `add_conditional_format`, `clear_conditional_formats` |
| Names, notes, links | `define_name`, `delete_name`, `add_comment`, `set_comment`, `delete_comment`, `set_hyperlink`, `delete_hyperlink` |
| Pictures | `insert_image` |
| Print and PDF | `set_page_setup`, `set_print_area`, `clear_print_area` |
| Protection | `protect_sheet`, `unprotect_sheet`, `protect_workbook`, `unprotect_workbook` |

## Common operations

The field names of these operations are the ones most often guessed wrong:

```json
{ "ops": [
  { "op": "rename_sheet", "sheet": "Sheet1", "name": "Data" },
  { "op": "insert_sheet", "name": "Summary", "at": 1 },
  { "op": "move_sheet", "sheet": "Data", "to": 1 },
  { "op": "sort_range", "sheet": "Data", "range": "A1:D100", "hasHeader": true,
    "by": [ { "column": "C", "order": "desc" }, { "column": "A" } ] },
  { "op": "insert_rows", "sheet": "Data", "at": 2, "count": 3 },
  { "op": "resize_rows", "sheet": "Data", "first": 1, "last": 1, "height": 24 },
  { "op": "resize_columns", "sheet": "Data", "first": "A", "last": "D" },
  { "op": "set_sheet_view", "sheet": "Data", "gridlines": false, "zoomPercent": 90 }
] }
```

## Addressing

- An operation's `sheet` defaults to the active sheet; name it every time.
- Range fields are unqualified A1 on the operation's sheet. Only the fields
  whose schema description says so (`copy_range.from`/`to`,
  `import_range.from`/`to`, `insert_pivot.sourceRange`, the chart and
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
`delete_duplicates`, `insert_pivot` and `refresh_pivot` calculate the batch's
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

- `set_values` stores each JSON string as text, whatever it looks like:
  `"1200"`, `"TRUE"`, `"2026-04-03"` and `"=DATE(2026,4,3)"` all read back
  `"t": "string"`. Send numbers and Booleans as JSON numbers and Booleans, and
  formulas with `set_formula`, or `--set`, where a value that starts with `=`
  is a formula. Dates: `aspose-cli docs cells/workbook-standards`, Number
  formats.
- `set_formula` writes the formula of the range's top-left cell and fills it:
  relative references shift per cell, `$` references stay. Every cell
  evaluates like Excel 365: array arithmetic inside a function covers the
  whole ranges, as in `=MAX(B2*$F$2:$F$8-$G$2:$G$8)`, with no need for
  `SUMPRODUCT`. Set a formula whose result is an array (`FILTER`, `UNIQUE`,
  `B2:B9*2`) on one cell: it spills into the cells beside and below it, or is
  `#SPILL!` (a `FORMULA_ERRORS` verification issue) when they are not empty.
  Each cell gets its own dynamic-array formula, which Excel before 365 and
  LibreOffice show as a legacy array formula `{=...}`. Fill a range only with
  a formula that returns one value: a spilling formula filled over a range is
  `#SPILL!` in every cell but the last.
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
  { "op": "import_sheet", "path": "us_raw.csv", "name": "US", "at": 2 },
  { "op": "set_formula", "sheet": "Report", "range": "J2:J8", "formula": "=SUM(F2:I2)" }
] }
```

- `import_range.from` without a sheet lies on the source's first sheet.
  `content` `values` (default) writes values, with formulas replaced by their
  results, and number formats; `all` keeps formulas as written and formatting,
  like `copy_range`.
- `import_sheet.sourceSheet` names the source sheet, the source's first sheet
  when omitted (`sheet` is not a field of `import_sheet`), and the new sheet
  takes its name unless `name` is given. A name the workbook already uses is
  refused, as by `insert_sheet` and `rename_sheet`; `at` is 1-based, appended
  when omitted. The sheet keeps its charts, tables, pivots,
  comments, validation and conditional formats.
- Imported formulas keep their sheet names: a reference to another source
  sheet points at the sheet of that name in this workbook, or becomes `#REF!`.
  Import the referenced sheets first, or import `values`. In `import_range`, a
  reference that names the copied sheet itself (`='Rates'!B2` copied from
  `Rates`) shifts onto the destination sheet like one that names no sheet,
  unless the source also has a sheet of the destination sheet's name: that
  sheet's references take the destination, and those to the copied sheet follow
  the rule for another source sheet. Defined names that
  imported formulas use come along unless this workbook defines them;
  `import_sheet` refuses a source whose workbook-level name this workbook
  defines differently.
- A reference back to the edited workbook itself, such as
  `='[budget.xlsx]Budget'!$D$6` in a source beside `budget.xlsx`, becomes an
  internal reference (`='Budget'!$D$6`), as `set_formula` makes it.
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
- An encrypted source needs `sourcePasswordEnv`. Sources open once per batch; the
  edited file can be a source, read as it is on disk before the edit.

## Charts

    { "op": "insert_chart", "sheet": "Data", "kind": "column",
      "dataRange": "A1:C5", "at": "E2:L18", "title": "Quarterly Sales" }

- `insert_chart` applies a modern look by itself: no plot-area fill, no outer
  border, a bottom legend, slim column and bar gaps, light value-axis
  gridlines and a distinct color per series (per slice of a pie): the
  workbook theme's six accents, then darker and lighter variants. A
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
- `set_chart` and `delete_chart` address a chart by `index` or `name` as
  `cells inspect --detail charts` lists them. Chart names need not be unique;
  address a shared name by `index`. An update leaves the value axis as it is.
- Chart fonts, axis bounds and category or percentage labels are not
  expressible.

## Pivot tables

    { "op": "insert_pivot", "sheet": "Pivot",
      "sourceRange": "Data!A1:D100", "at": "A1",
      "rows": ["Region"], "columns": ["Quarter"],
      "values": [ { "field": "Sales", "numberFormat": "#,##0" },
                  { "field": "Units", "function": "average" } ] }

- Fields are the header names of the source range's first row.
- Put a pivot on its own sheet (`insert_sheet` in the same batch) so it cannot
  collide with data. An omitted `name` becomes `PivotTableN`, the first number
  the workbook does not use.
- Give each value field a `numberFormat`; without one the aggregates render
  unformatted.
- Pivots do not follow source changes: add `refresh_pivot` after editing the
  source. Refresh reads the same source range, so rows added below it stay
  out, even rows a `resize_table` took into the table (see Tables below).
  Refresh keeps the pivot's formatting and the sheet's column widths.
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
- Two or more value fields are columns, as in Excel, after any column fields.
  With a column field, the grand-total column of each value field reads
  `Total <caption>` in every language; no pivot caption holds that word.
- `read` the target area to read the aggregated numbers.

## Conditional formatting

A rule formula is written for the range's top-left cell and shifts per cell
like `set_formula`. Anchoring the column with `$` and leaving the row relative
highlights whole rows from one status column:

```json
{ "ops": [
  { "op": "add_conditional_format", "range": "A2:F100",
    "rule": { "kind": "formula", "value1": "=$F2=\"OVERDUE\"" },
    "matchStyle": { "fillColor": "#FFC7CE" } }
] }
```

Row 2 tests `$F2`, row 3 tests `$F3`. Without the `$` the tested column
drifts right with the rule; with `$F$2` every row tests one cell and the rule
still applies cleanly, answering a different question. `cell-value` bounds
also accept an `=`-led formula: `"value1": "=$B2"` over `A2:A4` flags each
value that exceeds its own row's target in `B`. When two fill rules match one
cell, the later rule's fill wins.

Conditional formats cannot express text-contains, date-period or
above-average rules, rule priority, stop-if-true, or custom icon thresholds
or order.

## Sparklines

    { "op": "add_sparkline", "dataRange": "B2:E10", "at": "F2:F10",
      "kind": "line", "color": "#1F4E79" }

The `at` range's cell count decides the fan-out: one sparkline per data row, or
per data column. A sparkline cell must lie inside the sheet's used range to
show in renders and reads; give the sparkline column a header.

## Tables, validation, links and pictures

- `define_table` brings its own filter dropdowns and banding: never add
  `set_autofilter` or a painted header over the same range. An unknown style
  name fails with `STYLE_NOT_FOUND`.
- Values an edit writes just below a table stay outside it. Take them in
  with `resize_table` and the table's new range, such as `A1:D25` for a
  table at `A1:D20`: it keeps the header row and the columns, so the range
  starts at the table's top-left cell and ends in its last column, or the
  operation fails with `OPS_INVALID`. A totals row moves down to the new last
  row, which must be empty; a table with a totals row only grows.
  `define_table` again fails, because the name is taken.
- Only a calculated column fills the new rows: a formula the table stores for
  a whole column, as Excel makes when a formula is typed into a table column.
  No operation makes one. A formula `set_formula` writes over a column's
  cells, such as `=D2*E2` over `F2:F201`, is a cell formula, so that column
  stays empty in the new rows, and the new rows take no column's number
  format either. Write both in the same batch, such as `set_formula` with
  `=D202*E202` over `F202:F206` and `format_range` with the `numberFormat`
  that `read --content full` shows for the rows above.
- What names its cells by address keeps its range: a pivot's `sourceRange`
  (`Data!A1:F201`), a formula's fixed range (`SUMIFS($F$2:$F$201,...)`) and a
  print area. After the resize, `refresh_pivot` still reads the old range and
  warns `OP_NO_EFFECT` when nothing in it changed. A formula written with
  structured references, such as `=SUMIFS(Sales[Amount],Sales[Product],A2)`,
  follows the table; rewrite a fixed range with `set_formula`, in that form
  or over the new rows. No operation changes a pivot's source or deletes a
  pivot. To take the new rows in, rebuild a pivot that has its own sheet in
  one batch: `delete_sheet` that sheet, `insert_sheet` it again and
  `insert_pivot` over the new range with the fields and value formats
  `inspect --detail pivots` lists.
- Validation constrains a person typing in Excel, not the CLI: an ops batch or
  `--set` writes any value into a validated cell and exits 0. Verify your own
  writes.
- `set_hyperlink.target` is a cell or range, sheet-qualified when it lies on
  another sheet; defined names are not accepted.
- `set_validation` replaces the validation its range had and `set_hyperlink`
  the link that covered its cell, so repeating one leaves one. `add_comment`
  on a cell that already has a comment adds its text as a new paragraph;
  `set_comment` replaces the text of a comment the cell has, and on a cell
  without one fails with `COMMENT_NOT_FOUND`: add it with `add_comment`.
- `insert_image` removes from an SVG every reference to anything outside it,
  keeping `#id` fragments and base64 `data:` images whose bytes are PNG, JPEG
  or GIF, and warns `REMOTE_RESOURCES_BLOCKED`; an SVG that does not parse as
  XML is refused with `OPS_INVALID`, and any SVGZ with `FEATURE_UNSUPPORTED`.
  Embed the SVG's images or supply a raster image.
- `protect_sheet` locks every cell with action-level `allow` exceptions,
  except the cells `format_range` set `"locked": false`: format a form's
  input cells that way first, so Excel lets users fill them in.
- Protection guides Excel, not the CLI: an edit changes a protected sheet or a
  protected workbook structure without its password and warns
  `PROTECTION_NOT_ENFORCED`, except when it only writes or clears the contents
  of unlocked cells, as Excel allows. `cells inspect` reports `protected` and
  `passwordProtected` per sheet and `structureProtected` for the workbook.
  Confirm such a change is authorized, or run `unprotect_sheet` with its
  `passwordEnv` first; unprotecting checks the password and refuses a wrong
  one with `PASSWORD_INVALID`, a missing one with `PASSWORD_REQUIRED`.
  Protecting again what is protected warns too: `protect_sheet` replaces the
  allowed actions but keeps a sheet's existing password, and `protect_workbook`
  refuses a structure protected with a password (`OPS_INVALID`); unprotect
  first to change a password.

## Sheets and view

- `set_active_sheet` saves the sheet a workbook opens on; the browser preview
  honors it.
- `set_sheet_view` gridlines, zoomPercent and headings affect Excel and the live
  preview, never `render` or PDF output. Draw `set_borders` when a grid must
  appear in a render.
- `set_borders` draws the outline and inner lines of a range; a read reports
  each cell's lines in its style's `borders`, one `{side, lineStyle, color}`
  per side, and `format_range` writes the same `style.borders` cell by cell.
- `set_page_setup` writes the center header and footer (`headerCenter`,
  `footerCenter`); `cells inspect --detail layout` reads all three sections.
- `set_print_area` replaces the sheet's print area with what it gives: title
  rows or columns it omits are cleared, so repeat `titleRows` in every call
  that should keep them.

## Output formats and encryption

`cells edit` and `cells create` write the workbook formats that `aspose-cli
capabilities cells` lists as both loaded and converted; any other output, such
as `.pdf` or `.md`, is refused with `OPTION_INVALID` and a hint naming the
`cells convert` to run after; `cells convert` writes html with its images embedded. Encryption
follows the shared rule (`aspose-cli docs editing`). Of the Cells outputs,
xlsx, xltx, xlsm, xltm, xlsb, xls and ods can be encrypted: an encrypted
workbook keeps its password in them, and `--encrypt-env` changes it. Every
other output, csv, tsv, html, mhtml, pdf, xps, json and md included, cannot:
it drops the encryption with an `ENCRYPTION_REMOVED` warning, and
`--encrypt-env` on it is `OPTION_INVALID`. A single-sheet output of a multi-sheet workbook reports
`SHEETS_DROPPED`. `cells create` and `cells convert` follow the same rules.

## Reading the outcome

Each applied operation's `itemsAffected` counts the objects it changed, and
`targets` names them as `<unit>/<address>`: `cell/Sales!B2` (more than 100
cells read `cell/all`), `sheet/Sales`, `row/Sales!5`, `column/Sales!C`,
`merge/Sales!A1:C1`, `comment/Sales!B2`, `hyperlink/Sales!B2`,
`chart/Sales/1`, `image/Sales/1`, `validation/Sales!B2:B9`,
`conditional-format/Sales/1`, `sparkline-group/Sales/1`, `table/SalesTable`,
`pivot/Sales/PivotTable1`, `autofilter/Sales`, `freeze-panes/Sales`,
`page-setup/Sales`, `print-area/Sales`, `protection/Sales`, `sheet-view/Sales`,
`tab-color/Sales`, `name/TaxRate`, `workbook/default-font`,
`workbook/active-sheet` or `workbook/protection`. A sheet
or object name keeps its spelling, apostrophes and `!` included, except that
`/`, `%`, control characters and leading or trailing spaces are written as
`%XX` of their UTF-8 bytes: a sheet named ` Q3 100%` is `sheet/%20Q3 100%25`.
Before the `!` of a cell, row, column, merge, comment or hyperlink address a
`!` in the sheet name is `%21` too, so the first `!` ends the sheet:
`cell/a%21b!A1` is cell A1 of the sheet `a!b`.
Inserting or deleting rows or columns counts each line it added or removed
and each object anchored on the sheet that moved or resized with them, such
as a chart or a comment; the cells and formulas that only shift with their
lines are not counted.

## Built-in verification

`cells edit --verify` compares a temporary snapshot of the input with the
staged output before publishing and reports `verification`
(`aspose-cli schema v2/cells/edit-result`). `checkedOps` lists the operations
whose changes the comparison read back: those that write cell values or
formulas, or add, remove or rename a sheet, among the first 16 targets of the
batch. An operation that names no sheet is checked on the sheet that was
active when the batch started. Formula errors, sheet loss, grid truncation,
imported formulas whose results changed or an incomplete scan keep the edited
file and exit 8 with `verification.ok: false`; each issue's code is one that
`aspose-cli capabilities cells` lists. `FORMULA_ERRORS` is the common one:
`#NAME?` usually means a localized or misspelled function name, which
`FORMULA_FUNCTION_UNKNOWN` names with or without `--verify`.

The report sorts the changed cells into `directChanges`, inside an operation's
target, `formulaResultChanges`, formulas whose text stayed the same while their
result changed, and `otherChanges`. After changing input values, read
`verification.formulaResultChanges` for the totals and other results the change
moved, each with its `left` (before) and `right` (after) side, and report those
instead of working them out.

A formula error whose input cell had the same formula and the same error
carries `preexisting: true`; it still fails verification, so fix it or tell
the user it predates the edit. An edit that changes more than 1000 cells,
such as a large `sort_range`, lists only the first 1000 changes with
`truncated: true`; the checks still cover every cell, so it passes when they
find nothing. A reopen error, budget failure or cancellation aborts
publication. `--verify` needs the final recalculation, so it cannot accompany
`--dry-run` or `--no-recalc`.
