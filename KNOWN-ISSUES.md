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

### PDF-HTML-EGRESS

- **Defect:** the HTML importer requests http(s) stylesheets and images, and runs script, before
  it calls `HtmlLoadOptions.CustomLoaderOfExternalResources`. The Markdown importer and SVG images
  (`Image.File`, `ImageStamp`) have no resource hook: they fetch remote resources and read any
  local file a reference names, and Markdown resolves relative paths against the process working
  directory.
- **CLI behavior:** the process's network gate, which every command passes before it runs, blocks
  each http, https, ftp and file request the engine makes, so the request fails inside the process
  and the result lists its address in `REMOTE_RESOURCES_BLOCKED`. An SVG image, which the engine
  also reads through file and share paths the gate cannot see, reaches it with only the
  references inside itself, and the removed ones are listed there too. Markdown is refused with
  `FEATURE_UNSUPPORTED` unless every file it references is an ordinary file beneath its directory;
  only those files pass the gate. For trusted HTML,
  `pdf create --from-html --allow-network-resources` opens http and https for that command alone
  and lists every fetched address in `NETWORK_RESOURCES_REQUESTED`; ftp and file stay blocked.
  The importers run the page script of HTML and Markdown input, which has no
  `XMLHttpRequest`; script in an SVG file the page loads was not seen to run. The CLI does not
  scan for script: what a script adds to the page, such as a frame, an object, an image or a
  stylesheet, is loaded through the same gate and loader, so it reaches no network address and
  no file outside the input's directory (`PdfScriptReachTests`). An SVG image the CLI hands the
  engine itself (`--from-images`, image stamps and watermarks) has its script removed with its
  references.
- **Workaround:** keep resources beside the input and reference them by relative path, or
  convert HTML with `aspose-cli words convert`, which makes no network request.

### PDF-MOVE-BOOKMARK

- **Defect:** destination getters read an omitted (null) coordinate as 0 and no public API writes
  one (`XYZExplicitDestination` with `NaN` writes an invalid `NaN` token), so a destination that
  named a moved page cannot always be rebuilt exactly; the SDK has no page move.
- **CLI behavior:** `move_pages` retargets navigation to and from the moved pages exactly. An XYZ
  left that reads 0 is rebuilt as 0, which a reader opens at much the same place as an omitted
  left. A destination with any other coordinate that reads 0, such as an XYZ top, is left without
  a target and counted in a `NAVIGATION_DEGRADED` with `affectsCompleteness: true`, so
  `pdf edit --verify` exits 8.
- **Workaround:** in the same batch, after the page operations, delete the counted bookmarks with
  `delete_bookmarks` and add them again with `add_bookmark`, taking the pages they opened from
  `pdf inspect --detail outline` of the input; no operation re-creates a link or a named
  destination, so tell the user which no longer lead to their page.

### PDF-LINK-STALE-PAGE

- **Defect:** a link annotation keeps the destination it read first, page number included, so
  after a page deletion in memory a link read before it still names the number its page had:
  one to a deleted page can name a page the document still holds. Bookmarks and named destinations
  read their new numbers, and the saved file leads to the right page, or to none.
- **CLI behavior:** `pdf convert --pages` and `pdf edit`, which count the navigation a deletion
  breaks in the document in memory (`--dry-run`) and point the links to a page `move_pages` copies
  at the copy, take a link read before a deletion to lead to the page it led to then, and one the
  batch pointed at a copy to lead to that copy, and to none when that page is gone. A batch
  therefore leads each link where the same operations in separate edits do, and counts what
  `pdf edit` reads from the output it publishes, in a dry run too.

### PDF-REDACT-CALCULATED

- **Defect:** a redaction that removes a form field leaves the form's calculation order (`/CO`)
  naming it, so the field's object, with its value and appearance, stays in the saved file. Nor can
  the order be repaired through the public API: `Form.CalculatedFields` has no getter, and setting
  it to any fields writes no order at all.
