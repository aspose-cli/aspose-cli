# The ops vocabulary

The machine-readable source of truth is `aspose-cli schema v2/cells/ops`.
This reference adds the semantics and recipes.

General rules:

- The envelope accepts optional `schema` and `ifMatch` fields plus ordered
  `ops`. Every op may have a unique `id`; omitted ids are assigned
  deterministically. Copy `source.fingerprint.sha256` from a current query
  into `ifMatch` or `--if-match` to reject stale edits.
- The `--ops` value is a path, `-` (stdin), or the ops JSON itself when it
  starts with `{` or `[` (inline — no temp file needed; a file name that
  starts with `[` takes a `./` prefix). For single cells there is also
  `--set "SHEET!CELL=VALUE"` (repeatable; `=`-led values are formulas;
  compiled into the same batch after the `--ops` document). Windows
  PowerShell strips inner quotes from inline JSON — escape them as `\"`,
  pipe via `--ops -`, or use `--set`.
- Ops apply **in order** and **atomically** by default — one failure leaves
  the file untouched. Operation failures report `index`; malformed JSON or
  envelope errors may have no operation index. Unknown fields, duplicate JSON
  members and duplicate operation IDs are rejected before publication.
- `--dry-run` applies and validates in memory without publishing output.
  `--best-effort` continues after engine operation failures and exits 8 when
  any fail. A mid-operation failure may retain partial effects; it does not
  roll back each failed operation.
- `sheet` on any op defaults to the active sheet. Range fields are
  **unqualified** A1 (`B2:D10`); the sheet comes from `sheet`. Only
  `copy_range.from/to`, `create_pivot.sourceRange`, the chart ops'
  `dataRange`, `add_sparkline.dataRange` and `set_hyperlink.target` accept
  sheet-qualified references (for cross-sheet work). Write the sheet name
  as it appears (`P&L!A1:B9`, `'My Sheet'!A1`); the CLI quotes it for the
  engine and reports `SHEET_NOT_FOUND` for a sheet that does not exist.
- Rows are 1-based numbers; columns are letters — exactly as in A1.
- Editing a user-supplied file? Add `--backup --verify` to the first in-place
  edit. The CLI creates `book.backup.xlsx` once and never overwrites it.
  Read the result's `verification`, review the file, then diff against the
  stable backup at the end of the session (`aspose-cli docs cells/verification`).

## Op index

Operations in this build, grouped for reference. The authoritative field list
for each op is `aspose-cli schema v2/cells/ops`; the recipes live in the sections
named below.

