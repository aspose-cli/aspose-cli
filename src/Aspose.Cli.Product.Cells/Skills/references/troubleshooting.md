# Cells troubleshooting

The error envelope, exit codes, not-found details and the codes every product
shares are in `aspose-cli docs troubleshooting`; licensing and evaluation
disclosure in `aspose-cli docs licensing`. This page covers what is specific
to workbooks.

## Errors

| code | exit | cause and fix |
|------|------|---------------|
| `FILE_CORRUPT` | 3 | The file is not a readable workbook: it is damaged or has no recognizable signature. Ask for an intact copy; plain-text data needs a text extension (`.csv`, `.tsv`, `.txt`, `.json`) to import as text. |
| `FORMAT_MISMATCH` | 6 | The content is another product's format: renaming a `.docx` to `.xlsx` does not make a workbook. Convert it with that product or ask for the spreadsheet. |
| `INPUT_ENCODING_INVALID` | 3 | A CSV or TSV input is not UTF-8 and has no byte order mark; read as UTF-8 its text would be replaced. Import it with `cells convert data.csv --to xlsx --encoding <name>` (`gb18030` for Chinese Windows and ERP exports; `big5`, `shift_jis`, `windows-1252` for others), then work on the workbook. |
| `FORMAT_AMBIGUOUS` | 6 | A CSV or TSV input writes decimal commas (`2,71`, `1.253,96`); invariant formats would read different numbers. Import it with `cells convert data.csv --to xlsx --culture de-DE` (or the culture the file comes from); the culture also reads its dates. `details.sample` and `details.line` name the first such value. |
| `FILE_LOCKED` | 3 | Excel holds the file exclusively. Ask the user to close it and retry the identical command; nothing was written. |
| `OUTPUT_UNWRITABLE` | 5 | Also what an in-place save of a workbook open in Excel reports: reads work, the final replace fails. The backup is untouched; ask the user to close the file. |
| `SHEET_NOT_FOUND`, `NAME_NOT_FOUND`, `CHART_NOT_FOUND`, `PIVOT_NOT_FOUND`, `COMMENT_NOT_FOUND`, `HYPERLINK_NOT_FOUND`, `STYLE_NOT_FOUND` | 4 | `details.available` lists sheets, defined names, the sheet's charts or pivots, the cells with comments, the areas hyperlinks cover, or table styles. Sheet names match case-insensitively, as in Excel; results report the stored spelling. A chart `index` past the last chart reports only `availableCount`. |
| `RANGE_INVALID` | 4 | A command range such as `--range`. Use `C5`, `B2:D10`, `Sales!A1:C10` or `'My Sheet'!A1:C10`; whole rows and columns (`A:A`, `1:3`) are refused so output stays bounded. In an ops document the same mistake is `OPS_INVALID` with the field in `details.reason`. |
| `RANGE_TOO_LARGE` | 4 | The range exceeds `--max-cells`. Run the first page the hint suggests and then each `window.next`, or raise `--max-cells` when you need everything. |
| `RENDER_EMPTY` | 4 | The selected sheet has no content. Pick a sheet with data; `cells inspect` lists each sheet's used range. |
| `OPS_INVALID` | 4 | `details.index` and `details.op` name the operation, `details.reason` the rule or JSON path (`unknown field 'style.shiny'; style accepts: ...`, with the accepted fields in `details.allowedFields`). A failure while applying an operation keeps its own code, such as `SHEET_NOT_FOUND`, with the same details. Fix that operation and rerun the whole batch. |
| `EVALUATION_LIMIT` | 7 | Evaluation mode exports only the first worksheet to CSV, TSV or Markdown, and `--sheet` named another one. Nothing was written. Apply a Cells license, or select the first sheet if that is the data you want. |

## Warnings