- **CLI behavior:** `redact_area` clears the value and the appearance of each field whose every
  widget it covers before the engine removes it, and empties the calculation order when no field
  left has a calculate action, so neither the value nor a reference to the removed field stays.
  When another field still calculates, the order is kept and still names the removed field's
  object, now without a value or an appearance, but with the default value (`/DV`) the input gave
  it, which no public API writes.

### PDF-REDACT-EMPTY-PARENT

- **Defect:** a redaction that removes every widget of a field that holds them apart from itself,
  as children or as the buttons of a radio group, leaves the field in the form without a widget and
  with its value; one that removes the only field under a parent in the form's hierarchy, such as
  `form.person.name` under `form.person`, leaves the parent in the form as a field of its own,
  without a value or a widget.
- **CLI behavior:** `redact_area` deletes such a field once it covers every widget of it, and keeps
  one with a widget it leaves, with its value; it deletes such a parent once the fields it covers
  were every field under it, and keeps a parent that still holds another field.

### PDF-REDACT-RADIO-BUTTONS

- **Defect:** a redaction that removes some buttons of a radio group whose form was read before
  it leaves the group counting them while its options lose them, and setting the group's value
  then writes it and throws `IndexOutOfRangeException`.
- **CLI behavior:** `redact_area` that covers the last buttons of such a group deletes it without
  writing its value first, so the value leaves the file with the group unless the calculation
  order keeps it (PDF-REDACT-CALCULATED). `set_form_field` on such a group later in the same
  batch, and `redact_area` that covers its selected button but not its last, which clears its
  value, are refused with `OPS_INVALID` before they change anything, naming the group.
- **Workaround:** set the group's value, or redact its selected button, in an edit of its own
  after the redaction.

### PDF-PAGE-COPY-FIELDS

- **Defect:** no public API changes the order of a document's pages: `PageCollection` has no
  move, `Remove` throws `NotSupportedException`, inserting a page the document holds copies it with
  each field's widget still on the original page, and inserting one deleted from it throws. Copied
  into another document and inserted back, a page brings each of its form fields under a new
  top-level name (`ssn1` beside `ssn`, `form1.person.name` beside `form.person.name`), while the
  original field stays in the form without a widget and with its value.
- **CLI behavior:** `move_pages` copies pages without a widget where it can: moving a run of pages
  orders them as moving the pages it passes the other way does. Otherwise it copies pages whose
  every field has no parent in its name and has all its widgets on them, gives each copy its
  field's name and deletes the original. Any other move is refused with `OPS_INVALID`, naming the
  field, before it changes anything.
- **Workaround:** move the pages without form fields around the pages that hold them instead,
  which gives the same order without copying a field, and move the pages that hold one field's
  widgets together; where neither gives the order, as with fields that have parents in their names
  on both sides of a move, tell the user the move cannot keep the form.

### PDF-PDFA-ATTACHMENT-TYPE

- **Defect:** `Document.Convert` to PDF/A-3B gives every attachment that has no media type the
  type `application/pdf`, whatever its content.
- **CLI behavior:** `pdf convert --to pdfa-3b` labels such an attachment
  `application/octet-stream` unless its content is a PDF; a declared type is kept.

### PDF-ATTACHMENT-COPY-TYPE

- **Defect:** `EmbeddedFiles.Add` with the `FileSpecification` of another document's attachment
  gives the added attachment no media type (an empty `MIMEType`); its name, description and
  contents are kept. The type is read from the file only when first asked for, and a
  specification whose `MIMEType` was not read before the add reads empty afterwards as well, so
  the type is lost unless it is read first.
- **CLI behavior:** none visible; `pdf merge` and `pdf split` read each attachment's media type
  before adding it and set it on the added attachment.

### PDF-OUTLINE-DELETE-TITLE

- **Defect:** `OutlineItemCollection.Delete()` removes bookmarks by title rather than the item
  it is called on: it removes the first bookmark with that title it finds, which may be a
  different one, and when bookmarks at different levels share the title it can remove several.
