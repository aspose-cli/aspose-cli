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
spreadsheet extension when its content is delimited text.

## Names and ranges

A sheet, defined name, chart, pivot, comment, hyperlink or table style that is
not found follows the shared not-found rule (`aspose-cli docs editing`). Sheet
names match case-insensitively, as in Excel, and results report the stored
spelling.

## Outputs that hold less than the workbook

A format that holds one worksheet (csv, tsv, md) keeps one sheet and warns
`SHEETS_DROPPED`; select another with `--sheets`. It writes
that sheet's cells as a table and leaves out its charts, pictures and other
drawings, and warns `LOSSY_CONVERSION` with how many of each: convert to xlsx
or pdf to keep them, or `cells render` an image of the sheet. `cells convert`
and a `cells edit` that writes csv or tsv also warn `LOSSY_CONVERSION` for the
headers and footers of that sheet, which the table leaves out, and for its
comments. These warnings are no `--verify` issue. A target grid
smaller than the data, such as xls with 65,536 rows, warns `DATA_TRUNCATED`,
and formulas that referenced the lost cells become `#REF!` with
`FORMULAS_BROKEN`: save to xlsx, xlsb or ods instead.

An md output writes each cell as the sheet shows it (`700,000`, `38.6%`),
where csv and tsv write the stored value, so the table can go straight to
`words create --from-markdown`. It writes the sheet's comments after the
table as footnote definitions (`[^Comment_0_1]: ...`) that no cell refers to,
so a Markdown reader does not get them and `words create --from-markdown`
leaves them out.

A PDF or XPS prints only the comments its page setup prints, by default none,
and warns `LOSSY_CONVERSION` with how many comments each sheet it prints has.
To print them, set `printComments` to `sheet-end` with `set_page_setup` on the
sheet and convert again: they follow its last page, and the warning goes away.
An html or mhtml page holds no comments, shows the charts as pictures and
keeps the headers and footers only as Excel print data that a browser neither
shows nor prints; it warns `LOSSY_CONVERSION` for each of these the sheets
have.

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
out.

An SVG picture of any input
keeps only what it holds itself: references to other files or addresses are
removed with `REMOTE_RESOURCES_BLOCKED`, and an SVG picture that does not parse
or is compressed is left out with `IMAGES_SKIPPED`.

## Formulas

`FORMULAS_CALCULATED_ON_OPEN` says the workbook asks to be calculated when
opened and some results differ from the stored ones, usually because a tool
wrote formulas without results.

`FORMULA_FUNCTION_UNKNOWN` names each cell whose formula calls a function the
engine does not know, so it evaluates to `#NAME?`. Correct the
name, in English; an add-in or VBA function, or one newer than the engine,
stays in the file for Excel.

## Text tables that do not start at row 1

ERP and report exports often write a title and a query-condition row before
the header, leave an empty row between groups and end with a total row. The
CSV import keeps every row, so `cells inspect` and `cells convert` warn
`TEXT_TABLE_LAYOUT` for each finding, and each hint names the ranges to use.

The check reads the first 10,000 rows and the last rows of a longer sheet.

`cells convert --to json` runs the same check on every sheet it writes, of any
input: the JSON takes its keys from a sheet's first used row and writes every
row below it as a record, so a title row makes the real header a record, an
empty row a `null` record and a total row a record a sum counts twice.

## Numbers with a decimal comma

A CSV read without `--culture` that writes a decimal comma anywhere (`2,71`,
`1.253,96`) keeps every number written with a comma or a point as text, since
`1,234` may then be a decimal and `1.234` a thousand.
Every command that reads the file warns `NUMBERS_READ_AS_TEXT` with one such
cell, and its hint converts the file with `--culture de-DE`, which fits a file
whose decimals all have a comma. If the file also writes a decimal point
(`19.90`), a culture like en-US wrote it, and de-DE would read `19.90` as 1990.

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
  rows; a JSON output leaves the warning sheets out. Remove it before anything reads the output as data.
- A whole-workbook PDF, XPS, HTML or MHTML export prints the warning sheets an
  earlier evaluation save added as extra pages that hold only the notice; the
  `EVALUATION_MODE` message, or with a license `EVALUATION_INPUT_MARKED`, names them, and
  `review` then reports findings on those pages. Export the content sheets with `--sheets` (PDF).
- CSV, TSV and Markdown export only the first worksheet. Without `--sheets` the
  first sheet is written even when another one is active.
- `review` reports each warning sheet as `CELLS_EVALUATION_SHEET`, with or
  without a license.
