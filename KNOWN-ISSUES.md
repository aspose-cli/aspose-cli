# Known issues

Confirmed defects in the commercial Aspose SDKs this CLI runs on, and how the CLI handles each.
Each issue is recorded once under its id, below a heading with the SDK version `eng/products.json`
pins. One test in the `*KnownIssueTests` classes named under that heading reproduces it with the
SDK alone through `KnownIssue.Reproduces`, and the code that handles it, if any, names the id in a
comment; `KnownIssueCatalogTests` keeps this file true. To update an SDK, change its version in
`eng/products.json`, run `scripts/sync.ps1` and the `Full` scope; for each reproduction that now
fails, delete the issue's section, the code that names it and the test, then update the headings.
The last section, on memory, is a limit no single SDK version owns, and has no id.

## Aspose.Cells 26.9.0

Reproductions: [CellsKnownIssueTests](tests/Aspose.Cli.Product.Cells.Tests/CellsKnownIssueTests.cs),
[CellsConvertKnownIssueTests](tests/Aspose.Cli.Product.Cells.Tests/CellsConvertKnownIssueTests.cs).

### CELLS-SVG-EGRESS

- **Defect:** an SVG picture without a raster image, one being added or one a workbook stores
  alone (as an ODS can), is rasterized by reading every image its `<image>` elements name: a bare
  path from the file system, and a `file:` or `http:` URL through a request of the engine's own.
  `WorkbookSettings.ResourceProvider` and `LoadOptions.StreamProvider` are never consulted, and
  an ODS or OTS package's SVG picture is read this way while the package loads, no `LoadFilter`
  excepted. Loading HTML or MHTML, the engine also opens the images the page names by path, and no option
  turns that off.
- **CLI behavior:** Cells reads no ODS, OTS, HTML or MHTML: it only writes them, and refuses such
  an input as `FORMAT_UNSUPPORTED` before the engine loads it. A picture a batch adds is a raster
  image: the table refuses an SVG image file. Every command runs behind the network gate of
  SECURITY.md, which blocks the engine's own requests; an SVG picture a workbook already holds is
  drawn as the engine draws it.
- **Workaround:** embed an SVG's images as base64 `data:` URIs (PNG, JPEG or GIF), or add a raster
  image; save an ODS file or an HTML table as xlsx in the application that made it, then open
  that.

### CELLS-SVG-ORPHAN

- **Defect:** removing a picture whose SVG came with a loaded workbook (`Pictures.RemoveAt`,
  `Shapes.Remove`, `Shapes.Clear` alike) leaves its SVG in the saved package as a part no
  relationship refers to, while its raster image goes. No call changes the SVG of a picture in
  place: setting `Picture.Data` to an SVG stores it as the raster image and keeps the old SVG,
  and `Picture.Copy` replaces only the raster image.

  ```csharp
  var workbook = new Workbook("has-svg.xlsx");      // one SVG picture
  workbook.Worksheets[0].Pictures.RemoveAt(0);
  workbook.Save("out.xlsx");
  // out.xlsx keeps xl/media/image1.svg, and no .rels file refers to it
  ```

- **CLI behavior:** none: deleting a picture leaves its SVG part, as the engine saves it.
- **Workaround:** embed an SVG's images as base64 `data:` URIs (PNG, JPEG or GIF) before giving
  the workbook to the CLI.

### CELLS-SPARKLINE-APOSTROPHE

- **Defect:** `SparklineGroups.Add(type, dataRange, isVertical, locationRange)` throws
  `Invalid "'"` for a data range on a sheet whose name has an apostrophe, however it is quoted.
- **CLI behavior:** none: `add sparklineGroup` is the engine's `SparklineGroups.Add`, whose
  exception is reported as `ENGINE_ERROR`.

### CELLS-COPY-NAME-SCOPE

- **Defect:** `Worksheet.Copy` from another workbook, when the copied sheet uses a
  workbook-level name that the destination also defines and the source's definition refers to
  another sheet, adds the source's definition as a name scoped to the destination's first sheet.
  Once saved, that sheet's own formulas read the added name instead of the workbook's, and the
  copied sheet's formulas read the destination's definition.
- **CLI behavior:** none: `copy_sheet` from a file is the engine's `Worksheet.Copy`, and the name
  it adds is in the output as the engine wrote it.
- **Workaround:** rename or delete the name in one of the workbooks before the copy, or copy the
  cells with `copy_range`.

### CELLS-COPY-EXTERNAL-CACHE

- **Defect:** `Worksheet.Copy` and `Range.Copy` from another workbook, when a copied formula
  reads a third workbook through a link that caches no values (such as a link written while the
  linked file was absent), give the destination's link an empty cache. The formula evaluates to
  `#REF!` in the source but, once the destination is calculated, reads the linked cells as empty,
  usually as 0.
- **CLI behavior:** none: `copy_sheet` and `copy_range` from a file leave the link as the engine
  copied it.
- **Workaround:** set those formulas again with the cell's `formula` row, or save the source in
  Excel with the linked workbook available so the link caches its values.

### CELLS-LINK-RELATIVE-TARGET

- **Defect:** a formula that reads a workbook by its full path, when that workbook is in the
  folder the edited workbook was opened from, gets a link to the file name alone. An xlsx output
  stores it as the relative target `fx.xlsx` with the relationship type `xlPathMissing`, so once
  saved to another folder the link no longer names the file in the input's folder. How Excel
  resolves an `xlPathMissing` target is unverified.
- **CLI behavior:** none: a formula a batch sets keeps the link the engine stores.
- **Workaround:** keep the linked workbook in the output's folder, or copy its values with
  `copy_range` and `pasteType` `Values` instead of a link.

### CELLS-WIDTH-EAST-ASIAN

- **Defect:** `Cell.GetWidthOfValue` (and `CellsHelper.GetTextWidth`) measures East Asian
  characters with the metrics of the cell's font when that font has no glyphs for them, such
  as Calibri or Arial, while `AutoFitColumn` and rendering draw them with a fallback font. The
  measured width is then up to half the drawn width, or, for mixed text such as
  `配件 Accessories`, can exceed the auto-fitted column by a pixel.
- **CLI behavior:** none.
- **Workaround:** give such text a font with East Asian glyphs (for example Microsoft YaHei),
  which all three measure alike, or set the column width explicitly.

### CELLS-OVERFLOW-EDGE

- **Defect:** the page layout of `SheetRender` (`OnlyArea = false`) cuts text that spills over
  empty cells when its end falls on a column edge: it stops drawing at that edge instead of
  continuing into the next empty cell, which can cut away part of the last character. The page
  layout sizes columns apart from the cell model (a few percent wider with Microsoft YaHei), so
  the edge falls elsewhere in the print-area layout of `OnlyArea = true`, which also draws text
  that ends on an edge in full.
- **CLI behavior:** none: `cells render` of a whole sheet can cut such text, while `cells render
  --area` shows it whole; PDF export lays out its own pages.
- **Workaround:** widen the column the text starts in by one or two characters, or wrap the
  text.

### CELLS-PIVOT-TOTAL-CAPTION

- **Defect:** a pivot with a column field and two or more value fields labels each value
  field's grand-total column with the word of `PivotGlobalizationSettings.GetTextOfTotal`
  before the caption (`Total Sum of Amount`); overriding the word gives `汇总 求和项:金额`, never
  Excel's Simplified Chinese form `求和项:金额汇总`, and no pivot caption property sets it.
- **CLI behavior:** none: the captions are the engine's.
- **Workaround:** leave out the column field, so the value fields are the columns and the one
  grand-total row reads `总计`, or summarize with `SUMIFS` formulas.

### CELLS-VALIDATION-DELETE-END

- **Defect:** `Cells.DeleteRows` and `Cells.DeleteColumns` leave a data validation area whose
  last rows or columns they delete one line too long: deleting row 10 of `C2:C10` leaves
  `C2:C10`, and rows 9-11 leave `C2:C9`, while a conditional format over the same cells becomes
  `C2:C9` and `C2:C8`. Deleting lines in the middle of the area shrinks it correctly, and a
  validation whose every area is deleted stays in the sheet with no area.
- **CLI behavior:** none: after `delete_rows` or `delete_columns`, a validation's `areas` read
  what the engine left.
- **Workaround:** after the delete, `remove_validation_area` with the line the engine left, or
  delete a validation left with no area.

### CELLS-PIVOT-LABEL-INDENT

- **Defect:** in a pivot whose column area holds more than one field, rendering draws each item
  label of a column field other than the innermost one, such as the products over the value
  fields or the regions over the products, about 16 pixels at 96 DPI further right than the same
  text in a plain cell, although its style has no indent, and cuts it at its column's edge even
  when the next cell is empty. The value field captions, also as the outer field, the subtotal
  and grand total labels and the innermost field's items are drawn like plain cells.
  `AutoFitColumns` leaves the indent out, so an auto-fitted column is as wide as for the text
  alone and the label is cut.
- **CLI behavior:** none: `autofit_columns` leaves the label cut.
- **Workaround:** set the column's `width` a few characters wider than the label.

### CELLS-COPY-SELF-LINK