- **CLI behavior:** none visible; `delete_bookmarks` gives each selected bookmark a unique title
  before deleting it, so exactly the selected bookmarks and their children are removed.

### PDF-NAMES-WITHOUT-DESTS

- **Defect:** `Document.NamedDestinations.Names`, `Count` and the lookup by name
  (`NamedDestinations[name]`) throw `NullReferenceException` when the catalog's name tree has no
  `Dests` entry, for example a tree that holds only attachments, such as the one a merged document
  gets with the attachments of its inputs.
- **CLI behavior:** none visible; such a document is read as having no named destinations, and a
  name is looked up only when it is listed, so `pdf edit`, `pdf merge`, `pdf split` and
  `pdf convert --to pdfa-*` accept it.

### PDF-NAMES-BESIDE-DESTS

- **Defect:** when the catalog has both a `Dests` dictionary and a name tree of destinations
  (`Names` → `Dests`), `Document.NamedDestinations` reads only the dictionary: a name only the
  tree holds is not listed and its lookup returns null, so a link or bookmark that leads through
  it reads as leading to no page, while a reader follows the tree. The saved file keeps both.
- **CLI behavior:** `pdf inspect --detail links` and `--detail outline` show such a link or
  bookmark without a page, and since it read so before an edit as well, the edit's
  `NAVIGATION_DEGRADED` does not count it, even when the edit deletes or moves the page the tree
  leads it to.

### PDF-TAGGED-CONTENT-WRITES

- **Defect:** reading `Document.TaggedContent` rewrites the loaded document: it sets an empty
  title to `Tagged PDF`, creates XMP metadata dated now that declares PDF/UA (`pdfuaid:part` 1)
  and drops the PDF/A identification, so `IsPdfaCompliant` turns false.
- **CLI behavior:** none visible; `pdf inspect` reads the structure tree after every other
  property and never saves the inspected document.

### PDF-IMPORT-INFO-PLACEHOLDER

- **Defect:** the HTML and Markdown importers set the document title, author and subject to
  `Aspose`, whatever the source states; the HTML `title` element is ignored.
- **CLI behavior:** `pdf create --from-html` takes the title from the HTML `title` element and
  `--from-markdown` leaves it empty; both leave the author and subject empty.
- **Workaround:** set the other fields with a `set_properties` edit.

### PDF-IMPORT-LINK-ADDRESSES

- **Defect:** the HTML and Markdown importers resolve every relative link against a local folder,
  the HTML's `BasePath` or else the process working directory, and write the `file:` address they
  get into the link annotation's text and, for a link to another file, into its URI action, so
  the PDF holds the absolute path of that folder; an absolute or share path becomes a `file:`
  address there too. A link to an element `id` (`href="#part"`) gets neither a destination nor an
  action; one to an `<a name="part">` anchor leads to its page. The importers write no other
  action or file specification for a link.
- **CLI behavior:** `pdf create --from-html` and `--from-markdown` take each `file:` address out
  of a link's text and URI action: a link to a local file loses its action, and
  `NAVIGATION_DEGRADED` counts the links that lead nowhere. Web and mail links are kept.
- **Workaround:** mark each place a link leads to with `<a name="part"></a>`, and give a link to
  another document its https address.

### PDF-HTML-FORM-NAMES

- **Defect:** the HTML importer keeps the `name` of single-line text inputs only. It names check
  boxes, selects, text areas and buttons after their `id` attribute, or itself when they have
  none, and radio groups always itself (such as `field_#0`, `field_1` and `radio`), and it drops
  their `value` attributes: a check box is checked with `Yes` and radio buttons
  take the values `Item0`, `Item1` and so on. `HtmlLoadOptions` has no option to keep them.
- **CLI behavior:** `pdf create --from-html` warns `LOSSY_CONVERSION` with these names, saying
  which come from an `id` attribute and which are generated;
  `pdf inspect --detail forms` gives each field's `page` and `rect`.
- **Workaround:** match each generated field to the label beside it by its `rect` before filling.

### PDF-HTML-FORM-INPUTS

