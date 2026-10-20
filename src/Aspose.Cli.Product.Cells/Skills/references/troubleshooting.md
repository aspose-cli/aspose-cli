# Cells troubleshooting

The error envelope, exit codes, not-found details and the codes every product
shares are in `aspose-cli docs troubleshooting`; licensing and evaluation
disclosure in `aspose-cli docs licensing`. Every Cells code, with its exit
code, is listed by `aspose-cli capabilities cells`, and each error's message,
hint and details name the cause. This page says what to do about the ones a
workbook task commonly meets.

## Inputs that do not open

`FILE_CORRUPT` follows the shared rule (`aspose-cli docs troubleshooting`):
ask for an intact copy. Plain-text data imports as
text under a text extension (`.csv`, `.tsv`, `.txt`), or under another
spreadsheet extension when its content is delimited text; an extension no
input declares, such as `.json`, is refused.

CSV and TSV inputs are read as UTF-8 with invariant number and date formats,
and a file that needs anything else is refused rather than misread. A file in
another encoding without a byte order mark fails with `INPUT_ENCODING_INVALID`:
import it once with `cells convert data.csv --to xlsx --encoding gb18030`
(Chinese Windows and ERP exports; `big5`, `shift_jis` or `windows-1252` for
others) and work on the workbook. A file that writes decimal commas
(`2,71`, `1.253,96`) fails with `FORMAT_AMBIGUOUS`, whose `details.sample` and
`details.line` name the first such value: import it with
`cells convert data.csv --to xlsx --culture de-DE`, or the culture the file
comes from, which also reads its dates.

## Names and ranges

A sheet, defined name, chart, pivot, comment, hyperlink or table style that is
not found follows the shared not-found rule (`aspose-cli docs editing`). Sheet
names match case-insensitively, as in Excel, and results report the stored
spelling. A chart `index` past the last chart reports only
`details.availableCount`.

A range that is not A1 notation is `OPTION_INVALID` in an option, `--set`
included, and `OPS_INVALID` in an operation, with the reason in the message;
`details.reason` is the token `invalid-value`. Write `C5`, `B2:D10`, `Sales!A1:C10` or
`'My Sheet'!A1:C10`; whole rows and columns (`A:A`, `1:3`) are refused so output
stays bounded. `--sheets` on `cells convert` and `cells render` takes
positions from `cells inspect` (`2`, `1-3`, `all`), never names: select a sheet
by name with `--range Sales!A1:G20` or, on `cells read` and `cells search`,
with `--sheet`.

## Outputs that hold less than the workbook

A format that holds one worksheet (csv, tsv, md) keeps one sheet and warns
`SHEETS_DROPPED` with the sheet kept; select another with `--sheets`. A target
grid smaller than the data, such as xls with 65,536 rows, warns
`DATA_TRUNCATED`, and formulas that referenced the lost cells become `#REF!`
with `FORMULAS_BROKEN`: save to xlsx, xlsb or ods instead.

A PDF or XPS that prints a chart across two or more pages warns
`CHART_SPLIT_ACROSS_PAGES` with each chart and its page count, and `review`
reports such a workbook as `CELLS_CHART_SPLIT_ACROSS_PAGES`. Fit the sheet
with `set_page_setup` (`fitToWidth` 1 and `fitToHeight` 0, or `orientation`
landscape), or move or resize the chart, and convert again. No product reads
xps, so `review` cannot open an xps deliverable: review the PDF the same
workbook converts to instead. `cells convert --to xltx` saves an Excel
template (`xltm` keeps macros) that new workbooks made in Excel start from.

A JSON output of a sheet whose data is a single row, which the engine takes
for the header, would not be valid JSON (CELLS-JSON-HEADER-ONLY), so `cells
convert --to json` leaves such a sheet out, writes the other sheets as usual,
still one member per sheet, and warns `SHEETS_DROPPED` naming the sheets left
out. A workbook with no other sheet is refused with `FEATURE_UNSUPPORTED`
(`details.reason` `engine-defect`) before anything is written: add a data row
below the header, or convert to csv.

`RENDER_EMPTY` means the selected sheet has no content; `cells inspect` lists
each sheet's used range. When a render of several sheets cannot render some of
them it warns `SHEETS_SKIPPED`; render one alone with `--sheets` for its error.
An MHTML output warns `MHTML_RESOURCE_COVERAGE_UNVERIFIED` because the engine
does not report missing resources; check its images and styles yourself.

## Formulas

`FORMULAS_CALCULATED_ON_OPEN` says the workbook asks to be calculated when
opened and some results differ from the stored ones, usually because a tool
wrote formulas without results. The values shown are the engine's, as Excel
shows them, and `cells edit` stores them.

`FORMULA_FUNCTION_UNKNOWN` names each cell whose formula calls a function the
engine does not know, so it evaluates to `#NAME?`, and the closest known
function when one is a single edit away, such as `SUM` for `SUMM`. Correct the
name, in English; an add-in or VBA function, or one newer than the engine,
stays in the file for Excel.

