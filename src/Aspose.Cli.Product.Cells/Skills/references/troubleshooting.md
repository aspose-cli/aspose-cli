# Cells troubleshooting

The error envelope, exit codes, not-found details and the codes every product
shares are in `aspose-cli docs troubleshooting`; licensing and evaluation
disclosure in `aspose-cli docs licensing`. This page covers what is specific
to workbooks.

## Errors

| code | exit | cause and fix |
|------|------|---------------|
| `FILE_CORRUPT` | 3 | The content matches no spreadsheet signature; renaming a `.docx` to `.xlsx` does not make a workbook. Plain-text data needs a text extension (`.csv`, `.tsv`, `.txt`, `.json`) to import as text. |
| `INPUT_ENCODING_INVALID` | 3 | A CSV or TSV input is not UTF-8 and has no byte order mark; read as UTF-8 its text would be replaced. Import it with `cells convert data.csv --to xlsx --encoding <name>` (`gb18030` for Chinese Windows and ERP exports; `big5`, `shift_jis`, `windows-1252` for others), then work on the workbook. |
| `FORMAT_AMBIGUOUS` | 6 | A CSV or TSV input writes decimal commas (`2,71`, `1.253,96`); invariant formats would read different numbers. Import it with `cells convert data.csv --to xlsx --culture de-DE` (or the culture the file comes from); the culture also reads its dates. `details.sample` and `details.line` name the first such value. |
| `FILE_LOCKED` | 3 | Excel holds the file exclusively. Ask the user to close it and retry the identical command; nothing was written. |
| `OUTPUT_UNWRITABLE` | 5 | Also what an in-place save of a workbook open in Excel reports: reads work, the final replace fails. The backup is untouched; ask the user to close the file. |
| `SHEET_NOT_FOUND`, `NAME_NOT_FOUND`, `CHART_NOT_FOUND`, `PIVOT_NOT_FOUND`, `COMMENT_NOT_FOUND`, `HYPERLINK_NOT_FOUND`, `STYLE_NOT_FOUND` | 4 | `details.available` lists sheets, defined names, the sheet's charts or pivots, the cells with comments, the areas hyperlinks cover, or table styles. Sheet names match case-insensitively, as in Excel; results report the stored spelling. A chart `index` past the last chart reports only `availableCount`. |
| `RANGE_INVALID` | 4 | A command range such as `--range`. Use `C5`, `B2:D10`, `Sales!A1:C10` or `'My Sheet'!A1:C10`; whole rows and columns (`A:A`, `1:3`) are refused so output stays bounded. In an ops document the same mistake is `OPS_INVALID` with the field in `details.reason`. |
| `RANGE_TOO_LARGE` | 4 | The range exceeds `--max-cells`. Run the first page the hint suggests and then each `window.next`, or raise `--max-cells` when you need everything. |
| `RENDER_EMPTY` | 4 | The selected sheet has no content. Pick a sheet with data; `cells inspect` lists each sheet's used range. |
| `OPS_INVALID` | 4 | `details.index` and `details.op` name the operation, `details.reason` the rule or JSON path (`unknown field 'style.shiny'`). A failure while applying an operation keeps its own code, such as `SHEET_NOT_FOUND`, with the same details. Fix that operation and rerun the whole batch. |
| `EVALUATION_LIMIT` | 7 | Evaluation mode exports only the first worksheet to CSV, TSV or Markdown, and `--sheet` named another one. Nothing was written. Apply a Cells license, or select the first sheet if that is the data you want. |

## Warnings

| code | meaning |
|------|---------|
| `SHEETS_DROPPED` | The output format holds one worksheet; it names the sheet kept. |
| `SHEETS_SKIPPED` | `render --all-sheets` could not render some sheets; render one alone with `--sheet` for its error. |
| `DATA_TRUNCATED` | The target grid (for example xls, 65,536 rows) is smaller than the data; save to xlsx, xlsb or ods. |
| `FORMULAS_BROKEN` | Formulas that referenced cells beyond the target grid became `#REF!`; save to xlsx or xlsb. |
| `WORKBOOK_ENCRYPTION_REMOVED` | The output format cannot be encrypted, so the source encryption was dropped. |
| `MHTML_RESOURCE_COVERAGE_UNVERIFIED` | The engine resolves MHTML resources without reporting missing ones; check images and styles yourself. |

## Evaluation mode in workbooks

Without a Cells license, results carry `EVAL_MODE` and every saved workbook
gains an "Evaluation Warning" sheet plus watermark content. Disclose it
(`aspose-cli docs licensing`), and handle these effects:

- The warning sheet becomes the active sheet, so `query range` and `render`
  without `--sheet` read it. Always pass `--sheet`, and take names from
  `inspect`, never from sheet order.
- Each further save can add another ("Evaluation Warning (1)", ...); `inspect`
  lists them. Do not delete them.
- Data projections carry the marks too: a CSV gains a trailing watermark row,
  Markdown a trailing `# Evaluation Only` heading, whole-workbook JSON the
  warning sheets.
- CSV, TSV and Markdown export only the first worksheet (`EVALUATION_LIMIT`
  above).
- A licensed re-save does not remove existing marks. Rebuild the licensed
  deliverable from the original unmarked inputs.
