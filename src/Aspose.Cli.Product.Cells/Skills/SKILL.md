---
name: aspose-cli-cells
description: Read, edit, convert and render Excel workbooks with the local Aspose CLI. Edits are batches of set, add, delete, move and do operations on the Aspose.Cells object model, each read back from the saved file.
---

# Aspose Cells

`aspose-cli cells` reads, edits and converts XLSX, XLTX, XLSM, XLTM, XLAM, XLSB, XLS and XLT
through Aspose.Cells; `edit` writes the format it reads. CSV and TSV text is read only by
`convert`, with the text options it states. The shared rules (session start, secrets, licensing,
errors) are in the Platform Skill.

| Command | What it does |
|---|---|
| `cells read` | Every object of the workbook, or of one subtree, with every row of the object table, and the workbook's inventory |
| `cells search` | Matches in cell text, formulas and comment notes, each with the object's address |
| `cells edit` | One batch of operations; writes the input's format; nothing is written when one fails |
| `cells convert` | The engine's save to another format: PDF, XPS, HTML, MHTML, JSON, Markdown, ODS, CSV, TSV, or another workbook format; CSV or TSV text to a workbook |
| `cells render` | The engine's image of each sheet, as one page, or of one area of a sheet |
| `cells create` | The engine's new workbook, or a copy of `--template` saved as a new file |

## The object table