`EXTERNAL_LINK_CACHE_MISSING` means `import_sheet` or `import_range` (`all`)
copied formulas that read another workbook through a link that caches no
values: they show `#REF!` in the source but read the linked cells as empty,
usually 0, here, and `--verify` reports it as an issue. Replace them with
`set_formula` or `set_values`. `EXTERNAL_LINK_RELATIVE` means the edit stored a
link as a file name without a folder, relative to the output's folder; keep
those files beside the output, or use `import_range` instead of a link. A
formula naming a sheet the workbook does not have, such as `=Salse!B2`, is
stored this way too, and the hint then names the closest sheet: correct the
formula.

## Protection and comparison

`PROTECTION_NOT_ENFORCED` means the edit changed a protected sheet or the
protected workbook structure: protection guides Excel only, so the edit went
through it. Confirm the change is authorized (`aspose-cli docs cells/editing`,
Tables, validation, links and pictures).

`ROWS_SHIFTED` from `cells compare` names rows one side inserted or deleted;
the cells below each shift are compared with different rows, so their
differences are not edits. Apply the same `insert_rows` or `delete_rows` to a
copy of the left workbook and compare the copy.

## Text tables that do not start at row 1

ERP and report exports often write a title and a query-condition row before
the header, leave an empty row between groups and end with a total row. The
CSV import keeps every row, so `cells inspect` and `cells convert` warn
`TEXT_TABLE_LAYOUT` for each finding, and each hint names the ranges to use:

- Read the header row the message names with
  `cells read <file> --sheet "<sheet>" --range A3:F3` (a text file's
  one sheet is named after the file), and start data ranges, formulas and
  sorts on the row after it.
- Give sorts, filters and charts the whole table range rather than a range
  that stops at an empty row, or remove empty rows in the converted workbook
  with `delete_rows`; the rows below them move up, so inspect again.
- End sums and data ranges before the total row, for example `=SUM(C4:C19)`,
  and keep it out of sorts, pivots and charts.

The check reads the first 10,000 rows and the last rows of a longer sheet; a
message says when empty rows were looked for only in the first 10,000.

`cells convert --to json` runs the same check on every sheet it writes, of any
input: the JSON takes its keys from a sheet's first used row and writes every
row below it as a record, so a title row makes the real header a record, an
empty row a `null` record and a total row a record a sum counts twice. Each
`TEXT_TABLE_LAYOUT` names the sheet and rows; convert a copy without them
(`delete_rows` in `cells edit` with `--out`), or leave those records out
downstream.

## Evaluation mode in workbooks

Without a Cells license, every saved workbook gains an "Evaluation Warning"
sheet plus watermark content. Disclose it (`aspose-cli docs licensing`), and
handle these effects:

- The engine appends the warning sheet and makes it the active sheet; no
  option keeps your active sheet (`set_active_sheet` included). The
  `EVALUATION_MODE` message names the warning sheets the output carries and which
  of them is active.
- When a workbook's active sheet is such a warning sheet, in evaluation mode
  every command that defaults to the active sheet (`read` without `--sheet`,
  `convert` to CSV, TSV or Markdown without `--sheets`, an operation without
  `"sheet"`, the workbook preview) uses the first other sheet instead,
  preferring a visible one, and warns `ACTIVE_SHEET_SKIPPED` with the skipped
  sheet in `location`. With a license the active sheet stays the default,
  whatever its name. Only the default changes: the input file is not changed, and an edit
  or `convert` to a multi-sheet format keeps the warning sheet active unless
  a `set_active_sheet` in the batch succeeded. A command that names its sheet
  or covers every sheet does not warn.
  The sheet you made active is not recorded anywhere, so name any other one
  yourself: `--sheets` with the sheet's `position` for `convert` and `render`,
  and the sheet's `name` for `read --sheet` and an operation's `"sheet"` in
  `edit`; the workbook preview shows the active sheet, so read another one with
  `cells read --sheet`. Take both from `inspect`; never guess a position from
  the order you created the sheets in.
- Each further save adds another ("Evaluation Warning (1)", ...); `inspect`
  lists them. Do not delete them.
- Data outputs carry the notice as content, and the `EVALUATION_MODE` message
  says where: CSV and TSV gain an `Evaluation Only. ...` last
  row after the data, Markdown a closing `# Evaluation Only. ...` heading, and
  JSON a `{"watermark": ...}` record after the records of each sheet with data
  rows; a JSON output leaves the warning sheets out. Remove it before anything reads the output as data;
  until then `cells inspect` warns `TEXT_TABLE_LAYOUT` and `review` reports
  `CELLS_EVALUATION_NOTICE` on the CSV or TSV notice row.
- A whole-workbook PDF, XPS, HTML or MHTML export prints the warning sheets an
  earlier evaluation save added as extra pages that hold only the notice; the
  `EVALUATION_MODE` message, or with a license `EVALUATION_INPUT_MARKED`, names them, and
  `review` then reports findings on those pages. A licensed edit or
  conversion to a workbook format keeps the warning sheets and warns
  `EVALUATION_INPUT_MARKED` too. Export the content sheets with `--sheets` (PDF), or
  rebuild the workbook and the export with a license.
- CSV, TSV and Markdown export only the first worksheet. Without `--sheets` the
  first sheet is written even when another one is active, and
  `SHEETS_DROPPED` names it; `--sheets` selecting another sheet fails with
  `EVALUATION_LIMIT` and writes nothing.
- `review` reports each warning sheet as `CELLS_EVALUATION_SHEET`, with or
  without a license.