- **Defect:** the HTML importer drops inputs of type `email`, `tel`, `url`, `time`,
  `datetime-local`, `month`, `week`, `color`, `range`, `file`, `image` and `hidden`: they become
  no form field, and the value of a hidden input is lost.
- **CLI behavior:** `pdf create --from-html` counts such `<input>` tags in the HTML and names
  their types in its `LOSSY_CONVERSION` warning.
- **Workaround:** give text-like inputs `type="text"`; place the picture of an image input with
  `add_stamp_image`.

### PDF-HTML-CHECKBOX-BOX

- **Defect:** the HTML importer gives a check box neither a border nor a border colour, so its
  appearances draw no box: an unchecked box shows nothing and a checked one only its check
  mark. Radio buttons get a black border.
- **CLI behavior:** `pdf create --from-html` gives such a check box a black one-point border, so
  the engine draws its box.

### PDF-PDFA-RADIO-APPEARANCE

- **Defect:** the HTML importer writes a radio group's field into its page's annotations with an
  appearance stream of its own. `Document.Convert` to PDF/A returns success and leaves it, and
  validation of the saved file then fails the clause on annotation appearances, 6.3.3 in PDF/A-2
  and PDF/A-3 and 6.5.3 in PDF/A-1 (the appearance of a button field must be a subdictionary of
  states).
- **CLI behavior:** `pdf convert --to pdfa-*` validates the file it converted and fails with
  `PDFA_CONVERSION_FAILED` when it does not conform; for appearance problems `details.fields` names
  the button fields on those pages that have one appearance for all their states.
- **Workaround:** flatten the named fields (`flatten_forms`) before converting.

### PDF-PDFA-LOG-REPEATS

- **Defect:** the PDF/A log that `Document.Validate` writes lists a problem once for every use
  of the object it concerns, and marks it convertible (`Convertable="True"`) on only one of
  those entries: a font that is not embedded is reported as both fixable and not on the same
  page, although converting to the profile embeds it.
- **CLI behavior:** none visible: `pdf validate` and the PDF/A conversion errors list each
  problem once, as convertible when any of its entries says so.

### PDF-TEXT-GAP-SPACE

- **Defect:** text extraction and `TextFragmentAbsorber` read a horizontal gap of about a
  sixth of an em or more between two text runs as a space, and search the text with those
  spaces; no `TextSearchOptions` or `TextExtractionOptions` setting turns this off. Word leaves
  such a gap between East Asian text and digits or Latin letters (automatic spacing), so
  `2026年10月31日` in a Word-made PDF extracts as `2026年 10月 31日` (two spaces in the plain
  mode) and a search for it finds nothing.
- **CLI behavior:** literal patterns of `pdf search` and `redact_text`, and the `redact_text`
  verification, also match with up to two spaces, never a line break, wherever an East Asian
  character meets one that is not, such as a digit or Latin letter. A `redact_text` operation
  that still matches nothing is named in an `OP_NO_EFFECT` warning.
- **Workaround:** in a regular expression, allow the gaps with ` *`.

### PDF-ATTACHMENT-NAME-OPENS-FILE

- **Defect:** the `FileSpecification.Name` setter opens the file its value names, relative to
  the process working directory, for reading and writing. It throws when that file is open
  elsewhere, for example as the stream the specification was built from.
- **CLI behavior:** none visible; `add_attachment` names the attachment through the
  `FileSpecification` constructor and never sets `Name`, so the working directory is not read.

### PDF-REDACT-TEXT-SHIFT

- **Defect:** `RedactionAnnotation.Redact()` removes the glyphs under the annotation without
  keeping their advance, so a text run that starts where the previous one ended, without a
  position of its own, moves left by the removed width, under the cover. Word writes the runs
  of a line that way. `PdfAnnotationEditor.RedactArea` and replacing the text with an empty
  string move the runs the same way, and setting `TextFragment.Position` to put them back
  draws other runs of the line over each other.