`aspose-cli schema v2/cells/ops/directory` lists the vocabulary in one short JSON
document: each kind with its address forms, the rows a set writes (`rows`) and the rows that are
read only (`readOnlyRows`), and whether it can be deleted and moved; each add with the kind it adds
to and its parameters; each action with its parameters. One entry in full, with each row's type and
the engine member it names: `ops/style`, `ops/add.chart`, `ops/do.sort`. A
row names one engine member; setting it sets that member only, and whatever else the engine
changes with it is the engine's doing. Colors are `#RRGGBB`, sizes are points unless the row names
another unit (a column's `width` is in characters), enumeration values are the engine's member
names (`Solid`, `Center`, `Column`), and a date is the number the engine holds for it.

| Address | Object |
|---|---|
| `workbook` | The workbook: active sheet, default font, protection |
| `sheet/Sales` | A sheet by its name, in any case |
| `sheet/Sales/cell/B2`, `…/cell/B2/style` | A cell; its style |
| `sheet/Sales/range/A1:F20` | The values of an area, as rows of values |
| `sheet/Sales/row/5`, `sheet/Sales/column/C` | A row by its number, a column by its letters |
| `name/Total`, `name/Sales!Local` | A defined name by its full text |
| `sheet/Sales/merge/A1:C1`, `sheet/Sales/comment/B2` | A merged area by its area, a comment by its cell |
| `sheet/Sales/pageSetup`, `sheet/Sales/protection` | The sheet's page setup and protection |
| `…/table/0`, `…/pivot/0`, `…/validation/0`, `…/hyperlink/0`, `…/sparklineGroup/0` | Tables, pivot tables, validations, hyperlinks, sparkline groups by position |
| `…/conditionalFormatting/0/rule/1` | A conditional formatting by position; a rule of it |
| `…/chart/3`, `…/chart/3/series/0`, `…/series/0/point/2`, `…/picture/5` | A chart or picture by its shape id; a series and a point by position |
| `…/pivot/0/dataField/0` | A value field of a pivot table |

A sheet name is the address's text as it is, spaces and all: `sheet/Sales 2026/cell/B2` in an
operation's JSON. On a command line quote it: `--at "sheet/Sales 2026"`.

## Reading

```powershell
aspose-cli cells read book.xlsx
aspose-cli cells read book.xlsx --at "sheet/Sales/range/A1:F20"
aspose-cli cells read book.xlsx --at sheet/Sales --kind cell --max-objects 500
```

- Each object comes with its `at` address, its `kind` and its `props`.
- A read without `--kind` lists sheets, names, tables, charts, pivots and the other objects, but no
  row, column, cell, style or chart point: `window.notListedUnlessNamed` says which kinds it left
  out. Name them with `--kind` to list them; a cell is listed when the engine holds it.
- The values of an area are cheapest as a range: `--at sheet/Sales/range/A1:F20` reads one object
  whose `value` is a list of rows. A sheet's `maxDisplayRange` is the area that holds its data; a
  whole column such as `C:C` is all 1,048,576 rows, so read `maxDisplayRange` instead.
- A cell read with `--kind cell` is about 300 characters: 200 cells, the default window, are about
  65,000. A window holds at most `--max-objects` objects; when `window.truncated` is true, run the
  same command with `--from` set to `window.next.from`.
- To find where some text is, use `cells search`: each hit gives the address of the object that
  holds it.
- `inventory.read` counts the objects read lists; `inventory.notRead` counts content the table
  does not address, such as shapes other than charts and pictures, OLE objects, slicers, external
  links and VBA projects.

## Editing

```json
{"ops": [
  {"set": "sheet/Sales/range/A1:C2", "props": {"value": [["Region", "Q1", "Q2"], ["East", 1250, 1380]]}},
  {"set": "sheet/Sales/cell/D2", "props": {"formula": "=SUM(B2:C2)"}},
  {"set": "sheet/Sales/cell/A1/style", "props": {"font.isBold": true}},
  {"add": "sheet", "args": {"index": 1, "sheetType": "Worksheet", "sheetName": "Summary"}},
  {"do": "calculate"}
]}
```

```powershell
aspose-cli cells edit book.xlsx --ops ops.json --out book.edited.xlsx
```

- Operations run in order, each on what the ones before it left. When one fails, nothing is
  written, and the error names it: `details.index` (its position in `ops`), `details.at`.
- The result has one entry per operation, read from the saved file after it was opened again:
  `at` and `now` (every row of the object there); an action gives `returned`, the engine's return
  value, and `at` when it names an object. `counts` lists the inventory counts that changed.
- An add's entry gives the new object's address; a set that renames a sheet gives the sheet's new
  address. Use them in the next batch; do not guess ids.
- A position in `at` is read after the whole batch: when a later operation inserts or deletes
  before it, `now` is whatever object is at that position then.
- Every argument of an add or an action is the engine call's parameter of that name; the ones not
  given are not passed. `ops/add.<name>` and `ops/do.<name>` list them. An
  argument that takes an object takes its address: `{"do": "sort", "args": {"range":
  "sheet/Sales/range/A1:C5", …}}`.

Tasks, as the table does them:

- Values: set `value` on a range with as many rows and columns as the area, or on a cell. A
  value is a number, `true` or `false`, text, or `null` for none. The engine reads text as it
  reads typing: `#N/A` is an error value, and `'007` is the text `007` with `quotePrefix` set. A
  formula is the `formula` row, not a value.
- Formulas: the engine does not calculate when it saves. After values or formulas change, end
  the batch with `{"do": "calculate"}` so the saved cells hold their results. `set_shared_formula`
  fills an area from its top left cell; `set_dynamic_array_formula` returns the range it spills
  into.
- Formatting one cell: set rows of `…/cell/B2/style`. Formatting a range: `apply_style`, whose
  `style` holds style rows and whose `flag` names the StyleFlag properties applied:
  `CellShading` carries `pattern` with both colors, `NumberFormat` carries `number` and `custom`.
  A fill shows only with `pattern` set, such as `Solid`, beside `foregroundColor`:

  ```json
  {"do": "apply_style", "args": {"range": "sheet/Sales/range/A1:F1", "style": {"font.isBold": true, "foregroundColor": "#DDEBF7", "pattern": "Solid"}, "flag": ["FontBold", "CellShading"]}}
  ```

- Rows and columns: set `height` on `…/row/5` or `width` on `…/column/C`; `insert_rows`,
  `delete_rows`, `autofit_columns`, `group_rows` and the others are actions.
- A chart: `add chart` with its `type` and the `area` of cells it covers, then `add series` in it
  with the data `area`, such as `Sales!B2:E5`, and `isVertical`: the engine adds a series for
  each column of the area (`true`) or each row (`false`), and the entry's address is the first.
  Then set the chart's `nSeries.categoryData` and each series' `name`.
- A pivot table: `add pivot` with its source, then `add_pivot_field` once per field, then
  `calculate_pivot`.

## Converting and rendering

```powershell
aspose-cli cells convert book.xlsx --out book.pdf
aspose-cli cells convert book.xlsx --to csv --sheet "Sales 2026" --format-strategy None --out sales.csv
aspose-cli cells convert sales.csv --to xlsx --separator , --encoding utf-8 --convert-numbers true --convert-dates false
aspose-cli cells render book.xlsx --sheet Sales --area A1:F20 --out sales.png
```

- A CSV, TSV or Markdown output holds one sheet: the active sheet, or the one `--sheet` names.
  `--format-strategy None` writes values as they are stored; `DisplayString` as their number
  formats show them; without it, the engine's default.
- A PDF holds every sheet, or the ones `--sheet` names. An HTML, MHTML or Markdown output is one
  file that holds its images.
- A `.csv`, `.tsv` or `.txt` input is read with `--separator` (not for `.tsv`), `--encoding`,
  `--convert-numbers` and `--convert-dates`, and with `--culture` when a conversion is on.
- `render` draws every sheet, or the ones `--sheet` names, each as one image written to
  `<stem>.<sheet>.<ext>` when there are several; `--area` draws one area of the one sheet named.
  An empty or hidden sheet has no page, so a render that includes one writes nothing and fails
  with `RENDER_FAILED` naming it in `details.sheet`.

## Evaluation mode

Without a Cells license the result says `"license": {"mode": "evaluation"}`. The engine then adds
a sheet to every workbook it opens, `Evaluation Warning`, then `Evaluation Warning (1)` and so on,
and makes it the active sheet; reads list it, and a file it saves keeps it, so each edit adds
one. It writes the first sheet to CSV, whatever sheet is active, with a line of notice. One
process opens at most 100 files.

## More

- [Engine behavior](references/engine-behavior.md): what the engine does with values, formulas,
  styles, charts and conversions, which the CLI passes on unchanged.
- [Build a report](examples/build-a-report/README.md): a new workbook, data and formulas,
  formatting, a chart, a calculation, and a PDF to look at.
