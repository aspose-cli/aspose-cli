# Engine behavior

What Aspose.Cells does with what an edit or a conversion gives it. The CLI passes each of these on
unchanged; the ones that are engine defects are entries of `KNOWN-ISSUES.md`.

## Values and formulas

- A value is put as the engine puts typed text: `#N/A`, `#DIV/0!` and the other error texts become
  error values; text that starts with `'` is the rest of it, with the style's `quotePrefix` set;
  `=1+1` given as a value is text. Numbers given as JSON numbers stay numbers.
- A date reads as the number the engine holds for it, with the cell's number format saying it is
  a date: set `value` to the number and the style's `number` or `custom` to a date format.
- The engine does not calculate when it saves: a formula set in a batch holds no result until
  `{"do": "calculate"}`, and a cell that depends on a changed value keeps its old result.
- `set_dynamic_array_formula` with `calculateValue` false leaves the spilled cells without
  values until `calculate`.
- `sort` uses the workbook's one sorter; each `sort` clears its keys first, so a sort uses only
  the keys it names.

## Styles

- A fill shows only with `pattern` set: `foregroundColor` alone draws nothing.
- `apply_style` applies only the members its `flag` names. `CellShading` applies `pattern`,
  `foregroundColor` and `backgroundColor` together; `NumberFormat` applies `number` and `custom`
  together; `Font` applies every font member.
- A whole column or row given to `apply_style` (`C:C`, `5:5`) sets that column's or row's style.
- `font.doubleSize` is the size in points, halves included.

## Sheets, names and addresses

- The engine finds a sheet by its name in any case: `sheet/sales` is `Sales`.
- `add sheet` before the last sheet takes a name another sheet has, in any case; the new sheet's
  address then matches two sheets, and the edit stops with `ADDRESS_AMBIGUOUS`. At the end the
  engine refuses the name itself, `ENGINE_ERROR`. Nothing is written either way.
- `add sheet` takes any `index`: one past the last sheet, or more, adds it at the end.
- Setting `activeSheetName` to a name no sheet has leaves the active sheet as it was.
- A defined name's address is its full text: a sheet-level name is `name/Sales!Local`.

## Charts

- `add chart` makes an empty chart: it shows nothing until a series is added.
- `add series` adds one series for each column of its area (`isVertical: true`) or each row.
- Setting `valueAxis.isAutomaticMinValue` to false without a `valueAxis.minValue` makes the
  engine's save fail (`ENGINE_ERROR`); set `minValue`, which turns the automatic minimum off.
- A chart's or picture's id is the engine's shape id, which the add's result gives.

## Conversion

- A CSV, TSV or Markdown output is the active sheet unless `--sheet` names one.
- Without a license the engine adds an `Evaluation Warning` sheet to each workbook it opens, one
  more at each open, and makes it active, and writes the first sheet to CSV whatever sheet is
  active.
- An HTML or MHTML output holds its images (`SaveAsSingleFile`, `ExportImagesAsBase64`), as a
  Markdown output does (`ExportImagesAsBase64`).
- The engine lays out no page for an empty or a hidden sheet (`SheetRender.PageCount` is 0), so it
  has no image to render.
- A linked picture is drawn from a file beneath the input's folder; any other link is drawn as
  nothing, and the result's warnings count it. Nothing is fetched over the network.
- OpenDocument spreadsheets are written, not read: reading one, the engine fetches the URLs its
  pictures name, which no engine option stops.