| op | does | section |
|----|------|---------|
| `set_values` | Write literal values as a matrix anchored at a range | Data |
| `set_formula` | Set a formula across a range with fill semantics | Data |
| `clear_range` | Clear a range's contents, formats or both | Data |
| `copy_range` | Copy values, formulas and formatting to an anchor cell | Data |
| `format_range` | Apply the given style fields, preserving the rest | Formatting |
| `merge_cells` | Merge a range into one cell | Structure |
| `unmerge_cells` | Split a merged range apart | Structure |
| `insert_rows` | Insert rows at a 1-based position | Structure |
| `delete_rows` | Delete rows from a 1-based position | Structure |
| `insert_columns` | Insert columns at a column letter | Structure |
| `delete_columns` | Delete columns from a column letter | Structure |
| `resize_rows` | Set row heights, or auto-fit | Structure |
| `resize_columns` | Set column widths, or auto-fit | Structure |
| `add_sheet` | Add a sheet, appended or at a position | Structure |
| `rename_sheet` | Rename a sheet | Structure |
| `delete_sheet` | Delete an explicitly named sheet | Structure |
| `set_sheet_visibility` | Hide or unhide a sheet | Structure |
| `set_active_sheet` | Choose the worksheet shown when the workbook opens | Structure |
| `move_sheet` | Move a sheet to a new tab position | Structure |
| `freeze_panes` | Freeze the rows above and columns left of a cell | Structure |
| `create_chart` | Create a chart from a data range, placed over cells | Charts |
| `create_pivot` | Create a pivot table from a source range | Pivot tables |
| `set_page_setup` | Set orientation, paper, scaling, margins, header/footer | Page layout |
| `set_print_area` | Set or clear the print area and repeating titles | Page layout |
| `insert_image` | Place an image at a cell, optionally sized | Images |
| `refresh_pivot` | Recalculate pivot tables after source data changes | Pivot tables |
| `create_table` | Turn a range into a native table with filters | Tables, filtering and sorting |
| `set_autofilter` | Add or remove the sheet's filter dropdowns | Tables, filtering and sorting |
| `sort_range` | Sort a range in place by one or more columns | Tables, filtering and sorting |
| `set_validation` | Restrict a range's input (list, number, date…) | Data validation |
| `define_name` | Create a workbook-scoped defined name | Defined names |
| `delete_name` | Remove a defined name | Defined names |
| `add_comment` | Add a comment to a cell | Comments |
| `edit_comment` | Replace a cell comment's text | Comments |
| `delete_comment` | Remove a cell's comment | Comments |
| `protect_sheet` | Lock a sheet's cells, with allowed exceptions | Protection |
| `unprotect_sheet` | Remove a sheet's protection | Protection |
| `group_rows` | Group rows into a collapsible outline | Structure |
| `ungroup_rows` | Remove row grouping | Structure |
| `group_columns` | Group columns into a collapsible outline | Structure |
| `ungroup_columns` | Remove column grouping | Structure |
| `clear_validation` | Remove validation from a range | Data validation |
| `remove_duplicates` | Drop duplicate rows in a range | Tables, filtering and sorting |
| `protect_workbook` | Lock the workbook structure (sheet add/move/delete) | Protection |
| `unprotect_workbook` | Remove workbook structure protection | Protection |
| `set_hyperlink` | Link a cell to a URL or an internal target | Hyperlinks |
| `remove_hyperlink` | Clear the link covering a cell | Hyperlinks |
| `update_chart` | Change an existing chart's title, data or type | Charts |
| `add_conditional_format` | Add a rule: cell value, color scale, data bar, duplicates, formula, top/bottom, icon set | Conditional formatting |
| `clear_conditional_formats` | Remove conditional formatting overlapping a range | Conditional formatting |
| `set_borders` | Draw borders on a range: outline, inside grid, single edges | Formatting |
| `set_default_font` | Set the workbook default (Normal) font — do it first | Workbook look |
| `set_tab_color` | Color a sheet tab, or remove its color | Workbook look |
| `set_sheet_view` | Sheet view: gridlines on/off, zoom, headings | Workbook look |
| `delete_chart` | Remove a chart from a sheet | Charts |
| `add_sparkline` | Draw tiny in-cell charts, one per data row or column | Sparklines |

## Data

| op | fields | notes |
|----|--------|-------|
| `set_values` | `range`, `values` | Single-cell range = top-left anchor for a matrix of any size; multi-cell range must match the matrix dimensions. `null` clears a cell. Numbers stay numbers — never send `"1200"` when you mean `1200`. |
| `set_formula` | `range`, `formula` | Formula is written for the range's **top-left** cell; relative references shift per cell (fill semantics), `$` references stay fixed. |
| `clear_range` | `range`, `what?` | `contents` (default), `formats`, `all`. |
| `copy_range` | `from`, `to` | Copies values, formulas and formatting. `to` is a single anchor cell. Both may be sheet-qualified: `{"from": "Data!A1:C10", "to": "Summary!B2"}`. |

Edits calculate the whole workbook once after the complete batch unless
`--no-recalc` is set, including changes appended with `--set`. Ops that
depend on formula results (auto-fit resizing, `sort_range`,
`remove_duplicates`, pivots) calculate the batch's earlier edits first.
Queries read stored results.

## Formatting