- **Defect:** `Range.Copy` and `Worksheet.Copy` from another workbook keep a formula that reads
  the destination workbook through a link stored relative to the source, as Excel stores a link
  to a workbook in the same folder, such as `='[budget.xlsx]Budget'!$D$6` copied from a source
  beside `budget.xlsx` into it, as an external link to the destination itself, which reads as 0
  once calculated. Setting the same formula with `Cell.Formula` gives the reference inside the
  workbook, `=Budget!$D$6`.
- **CLI behavior:** none: `copy_range` and `copy_sheet` from a file keep the link as the engine
  copied it.
- **Workaround:** set such a formula again with the cell's `formula` row, which gives the
  reference inside the workbook.

### CELLS-JSON-HEADER-ONLY

- **Defect:** saving as JSON a sheet whose data is a single row, which the engine takes for the
  header, writes the header's names as members of an array, such as `["Name": null]`, which is
  not JSON; no `JsonSaveOptions` setting writes it as an empty list of records.
- **CLI behavior:** none: `cells convert --to json` writes what the engine saves.
- **Workaround:** add a data row below the header, or convert the sheet to csv with `--sheet`,
  which writes the header alone.

### CELLS-AUTOFIT-RICH-TEXT

- **Defect:** `Worksheet.AutoFitRows` fits a row to the cell's font when the cell holds rich text
  that does not wrap, so a run larger than that font is cut off, though
  `Cell.GetHeightOfValue` measures the run, and wrapped rich text is fitted.
- **CLI behavior:** none.
- **Workaround:** set the row's `height`.

### CELLS-AUTOFIT-GENERAL-NUMBER

- **Defect:** `Worksheet.AutoFitColumn` and `AutoFitColumns` make a column holding a number in
  the General format one to three pixels narrower than the engine needs to show it in full in
  some fonts: DengXian 11, the default font of Chinese Excel, and Microsoft YaHei 10 for numbers
  with decimals, and Calibri 11, Arial 10 or Times New Roman 12 for some negative ones.
  `Cell.GetStringValue` with `DisplayString` then rounds the number, 15600.75 to 15600.8, and in
  DengXian 11 rendering draws it in full only one pixel wider than that display text needs. No
  `AutoFitterOptions` setting (`FormatStrategy`, `ForRendering`) changes the width; numbers in
  other formats, dates and text are fitted in full.
- **CLI behavior:** none: `autofit_columns` sets the width the engine computes.
- **Workaround:** set the column's `width` one character wider than the fitted one.

### CELLS-SHEET-END-COMMENT-PAGES

- **Defect:** on a sheet whose `PageSetup.PrintComments` is `PrintSheetEnd`,
  `Worksheet.GetPrintingPageBreaks` reports each page of comments as the cell area A1:A1, and
  throws a `NullReferenceException` when a comment holds a character such as Chinese, Japanese or
  an emoji. Saving the same sheet as PDF or XPS, and `SheetRender`, print its comments at the end.
- **CLI behavior:** none: the CLI calls no page-break query; a PDF or XPS prints the comments at
  the end of the sheet.

### CELLS-CSV-SAVE-CULTURE

- **Defect:** saving delimited text with `TxtSaveOptions.FormatStrategy` set to
  `CellValueFormatStrategy.None` writes each number with the decimal separator of the thread's
  culture, whatever culture `TxtLoadOptions.CultureInfo` or `WorkbookSettings.CultureInfo` names.

  ```csharp
  CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
  File.WriteAllText("p.csv", "k,19.9\n");
  var workbook = new Workbook("p.csv", new TxtLoadOptions(LoadFormat.Csv) { CultureInfo = CultureInfo.InvariantCulture });
  workbook.Save("out.csv", new TxtSaveOptions(SaveFormat.Csv) { FormatStrategy = CellValueFormatStrategy.None });
  // out.csv holds k,"19,9"
  ```

- **CLI behavior:** none visible: the process's culture is en-US, so a number is written with a
  decimal point.

### CELLS-CSV-FRACTION-CULTURE

- **Defect:** loading delimited text that holds a fraction, such as `3 1/2`, throws
  `FormatException` when the thread's culture writes a decimal comma, whatever culture
  `TxtLoadOptions.CultureInfo` names.

  ```csharp
  CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
  File.WriteAllText("f.csv", "k,3 1/2\n");
  var workbook = new Workbook("f.csv", new TxtLoadOptions(LoadFormat.Csv) { CultureInfo = CultureInfo.InvariantCulture });
  // throws FormatException: The input string '3,5' was not in a correct format.
  ```

- **CLI behavior:** none visible: the process's culture is en-US, whose decimal point the load
  expects.

### CELLS-CSV-KEEP-PRECISION

- **Defect:** `TxtLoadOptions.KeepPrecision`, documented as whether a string value of length 15
  is not parsed, changes nothing: with it, values of 15, 16 and 20 digits are read as numbers,
  as without it, and keep 15 significant digits.

  ```csharp
  File.WriteAllText("p.csv", "k,12345678901234567890\n");
  var options = new TxtLoadOptions(LoadFormat.Csv) { KeepPrecision = true };
  Cell cell = new Workbook("p.csv", options).Worksheets[0].Cells["B1"];
  // cell.Type is IsNumeric and cell.StringValue 1.23457E+19, not the text
  ```

- **CLI behavior:** none: `cells convert` does not set the option, and `--convert-numbers true`
  reads such a value as a number of 15 significant digits.

### CELLS-CSV-ERROR-TEXT

- **Defect:** loading delimited text types the text of an error value, such as `#N/A`, `#DIV/0!`
  or `#REF!`, as that error even where the load's `ICustomParser` returns the text as the string
  it is, and `Cell.PutValue(text, isConverted: false)` types it the same way.

  ```csharp
  File.WriteAllText("e.csv", "k,#N/A\n");
  var options = new TxtLoadOptions(LoadFormat.Csv) { PreferredParsers = [new TextParser()] };
  Cell cell = new Workbook("e.csv", options).Worksheets[0].Cells["B1"];
  // TextParser.ParseObject returns the value it is given; cell.Type is IsError, not IsString
  ```

  Excel itself opens such a csv with the error values, so reading them as text is the CLI's
  choice; the defect is only that the engine overrides what the parser returns.

- **CLI behavior:** none: `cells convert` of a csv or tsv input gives the engine's error values,
  and a cell value a batch writes is put as the engine puts typed text, so `#N/A` becomes the
  error value and `'#N/A` the text.

### CELLS-MARKDOWN-DETECTION

- **Defect:** `FileFormatUtil.DetectFileFormat` takes lines of plain data for Markdown by their
  line ends and characters, not by any Markdown they hold: two lines `a-b` and `c-d` are
  `Unknown` when they end in LF and `Markdown` in CRLF, two numbers `1E-30` and `1E-29` are
  `Markdown`, and a Markdown table is `Unknown`.

  ```csharp
  FileFormatType Detect(string text) => FileFormatUtil.DetectFileFormat(new MemoryStream(Encoding.UTF8.GetBytes(text))).FileFormatType;
  Detect("a-b\nc-d\n");                         // Unknown
  Detect("a-b\r\nc-d\r\n");                     // Markdown
  Detect("1E-30\n1E-29\n");                     // Markdown
  Detect("| a | b |\n|---|---|\n| 1 | 2 |\n");  // Unknown
  ```

- **CLI behavior:** none visible: `cells convert` reads a `.csv`, `.tsv` or `.txt` input as
  delimited text with the options it states, without the detection, and read, search and edit take
  no such input.

### CELLS-ODS-PRINT-COMMENTS

- **Defect:** an ods save drops a sheet's `PageSetup.PrintComments`, though an ODF page layout
  can print annotations: set to print at the sheet's end or in place, it reads back as
  `PrintNoComments`. xlsx, xls and xlsb keep it.

  ```csharp
  sheet.PageSetup.PrintComments = PrintCommentsType.PrintSheetEnd;
  workbook.Save("c.ods", SaveFormat.Ods);
  // new Workbook("c.ods").Worksheets[0].PageSetup.PrintComments is PrintNoComments
  ```

- **CLI behavior:** none: an ods output leaves the setting out, as the engine saves it.
- **Workaround:** keep the workbook as xlsx, xls or xlsb, which keep the setting.

### CELLS-PRINT-AREA-BAND-COMMENTS

- **Defect:** a print area that is a band of whole columns (`A:C`) or whole rows (`1:5`), alone
  or beside bounded ranges, prints none of the comments on the band's cells, at the sheet's end
  or in place, though Excel prints them; a bounded print area (`A1:C5`) prints the comments on
  its cells. A PDF and `SheetRender` alike.

  ```csharp
  sheet.Comments[sheet.Comments.Add("A1")].Note = "a note";
  sheet.PageSetup.PrintComments = PrintCommentsType.PrintSheetEnd;
  sheet.PageSetup.PrintArea = "A:A";   // with "A1:A5" the comment prints on a page of its own
  new SheetRender(sheet, new ImageOrPrintOptions()).PageCount;   // 1: no page of comments
  ```

- **CLI behavior:** none: a PDF or image of such a print area prints no comment of the band.
- **Workaround:** give the print area as a bounded range, such as `A1:C200`.

### CELLS-AXIS-AUTOMATIC-MIN-SAVE