| code | meaning |
|------|---------|
| `SHEETS_DROPPED` | The output format holds one worksheet; it names the sheet kept. |
| `SHEETS_SKIPPED` | `render --all-sheets` could not render some sheets; render one alone with `--sheet` for its error. |
| `DATA_TRUNCATED` | The target grid (for example xls, 65,536 rows) is smaller than the data; save to xlsx, xlsb or ods. |
| `FORMULAS_BROKEN` | Formulas that referenced cells beyond the target grid became `#REF!`; save to xlsx or xlsb. |
| `FORMULAS_CALCULATED_ON_OPEN` | The workbook asks to be calculated when opened and some formula results shown differ from the stored ones, usually because a tool wrote formulas without results. The values shown are the engine's, as Excel shows them; `cells edit` stores them. |
| `WORKBOOK_ENCRYPTION_REMOVED` | The output format cannot be encrypted, so the source encryption was dropped. |
| `EXTERNAL_LINK_CACHE_MISSING` | `import_sheet` or `import_range` (`all`) copied formulas that read another workbook through a link that caches no values. They show `#REF!` in the source but read the linked cells as empty, usually 0, here; the message lists them and `location` names the first; `--verify` reports it as an issue. Replace them with `set_formula` or `set_values`. |
| `EXTERNAL_LINK_RELATIVE` | The edit added a link the output stores as a file name without a folder, relative to the output's folder; a full path to a file in the input's folder is stored this way too. The message names the stored targets. A formula naming a sheet the workbook does not have, such as `=Salse!B2`, is stored this way too; the hint then names the closest sheet, so correct the formula. Otherwise keep those files beside the output, or use `import_range` instead of a link. |
| `CHART_SPLIT_ACROSS_PAGES` | A PDF conversion printed a chart across two or more pages; the message names each chart and its page count. Fit the sheet with `set_page_setup` (`fitToWidth` 1 and `fitToHeight` 0, or `orientation` landscape), or move or resize the chart, and convert again. |
| `MHTML_RESOURCE_COVERAGE_UNVERIFIED` | The engine resolves MHTML resources without reporting missing ones; check images and styles yourself. |
| `EVALUATION_SHEET_ADDED` | An evaluation save added the warning sheet `location` names and made it the active sheet in place of the one the message names; see below. |
| `EVALUATION_NOTICE_ADDED` | An evaluation save wrote its notice into a CSV, TSV, Markdown or JSON output as content (a last row, a heading, records and warning sheets), or a PDF, XPS, HTML or MHTML export printed the input's warning sheets as extra pages; it is not data. See below. |
| `EVALUATION_SHEET_SKIPPED` | The input's active sheet is the evaluation warning sheet `location` names, and this command named no sheet, so it used the sheet the message names instead; see below. |
| `ROWS_SHIFTED` | `cells compare` found rows one side inserted or deleted (the message names them, `location` the sheet); the cells below each shift are compared with different rows, so their differences are not edits. Apply the same `insert_rows` or `delete_rows` to a copy of the left workbook and compare the copy. |
| `PROTECTION_NOT_ENFORCED` | The edit changed a protected sheet (`location` names it when there is one) or the protected workbook structure; protection guides Excel only, so the edit went through it. Confirm the change is authorized. |
| `TEXT_TABLE_LAYOUT` | A CSV or TSV input is not a plain table from row 1; one warning per finding, with the rows in `location`. The header comes after a title or notes, rows that hold at most one value each (`1:2`: the header is the row after them), empty rows lie inside the table, a trailing row is labeled as a total (`合计`, `总计`, `小计`, `Total`, `Grand Total`, `Subtotal`, `Sum`), or the last row is the notice of an evaluation export. The rows are imported as they are; see below. |

## Text tables that do not start at row 1

ERP and report exports often write a title and a query-condition row before
the header, leave an empty row between groups and end with a total row. The
CSV import keeps every row, so `cells inspect` and `cells convert` warn
`TEXT_TABLE_LAYOUT` for each finding, and each hint names the ranges to use:

- Read the header row the message names with
  `cells query range <file> --sheet "<sheet>" --range A3:F3` (a text file's
  one sheet is named after the file), and start data ranges, formulas and
  sorts on the row after it.
- Give sorts, filters and charts the whole table range rather than a range
  that stops at an empty row, or remove empty rows in the converted workbook
  with `delete_rows`; the rows below them move up, so inspect again.
- End sums and data ranges before the total row, for example `=SUM(C4:C19)`,
  and keep it out of sorts, pivots and charts.

The check reads the first 10,000 rows and the last rows of a longer sheet; a
message says when empty rows were looked for only in the first 10,000.

## Evaluation mode in workbooks

Without a Cells license, results carry `EVAL_MODE` and every saved workbook
gains an "Evaluation Warning" sheet plus watermark content. Disclose it
(`aspose-cli docs licensing`), and handle these effects:

- The engine appends the warning sheet and makes it the active sheet; no
  option keeps your active sheet (`set_active_sheet` included). The result
  says so with `EVALUATION_SHEET_ADDED`.
- When a workbook's active sheet is such a warning sheet, every command that
  defaults to the active sheet (`query range` or `render` without `--sheet`,
  `convert` to CSV, TSV or Markdown without `--sheet`, an operation without
  `"sheet"`, the workbook preview) uses the first other sheet instead,
  preferring a visible one, and warns `EVALUATION_SHEET_SKIPPED`; the file is
  not changed. A command that names its sheet or covers every sheet does not
  warn.
  The sheet you made active is not recorded anywhere, so pass `--sheet` (or
  `"sheet"`) for any other one, and take names from `inspect`, never from
  sheet order.
- Each further save adds another ("Evaluation Warning (1)", ...); `inspect`
  lists them. Do not delete them.
- Data outputs carry the notice as content, and the result warns
  `EVALUATION_NOTICE_ADDED`: CSV and TSV gain an `Evaluation Only. ...` last
  row after the data, Markdown a closing `# Evaluation Only. ...` heading, and
  JSON a `{"watermark": ...}` record after the records of each sheet with data
  rows, plus the warning sheets. Remove it before anything reads the output as data;
  until then `cells inspect` warns `TEXT_TABLE_LAYOUT` and `review` reports
  `CELLS_EVALUATION_NOTICE` on the CSV or TSV notice row.
- A whole-workbook PDF, XPS, HTML or MHTML export prints the warning sheets an
  earlier evaluation save added as extra pages that hold only the notice, and
  warns `EVALUATION_NOTICE_ADDED` with their names; `review` then reports
  findings on those pages. Export the content sheets with `--sheet` (PDF), or
  rebuild the workbook and the export with a license.
- CSV, TSV and Markdown export only the first worksheet. Without `--sheet` the
  first sheet is written even when another one is active, and
  `SHEETS_DROPPED` names it; `--sheet` naming another sheet fails with
  `EVALUATION_LIMIT` and writes nothing.
- A licensed re-save does not remove existing marks. Rebuild the licensed
  deliverable from the original unmarked inputs. `review` reports each warning
  sheet as `CELLS_EVALUATION_SHEET`, with or without a license.