- **CLI behavior:** `redact_text` removes the matches of a page from the last to the first, so
  each still lies where the search found it. `redact_text` and `redact_area` compare where the
  runs of each redacted page start before and after, and a `REDACTION_TEXT_MOVED` warning names
  the operations and pages on which a run moved left. Only runs that keep their whole text are
  followed, so the rest of a run the redaction cut can move without a warning: no warning does
  not prove that nothing moved. The moved text is still in the file but no longer visible.
- **Workaround:** redact the source document and create the PDF again. With only the PDF, the
  values can all be removed, with `redact_text` or with `redact_area` on search rectangles
  without a margin, the areas of a line from right to left, but the text that follows them on
  their lines stays hidden under the cover.

### PDF-ENCRYPTED-INFO-TEXT

- **Defect:** a document information entry (title, author, subject, keywords or a custom entry)
  set in a session whose save encrypts the file is written with only the low byte of each
  character, so text beyond Latin-1, such as `采购申请`, reads back garbled. Entries read from the
  file and entries written to a file saved without encryption are kept.
- **CLI behavior:** when the output's document information holds text beyond Latin-1 (past
  U+00FF), a PDF command given `--encrypt-env`, and a `pdf edit` batch with both
  `set_properties` and `encrypt_document`, save the document without encryption, reopen it and
  encrypt that copy. On an encrypted input, `set_properties` with such text that neither a later
  `encrypt_document` nor `--encrypt-env` follows is refused with `OPS_INVALID`, because the CLI
  does not know all the input's passwords to encrypt a copy.
- **Workaround:** add `encrypt_document` after `set_properties` in the same batch, or pass
  `--encrypt-env`; or add `decrypt_document` before `set_properties` to save the output without
  encryption.

### PDF-INFO-KEY-BEYOND-LATIN1

- **Defect:** a document information entry whose name has a character beyond Latin-1, such as
  `项目编号`, is saved under a name written from each character's code as if it were one byte,
  so the name reads back garbled while its value is kept. Names within Latin-1 are kept.
- **CLI behavior:** `set_properties` refuses a `custom` name with a character beyond Latin-1 with
  `OPS_INVALID`; the value of an entry may hold any text.
- **Workaround:** name the entry in Latin-1, such as ASCII letters, and put the text in its value.

### PDF-SIGN-FULL-FONT

- **Defect:** `PdfFileSignature.Sign` embeds the whole font program the signature appearance
  draws in, visible or not: Arial (about 560 KB) by default, a complete CJK font such as SimSun
  (about 10 MB) for a reason, location, contact or signer name in Chinese. Helvetica, which is not
  embedded, keeps printable ASCII without a font; any other character embeds a whole font again.
- **CLI behavior:** `pdf sign` draws an appearance whose texts are all printable ASCII in
  Helvetica, except in a PDF/A document, which needs embedded fonts; other signatures keep the
  engine's font, grow the file by its size and warn `SIGNATURE_FONT_EMBEDDED` with the growth.
- **Workaround:** keep `--reason`, `--location` and `--contact` in ASCII, and use a certificate
  whose subject name is ASCII.

### PDF-SIGN-CJK-SIGNER-FONT

- **Defect:** `PdfFileSignature.Sign` chooses the font of a visible signature's appearance from
  its reason, location and contact alone. When none of them has Chinese text, it draws in Arial,
  and a signer name in Chinese is left out: the first line reads `Digitally signed by 'CN='`.
  Japanese text, such as `批准`, gives a Japanese font, MS Gothic, which leaves out a simplified
  Chinese name such as `张伟`. Naming a Chinese font, such as SimSun, through
  `SignatureCustomAppearance.FontFamilyName` shows the name.
- **CLI behavior:** `pdf sign --visible` draws a signer name with East Asian characters (U+2E80
  and above) in SimSun or, failing that, Microsoft YaHei when `--reason`, `--location` and
  `--contact` have none; with neither font installed the name is left out and the result warns
  `SIGNATURE_TEXT_CLIPPED`. When one of them has East Asian text the engine's choice stands, so
  a Chinese name can still be left out without a warning, as with `批准` in MS Gothic. A Korean
  signer name is left out in SimSun, also without a warning.
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
- **CLI behavior:** `pdf sign --rect` measures from the top-left corner of the visible page, and
  the CLI swaps the rectangle back before it signs a page rotated by 90 or 270 degrees, so the
  signature lies where `--rect` names on every rotation.