- **Defect:** setting `Chart.ValueAxis.IsAutomaticMinValue` to false on an axis whose `MinValue`
  was never set makes the next `Workbook.Save` throw `NullReferenceException`; setting `MinValue`
  first, which turns the automatic minimum off itself, saves.

  ```csharp
  chart.NSeries.Add("A1:A4", true);
  chart.ValueAxis.IsAutomaticMinValue = false;
  workbook.Save("axis.xlsx");   // throws NullReferenceException
  ```

- **CLI behavior:** an edit that sets `valueAxis.isAutomaticMinValue` to false without
  `valueAxis.minValue` fails at its save with `ENGINE_ERROR`, and writes nothing.
- **Workaround:** set `valueAxis.minValue`, which the engine takes as turning the automatic
  minimum off.

### CELLS-COLOR-TINT-KEPT

- **Defect:** setting `CellsColor.Color` on a theme color the engine returns, such as the default
  `SparklineGroup.SeriesColor` (accent 1, tint -0.5), keeps the tint and applies it to the new
  color: `#12AB34` reads back as `#09561A` before any save, and the saved file holds that color.
  Assigning a new `Workbook.CreateCellsColor()` with the color gives it as set.

  ```csharp
  SparklineGroup group = sheet.SparklineGroups[sheet.SparklineGroups.Add(SparklineType.Line, "A1:D1", false, area)];
  group.SeriesColor.Color = Color.FromArgb(0x12, 0xAB, 0x34);
  // group.SeriesColor.Color is #09561A
  ```

- **CLI behavior:** the row `sparklineGroup.seriesColor` sets `SeriesColor.Color`, and the edit
  reads back the color the engine holds.
- **Workaround:** none in the CLI; in Excel, set the color again from the sparkline's color
  picker.

### CELLS-SHEET-INSERT-DUPLICATE

- **Defect:** `WorksheetCollection.Insert(index, type, name)` before the last sheet takes a name
  another sheet already has, in any case, leaving two sheets of one name, which Excel never
  allows; at the end of the collection it refuses the name with a `CellsException`.

  ```csharp
  workbook.Worksheets[0].Name = "Data";
  workbook.Worksheets.Insert(0, SheetType.Worksheet, "DATA");   // taken: two sheets named Data
  workbook.Worksheets.Insert(workbook.Worksheets.Count, SheetType.Worksheet, "Data");   // throws
  ```

- **CLI behavior:** `add sheet` with such a name before the last sheet stops the edit with
  `ADDRESS_AMBIGUOUS`, as the new sheet's address names two sheets; at the end the engine's
  refusal is `ENGINE_ERROR`. Nothing is written either way.
- **Workaround:** give the new sheet a name no sheet has.

## Aspose.PDF.Drawing 26.8.0

Reproductions: [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs).

### PDF-HTML-EMPTY-FONT

- **Defect:** the HTML importer throws `EndOfStreamException` when
  `HtmlLoadOptions.CustomLoaderOfExternalResources` supplies no bytes for a font an `@font-face`
  rule names; with no bytes for a picture it imports the page without the picture.
- **CLI behavior:** `pdf convert` of such a page, whose font is on the network or outside the
  page's folder, fails as the engine fails, `FEATURE_UNSUPPORTED` naming the engine's
  exception, and writes nothing; `details.notSupplied` lists the references the loader supplied
  nothing for.
- **Workaround:** keep the font beside the page, or take the `@font-face` rule out.

### PDF-HTML-EMPTY-OBJECT

- **Defect:** the HTML importer throws `NullReferenceException` when
  `HtmlLoadOptions.CustomLoaderOfExternalResources` supplies no bytes for the data of an
  `<object>` element; with no bytes for a picture it imports the page without the picture.
- **CLI behavior:** `pdf convert` of such a page, whose object data is on the network or outside
  the page's folder, fails as the engine fails, `FEATURE_UNSUPPORTED` naming the engine's
  exception, and writes nothing; `details.notSupplied` lists the references the loader supplied
  nothing for.
- **Workaround:** keep the object's file beside the page, or use `<img>` for a picture.

### PDF-ATTACHMENT-KEYS-EMPTY

- **Defect:** `Document.EmbeddedFiles.Keys` throws an exception of the engine's own, whose name is
  obfuscated, on a document without attachments; `Count` and the enumeration give none.
- **CLI behavior:** an attachment is addressed by its position, `attachment/1`, from the
  enumeration, which needs no key; deleting one reads `Keys`, which then holds it.

### PDF-ATTACHMENT-DESCRIPTION-NOT-SAVED

- **Defect:** setting `FileSpecification.Description` on an attachment the engine read from a
  file, `Document.EmbeddedFiles[i]`, is not saved: the saved file keeps the old description, while
  `MIMEType` set the same way is saved.
- **CLI behavior:** none: the `description` row of an attachment reads back what the saved file
  holds.

### PDF-EVALUATION-COLLECTIONS