`format_range` applies only the style fields you set — everything else is
preserved. Fields: `font`, `size` (1–409 points; fractional sizes such as
10.5 are kept), `bold`, `italic`, `color` (#RRGGBB text color), `bg`
(#RRGGBB fill), `numberFormat` (Excel format code, e.g. `0.0%`,
`#,##0.00`, `yyyy-mm-dd`), `hAlign` (left/center/right), `vAlign`
(top/middle/bottom), `wrap`, `underline`, `strikethrough`, `indent`
(0–250; 0 removes the indent). A style must set at least one field.

Recipe — header row:

    { "op": "format_range", "range": "A1:F1",
      "style": { "bold": true, "bg": "#1F4E79", "color": "#FFFFFF" } }

Recipe — percentage column:

    { "op": "format_range", "range": "E2:E100", "style": { "numberFormat": "0.0%" } }

`set_borders` draws borders on a range without touching any other styling
(fills, fonts, number formats and borders you did not name are preserved).
Fields: `range`; `edges` — an array of `outline` (the 4 outer edges),
`inside` (the inner grid lines), `top`/`bottom`/`left`/`right`,
`horizontal`/`vertical` (one inner direction), or `all` (the full grid);
`style` — `hair`, `thin` (default), `medium`, `thick`, `double`, `dashed`,
`dotted`; `color` (#RRGGBB, default `#000000`). A single-cell range needs
at least one outer edge — inside-only edges have nothing to draw there.
`hair` is only distinguishable from `thin` in renders at 150+ dpi.

Recipe — a bordered table (light grid, heavier frame and header rule):

    { "op": "set_borders", "range": "A1:F20", "edges": ["all"], "color": "#D9D9D9" }
    { "op": "set_borders", "range": "A1:F1", "edges": ["bottom"], "style": "medium" }
    { "op": "set_borders", "range": "A1:F20", "edges": ["outline"], "style": "medium" }

## Structure

| op | fields |
|----|--------|
| `merge_cells` / `unmerge_cells` | `range` |
| `insert_rows` / `delete_rows` | `at` (1-based), `count?` (default 1) |
| `insert_columns` / `delete_columns` | `at` (letter), `count?` |
| `resize_rows` | `from`, `to?`, `height?` (points; omit to auto-fit) |
| `resize_columns` | `from`, `to?` (letters), `width?` (chars; omit to auto-fit) |
| `add_sheet` | `name`, `position?` (zero-based; appended when omitted) |
| `rename_sheet` | `sheet` (required, current name), `to` |
| `delete_sheet` | `sheet` (required, explicit) |
| `set_sheet_visibility` | `sheet` (required), `hidden` |
| `set_active_sheet` | `sheet` (required and visible) |
| `move_sheet` | `sheet` (required), `position` (zero-based; past the end moves to last) |
| `freeze_panes` | `cell` (`B2` freezes row 1 + column A; `A1` unfreezes) |
| `group_rows` / `ungroup_rows` | `from`, `to?` (1-based), `collapse?` (group only) |
| `group_columns` / `ungroup_columns` | `from`, `to?` (letters), `collapse?` (group only) |

Inserting or deleting rows/columns updates formula references, exactly as
in Excel. Grouping adds the collapsible outline (+/-) summaries.

Use `set_active_sheet` after building a dashboard so the saved workbook opens
on its highest-value sheet:

    { "op": "set_active_sheet", "sheet": "Dashboard" }

The target must be visible. The active-sheet choice is saved in the workbook
and is also honored by the Cells browser Preview. An evaluation-mode save
activates its own "Evaluation Warning" sheet instead.

## Charts

    { "op": "create_chart", "sheet": "Data", "type": "column",
      "dataRange": "A1:C5", "at": "E2:L18", "title": "Quarterly Sales" }

- `type`: column, bar, line, pie, scatter, area.
- `dataRange` includes the headers; series come from columns by default
  (`"seriesInRows": true` flips it).
- `dataRange` may be sheet-qualified (`"Data!A1:C5"`) to plot data from
  another sheet — the usual dashboard layout, a chart on one sheet sourcing
  a data sheet. `at` (placement) is always on the op's own `sheet`.
- `at` is the cell range the chart is placed over — size it generously
  (roughly 8 columns by 15 rows reads well).
- Verify visually: `aspose-cli cells render <file> --sheet Data --out chart.png`.

`create_chart` applies a modern default look on its own: white plot area
(no gray fill), no outer chart border, bottom legend, slim column/bar gaps
and the `#1F4E79`/`#2E75B6`/`#9DC3E6`/`#D9D9D9` series palette. A column,
bar or area chart whose values are all positive starts its value axis at
zero. Do not re-specify any of that — set the cosmetic fields only to
deviate from it.

Cosmetic fields, accepted by `create_chart` and `update_chart` alike:

| field | shape | notes |
|-------|-------|-------|
| `legend` | `{"visible": bool, "position": "right"/"bottom"/"top"/"left"}` | No `none` position — hide the legend with `"visible": false`. |
| `axisTitles` | `{"category": "...", "value": "..."}` | Rejected on a `pie` chart — it has no axes. |
| `seriesColors` | `["#RRGGBB", ...]` | Applied in series order; a pie's slices count as the series; extra colors beyond the count are ignored. |
| `dataLabels` | `{"visible": bool, "format": "#,##0"}` | `format` is a number format code applied to the value labels. |

A column chart with the full set:

    { "op": "create_chart", "sheet": "Data", "type": "column",
      "dataRange": "A1:C5", "at": "E2:L18", "title": "Quarterly Sales",
      "legend": { "position": "bottom" },
      "axisTitles": { "category": "Region", "value": "Sales (USD)" },
      "seriesColors": ["#1F4E79", "#2E75B6"],
      "dataLabels": { "visible": true, "format": "#,##0" } }

Update an existing chart with `update_chart` — identify it by `index`
(zero-based) or `name`, then set any of `title`, `dataRange`, `type`,
`seriesInRows` or the cosmetic fields above. `seriesInRows` is accepted only
with `dataRange`: orientation is applied when the chart's data range is reset.
An update leaves the value axis as the chart already has it:

    { "op": "update_chart", "sheet": "Data", "index": 0,
      "title": "Revised", "type": "bar" }

Remove a chart with `delete_chart`, addressed the same way (exactly one of
`index` or `name`):

    { "op": "delete_chart", "sheet": "Data", "index": 0 }

## Pivot tables

    { "op": "create_pivot", "sheet": "Pivot",
      "sourceRange": "Data!A1:D100", "at": "A1",
      "rows": ["Region"], "columns": ["Quarter"],
      "values": [ { "field": "Sales" }, { "field": "Units", "function": "average" } ] }

- Fields are referenced by **header name** from the source range's first row.
- `name` must be unique on its sheet; when omitted the pivot is named
  `PivotTableN` with the first number no pivot in the workbook uses.
- `function`: sum (default), count, average, max, min.
- `values[].numberFormat` formats the aggregated numbers, e.g.
  `{ "field": "Sales", "function": "sum", "numberFormat": "#,##0" }` —
  without it pivot values render as naked unformatted numbers.
- The pivot is calculated on creation; `query range` the target area to see the
  aggregated numbers.
- Put pivots on their own sheet (`add_sheet` first) to avoid collisions.
- After changing source data, `{ "op": "refresh_pivot", "sheet": "Pivot" }`
  recalculates it (omit `name` to refresh every pivot on the sheet).
  Creation and refresh calculate source formulas before refreshing the pivot
  cache; an existing pivot retains its formatting and worksheet column widths.

## Page layout (print & PDF)

Get a report ready to print or export to PDF. Only the fields you set are
applied; the rest of the page setup is preserved.

| op | fields |
|----|--------|
| `set_page_setup` | `orientation` (portrait/landscape), `paperSize` (letter/legal/a3/a4/a5/tabloid), `fitToWidth?`/`fitToHeight?` (pages; 0 = auto), `scale?` (10–400), `margins?` (inches: top/bottom/left/right/header/footer), `header?`/`footer?` (center text; Excel codes `&P` page, `&N` pages, `&D` date) |
| `set_print_area` | `range?`, `titleRows?` (`1:2` or `1`), `titleColumns?` (`A:B` or `A`); titles alone keep the current print area, and an op with no fields clears it |

## Tables, filtering and sorting

| op | fields | notes |
|----|--------|-------|
| `create_table` | `range` (includes headers), `name?`, `style?` (`TableStyleMedium2`), `totalsRow?` | A native table with its own filter dropdowns. A `name` follows Excel's rules and is unique among the workbook's tables and defined names; one that reads as a cell reference (`T1`, `R1C1`), contains a space or other punctuation, or is taken fails as `OPS_INVALID` before anything changes. `style` is a built-in name (`TableStyleLight1`–`21`, `TableStyleMedium1`–`28`, `TableStyleDark1`–`11`) or a custom style the workbook defines; any other name is rejected. Do **not** also `set_autofilter` the same range. |
| `set_autofilter` | `range`, `off?` | `{"off": true}` removes the sheet filter. |
| `sort_range` | `range`, `by`, `hasHeader?` | `by` is `[{ "column": "B", "order": "desc" }]` (asc default); sorts in place by one or more columns. |
| `remove_duplicates` | `range`, `columns?`, `hasHeader?` | Drops duplicate rows. `columns` (letters) restricts the comparison to a subset; omit to compare all columns. |

## Data validation

    { "op": "set_validation", "range": "B2:B100", "type": "list",
      "listItems": ["Low", "Medium", "High"], "inputMessage": "Pick one" }

    { "op": "set_validation", "range": "C2:C100", "type": "wholeNumber",
      "operator": "between", "value1": "0", "value2": "1000000",
      "errorMessage": "Enter a positive amount" }

- `type`: `list` (give `listItems` **or** a `listSource` range), `wholeNumber`,
  `decimal`, `date`, `textLength` (give `operator` + `value1`, plus `value2`
  for `between`/`notBetween`), or `custom` (`value1` is a formula).
- `listItems` are stored as one comma-separated literal: no item may contain
  a comma or a double quote, and the joined list stays within Excel's 255
  characters. Put longer lists in cells and give `listSource`.
- `operator`: between, notBetween, equal, notEqual, greaterThan, lessThan,
  greaterOrEqual, lessOrEqual.
- Optional `inputMessage`, `errorMessage`, `allowBlank` (default true).
- `{ "op": "clear_validation", "range": "B2:B100" }` removes validation from a range.

## Images

    { "op": "insert_image", "sheet": "Data", "path": "logo.png",
      "at": "H1", "width": 180, "height": 60 }

`width`/`height` are pixels; omit them for the image's natural size.
An SVG image that names any network address, and a compressed SVG (SVGZ), is
refused with `FEATURE_UNSUPPORTED`, because the engine would fetch the address
while adding the picture. Embed its images in the SVG or supply a raster image.

## Defined names

| op | fields |
|----|--------|
| `define_name` | `name`, `refersTo` (sheet-qualified, e.g. `Config!$B$2`; a range or formula) |
| `delete_name` | `name` |

Workbook-scoped named ranges — the backbone of a maintainable model.

## Comments

| op | fields |
|----|--------|
| `add_comment` | `cell`, `text`, `author?` |
| `edit_comment` | `cell`, `text`, `author?` |
| `delete_comment` | `cell` |

## Protection

    { "op": "protect_sheet", "passwordEnv": "SHEET_PWD",
      "allow": ["sort", "autoFilter"] }
    { "op": "unprotect_sheet", "passwordEnv": "SHEET_PWD" }

- Everything is locked by default; `allow` lists the still-permitted actions:
  formatCells, insertRows, insertColumns, deleteRows, deleteColumns, sort,
  autoFilter.
- **Passwords are never written into the ops file.** `passwordEnv` names an
  environment variable the CLI reads at run time; the value never appears in
  output, logs or errors. Set it first, e.g. `export SHEET_PWD=…`. A missing
  or empty variable fails only the operation that names it, with `OPS_INVALID`
  naming the variable; `--best-effort` still applies the other operations
  (exit 8) and `--dry-run` reports every outcome.
- `protect_workbook` / `unprotect_workbook` (both take an optional
  `passwordEnv`) lock the **structure** — adding, deleting, moving or hiding
  sheets — rather than a sheet's cells.

## Hyperlinks

    { "op": "set_hyperlink", "cell": "A1", "url": "https://example.com", "display": "Open" }
    { "op": "set_hyperlink", "cell": "A2", "target": "Summary!B10" }
    { "op": "remove_hyperlink", "cell": "A1" }

Give **either** `url` (an absolute `http`, `https` or `mailto` URL) **or** `target`
(an internal cell or range, sheet-qualified when it lies on another sheet; defined
names are not accepted).
`display` sets the cell text; `remove_hyperlink` clears the link covering a cell.

## Conditional formatting

    { "op": "add_conditional_format", "range": "B2:B100",
      "rule": { "kind": "cellValue", "operator": "greaterThan", "value1": "1000" },
      "style": { "bg": "#FFC7CE" } }

    { "op": "add_conditional_format", "range": "C2:C100",
      "rule": { "kind": "colorScale", "minColor": "#FFFFFF", "maxColor": "#63BE7B" } }

- `rule.kind`:
  - `cellValue` — `operator` (same set as validation) + `value1` (+`value2`
    for between), plus a `style` to apply.
  - `colorScale` — `minColor` + `maxColor` (#RRGGBB); add `midColor` for a
    3-point scale.
  - `dataBar` — `barColor`.
  - `duplicates` — highlights duplicate values; needs a `style`.
  - `formula` — `value1` is a formula; every cell where it is true gets the
    `style`. The missing leading `=` is added for you (without it Excel
    would compare against a literal string that never matches).
  - `topBottom` — `rank` (1–1000; 1–100 with `"percent": true`) +
    optional `percent`/`bottom` booleans, plus a `style`. Top-3 values,
    bottom 10 percent, and so on.
  - `iconSet` — `iconSet` names the set: `arrows3`, `trafficLights3`,
    `symbols3`, `rating4`, `rating5`. Thresholds are automatic; icons are
    the formatting, so **omit `style`** (it is rejected).
- A conditional `style` may set only `bold`, `italic`, `underline`,
  `strikethrough`, `color`, `bg` and `numberFormat` — Excel ignores fonts,
  sizes, alignment, wrapping and indents in a conditional format, so those
  fields are rejected.
- `{ "op": "clear_conditional_formats", "range": "B2:B100" }` removes all
  conditional formatting overlapping a range.

A rule formula is written for the range's top-left cell and shifts per cell
with the same fill semantics as `set_formula` — relative parts move, `$`
parts stay. Anchoring the *column* with `$` while leaving the row relative
is the whole-row-highlight recipe: over a multi-column range, every cell of
a row tests the same status cell, so the whole row lights up.

```json
{ "op": "add_conditional_format", "range": "A2:F100",
  "rule": { "kind": "formula", "value1": "=$F2=\"OVERDUE\"" },
  "style": { "bg": "#FFC7CE" } }
```

Row 2 tests `$F2`, row 3 tests `$F3`, … — each row of `A2:F100` fills red
exactly when its own Status cell in column F says OVERDUE. Without the `$`
the test column would drift as the rule moves right (`A2` tests `F2` but
`B2` tests `G2`); with `$F$2` every row would test the one cell `F2`. The
same mixed-anchor trick works in `cellValue` rules: `value1`/`value2` also
accept an `=`-led formula, so `{ "kind": "cellValue", "operator":
"greaterThan", "value1": "=$B2" }` over `A2:A4` flags each actual in `A`
that exceeds its own row's target in `B`.

## Sparklines

Tiny in-cell charts summarizing a row (or column) of data — the KPI-table
staple. One op draws a whole group:

    { "op": "add_sparkline", "dataRange": "B2:E10", "location": "F2:F10",
      "type": "line", "color": "#1F4E79" }

- `dataRange` — the data block; may be sheet-qualified (`Data!B2:E10`) so a
  dashboard sheet can plot a data sheet.
- `location` — a single cell or a one-row/one-column strip **on the op's
  sheet**. One sparkline per data row (or column) lands in each location
  cell, so the cell count decides the fan-out: 9 cells against 9 data rows
  draws one sparkline per row; 4 cells against 4 data columns draws one per
  column; matching neither is rejected with both counts named.
- `type` — `line` (default), `column`, or `winloss` (Excel's name for
  equal-height columns above/below the axis by sign; the file format calls
  it "stacked").
- `color` — the series color (#RRGGBB).

Sparklines live in otherwise-empty cells; give the location column a header
so it sits inside the used range and shows up in renders and reads.

## Workbook look

| op | fields | notes |
|----|--------|-------|
| `set_default_font` | `name`, `size?` (1–409 pt) | Workbook-scoped (`sheet` is ignored): rewrites the Normal style every cell without an explicit font derives from. Cells with an explicit `format_range` font keep it. |
| `set_tab_color` | `sheet`, `color?` (#RRGGBB) | Omit `color` to remove the tab color. |
| `set_sheet_view` | `sheet`, `gridlines?`, `zoom?` (10–400), `headings?` | Only the fields present are applied; at least one is required. |

Two caveats, both measured:

- **Set the default font FIRST — before content and column widths.** Column
  width units are measured in the Normal font, so changing it later rescales
  every column's rendered width. The one metric-neutral upgrade is the
  default Arial 10 → Calibri 11 (probe-measured pixel-identical column
  edges); any other target font rescales, so re-check widths after the swap.
- **`gridlines` is a view setting.** It affects Excel and the live
  preview, NOT the PNG `render` output — renders follow print semantics and
  are byte-identical either way (probe-measured), and print gridlines are
  not expressible in the v2 ops. When a grid must appear in a render or PDF,
  draw real borders with `set_borders`.

Recipe — a dashboard sheet that looks like one (colored tab, clean canvas):

    { "op": "set_default_font", "name": "Calibri", "size": 11 }
    { "op": "set_tab_color", "sheet": "Dashboard", "color": "#1F4E79" }
    { "op": "set_sheet_view", "sheet": "Dashboard", "gridlines": false }

## Batching strategy

One `edit` invocation per logical change-set. A report typically needs
exactly one batch: values → formulas → formats → structure → chart/pivot.
Use `--dry-run` first when the batch is large or destructive
(delete_sheet, delete_rows); it validates everything without writing.

## Output and verification boundary

Editable output formats are xlsx, xlsm, xlsb, xls, ods, csv, tsv, html and mhtml.
Use `cells convert` for other export formats. HTML output is self-contained and
embeds its images. Editing encrypted input keeps the password in xlsx, xlsm,
xlsb, xls and ods output; `--encrypt-env` changes it. Other outputs cannot be
encrypted: they drop the source encryption with a `WORKBOOK_ENCRYPTION_REMOVED`
warning, and `--encrypt-env` on them is OPTION_INVALID. `cells convert` follows
the same rule for its output.

`--verify` checks the staged candidate before publication, including when
`--timeout` or MCP is used. Semantic findings are returned with
`verification.ok=false` and exit 8; execution errors, resource failures and
cancellation abort publication.

Create, edit and convert share the same save policy. Text output reports
`SHEETS_DROPPED` when only one of several worksheets can be retained. During
`--verify`, sheet loss, grid truncation, broken formulas and incomplete error
scans make verification incomplete while preserving the committed partial result.