### PDF-TEXT-RECT-ROTATED-CROP

- **Defect:** on a rotated page that has a crop box, `TextFragment.Rectangle` is given in the
  space of the visible, rotated page, but not from that page's lower-left corner: at 90 degrees
  its x is offset by the crop box's bottom, at 270 degrees its y by the crop box's left, at 180
  degrees neither, and upright both, as the page's own space has them.
- **CLI behavior:** none visible: `pdf search` takes each offset back out, so a match's `rect`
  is measured from the top-left corner of the visible page on every rotation and passes to
  `redact_area` as it is.

### PDF-REDACT-ROTATED

- **Defect:** on a rotated page, `RedactionAnnotation.Redact()` draws its cover where its
  rectangle lies in the page's unrotated space but removes the text it finds there in the
  rotated page's space, so text under the cover stays in the file.
- **CLI behavior:** none visible: `redact_text` and `redact_area` clear the page's rotation while
  they redact and set it again after.

### PDF-TEXT-SEARCH-WRAPS-CONTENT

- **Defect:** searching a page's text with `TextFragmentAbsorber` changes the page it only
  reads: on a page that carries a stamp, it wraps the page's content in one more saved graphics
  state (`q` … `Q`), which draws nothing different.
- **CLI behavior:** none visible: an edit compares a page's content without its saved graphics
  states, so a `redact_text` that matched nothing reports 0 with `OP_NO_EFFECT`, and one that
  matched counts only the pages it removed text from.

### PDF-DOCX-EMBED-FONTS

- **Defect:** saving as docx writes `w:embedTrueTypeFonts` and `w:embedSystemFonts` into
  `word/settings.xml` but embeds at most the few font subsets the PDF itself embeds, and none of
  the fonts it does not, so the next save by Aspose.Words or Word embeds every other font the
  document uses in full: a one-page document in a standard PDF font grows from about 7 KB to
  about 3 MB. `DocSaveOptions` has no setting for these flags.
- **CLI behavior:** a Words command that saves such a document warns `FONTS_EMBEDDED` that the
  save embeds the missing fonts.
- **Workaround:** deliver the docx as `pdf convert` wrote it, or write it with
  `aspose-cli words convert <pdf> --to docx`, which asks for no embedded fonts.

### PDF-ENCRYPTED-OBJECT-STREAMS

- **Defect:** `OptimizeResources` with `CompressObjects` on an encrypted document writes a file
  that no reader opens: Aspose.PDF reads it back with neither encryption nor text, and other
  readers report a corrupt object stream, with the password or without. Decrypting first and
  encrypting after the optimization writes a sound file.
- **CLI behavior:** `optimize_document` with `compressStreams` (its default) on an encrypted
  document is refused before anything is written.
- **Workaround:** set `"compressStreams": false`, or put `decrypt_document` before
  `optimize_document` and `encrypt_document` after it (or pass `--encrypt-env`).

### PDF-FORM-RECURSION-CRASH

- **Defect:** reading the text of a page (`TextAbsorber`) whose Form XObject draws itself
  recurses until the stack overflows, which ends the process at once with exit code `0xC00000FD`
  and no exception that code could catch. Rendering the same page with `PngDevice` completes.
- **CLI behavior:** a command that reads the text of such a page ends the same way, and so does
  every command whose evaluation-notice check reaches it: licensed or not, that check reads the
  text of up to four pages of every document a PDF command writes or renders. `pdf read`,
  `pdf edit`, `pdf convert` and `pdf render` are among them. The command exits with
  `0xC00000FD`, with nothing on stdout and the runtime's `Stack overflow.` and stack trace on
  stderr. An output published before the check stays, as `pdf render`'s image does; one only
  staged, as the outputs of `pdf edit` and `pdf convert` are, is not published, but its
  `.aspose-publication-*` staging stays in the output's directory.

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