- **Defect:** in evaluation mode `PageCollection.Count` gives every page while its enumerator gives
  the first four only, and copying the collection (`CopyTo`, as a `ToArray` does) fills the rest
  with null; reading the annotations of a later page, or a later element of any collection, throws
  `IndexOutOfRangeException` ("At most 4 elements (for any collection) can be viewed in
  evaluation mode").
- **CLI behavior:** read lists what the enumerators give and the inventory counts `pages` with
  `Count`, so without a license a document of six pages counts 6 and lists 4; an address past the
  fourth, such as `page/5`, is the engine's error, `ENGINE_ERROR`.

### PDF-PAGE-COPY-OWN-WIDGETS

- **Defect:** copying a page that holds form fields within its own document
  (`Pages.Insert(1, Pages[1])`) and saving it leaves the form's field count as it was, and the
  saved copy holds twice the widget annotations the original does.
- **CLI behavior:** none: `add page` with a `page` of the same document copies it as the engine
  does; `pdf read --kind annotation` and `--kind field` show the result.

### PDF-IMAGES-READ-WRITES

- **Defect:** reading `Page.Resources.Images` creates an empty `/XObject` dictionary in the page's
  resources of a page that has none, which a save then writes.
- **CLI behavior:** none visible: no read of the table reads a page's images, so an edit saves
  what the engine alone saves for the same change.

### PDF-HTML-EGRESS

- **Defect:** the HTML importer requests http(s) stylesheets and images, and runs script, before
  it calls `HtmlLoadOptions.CustomLoaderOfExternalResources`. The Markdown importer and SVG images
  (`Image.File`, `ImageStamp`) have no resource hook: they fetch remote resources and read any
  local file a reference names, and Markdown resolves relative paths against the process working
  directory.
- **CLI behavior:** the process's network gate, which every command passes before it runs, blocks
  each http, https, ftp and file request the engine makes, so the request fails inside the process
  and the result lists its address in `REMOTE_RESOURCES_BLOCKED`. `pdf convert` of an HTML page
  supplies through the loader only the ordinary files beneath the page's folder. No PDF command
  reads Markdown (`words convert` does), and an image the CLI hands the engine is no SVG. The
  importer runs the page's script, which has no `XMLHttpRequest`; what a script adds to the page
  is loaded through the same gate and loader (`PdfScriptReachTests`).
- **Workaround:** keep resources beside the HTML page and reference them by relative path, or
  convert HTML with `aspose-cli words convert`, which makes no network request.

### PDF-MOVE-BOOKMARK

- **Defect:** destination getters read an omitted (null) coordinate as 0 and no public API writes
  one (`XYZExplicitDestination` with `NaN` writes an invalid `NaN` token), so a destination that
  named a moved page cannot always be rebuilt exactly; the SDK has no page move.
- **CLI behavior:** none: the CLI has no page move. A page copied with `add page` and its original
  deleted keep what the engine keeps.
- **Workaround:** after copying and deleting pages, `pdf read --kind bookmark` shows where each
  bookmark leads; delete and add again the ones to change.

### PDF-LINK-STALE-PAGE

- **Defect:** a link annotation keeps the destination it read first, page number included, so
  after a page deletion in memory a link read before it still names the number its page had:
  one to a deleted page can name a page the document still holds. Bookmarks and named destinations
  read their new numbers, and the saved file leads to the right page, or to none.
- **CLI behavior:** none visible: an edit reads each operation back from the saved file, where the
  engine has resolved the page.

### PDF-REDACT-CALCULATED

- **Defect:** a redaction that removes a form field leaves the form's calculation order (`/CO`)
  naming it, so the field's object, with its value and appearance, stays in the saved file. Nor can
  the order be repaired through the public API: `Form.CalculatedFields` has no getter, and setting
  it to any fields writes no order at all.
- **CLI behavior:** none: `redact_area` is the engine's redaction alone, and the field stays in
  the calculation order as the engine leaves it.

### PDF-REDACT-EMPTY-PARENT

- **Defect:** a redaction that removes every widget of a field that holds them apart from itself,
  as children or as the buttons of a radio group, leaves the field in the form without a widget and
  with its value; one that removes the only field under a parent in the form's hierarchy, such as
  `form.person.name` under `form.person`, leaves the parent in the form as a field of its own,
  without a value or a widget.
- **CLI behavior:** none: `redact_area` is the engine's redaction alone, and such a field stays as
  the engine leaves it.

### PDF-REDACT-RADIO-BUTTONS

- **Defect:** a redaction that removes some buttons of a radio group whose form was read before
  it leaves the group counting them while its options lose them, and setting the group's value
  then writes it and throws `IndexOutOfRangeException`.
- **CLI behavior:** none: `redact_area` is the engine's redaction alone, and the group stays as
  the engine leaves it.
- **Workaround:** set the group's value, or redact its selected button, in an edit of its own
  after the redaction.

### PDF-PAGE-COPY-FIELDS

- **Defect:** no public API changes the order of a document's pages: `PageCollection` has no
  move, `Remove` throws `NotSupportedException`, inserting a page the document holds copies it with
  each field's widget still on the original page, and inserting one deleted from it throws. Copied
  into another document and inserted back, a page brings each of its form fields under a new
  top-level name (`ssn1` beside `ssn`, `form1.person.name` beside `form.person.name`), while the
  original field stays in the form without a widget and with its value.
- **CLI behavior:** none: `add page` with a `page` and `import_pages` copy pages as the engine
  does; `pdf read --kind field` shows the names the copy has.
- **Workaround:** move the pages without form fields around the pages that hold them instead,
  which gives the same order without copying a field, and move the pages that hold one field's
  widgets together; where neither gives the order, as with fields that have parents in their names
  on both sides of a move, tell the user the move cannot keep the form.

### PDF-PDFA-ATTACHMENT-TYPE

- **Defect:** `Document.Convert` to PDF/A-3B gives every attachment that has no media type the
  type `application/pdf`, whatever its content.
- **CLI behavior:** none: `pdf convert --to pdfa-3b` is the engine's conversion; its result holds
  the engine's log in `conversionLog`.

### PDF-ATTACHMENT-COPY-TYPE

- **Defect:** `EmbeddedFiles.Add` with the `FileSpecification` of another document's attachment
  gives the added attachment no media type (an empty `MIMEType`); its name, description and
  contents are kept. The type is read from the file only when first asked for, and a
  specification whose `MIMEType` was not read before the add reads empty afterwards as well, so
  the type is lost unless it is read first.
- **CLI behavior:** none: `pdf merge` is `PdfFileEditor.Concatenate`; an attachment's `mimeType`
  reads what the merged file holds.

### PDF-OUTLINE-DELETE-TITLE

- **Defect:** `OutlineItemCollection.Delete()` removes bookmarks by title rather than the item
  it is called on: it removes the first bookmark with that title it finds, which may be a
  different one, and when bookmarks at different levels share the title it can remove several.
- **CLI behavior:** none visible: deleting a bookmark removes that item from its parent with
  `Remove`, never by title.

### PDF-NAMES-WITHOUT-DESTS

- **Defect:** `Document.NamedDestinations.Names`, `Count` and the lookup by name
  (`NamedDestinations[name]`) throw `NullReferenceException` when the catalog's name tree has no
  `Dests` entry, for example a tree that holds only attachments, such as the one a merged document
  gets with the attachments of its inputs.
- **CLI behavior:** none visible: the inventory counts destinations through
  `Document.Destinations`, and no command reads `NamedDestinations`.

### PDF-NAMES-BESIDE-DESTS

- **Defect:** when the catalog has both a `Dests` dictionary and a name tree of destinations
  (`Names` → `Dests`), `Document.NamedDestinations` reads only the dictionary: a name only the
  tree holds is not listed and its lookup returns null, so a link or bookmark that leads through
  it reads as leading to no page, while a reader follows the tree. The saved file keeps both.
- **CLI behavior:** a link's or bookmark's `destinationPage` reads as the engine resolves it: null
  for such a name.

### PDF-TAGGED-CONTENT-WRITES

- **Defect:** reading `Document.TaggedContent` rewrites the loaded document: it sets an empty
  title to `Tagged PDF`, creates XMP metadata dated now that declares PDF/UA (`pdfuaid:part` 1)
  and drops the PDF/A identification, so `IsPdfaCompliant` turns false.
- **CLI behavior:** the structure tree is a kind of its own, `taggedContent`, which only `pdf read
  --at taggedContent` reads, on a document nothing saves. The engine changes the title and format
  of that read's document in memory the moment it is read, so no other row is read after it.

### PDF-IMPORT-INFO-PLACEHOLDER

- **Defect:** the HTML and Markdown importers set the document title, author and subject to
  `Aspose`, whatever the source states; the HTML `title` element is ignored.
- **CLI behavior:** none: `pdf convert` of an HTML page writes what the engine writes; `pdf edit`
  sets `info.title`, `info.author` and `info.subject`.
- **Workaround:** set those rows of `document` with `pdf edit`.

### PDF-IMPORT-LINK-ADDRESSES

- **Defect:** the HTML and Markdown importers resolve every relative link against a local folder,
  the HTML's `BasePath` or else the process working directory, and write the `file:` address they
  get into the link annotation's text and, for a link to another file, into its URI action, so
  the PDF holds the absolute path of that folder; an absolute or share path becomes a `file:`
  address there too. A link to an element `id` (`href="#part"`) gets neither a destination nor an
  action; one to an `<a name="part">` anchor leads to its page. The importers write no other
  action or file specification for a link.
- **CLI behavior:** none: the output holds the `file:` addresses the engine writes, the folder of
  the HTML page among them.
- **Workaround:** mark each place a link leads to with `<a name="part"></a>`, and give a link to
  another document its https address.

### PDF-HTML-FORM-NAMES

- **Defect:** the HTML importer keeps the `name` of single-line text inputs only. It names check
  boxes, selects, text areas and buttons after their `id` attribute, or itself when they have
  none, and radio groups always itself (such as `field_#0`, `field_1` and `radio`), and it drops
  their `value` attributes: a check box is checked with `Yes` and radio buttons
  take the values `Item0`, `Item1` and so on. `HtmlLoadOptions` has no option to keep them.
- **CLI behavior:** none: `pdf read --kind field` gives the names the engine chose.
- **Workaround:** match each generated field to the label beside it by its `rect` before filling.

### PDF-HTML-FORM-INPUTS

- **Defect:** the HTML importer drops inputs of type `email`, `tel`, `url`, `time`,
  `datetime-local`, `month`, `week`, `color`, `range`, `file`, `image` and `hidden`: they become
  no form field, and the value of a hidden input is lost.
- **CLI behavior:** none: the PDF holds the fields the engine made.
- **Workaround:** give text-like inputs `type="text"`; place the picture of an image input with
  `stamp_image`.

### PDF-HTML-CHECKBOX-BOX

- **Defect:** the HTML importer gives a check box neither a border nor a border colour, so its
  appearances draw no box: an unchecked box shows nothing and a checked one only its check
  mark. Radio buttons get a black border.
- **CLI behavior:** none: the check box is drawn as the engine imports it.

### PDF-PDFA-RADIO-APPEARANCE

- **Defect:** the HTML importer writes a radio group's field into its page's annotations with an
  appearance stream of its own. `Document.Convert` to PDF/A returns success and leaves it, and
  validation of the saved file then fails the clause on annotation appearances, 6.3.3 in PDF/A-2
  and PDF/A-3 and 6.5.3 in PDF/A-1 (the appearance of a button field must be a subdictionary of
  states).
- **CLI behavior:** none: the result of `pdf convert --to pdfa-*` holds the engine's return value
  and log; `pdf validate` gives the engine's verdict on the output.
- **Workaround:** flatten the named fields (`flatten_field`, `flatten_form`) before converting.

### PDF-PDFA-LOG-REPEATS

- **Defect:** the PDF/A log that `Document.Validate` writes lists a problem once for every use
  of the object it concerns, and marks it convertible (`Convertable="True"`) on only one of
  those entries: a font that is not embedded is reported as both fixable and not on the same
  page, although converting to the profile embeds it.
- **CLI behavior:** none: `pdf validate` and `pdf convert --to pdfa-*` give the engine's log as it
  wrote it.

### PDF-TEXT-GAP-SPACE

- **Defect:** text extraction and `TextFragmentAbsorber` read a horizontal gap of about a
  sixth of an em or more between two text runs as a space, and search the text with those
  spaces; no `TextSearchOptions` or `TextExtractionOptions` setting turns this off. Word leaves
  such a gap between East Asian text and digits or Latin letters (automatic spacing), so
  `2026年10月31日` in a Word-made PDF extracts as `2026年 10月 31日` (two spaces in the plain
  mode) and a search for it finds nothing.
- **CLI behavior:** none: `pdf search` matches what `TextFragmentAbsorber` finds; a page's `text`
  reads the space.
- **Workaround:** in a regular expression, allow the gaps with ` *`.

### PDF-ATTACHMENT-NAME-OPENS-FILE

- **Defect:** the `FileSpecification.Name` setter opens the file its value names, relative to
  the process working directory, for reading and writing. It throws when that file is open
  elsewhere, for example as the stream the specification was built from.
- **CLI behavior:** none visible: `add attachment` names the file through the constructor, and an
  attachment's `name` row is read only.

### PDF-REDACT-TEXT-SHIFT

- **Defect:** `RedactionAnnotation.Redact()` removes the glyphs under the annotation without
  keeping their advance, so a text run that starts where the previous one ended, without a
  position of its own, moves left by the removed width, under the cover. Word writes the runs
  of a line that way. `PdfAnnotationEditor.RedactArea` and replacing the text with an empty
  string move the runs the same way, and setting `TextFragment.Position` to put them back
  draws other runs of the line over each other.
- **CLI behavior:** none: each `redact_area` is the engine's redaction alone.
- **Workaround:** redact a line's matches from the right to the left, or search again between two
  redactions.

### PDF-ENCRYPTED-INFO-TEXT

- **Defect:** a document information entry (title, author, subject, keywords or a custom entry)
  set in a session whose save encrypts the file is written with only the low byte of each
  character, so text beyond Latin-1, such as `采购申请`, reads back garbled. Entries read from the
  file and entries written to a file saved without encryption are kept.
- **CLI behavior:** none: an encrypted output's document information is written as the engine
  writes it.
- **Workaround:** keep the document information in Latin-1 while the document is encrypted, or
  `decrypt` it before such text is set.

### PDF-INFO-KEY-BEYOND-LATIN1

- **Defect:** a document information entry whose name has a character beyond Latin-1, such as
  `项目编号`, is saved under a name written from each character's code as if it were one byte,
  so the name reads back garbled while its value is kept. Names within Latin-1 are kept.
- **CLI behavior:** none: `add info` passes the key to the engine as given.
- **Workaround:** name the entry in Latin-1, such as ASCII letters, and put the text in its value.

### PDF-SIGN-FULL-FONT

- **Defect:** `PdfFileSignature.Sign` embeds the whole font program the signature appearance
  draws in, visible or not: Arial (about 560 KB) by default, a complete CJK font such as SimSun
  (about 10 MB) for a reason, location, contact or signer name in Chinese. Helvetica, which is not
  embedded, keeps printable ASCII without a font; any other character embeds a whole font again.
- **CLI behavior:** none: `pdf sign` passes the texts and the `--font` given, and the engine
  embeds what it embeds.
- **Workaround:** keep `--reason`, `--location` and `--contact` in ASCII, and use a certificate
  whose subject name is ASCII.

### PDF-SIGN-CJK-SIGNER-FONT

- **Defect:** `PdfFileSignature.Sign` chooses the font of a visible signature's appearance from
  its reason, location and contact alone. When none of them has Chinese text, it draws in Arial,
  and a signer name in Chinese is left out: the first line reads `Digitally signed by 'CN='`.
  Japanese text, such as `批准`, gives a Japanese font, MS Gothic, which leaves out a simplified
  Chinese name such as `张伟`. Naming a Chinese font, such as SimSun, through
  `SignatureCustomAppearance.FontFamilyName` shows the name.
- **CLI behavior:** none: `pdf sign --font` names the appearance's font family; without it the
  engine picks one.
- **Workaround:** give `--reason` in simplified Chinese, such as `采购申请`, or sign with a
  certificate whose subject name is in Latin letters.

### PDF-RENDER-THIN-GLYPH

- **Defect:** `PngDevice` and the other raster devices leave out thin glyph strokes, such as
  the underscores of SimSun, at some positions on the page below about 300 DPI: the line is not
  drawn at all, not even as grey. Paths of the same thickness are drawn.
- **CLI behavior:** none: `pdf render` and `pdf convert --to tiff` draw at the resolution they
  use.
- **Workaround:** render the page with `pdf render --dpi 300` or higher.

### PDF-CONCURRENT-SAVE-MODDATE

- **Defect:** `Document.Save` writes the modification date (`ModDate`) into the document
  information of a PDF it saves, but may leave it out when other documents of the same process
  are being saved at the same time. How many of the concurrent outputs lose it varies from run
  to run; saved one at a time, every output has it.
- **CLI behavior:** none visible: every command runs in its own process.

### PDF-SIGN-RECT-ROTATED

- **Defect:** `PdfFileSignature.Sign(page, visible, rectangle, signature)` places the rectangle in
  the page's unrotated space from its lower-left corner, but on a page rotated by 90 or 270
  degrees it first swaps the rectangle's x with y and its width with height; on a page rotated by
  180 degrees it does not swap them.
- **CLI behavior:** none: `pdf sign --rect` is the engine's rectangle, passed as given.

### PDF-TEXT-RECT-ROTATED-CROP

- **Defect:** on a rotated page that has a crop box, `TextFragment.Rectangle` is given in the
  space of the visible, rotated page, but not from that page's lower-left corner: at 90 degrees
  its x is offset by the crop box's bottom, at 270 degrees its y by the crop box's left, at 180
  degrees neither, and upright both, as the page's own space has them.
- **CLI behavior:** none: a `pdf search` hit's `rect` is the engine's rectangle as it gives it.

### PDF-REDACT-ROTATED

- **Defect:** on a rotated page, `RedactionAnnotation.Redact()` draws its cover where its
  rectangle lies in the page's unrotated space but removes the text it finds there in the
  rotated page's space, so text under the cover stays in the file.
- **CLI behavior:** none: `redact_area` redacts the page as it is.

### PDF-TEXT-SEARCH-WRAPS-CONTENT

- **Defect:** searching a page's text with `TextFragmentAbsorber` changes the page it only
  reads: on a page that carries a stamp, it wraps the page's content in one more saved graphics
  state (`q` … `Q`), which draws nothing different.
- **CLI behavior:** none visible: `pdf search` runs on a document it never saves.

### PDF-DOCX-EMBED-FONTS

- **Defect:** saving as docx writes `w:embedTrueTypeFonts` and `w:embedSystemFonts` into
  `word/settings.xml` but embeds at most the few font subsets the PDF itself embeds, and none of
  the fonts it does not, so the next save by Aspose.Words or Word embeds every other font the
  document uses in full: a one-page document in a standard PDF font grows from about 7 KB to
  about 3 MB. `DocSaveOptions` has no setting for these flags.
- **CLI behavior:** none: a Words command saves such a document as the engine does, with the
  fonts embedded in full.
- **Workaround:** deliver the docx as `pdf convert` wrote it, or write it with
  `aspose-cli words convert <pdf> --to docx`, which asks for no embedded fonts.

### PDF-ENCRYPTED-OBJECT-STREAMS

- **Defect:** `OptimizeResources` with `CompressObjects` on an encrypted document writes a file
  that no reader opens: Aspose.PDF reads it back with neither encryption nor text, and other
  readers report a corrupt object stream, with the password or without. Decrypting first and
  encrypting after the optimization writes a sound file.
- **CLI behavior:** none: `optimize` with `compressObjects` on an encrypted document writes what
  the engine writes.
- **Workaround:** optimize before `encrypt`, or leave `compressObjects` unset on an encrypted
  document.

### PDF-FORM-RECURSION-CRASH

- **Defect:** reading the text of a page (`TextAbsorber`) whose Form XObject draws itself
  recurses until the stack overflows, which ends the process at once with exit code `0xC00000FD`
  and no exception that code could catch. Rendering the same page with `PngDevice` completes.
- **CLI behavior:** a command that reads the text of such a page, such as `pdf read --kind text`
  or `pdf search`, ends the same way: exit `0xC00000FD`, nothing on stdout and the runtime's
  `Stack overflow.` and stack trace on stderr.

### PDF-MD-FIELD-REPEATS

- **Defect:** the Markdown export writes the value of a flattened text field that the HTML
  importer made, which it draws with a border, twice: as a one-cell table and in the line of text
  the field sits in. `TextAbsorber` reads the value once on the same page. A bordered text field
  made with `TextBoxField` is written once.

  ```csharp
  // form.html: <html><body><p>Name: <input type="text" name="name" value=""></p></body></html>
  var html = new Document("form.html", new HtmlLoadOptions());
  html.Form.Fields.Single().Value = "ZEBRA42"; html.Save("form.pdf");
  var doc = new Document("form.pdf");
  doc.Form.Flatten(); doc.Save("flat.pdf");
  var re = new Document("flat.pdf");
  re.Save("flat.md", SaveFormat.Markdown);  // ZEBRA42 twice: a one-cell table, then its line
  var abs = new TextAbsorber(); re.Pages.Accept(abs);
  Console.WriteLine(abs.Text);              // ZEBRA42 once
  ```

- **CLI behavior:** none: `pdf convert --to md` writes the engine's export.
- **Workaround:** to search or count the values, convert the flattened file to `txt`.
## Aspose.Slides.NET6.CrossPlatform 26.9.0

Reproductions: [SlidesKnownIssueTests](tests/Aspose.Cli.Product.Slides.Tests/SlidesKnownIssueTests.cs),
[SlidesConvertKnownIssueTests](tests/Aspose.Cli.Product.Slides.Tests/SlidesConvertKnownIssueTests.cs).

### SLIDES-AUTOFIT-RECT

- **Defect:** in a text frame that shrinks its text on overflow, `IParagraph.GetRect` reports
  laid-out lines that end past the right edge of the frame once the text overflows, while
  rendering shrinks the text and draws every line inside the frame. The runs' rectangles
  (`IPortion.GetRect`) follow the shrunk text as rendering draws it.
- **CLI behavior:** none: the CLI passes the engine's behavior on.

### SLIDES-CHART-TITLE

- **Defect:** loading a chart whose automatic title is implicit (no `c:title`,
  `autoTitleDeleted` 0) reports a title that overlays the plot, and saving writes it, so the plot
  area grows and its automatic axis scale can change.
- **CLI behavior:** none: the CLI passes the engine's behavior on, so an edit, a conversion or a
  rendering of such a chart writes or draws the title over the plot.
- **Workaround:** inspect those charts in PowerPoint and restore their title layout there.

### SLIDES-CJK-FALLBACK

- **Defect:** when a run's font has no glyph for a Chinese, Japanese or Korean character, the
  engine picks a fallback font per glyph and alternates between Japanese and Chinese fonts inside
  one word (MS Gothic and SimSun on Windows), so rendered and exported text mixes stroke weights.
  PowerPoint draws the run in one East Asian font.
- **CLI behavior:** none: the CLI passes the engine's behavior on and adds no fallback rules.
- **Workaround:** give the template's theme an East Asian font that has the glyphs.

### SLIDES-FALLBACK-STDOUT

- **Defect:** every rendering or export that uses a font fallback rule writes
  `Updating of Inner rules` to `Console.Out`.
- **CLI behavior:** none visible; every Slides engine call runs with `Console.Out` muted, so the
  JSON result stays intact.

### SLIDES-FONT-REGISTRY

- **Defect:** font initialization casts every value under
  `HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts` to `string`, so a `REG_DWORD` or
  `REG_BINARY` value written by other software throws `InvalidCastException`.
- **CLI behavior:** none: the CLI passes the engine's exception on. A command whose engine calls
  lay text out, such as a save, a conversion or a rendering, fails: `slides create`,
  `slides convert` and `slides render` with `FEATURE_UNSUPPORTED` and a message such as
  `Slides failed inside its document engine: Unable to cast object of type 'System.Int32' to type 'System.String'.`
  for a `REG_DWORD` value, and `slides edit` with `ENGINE_ERROR`, whose `details.engineException`
  is `System.InvalidCastException`.
- **Workaround:** remove the values under that key that are not strings; Windows writes only
  string values there.

### SLIDES-ODP-NATIVE-CONTENT

- **Defect:** saving a presentation as ODP writes each native chart as a picture and drops the
  slide transitions and the sections, although OpenDocument presentations can hold all three.
- **CLI behavior:** none: `slides convert` to ODP writes what the engine's save writes.
- **Workaround:** keep the PPTX for further edits.

### SLIDES-PPT-NATIVE-CONTENT

- **Defect:** saving a presentation as PPT writes each native chart as a picture and drops the
  table styles, so a styled table keeps its cells and text but not its fills, borders and text
  formatting. PowerPoint's own PPT save keeps the chart editable and the table's look.
- **CLI behavior:** none: `slides convert` to PPT writes what the engine's save writes.
- **Workaround:** keep the PPTX for further edits.

### SLIDES-PPT-TEMPLATE-LAYOUTS

- **Defect:** loading some PowerPoint 97-2003 binary files (PPT, PPS, POT) lists extra layouts
  in `LayoutSlides`, with the name and type of another layout (a second "Title Slide" and
  "Section Header" for a design whose PPTX has one of each). They belong to the master like the
  others, but a slide added on one of them makes the PPTX save throw `KeyNotFoundException` for
  `Aspose.Slides.LayoutSlide`. A PPTX save of the loaded file without such a slide drops them.
- **CLI behavior:** none: the CLI passes the engine's behavior on; `slides create --template` and
  `slides edit` see the layouts of a PPT, PPS or POT file as the engine lists them.

### SLIDES-MD-INHERITED-BULLETS

- **Defect:** Markdown export writes a paragraph as a list item only when the paragraph itself
  sets its bullet. Paragraphs whose bullets come from the layout or master, as in a body
  placeholder, are written as plain lines without their level; no `MarkdownSaveOptions`
  setting changes this.
- **CLI behavior:** none: `slides convert --to md` writes such paragraphs as plain lines.

### SLIDES-ALT-TEXT-TAB

- **Defect:** saving a shape's alternative text writes a tab in it unescaped into the `descr`
  attribute, and loading the file reads the text without the tab.

  ```csharp
  using (var deck = new Presentation())
  {
      deck.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 50, 50).AlternativeText = "a  b\tc";
      deck.Save("alt.pptx", SaveFormat.Pptx);       // descr="a  b<tab>c"
  }
  using var loaded = new Presentation("alt.pptx");  // Shapes[0].AlternativeText is "a  bc"
  ```

- **CLI behavior:** none: alternative text written with a tab reads back from the saved file
  without it.
- **Workaround:** write spaces in place of tabs in alternative text.

### SLIDES-CHART-NO-SAMPLE-CATEGORIES

- **Defect:** a chart added without sample data
  (`IShapeCollection.AddChart(…, initWithSample: false)`) is saved as PPTX without categories
  (`c:cat`) for one of its series. With one series, the reopened chart has no categories, whether
  they came from worksheet cells or from literals; with two series, the file gives the categories
  to one series only.
- **CLI behavior:** none.

### SLIDES-CHART-NO-SAMPLE-NAME

- **Defect:** on a chart added without sample data, a PPTX save throws `NullReferenceException`
  when its series has no name cell: a series named by a string literal (`Name.DataSourceType`
  `StringLiterals`), or one added by `IChartSeriesCollection.Add(ChartType)` without a name. The
  series named by a literal saves on a chart added with sample data.
- **CLI behavior:** none.

### SLIDES-CHART-SHEET-CREATED

- **Defect:** `IChartDataWorkbook.GetCell` with a worksheet index or name the workbook does not
  have adds a worksheet instead of failing: index 1 of a workbook of one sheet adds a second
  `Sheet1`, the name `sheet1` beside `Sheet1` adds `sheet1`, and a null name adds a sheet whose
  name is null. The embedded workbook is saved with these names, the null one empty, although
  sheet names are unique without regard to case and never empty.
- **CLI behavior:** a chart cell that an address or an argument names on a worksheet the workbook
  does not have is `ADDRESS_NOT_FOUND`, as any target that does not exist is; `GetCell` is not
  called for it.

### SLIDES-CHART-CELL-RANGE

- **Defect:** `IChartDataWorkbook.GetCell` takes row -1, row 1048576 and column 16384, which lie
  outside a worksheet, and the embedded workbook is saved with cells `A0`, `A1048577` and `XFE1`.
- **CLI behavior:** none.

### SLIDES-CHART-CELL-READ

- **Defect:** reading an empty cell through `IChartDataWorkbook.GetCell` changes the saved
  embedded workbook: two saves write the same worksheet part, and after `GetCell(0, 300, 70)` it
  holds a cell `BS301`.
- **CLI behavior:** none.

### SLIDES-CHART-POINT-REMOVE

- **Defect:** on a chart none of whose point values has been read, removing a data point
  (`IChartDataPointCollection.RemoveAt` or `Remove`, or `IChartDataPoint.Remove`) lowers `Count`
  but keeps the point: a PPTX save writes it, and the first point value read afterwards brings it
  back. Removing point 2 of the 4 of a sample chart's series leaves 3; the saved chart reopens
  with 4, and once a value is read the series in memory has 4 points again, with the indexes 0,
  1, 3 and 3. After any point value has been read, the same calls remove the point.
- **CLI behavior:** none.

### SLIDES-CHART-POINT-BY-INDEX

- **Defect:** `IChartDataPointCollection.GetOrCreateDataPointByIdx` adds empty points up to the
  index, as documented, but a PPTX save drops them: a series of 4 points has 11 once point 10 is
  created, and 4 once saved and reopened.
- **CLI behavior:** none.

### SLIDES-CHART-MIXED-ORDER

- **Defect:** a chart whose series have different types reopens with its series in another
  order: a pie series added after the three column series of a sample chart is the first series
  once the chart is saved as PPTX and reopened.
- **CLI behavior:** none.

### SLIDES-CHART-DATA-SETTER

- **Defect:** the `IBaseChartValue.Data` setter treats a string by where the value comes from: a
  point value read from a worksheet cell writes it into the cell, a point value given as a
  literal (`DoubleLiterals`) throws `ArgumentException` ("Assigned value is not appropriate to
  DataSourceType."), and a series name read from a worksheet cell throws the same
  `ArgumentException`.
- **CLI behavior:** none.

### SLIDES-CHART-UNUSED-VALUE

- **Defect:** a point value member the chart type does not use, such as `IChartDataPoint.XValue`
  of a column chart, has no cell and reads `Data` as null, and setting its `Data` throws
  `NullReferenceException` instead of an exception that names the cause.
- **CLI behavior:** none.

### SLIDES-CATEGORY-AXIS-DATE

- **Defect:** `IAxis.CategoryAxisType` set to `CategoryAxisType.Date` on the horizontal axis of a
  column chart with text categories, as `AddChart` creates it with sample data, reads `Date` in
  memory, but a PPTX save writes a text axis (`c:catAx`, no `c:dateAx`), so the reopened axis
  reads `Text`.
- **CLI behavior:** none.

### SLIDES-SLIDE-NUMBER-SETTER

- **Defect:** `ISlide.SlideNumber` reads the slide's index plus `IPresentation.FirstSlideNumber`,
  but its setter moves the slide to the index of the value minus 1, as if numbering started at 1.
  With `FirstSlideNumber` 5 and three slides, setting the first slide's number to 7, the last
  slide's number, throws `PptxEditException`, and setting it to 4 throws the same exception after
  changing `FirstSlideNumber` to 4.
- **CLI behavior:** none.

### SLIDES-INSERT-CLONE-RANGE

- **Defect:** `ISlideCollection.InsertClone` takes any index from 0 to `Count`, and refuses a
  larger one with `PptxEditException` "Index must lay within [0..Count-1] range.", a range that
  leaves out `Count`.
- **CLI behavior:** none.

### SLIDES-SECTION-INSERT-END

- **Defect:** `ISectionCollection.AddEmptySection(name, index)` refuses the index `Count` with
  `PptxEditException`, while the engine's other insert calls, such as
  `ISlideCollection.InsertEmptySlide`, take `Count` as the end; only `AppendEmptySection` adds a
  section at the end.
- **CLI behavior:** none.

### SLIDES-TABLE-INDEX-MESSAGE

- **Defect:** `ITable[column, row]` out of range throws `IndexOutOfRangeException` with messages
  that give different upper bounds: in a table of 3 columns and 2 rows, column 3 is refused with
  "value of index = 3 is out of range 0..3", a range that includes it, and row 2 with "value of
  index = 2 is out of range 0..1".
- **CLI behavior:** none.

### SLIDES-BULLET-CHAR-UNSET

- **Defect:** `IBulletFormat.Char` of a paragraph that sets no bullet reads U+FFFF, a Unicode
  noncharacter, while the effective bullet character is U+2022. Its documentation names no value
  for an unset character, as that of `IBulletFormat.Height` names `float.NaN`.
- **CLI behavior:** none.

### SLIDES-FOOTER-LAYOUT-ONLY

- **Defect:** `ISlideHeaderFooterManager.SetFooterVisibility(true)`,
  `SetSlideNumberVisibility(true)` and `SetDateTimeVisibility(true)` on a slide whose layout has
  no such placeholder return without an error and change nothing, even when the master has the
  placeholder: `IsFooterVisible`, `IsSlideNumberVisible` and `IsDateTimeVisible` still read false,
  and the slide gets no shape.
- **CLI behavior:** none.

### SLIDES-PARENT-GROUP-ROOT

- **Defect:** `IShape.ParentGroup` of a shape that is not grouped (`IsGrouped` false) is the
  slide's root group shape (`OfficeInteropShapeId` 1), which is not among the slide's shapes; the
  documentation says it is null for a shape that is not grouped.
- **CLI behavior:** none.

### SLIDES-UNIQUEID-TAGS

- **Defect:** once the `IShape.UniqueId` of one shape has been read, saving writes a custom tag
  part (`ppt/tags/tagN.xml`) holding the unique id for every shape of the presentation, those of
  its masters and layouts too, and every later save of the file keeps them. A new
  presentation with two shapes is saved without such a part before the read, and with more parts
  than the slide has shapes after it.
- **CLI behavior:** none.

### SLIDES-ENVIRONMENT-TAGS

- **Defect:** every presentation the engine creates or opens has five custom tags in
  `IPresentation.CustomData.Tags` that neither the file nor the caller set, each named `AS_` and a
  word: the .NET version, the operating system and its version, and the engine's release date,
  title and version. A PowerPoint file without tags opens with them, and a save writes all five
  even after one is removed, so each saved file names the operating system that saved it.
- **CLI behavior:** none.

### SLIDES-DUPLICATE-SHAPE-ID

- **Defect:** loading a slide on which two shapes share an id (`p:cNvPr id`) gives the second one
  the slide's highest id plus 1, without a warning through `LoadOptions.WarningCallback`: shapes
  of ids 2, 2 and 4 load as 2, 5 and 4.
- **CLI behavior:** none.

### SLIDES-IMAGE-PART-WARNING

- **Defect:** loading a presentation whose image part is cut short or empty reports no warning
  through `LoadOptions.WarningCallback`, while a missing image part is reported (`DataLoss`,
  "Empty image part.").
- **CLI behavior:** none.

### SLIDES-IMAGE-EMPTY-PART

- **Defect:** a presentation whose image part is empty (0 bytes) opens, but the image's
  `IPPImage.Width` and `Height` throw `EndOfStreamException` ("Unable to read beyond the end of
  the stream."); its `BinaryData` reads 0 bytes.
- **CLI behavior:** none: a `slides read` that reaches the image fails with `ENGINE_ERROR`, whose
  `details.at` is the image's address (`image/<n>`) and `details.row` is `width`; a read whose
  `--kind` leaves out `image` succeeds.

### SLIDES-ACTIVEX-EMPTY-SAVE

- **Defect:** an ActiveX control part with an all-zero `classid` and no properties loads as a
  control, and saving the presentation throws `NullReferenceException`; with one property, it
  saves.
- **CLI behavior:** none.

### SLIDES-IMAGE-UNKNOWN-BYTES

- **Defect:** `IImageCollection.AddImage(byte[])` takes bytes in no image format it decodes, an
  SVG document among them, without an error or a warning. The image reads `ContentType`
  `image/unknown` and the size 0 × 0, a picture frame of it draws nothing, and a PPTX save stores
  it as `ppt/media/image1.image` of the content type `image/unknown`. The same SVG given as an
  `ISvgImage` (`AddImage(ISvgImage)`) draws.
- **CLI behavior:** none: a picture the CLI adds from a file gets the file's bytes through
  `AddImage(byte[])`, and an SVG file is refused before the engine is called.

### SLIDES-ODP-EMPTY-DECK

- **Defect:** a presentation without slides saved as ODP reopens with one slide; saved as PPTX,
  it reopens with none.
- **CLI behavior:** none.

### SLIDES-ODP-MIMETYPE

- **Defect:** an ODP save writes the package's `mimetype` entry compressed and after other
  entries, while OpenDocument requires it to be the first entry, stored without compression.
- **CLI behavior:** none.

### SLIDES-ODP-LENGTH-PRECISION

- **Defect:** an ODP save writes lengths in centimetres rounded to 0.001 cm (about 0.03 pt), so
  shape positions reopen changed: shapes at X 205.5, 41.5, 312 and 100.25 pt are written as
  `7.249cm`, `1.464cm`, `11.006cm` and `3.536cm` and reopen at 205.4962, 41.50179, 312.00046 and
  100.23929 pt. A PPTX save keeps them exactly. The engine has no save options for ODP.
- **CLI behavior:** none.

## Aspose.Words 26.9.0

Reproductions: [WordsKnownIssueTests](tests/Aspose.Cli.Product.Words.Tests/WordsKnownIssueTests.cs).

### WORDS-TABLE-STYLE-RESET

- **Defect:** setting a table's style (`Table.Style`, `StyleName` or `StyleIdentifier`) clears
  every setting the table makes itself, not only the borders, cell margins and cell spacing the
  style decides: its alignment and left indent, text wrapping and position, right-to-left
  layout, title and description and style options, each row's heading, row-break and hidden
  setting and each cell's vertical alignment, which the document stores apart from the style it
  names.
- **CLI behavior:** a set of a table's `styleName` sets `Table.StyleName`, and the report reads
  the table as the engine left it.
- **Workaround:** set the table's own rows, such as `alignment`, `rowFormat.headingFormat` and
  `cellFormat.verticalAlignment`, again after the style, in the same batch.

### WORDS-LAYOUT-TABLE-GRID

- **Defect:** reading `Document.PageCount` lays the document out, and the layout rewrites the
  column widths of each table's grid (`w:tblGrid`), which the next save writes: a document read
  for its page count and saved differs from one only saved.
- **CLI behavior:** read, search and edit lay nothing out: the page count is the `pageCount` row
  of `layout`, which only `--at layout` reads, and an edit reads its operations' rows from the
  saved file after it is opened again. `update_page_layout` lays the document out when asked.

### WORDS-EMPTY-TABLE-DROPPED

- **Defect:** a save does not keep a table without rows, or a row without cells; a table that
  ended its story leaves an empty paragraph there.
- **CLI behavior:** `add table`, `add row` and `add cell` create empty nodes, as the engine's
  constructors do; the report of a table left empty reads `exists: false`, and `counts` shows the
  paragraph the save added.

### WORDS-EVALUATION-TRACKED-SAVE

- **Defect:** without a license, `Document.Save` throws `ArgumentException` "Cannot insert a node
  of this type at this location" when `StartTrackRevisions` is in effect and the document holds
  revisions; after `StopTrackRevisions`, or with a license, the same document saves.
- **CLI behavior:** an edit whose batch leaves `start_track_revisions` in effect fails at save in
  evaluation mode with `ENGINE_ERROR`, and nothing is written.
- **Workaround:** end the batch with `stop_track_revisions`; the revisions stay in the document.

### WORDS-ADJACENT-TABLES

- **Defect:** two tables with nothing between them are saved as two and opened as one.
- **CLI behavior:** an add puts a table where `before` names; the report reads the saved file,
  where the two are one table.
- **Workaround:** add a paragraph between the tables.

### WORDS-ODT-FIELDS

- **Defect:** saving to ODT or OTT drops the results of `PAGE` and `NUMPAGES` fields and drops
  formula (`=`) fields.
- **CLI behavior:** `words convert` and `words edit` of an ODT or OTT save as the engine saves.

### WORDS-HEADER-EMPTY-LINKED

- **Defect:** `HeaderFooter.IsLinkedToPrevious` reads true for a header or footer that holds no
  paragraph, though the section has one of its own of that type.
- **CLI behavior:** the `isLinkedToPrevious` row reads the member: an `add headerFooter` whose
  report is read before a paragraph is added in the same batch says true.

### WORDS-HORIZONTAL-MERGE-SPAN

- **Defect:** a horizontal merge (`CellFormat.HorizontalMerge` `First` and `Previous`) is saved
  as one cell spanning the merged ones: once opened again the row has one cell fewer, whose
  `HorizontalMerge` is `None`.
- **CLI behavior:** the report reads the saved file: the merged cells read as one.

### WORDS-TEXT-COMMENTS

- **Defect:** saving to plain text or Markdown writes each comment's text into the body, as a
  paragraph beside the one that anchors the comment, where it reads as document text.
  `TxtSaveOptions` and `MarkdownSaveOptions` have no option to leave comments out.
- **CLI behavior:** `words convert` to `txt` or `md` saves as the engine saves; `words edit`
  writes neither format.
- **Workaround:** delete the comments (`delete` of each `comment/<index>`) in a `words edit` that
  writes a Word format such as docx, then convert that file to the text format.

### WORDS-TEXT-DELETIONS

- **Defect:** saving a document with tracked changes to plain text or Markdown writes deleted and
  moved-from text beside the text that replaces it, so the output reads as neither the original
  nor the revised document.
- **CLI behavior:** `words convert` to `txt` or `md` saves as the engine saves; `words edit`
  writes neither format.
- **Workaround:** accept or reject the revisions, as the reviewer decides, with the revision
  actions in a `words edit` that writes a Word format such as docx, then convert that file to the
  text format.

### WORDS-PAGE-FIELD-LAYOUT

- **Defect:** a page field (`PAGE`, `NUMPAGES`, `SECTIONPAGES`, `PAGEREF`) that
  `DocumentBuilder.InsertField` inserts after the document was laid out, for example by reading
  `Document.PageCount`, gets an empty result, and `Field.Update` leaves it empty until
  `Document.UpdatePageLayout` rebuilds the layout.
- **CLI behavior:** an edit lays nothing out unless `update_page_layout` asks; `add field` calls
  `Paragraph.AppendField`.
- **Workaround:** after adding a page field to a document an earlier operation laid out, run
  `update_page_layout`, then `update_field` or `update_fields`.

### WORDS-PDF-HEADER-FOOTER

- **Defect:** loading a PDF guesses its headers and footers. In a PDF of one page they are
  written into the body as ordinary paragraphs, and the rebuilt layout can push them onto a
  page of their own: a one-page PDF with a footer page number loads as two pages. In a longer
  PDF, text repeated at the top or bottom of the pages becomes a real header or footer, but a
  number in it that matches a page number becomes a `PAGE` field, so a footer reading
  "Version 1  Page 1" turns into two page fields and reads "Version 2" on the second page.
  `PdfLoadOptions` has only `PageIndex`, `PageCount` and `SkipPdfImages`, so no option keeps
  headers and footers as text.
- **CLI behavior:** `words convert` of a PDF input loads it as the engine does.
- **Workaround:** read the converted document's headers, footers and fields, then correct them
  with an edit.

### WORDS-PDF-CJK-LINE-END

- **Defect:** loading a PDF ends each line of Chinese, Japanese or Korean text with a space, so
  "甲乙双方" wrapped after "甲" loads as "甲 乙双方", and it may merge the lines of separate
  paragraphs, such as a heading and the clause after it, into one paragraph. A space is right
  between Latin words, not inside East Asian text, which has none. `PdfLoadOptions` has no
  option for either.
- **CLI behavior:** `words convert` of a PDF input loads it as the engine does.
- **Workaround:** search with a regex that allows a space where a line may have ended, such as
  `甲 ?乙双方`, and remove a space only where it was read.

### WORDS-PDF-ENCRYPTION

- **Defect:** `FileFormatUtil.DetectFileFormat` reports `IsEncrypted` false for every PDF,
  including one that needs a password to open and one with only an owner password, although
  loading the first refuses no password and loading either refuses a wrong one.
- **CLI behavior:** a PDF input's `encrypted` in the result is what detection reports, false. A
  PDF that needs a password fails to load without `--password-env` with the engine's refusal,
  reported as a password error.

### WORDS-CJK-LINE-BREAK

- **Defect:** page layout breaks East Asian lines against the line-breaking rules the paragraph
  turns on (`ParagraphFormat.FarEastLineBreakControl`). Text whose East Asian language is not
  Chinese, Japanese or Korean, such as the English that a new `Document` and many converted
  documents declare, may start a line with "，" or "。" while the process culture is Chinese,
  and on a Windows set to Chinese whatever the process culture is: under en-US as well, in
  process and through the CLI (seen on a Windows whose display language and regional format are
  Chinese). And in a document without Word 2013 compatibility mode, East Asian text drawn in one
  font for all scripts, as PDF loading writes it, breaks only at spaces after Latin text, so
  "人民币 1,920,000.00 元（大写：…）" ends its line after the number although the rest would fit
  in part.
- **CLI behavior:** the CLI pins its process culture to en-US, which does not avoid the first
  case on a Windows set to Chinese. `words create` writes the engine's new document, which
  declares English; documents keep their own language and compatibility settings.

### WORDS-TRACKED-DETACHED-TEXT

- **Defect:** while `Document.StartTrackRevisions` is on, setting `Run.Text` on a run that is not
  in the document tree, such as a copy from `Node.Clone`, throws `NullReferenceException`.
- **CLI behavior:** none visible: every set addresses a node of the document, and `clone`
  inserts its copy before any operation can set it.

### WORDS-REGEX-TIMEOUT

- **Defect:** `Range.Replace(Regex, string, FindReplaceOptions)` ignores the expression's
  `MatchTimeout`: an expression that backtracks catastrophically, such as `(a+)+$` on a run of
  `a` that ends in another character, runs until it finishes, which can take hours.
- **CLI behavior:** `replace_regex` calls `Range.Replace` with the expression, so such an
  expression runs until it finishes. A command has no deadline.
- **Workaround:** test an expression with `words search --regex`, which stops at its budget,
  before `replace_regex`.

### WORDS-REGEX-ANCHORS

- **Defect:** `Range.Replace(Regex, string, FindReplaceOptions)` matches `^` only at the start of
  the range, even with `(?m)`, and `$` at no paragraph end, even in the range of one paragraph,
  so an expression anchored at each paragraph replaces at most the first one.
- **CLI behavior:** `replace_regex` calls `Range.Replace` on the range of the object `node` names,
  or of the document.
- **Workaround:** replace in one paragraph at a time, `node` naming it, with `^` for its start.

### WORDS-MD-FOOTNOTE-HEADING

- **Defect:** loading Markdown takes a heading that follows a footnote definition after a blank
  line, such as `# B` after `[^1]: Note one.`, into the footnote's text instead of starting a
  new heading, although CommonMark ends the definition at that unindented line. A paragraph of
  ordinary text after the definition ends it as expected.
- **CLI behavior:** `words convert` of a Markdown input loads it as written.
- **Workaround:** write every `[^id]: text` definition at the end of the Markdown, after the
  last heading.

### WORDS-MD-NUMBER-ESCAPE

- **Defect:** saving to Markdown escapes a paragraph or table cell that starts with digits and a
  period or closing parenthesis as an ordered-list marker, whatever follows: `22.75` becomes
  `22\.75`, which renders right but gives a script that reads the bytes a backslash, though
  CommonMark takes digits and a period as a marker only before a space or the line end; and
  `22) total` becomes `\22) total`, whose backslash before a digit CommonMark keeps as text.
  `MarkdownSaveOptions` has no option to turn the escaping off.
- **CLI behavior:** `words convert --to md` and a Markdown redline of `words compare` write the
  Markdown as the SDK escapes it.
- **Workaround:** read exact numbers with `words read`, which gives the text as the document
  holds it.

## Memory on untrusted documents

- **Limit:** an SDK expands a document in memory while it loads it, before the CLI's resource
  budgets can count what is in it, and nothing limits that memory. A document that compresses
  well is small on disk and large once loaded: a 300 KB DOCX holding 100 MB of XML reaches 1.4 GB
  before `INPUT_BUDGET_EXCEEDED` refuses it, and a 500 KB XLSX of 8 million cells reaches 0.76 GB
  and loads.
- **CLI behavior:** `--max-input-bytes` admits a document by the size of its file, which such a
  document passes. A command has no deadline.
- **Workaround:** on an untrusted document pass `--max-input-bytes` on every command, and to cap
  memory run the CLI under an operating-system limit on the process, such as a Windows job
  object.