- **CLI behavior:** none: `pdf convert --to md` of such a form after `flatten_forms` writes each
  value twice, and `--to txt` once.
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
- **CLI behavior:** `set_table_style` reads these settings, changes the style away and back to
  read what the old style alone gives, and writes back those the table set itself over the new
  style. A setting the table set to the very value the old style gives cannot be told from the
  style's and takes the new style's. Writing a row's hidden setting back also hides the text of
  its runs and paragraph marks, so each of those that changed is set back to what it was.

### WORDS-ROW-HIDDEN-UNSAVED

- **Defect:** setting `Row.Hidden` on a row the document has held only in memory, one
  `DocumentBuilder` or `new Row` made or a clone of one, throws `NullReferenceException` until the
  document is saved once.
- **CLI behavior:** no operation hides or shows a row; `set_table_style` writes a row's hidden
  setting back only on a row that held one, which a row made in memory never does.

### WORDS-TEXT-COMMENTS

- **Defect:** saving to plain text or Markdown writes each comment's text into the body, as a
  paragraph beside the one that anchors the comment, where it reads as document text.
  `TxtSaveOptions` and `MarkdownSaveOptions` have no option to leave comments out.
- **CLI behavior:** `words convert` to `txt` or `md` warns `LOSSY_CONVERSION` with the number of
  comments written into the body; `words edit` writes neither format.
- **Workaround:** delete the comments with `delete_comments` in a `words edit` that writes a Word
  format such as docx, then convert that file to the text format.

### WORDS-TEXT-DELETIONS

- **Defect:** saving a document with tracked changes to plain text or Markdown writes deleted and
  moved-from text beside the text that replaces it, so the output reads as neither the original
  nor the revised document.
- **CLI behavior:** `words convert` to `txt` or `md` warns `LOSSY_CONVERSION` when the document
  has tracked deletions or moves; `words edit` writes neither format.
- **Workaround:** accept or reject the revisions, as the reviewer decides, with
  `accept_revisions` or `reject_revisions` in a `words edit` that writes a Word format such as
  docx, then convert that file to the text format; with `delete_comments` in the same edit when
  the document has comments too.

### WORDS-PAGE-FIELD-LAYOUT

- **Defect:** a page field (`PAGE`, `NUMPAGES`, `SECTIONPAGES`, `PAGEREF`) that
  `DocumentBuilder.InsertField` inserts after the document was laid out, for example by reading
  `Document.PageCount`, gets an empty result, and `Field.Update` leaves it empty until
  `Document.UpdatePageLayout` rebuilds the layout.
- **CLI behavior:** none visible; `insert_field` rebuilds the layout and updates such a field
  when it has no result.

### WORDS-PDF-HEADER-FOOTER

- **Defect:** loading a PDF guesses its headers and footers. In a PDF of one page they are
  written into the body as ordinary paragraphs, and the rebuilt layout can push them onto a
  page of their own: a one-page PDF with a footer page number loads as two pages. In a longer
  PDF, text repeated at the top or bottom of the pages becomes a real header or footer, but a
  number in it that matches a page number becomes a `PAGE` field, so a footer reading
  "Version 1  Page 1" turns into two page fields and reads "Version 2" on the second page.
  `PdfLoadOptions` has only `PageIndex`, `PageCount` and `SkipPdfImages`, so no option keeps
  headers and footers as text.
- **CLI behavior:** `words convert`, `words split` and `words edit` of a PDF input warn
  `LOSSY_CONVERSION` that headers and footers may become body text or page-number fields and
  that the layout may add pages.
- **Workaround:** read the headers, footers and fields with
  `words inspect --detail sections --detail fields`, then restore them with `set_header`,
  `set_footer` or `set_page_numbers` and remove body paragraphs that held them with
  `delete_blocks`.

### WORDS-PDF-CJK-LINE-END

- **Defect:** loading a PDF ends each line of Chinese, Japanese or Korean text with a space, so
  "甲乙双方" wrapped after "甲" loads as "甲 乙双方", and it may merge the lines of separate
  paragraphs, such as a heading and the clause after it, into one paragraph. A space is right
  between Latin words, not inside East Asian text, which has none. `PdfLoadOptions` has no
  option for either.
- **CLI behavior:** `words convert`, `words split` and `words edit` of a PDF input whose text
  holds East Asian characters warn `LOSSY_CONVERSION` about the spaces and merged paragraphs.
- **Workaround:** read the blocks before editing; find a phrase with a regex that allows a space
  where a line may have ended, such as `甲 ?乙双方`, and remove a space only where it was read.

### WORDS-PDF-ENCRYPTION

- **Defect:** `FileFormatUtil.DetectFileFormat` reports `IsEncrypted` false for every PDF,
  including one that needs a password to open and one with only an owner password, although
  loading the first refuses no password and loading either refuses a wrong one.
- **CLI behavior:** none visible: Words loads the first page of a PDF input again with a password
  no one chose and, when that is refused, without one, and reports the input encrypted from what
  those loads refuse. A DOCX copy of a PDF that needs a password to open keeps that password,
  and one of a PDF with only an owner password warns `ENCRYPTION_REMOVED`.

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
  case on a Windows set to Chinese: there `words convert --to pdf` breaks such text as it did
  under a Chinese process culture. The built-in design declares Chinese as its East Asian
  language, so documents created without `--template` follow the rules. Other documents keep
  their own language and compatibility settings, which no operation changes; the Words Skill
  tells reviewers to disclose such lines.

### WORDS-TRACKED-DETACHED-TEXT

- **Defect:** while `Document.StartTrackRevisions` is on, setting `Run.Text` on a run that is not
  in the document tree, such as a copy from `Node.Clone`, throws `NullReferenceException`.
- **CLI behavior:** none visible; `insert_paragraphs`, `set_text` and `replace_text` set the text
  of the run they copy, from the paragraph before the insertion point or from the replaced text,
  with tracking paused, and the run's insertion is then tracked as usual.

### WORDS-REGEX-TIMEOUT

- **Defect:** `Range.Replace(Regex, string, FindReplaceOptions)` ignores the expression's
  `MatchTimeout`: an expression that backtracks catastrophically, such as `(a+)+$` on a run of
  `a` that ends in another character, runs until it finishes, which can take hours.
- **CLI behavior:** `replace_text` does not call `Range.Replace`: it finds its matches under the
  expression's budget in the text `words search` reads and replaces them in place, so an
  expression that runs past the budget fails with `OPERATION_TIMEOUT` and the edit writes
  nothing.

### WORDS-REGEX-ANCHORS

- **Defect:** `Range.Replace(Regex, string, FindReplaceOptions)` matches `^` only at the start of
  the range, even with `(?m)`, and `$` at no paragraph end, even in the range of one paragraph,
  so an expression anchored at each paragraph replaces at most the first one.
- **CLI behavior:** `replace_text` does not call `Range.Replace`: it replaces the matches
  `words search` finds, in the text of each paragraph, comment and note, so `^` and `$` anchor
  at each of them.

### WORDS-MD-FOOTNOTE-HEADING

- **Defect:** loading Markdown takes a heading that follows a footnote definition after a blank
  line, such as `# B` after `[^1]: Note one.`, into the footnote's text instead of starting a
  new heading, although CommonMark ends the definition at that unindented line. A paragraph of
  ordinary text after the definition ends it as expected.
- **CLI behavior:** `words create --from-markdown`, `insert_markdown` and Markdown headers and
  footers load the Markdown as written; the Words Skill gives the workaround.
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
  Markdown as the SDK escapes it; the Words Skill says to read exact numbers with `words read`.
- **Workaround:** read the numbers with `words read`, or a table's with `words extract --what
  tables`, which give the text as the document holds it.

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
