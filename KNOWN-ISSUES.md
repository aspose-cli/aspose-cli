# Known issues

Confirmed behaviors of the commercial Aspose SDKs this CLI runs on, each under its id below the
SDK version `eng/products.json` pins. One test in the `*KnownIssueTests` classes a section names
reproduces each issue with the SDK alone (`KnownIssue.Reproduces`), and `KnownIssueCatalogTests`
keeps this file true. When an SDK is updated, run the `Full` test scope, delete each issue whose
reproduction now fails together with its test, then update the heading. The last section, on
memory, is a limit no single SDK version owns, and has no id.

## Aspose.Cells 26.9.0

Reproductions: [CellsKnownIssueTests](tests/Aspose.Cli.Product.Cells.Tests/CellsKnownIssueTests.cs),
[CellsConvertKnownIssueTests](tests/Aspose.Cli.Product.Cells.Tests/CellsConvertKnownIssueTests.cs).

### CELLS-SVG-EGRESS

To rasterize an SVG picture that has no raster image, whether one being added or one a workbook
stores alone (as an ODS can), the engine reads every image its `<image>` elements name: a bare path
from the file system, and a `file:` or `http:` URL through a request of its own. It never consults
`WorkbookSettings.ResourceProvider` or `LoadOptions.StreamProvider`, and it reads an ODS or OTS
package's SVG picture this way while the package loads, whatever `LoadFilter` is set. Loading HTML
or MHTML, it also opens the images the page names by path, and no option turns that off.

The CLI does not read ODS, OTS, HTML or MHTML with Cells (`FORMAT_UNSUPPORTED`), and does not add
an SVG picture.

### CELLS-SVG-ORPHAN

Removing a picture whose SVG came with a loaded workbook (`Pictures.RemoveAt`, `Shapes.Remove` and
`Shapes.Clear` alike) removes its raster image but leaves its SVG in the saved package, as a part
no relationship refers to. No call changes the SVG of a picture in place: setting `Picture.Data`
to an SVG stores it as the raster image and keeps the old SVG, and `Picture.Copy` replaces only
the raster image.

  ```csharp
  var workbook = new Workbook("has-svg.xlsx");      // one SVG picture
  workbook.Worksheets[0].Pictures.RemoveAt(0);
  workbook.Save("out.xlsx");
  // out.xlsx keeps xl/media/image1.svg, and no .rels file refers to it
  ```

### CELLS-SPARKLINE-APOSTROPHE

`SparklineGroups.Add(type, dataRange, isVertical, locationRange)` throws `Invalid "'"` for a data
range on a sheet whose name has an apostrophe, however the name is quoted.

### CELLS-COPY-NAME-SCOPE

`Worksheet.Copy` from another workbook, when the copied sheet uses a workbook-level name that the
destination also defines and the source's definition refers to another sheet, adds the source's
definition as a name scoped to the destination's first sheet. Once saved, that sheet's own
formulas read the added name instead of the workbook's, and the copied sheet's formulas read the
destination's definition.

### CELLS-COPY-EXTERNAL-CACHE

`Worksheet.Copy` and `Range.Copy` from another workbook, when a copied formula reads a third
workbook through a link that caches no values (such as a link written while the linked file was
absent), give the destination's link an empty cache. The formula evaluates to `#REF!` in the
source but, once the destination is calculated, reads the linked cells as empty, usually as 0.

### CELLS-LINK-RELATIVE-TARGET

A formula that reads a workbook by its full path, when that workbook is in the folder the edited
workbook was opened from, gets a link to the file name alone. An xlsx save stores it as the
relative target `fx.xlsx` with the relationship type `xlPathMissing`, so a workbook saved to
another folder no longer links to the file in the folder it was opened from. How Excel resolves an
`xlPathMissing` target is unverified.

### CELLS-WIDTH-EAST-ASIAN

`Cell.GetWidthOfValue` (and `CellsHelper.GetTextWidth`) measures East Asian characters with the
metrics of the cell's font when that font has no glyphs for them, such as Calibri or Arial, while
`AutoFitColumn` and rendering draw them with a fallback font. The measured width is then up to
half the drawn width, or, for mixed text such as `配件 Accessories`, can exceed the auto-fitted
column by a pixel.

### CELLS-OVERFLOW-EDGE

The page layout of `SheetRender` (`OnlyArea = false`) cuts text that spills over empty cells when
its end falls on a column edge: it stops drawing at that edge instead of continuing into the next
empty cell, which can cut away part of the last character. That layout sizes columns apart from
the cell model (a few percent wider with Microsoft YaHei), so the edge falls elsewhere in the
print-area layout of `OnlyArea = true`, which also draws text that ends on an edge in full.

### CELLS-PIVOT-TOTAL-CAPTION

A pivot with a column field and two or more value fields labels each value field's grand-total
column with the word of `PivotGlobalizationSettings.GetTextOfTotal` before the caption
(`Total Sum of Amount`). Overriding the word gives `汇总 求和项:金额`, never Excel's Simplified
Chinese form `求和项:金额汇总`, and no pivot caption property sets it.

### CELLS-VALIDATION-DELETE-END

`Cells.DeleteRows` and `Cells.DeleteColumns` leave a data validation area whose last rows or
columns they delete one line too long: deleting row 10 of `C2:C10` leaves `C2:C10`, and rows 9-11
leave `C2:C9`, while a conditional format over the same cells becomes `C2:C9` and `C2:C8`.
Deleting lines in the middle of the area shrinks it correctly, and a validation whose every area
is deleted stays in the sheet with no area.

### CELLS-PIVOT-LABEL-INDENT

In a pivot whose column area holds more than one field, rendering draws each item label of a
column field other than the innermost one (such as the products over the value fields, or the
regions over the products) about 16 pixels at 96 DPI further right than the same text in a plain
cell, although its style has no indent, and cuts it at its column's edge even when the next cell
is empty. The value field captions, also when they are the outer field, the subtotal and grand
total labels and the innermost field's items are drawn like plain cells. `AutoFitColumns` leaves
the indent out, so an auto-fitted column is as wide as for the text alone and the label is cut.

### CELLS-COPY-SELF-LINK

`Range.Copy` and `Worksheet.Copy` from another workbook keep a formula that reads the destination
workbook through a link stored relative to the source (as Excel stores a link to a workbook in the
same folder) as an external link to the destination itself, which reads as 0 once calculated: for
example `='[budget.xlsx]Budget'!$D$6`, copied into `budget.xlsx` from a source beside it. Setting
the same formula with `Cell.Formula` gives the reference inside the workbook, `=Budget!$D$6`.

### CELLS-JSON-HEADER-ONLY

Saving as JSON a sheet whose data is a single row, which the engine takes for the header, writes
the header's names as members of an array, such as `["Name": null]`, which is not JSON. No
`JsonSaveOptions` setting writes it as an empty list of records.

### CELLS-AUTOFIT-RICH-TEXT

`Worksheet.AutoFitRows` fits a row to the cell's font when the cell holds rich text that does not
wrap, so a run larger than that font is cut off, although `Cell.GetHeightOfValue` measures the
run. Wrapped rich text is fitted.

### CELLS-AUTOFIT-GENERAL-NUMBER

`Worksheet.AutoFitColumn` and `AutoFitColumns` make a column holding a number in the General
format one to three pixels narrower than the engine needs to show it in full in some fonts:
DengXian 11 (the default font of Chinese Excel) and Microsoft YaHei 10 for numbers with decimals,
and Calibri 11, Arial 10 or Times New Roman 12 for some negative ones. `Cell.GetStringValue` with
`DisplayString` then rounds the number, 15600.75 to 15600.8, and in DengXian 11 rendering draws it
in full only one pixel wider than that display text needs. No `AutoFitterOptions` setting
(`FormatStrategy`, `ForRendering`) changes the width; numbers in other formats, dates and text are
fitted in full.

### CELLS-SHEET-END-COMMENT-PAGES

On a sheet whose `PageSetup.PrintComments` is `PrintSheetEnd`, `Worksheet.GetPrintingPageBreaks`
reports each page of comments as the cell area A1:A1, and throws `NullReferenceException` when a
comment holds a character such as Chinese, Japanese or an emoji. Saving the same sheet as PDF or
XPS, and `SheetRender`, print its comments at the end.

### CELLS-CSV-SAVE-CULTURE

Saving delimited text with `TxtSaveOptions.FormatStrategy` set to `CellValueFormatStrategy.None`
writes each number with the decimal separator of the thread's culture, whatever culture
`TxtLoadOptions.CultureInfo` or `WorkbookSettings.CultureInfo` names.

  ```csharp
  CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
  File.WriteAllText("p.csv", "k,19.9\n");
  var workbook = new Workbook("p.csv", new TxtLoadOptions(LoadFormat.Csv) { CultureInfo = CultureInfo.InvariantCulture });
  workbook.Save("out.csv", new TxtSaveOptions(SaveFormat.Csv) { FormatStrategy = CellValueFormatStrategy.None });
  // out.csv holds k,"19,9"
  ```

### CELLS-CSV-FRACTION-CULTURE

Loading delimited text that holds a fraction, such as `3 1/2`, throws `FormatException` when the
thread's culture writes a decimal comma, whatever culture `TxtLoadOptions.CultureInfo` names.

  ```csharp
  CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
  File.WriteAllText("f.csv", "k,3 1/2\n");
  var workbook = new Workbook("f.csv", new TxtLoadOptions(LoadFormat.Csv) { CultureInfo = CultureInfo.InvariantCulture });
  // throws FormatException: The input string '3,5' was not in a correct format.
  ```

### CELLS-CSV-KEEP-PRECISION

`TxtLoadOptions.KeepPrecision`, documented as whether a string value of length 15 is not parsed,
changes nothing: with it, values of 15, 16 and 20 digits are read as numbers, as without it, and
keep 15 significant digits.

  ```csharp
  File.WriteAllText("p.csv", "k,12345678901234567890\n");
  var options = new TxtLoadOptions(LoadFormat.Csv) { KeepPrecision = true };
  Cell cell = new Workbook("p.csv", options).Worksheets[0].Cells["B1"];
  // cell.Type is IsNumeric and cell.StringValue 1.23457E+19, not the text
  ```

### CELLS-CSV-ERROR-TEXT

Loading delimited text types the text of an error value, such as `#N/A`, `#DIV/0!` or `#REF!`, as
that error even where the load's `ICustomParser` returns the text as the string it is, and
`Cell.PutValue(text, isConverted: false)` types it the same way.

  ```csharp
  File.WriteAllText("e.csv", "k,#N/A\n");
  var options = new TxtLoadOptions(LoadFormat.Csv) { PreferredParsers = [new TextParser()] };
  Cell cell = new Workbook("e.csv", options).Worksheets[0].Cells["B1"];
  // TextParser.ParseObject returns the value it is given; cell.Type is IsError, not IsString
  ```

Excel itself opens such a csv with the error values; the defect is only that the engine overrides
what the parser returns.

### CELLS-MARKDOWN-DETECTION

`FileFormatUtil.DetectFileFormat` takes lines of plain data for Markdown by their line ends and
characters, not by any Markdown they hold: two lines `a-b` and `c-d` are `Unknown` when they end in
LF and `Markdown` in CRLF, two numbers `1E-30` and `1E-29` are `Markdown`, and a Markdown table is
`Unknown`.

  ```csharp
  FileFormatType Detect(string text) => FileFormatUtil.DetectFileFormat(new MemoryStream(Encoding.UTF8.GetBytes(text))).FileFormatType;
  Detect("a-b\nc-d\n");                         // Unknown
  Detect("a-b\r\nc-d\r\n");                     // Markdown
  Detect("1E-30\n1E-29\n");                     // Markdown
  Detect("| a | b |\n|---|---|\n| 1 | 2 |\n");  // Unknown
  ```

### CELLS-ODS-PRINT-COMMENTS

An ODS save drops a sheet's `PageSetup.PrintComments`, although an ODF page layout can print
annotations: set to print at the sheet's end or in place, it reads back as `PrintNoComments`.
xlsx, xls and xlsb keep it.

  ```csharp
  sheet.PageSetup.PrintComments = PrintCommentsType.PrintSheetEnd;
  workbook.Save("c.ods", SaveFormat.Ods);
  // new Workbook("c.ods").Worksheets[0].PageSetup.PrintComments is PrintNoComments
  ```

### CELLS-PRINT-AREA-BAND-COMMENTS

A print area that is a band of whole columns (`A:C`) or whole rows (`1:5`), alone or beside
bounded ranges, prints none of the comments on the band's cells, at the sheet's end or in place,
although Excel prints them; a bounded print area (`A1:C5`) prints the comments on its cells. PDF
export and `SheetRender` alike.

  ```csharp
  sheet.Comments[sheet.Comments.Add("A1")].Note = "a note";
  sheet.PageSetup.PrintComments = PrintCommentsType.PrintSheetEnd;
  sheet.PageSetup.PrintArea = "A:A";   // with "A1:A5" the comment prints on a page of its own
  new SheetRender(sheet, new ImageOrPrintOptions()).PageCount;   // 1: no page of comments
  ```

### CELLS-AXIS-AUTOMATIC-MIN-SAVE

Setting `Chart.ValueAxis.IsAutomaticMinValue` to false on an axis whose `MinValue` was never set
makes the next `Workbook.Save` throw `NullReferenceException`; setting `MinValue` first, which
itself turns the automatic minimum off, saves.

  ```csharp
  chart.NSeries.Add("A1:A4", true);
  chart.ValueAxis.IsAutomaticMinValue = false;
  workbook.Save("axis.xlsx");   // throws NullReferenceException
  ```

### CELLS-COLOR-TINT-KEPT

Setting `CellsColor.Color` on a theme color the engine returns, such as the default
`SparklineGroup.SeriesColor` (accent 1, tint -0.5), keeps the tint and applies it to the new
color: `#12AB34` reads back as `#09561A` before any save, and the saved file holds that color.
Assigning a new `Workbook.CreateCellsColor()` with the color gives it as set.

  ```csharp
  SparklineGroup group = sheet.SparklineGroups[sheet.SparklineGroups.Add(SparklineType.Line, "A1:D1", false, area)];
  group.SeriesColor.Color = Color.FromArgb(0x12, 0xAB, 0x34);
  // group.SeriesColor.Color is #09561A
  ```

### CELLS-SHEET-INSERT-DUPLICATE

`WorksheetCollection.Insert(index, type, name)` before the last sheet takes a name another sheet
already has, in any case, leaving two sheets of one name, which Excel never allows; at the end of
the collection it refuses the name with a `CellsException`.

  ```csharp
  workbook.Worksheets[0].Name = "Data";
  workbook.Worksheets.Insert(0, SheetType.Worksheet, "DATA");   // taken: two sheets named Data
  workbook.Worksheets.Insert(workbook.Worksheets.Count, SheetType.Worksheet, "Data");   // throws
  ```

## Aspose.PDF.Drawing 26.8.0

Reproductions: [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs).

### PDF-HTML-EMPTY-FONT

The HTML importer throws `EndOfStreamException` when
`HtmlLoadOptions.CustomLoaderOfExternalResources` supplies no bytes for a font an `@font-face`
rule names; with no bytes for a picture it imports the page without the picture.

The CLI's loader supplies no bytes for a resource on the network or outside the page's folder.

### PDF-HTML-EMPTY-OBJECT

The HTML importer throws `NullReferenceException` when
`HtmlLoadOptions.CustomLoaderOfExternalResources` supplies no bytes for the data of an `<object>`
element; with no bytes for a picture it imports the page without the picture.

The CLI's loader supplies no bytes for a resource on the network or outside the page's folder.

### PDF-ATTACHMENT-KEYS-EMPTY

`Document.EmbeddedFiles.Keys` throws an exception of the engine's own, whose name is obfuscated,
on a document without attachments; `Count` and the enumeration give none.

### PDF-ATTACHMENT-DESCRIPTION-NOT-SAVED

Setting `FileSpecification.Description` on an attachment read from a file
(`Document.EmbeddedFiles[i]`) is not saved: the saved file keeps the old description, while
`MIMEType` set the same way is saved.

### PDF-EVALUATION-COLLECTIONS

In evaluation mode `PageCollection.Count` gives every page while its enumerator gives the first
four only, and copying the collection (`CopyTo`, as a `ToArray` does) fills the rest with null.
Reading the annotations of a later page, or a later element of any collection, throws
`IndexOutOfRangeException` ("At most 4 elements (for any collection) can be viewed in evaluation
mode").

### PDF-PAGE-COPY-OWN-WIDGETS

Copying a page that holds form fields within its own document (`Pages.Insert(1, Pages[1])`) and
saving it leaves the form's field count as it was, and the saved copy holds twice the widget
annotations the original does.

### PDF-IMAGES-READ-WRITES

Reading `Page.Resources.Images` creates an empty `/XObject` dictionary in the resources of a page
that has none, which a save then writes.

### PDF-HTML-EGRESS

The HTML importer requests http(s) stylesheets and images, and runs script, before it calls
`HtmlLoadOptions.CustomLoaderOfExternalResources`. The Markdown importer and SVG images
(`Image.File`, `ImageStamp`) have no resource hook: they fetch remote resources and read any local
file a reference names, and the Markdown importer resolves relative paths against the process
working directory.

The CLI blocks every http, https, ftp and file request the engine makes itself, and its loader
supplies only the files beneath the HTML page's folder.

### PDF-MOVE-BOOKMARK

Destination getters read an omitted (null) coordinate as 0, and no public API writes one
(`XYZExplicitDestination` with `NaN` writes an invalid `NaN` token), so a destination that named a
moved page cannot always be rebuilt exactly; the SDK has no page move.

### PDF-LINK-STALE-PAGE

A link annotation keeps the destination it read first, page number included, so after a page
deletion in memory a link read before it still names the number its page had: one to a deleted
page can name a page the document still holds. Bookmarks and named destinations read their new
numbers, and the saved file leads to the right page, or to none.

### PDF-REDACT-CALCULATED

A redaction that removes a form field leaves the form's calculation order (`/CO`) naming it, so
the field's object, with its value and appearance, stays in the saved file. The public API cannot
repair the order: `Form.CalculatedFields` has no getter, and setting it to any fields writes no
order at all.

### PDF-REDACT-EMPTY-PARENT

A redaction that removes every widget of a field that holds them apart from itself, as children or
as the buttons of a radio group, leaves the field in the form without a widget and with its value.
One that removes the only field under a parent in the form's hierarchy, such as
`form.person.name` under `form.person`, leaves the parent in the form as a field of its own,
without a value or a widget.

### PDF-REDACT-RADIO-BUTTONS

A redaction that removes some buttons of a radio group whose form was read before it leaves the
group counting them while its options lose them; setting the group's value then writes it and
throws `IndexOutOfRangeException`.

### PDF-PAGE-COPY-FIELDS

No public API changes the order of a document's pages: `PageCollection` has no move, `Remove`
throws `NotSupportedException`, inserting a page the document holds copies it with each field's
widget still on the original page, and inserting one deleted from it throws. Copied into another
document and inserted back, a page brings each of its form fields under a new top-level name
(`ssn1` beside `ssn`, `form1.person.name` beside `form.person.name`), while the original field
stays in the form without a widget and with its value.

### PDF-PDFA-ATTACHMENT-TYPE

`Document.Convert` to PDF/A-3B gives every attachment that has no media type the type
`application/pdf`, whatever its content.

### PDF-ATTACHMENT-COPY-TYPE

`EmbeddedFiles.Add` with the `FileSpecification` of another document's attachment gives the added
attachment no media type (an empty `MIMEType`); its name, description and contents are kept. The
type is read from the file only when first asked for, and a specification whose `MIMEType` was
not read before the add reads empty afterwards as well, so the type is lost unless it is read
first.

### PDF-OUTLINE-DELETE-TITLE

`OutlineItemCollection.Delete()` removes bookmarks by title rather than the item it is called on:
it removes the first bookmark with that title it finds, which may be a different one, and when
bookmarks at different levels share the title it can remove several.

### PDF-NAMES-WITHOUT-DESTS

`Document.NamedDestinations.Names`, `Count` and the lookup by name (`NamedDestinations[name]`)
throw `NullReferenceException` when the catalog's name tree has no `Dests` entry, for example a
tree that holds only attachments, such as the one a merged document gets with the attachments of
its inputs.

### PDF-NAMES-BESIDE-DESTS

When the catalog has both a `Dests` dictionary and a name tree of destinations (`Names` → `Dests`),
`Document.NamedDestinations` reads only the dictionary: a name only the tree holds is not listed
and its lookup returns null, so a link or bookmark that leads through it reads as leading to no
page, while a reader follows the tree. The saved file keeps both.

### PDF-TAGGED-CONTENT-WRITES

Reading `Document.TaggedContent` rewrites the loaded document: it sets an empty title to
`Tagged PDF`, creates XMP metadata dated now that declares PDF/UA (`pdfuaid:part` 1), and drops
the PDF/A identification, so `IsPdfaCompliant` turns false.

### PDF-IMPORT-INFO-PLACEHOLDER

The HTML and Markdown importers set the document title, author and subject to `Aspose`, whatever
the source states; the HTML `title` element is ignored.

### PDF-IMPORT-LINK-ADDRESSES

The HTML and Markdown importers resolve every relative link against a local folder, the HTML's
`BasePath` or else the process working directory, and write the `file:` address they get into the
link annotation's text and, for a link to another file, into its URI action, so the PDF holds the
absolute path of that folder; an absolute or share path becomes a `file:` address there too. A
link to an element `id` (`href="#part"`) gets neither a destination nor an action; one to an
`<a name="part">` anchor leads to its page. The importers write no other action or file
specification for a link.

### PDF-HTML-FORM-NAMES

The HTML importer keeps the `name` of single-line text inputs only. It names check boxes, selects,
text areas and buttons after their `id` attribute, or with a name of its own when they have none,
and radio groups always with a name of its own (such as `field_#0`, `field_1` and `radio`), and it
drops their `value` attributes: a check box is checked with `Yes` and radio buttons take the
values `Item0`, `Item1` and so on. `HtmlLoadOptions` has no option to keep them.

### PDF-HTML-FORM-INPUTS

The HTML importer drops inputs of type `email`, `tel`, `url`, `time`, `datetime-local`, `month`,
`week`, `color`, `range`, `file`, `image` and `hidden`: they become no form field, and the value of
a hidden input is lost.

### PDF-HTML-CHECKBOX-BOX

The HTML importer gives a check box neither a border nor a border color, so its appearances draw
no box: an unchecked box shows nothing and a checked one only its check mark. Radio buttons get a
black border.

### PDF-PDFA-RADIO-APPEARANCE

The HTML importer writes a radio group's field into its page's annotations with an appearance
stream of its own. `Document.Convert` to PDF/A returns success and leaves it, and validation of
the saved file then fails the clause on annotation appearances, 6.3.3 in PDF/A-2 and PDF/A-3 and
6.5.3 in PDF/A-1 (the appearance of a button field must be a subdictionary of states).

### PDF-PDFA-LOG-REPEATS

The PDF/A log that `Document.Validate` writes lists a problem once for every use of the object it
concerns, and marks it convertible (`Convertable="True"`) on only one of those entries: a font
that is not embedded is reported as both fixable and not on the same page, although converting
to the profile embeds it.

### PDF-TEXT-GAP-SPACE

Text extraction and `TextFragmentAbsorber` read a horizontal gap of about a sixth of an em or more
between two text runs as a space, and search the text with those spaces; no `TextSearchOptions`
or `TextExtractionOptions` setting turns this off. Word leaves such a gap between East Asian text
and digits or Latin letters (automatic spacing), so `2026年10月31日` in a Word-made PDF extracts
as `2026年 10月 31日` (two spaces in the plain mode) and a search for it finds nothing.

### PDF-ATTACHMENT-NAME-OPENS-FILE

The `FileSpecification.Name` setter opens the file its value names, relative to the process
working directory, for reading and writing. It throws when that file is open elsewhere, for
example as the stream the specification was built from.

### PDF-REDACT-TEXT-SHIFT

`RedactionAnnotation.Redact()` removes the glyphs under the annotation without keeping their
advance, so a text run that starts where the previous one ended, without a position of its own,
moves left by the removed width, under the cover. Word writes the runs of a line that way.
`PdfAnnotationEditor.RedactArea` and replacing the text with an empty string move the runs the
same way, and setting `TextFragment.Position` to put them back draws other runs of the line over
each other.

### PDF-ENCRYPTED-INFO-TEXT

A document information entry (title, author, subject, keywords or a custom entry) set in a session
whose save encrypts the file is written with only the low byte of each character, so text beyond
Latin-1, such as `采购申请`, reads back garbled. Entries read from the file, and entries written to
a file saved without encryption, are kept.

### PDF-INFO-KEY-BEYOND-LATIN1

A document information entry whose name has a character beyond Latin-1, such as `项目编号`, is
saved under a name written from each character's code as if it were one byte, so the name reads
back garbled while its value is kept. Names within Latin-1 are kept.

### PDF-SIGN-FULL-FONT

`PdfFileSignature.Sign` embeds the whole font program the signature appearance draws in, visible
or not: Arial (about 560 KB) by default, and a complete CJK font such as SimSun (about 10 MB) for a
reason, location, contact or signer name in Chinese. With Helvetica, which is not embedded,
printable ASCII needs no font; any other character embeds a whole font again.

### PDF-SIGN-CJK-SIGNER-FONT

`PdfFileSignature.Sign` chooses the font of a visible signature's appearance from its reason,
location and contact alone. When none of them has Chinese text, it draws in Arial and leaves out a
signer name in Chinese: the first line reads `Digitally signed by 'CN='`. Japanese text, such as
`批准`, gives a Japanese font, MS Gothic, which leaves out a simplified Chinese name such as `张伟`.
Naming a Chinese font, such as SimSun, through `SignatureCustomAppearance.FontFamilyName` shows the
name.

### PDF-RENDER-THIN-GLYPH

`PngDevice` and the other raster devices leave out thin glyph strokes, such as the underscores of
SimSun, at some positions on the page below about 300 DPI: the line is not drawn at all, not even
as gray. Paths of the same thickness are drawn.

### PDF-CONCURRENT-SAVE-MODDATE

`Document.Save` writes the modification date (`ModDate`) into the document information of a PDF
it saves, but may leave it out when other documents of the same process are being saved at the
same time. How many of the concurrent outputs lose it varies from run to run; saved one at a
time, every output has it.

### PDF-SIGN-RECT-ROTATED

`PdfFileSignature.Sign(page, visible, rectangle, signature)` places the rectangle in the page's
unrotated space from its lower-left corner, but on a page rotated by 90 or 270 degrees it first
swaps the rectangle's x with y and its width with height; on a page rotated by 180 degrees it does
not swap them.

### PDF-TEXT-RECT-ROTATED-CROP

On a rotated page that has a crop box, `TextFragment.Rectangle` is given in the space of the
visible, rotated page, but not from that page's lower-left corner: at 90 degrees its x is offset
by the crop box's bottom, at 270 degrees its y by the crop box's left, at 180 degrees neither, and
upright both, as the page's own space has them.

### PDF-REDACT-ROTATED

On a rotated page, `RedactionAnnotation.Redact()` draws its cover where its rectangle lies in the
page's unrotated space but removes the text it finds there in the rotated page's space, so text
under the cover stays in the file.

### PDF-TEXT-SEARCH-WRAPS-CONTENT

Searching a page's text with `TextFragmentAbsorber` changes the page it only reads: on a page that
carries a stamp, it wraps the page's content in one more saved graphics state (`q` … `Q`), which
draws nothing different.

### PDF-DOCX-EMBED-FONTS

Saving as docx writes `w:embedTrueTypeFonts` and `w:embedSystemFonts` into `word/settings.xml` but
embeds at most the few font subsets the PDF itself embeds, and none of the fonts it does not, so
the next save by Aspose.Words or Word embeds every other font the document uses in full: a
one-page document in a standard PDF font grows from about 7 KB to about 3 MB. `DocSaveOptions` has
no setting for these flags.

### PDF-ENCRYPTED-OBJECT-STREAMS

`OptimizeResources` with `CompressObjects` on an encrypted document writes a file that no reader
opens: Aspose.PDF reads it back with neither encryption nor text, and other readers report a
corrupt object stream, with the password or without. Decrypting first and encrypting after the
optimization writes a sound file.

### PDF-FORM-RECURSION-CRASH

Reading the text of a page (`TextAbsorber`) whose Form XObject draws itself recurses until the
stack overflows, which ends the process at once with exit code `0xC00000FD` and no exception that
code could catch. Rendering the same page with `PngDevice` completes.

### PDF-MD-FIELD-REPEATS

The Markdown export writes the value of a flattened text field that the HTML importer made, which
it draws with a border, twice: as a one-cell table and in the line of text the field sits in.
`TextAbsorber` reads the value once on the same page. A bordered text field made with
`TextBoxField` is written once.

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

## Aspose.Slides.NET6.CrossPlatform 26.9.0

Reproductions: [SlidesKnownIssueTests](tests/Aspose.Cli.Product.Slides.Tests/SlidesKnownIssueTests.cs),
[SlidesConvertKnownIssueTests](tests/Aspose.Cli.Product.Slides.Tests/SlidesConvertKnownIssueTests.cs).

### SLIDES-AUTOFIT-RECT

In a text frame that shrinks its text on overflow, `IParagraph.GetRect` reports laid-out lines
that end past the right edge of the frame once the text overflows, while rendering shrinks the
text and draws every line inside the frame. The runs' rectangles (`IPortion.GetRect`) follow the
shrunk text as rendering draws it.

### SLIDES-CHART-TITLE

Loading a chart whose automatic title is implicit (no `c:title`, `autoTitleDeleted` 0) reports a
title that overlays the plot, and saving writes it, so the plot area grows and its automatic axis
scale can change.

### SLIDES-FALLBACK-STDOUT

Every rendering or export that uses a font fallback rule writes `Updating of Inner rules` to
`Console.Out`.

### SLIDES-FONT-REGISTRY

Font initialization casts every value under
`HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts` to `string`, so a `REG_DWORD` or
`REG_BINARY` value written by other software throws `InvalidCastException` from any call that
lays text out, such as a save, a conversion or a rendering.

### SLIDES-ODP-NATIVE-CONTENT

Saving a presentation as ODP writes each native chart as a picture and drops the slide
transitions, although OpenDocument presentations can hold both.

### SLIDES-PPT-NATIVE-CONTENT

Saving a presentation as PPT writes each native chart as a picture and drops the table styles, so
a styled table keeps its cells and text but not its fills, borders and text formatting.
PowerPoint's own PPT save keeps the chart editable and the table's look.

### SLIDES-PPT-TEMPLATE-LAYOUTS

Loading some PowerPoint 97-2003 binary files (PPT, PPS, POT) lists extra layouts in
`LayoutSlides`, with the name and type of another layout (a second "Title Slide" and "Section
Header" for a design whose PPTX has one of each). They belong to the master like the others, but a
slide added on one of them makes the PPTX save throw `KeyNotFoundException` for
`Aspose.Slides.LayoutSlide`. A PPTX save of the loaded file without such a slide drops them.

### SLIDES-MD-INHERITED-BULLETS

Markdown export writes a paragraph as a list item only when the paragraph itself sets its bullet.
Paragraphs whose bullets come from the layout or master, as in a body placeholder, are written as
plain lines without their level; no `MarkdownSaveOptions` setting changes this.

### SLIDES-ALT-TEXT-TAB

Saving a shape's alternative text writes a tab in it unescaped into the `descr` attribute, and
loading the file reads the text without the tab.

  ```csharp
  using (var deck = new Presentation())
  {
      deck.Slides[0].Shapes.AddAutoShape(ShapeType.Rectangle, 10, 10, 50, 50).AlternativeText = "a  b\tc";
      deck.Save("alt.pptx", SaveFormat.Pptx);       // descr="a  b<tab>c"
  }
  using var loaded = new Presentation("alt.pptx");  // Shapes[0].AlternativeText is "a  bc"
  ```

### SLIDES-CHART-NO-SAMPLE-CATEGORIES

A chart added without sample data (`IShapeCollection.AddChart(…, initWithSample: false)`) is saved
as PPTX without categories (`c:cat`) for one of its series. With one series, the reopened chart
has no categories, whether they came from worksheet cells or from literals; with two series, the
file gives the categories to one series only.

### SLIDES-CHART-NO-SAMPLE-NAME

On a chart added without sample data, a PPTX save throws `NullReferenceException` when its series
has no name cell: a series named by a string literal (`Name.DataSourceType` `StringLiterals`), or
one added by `IChartSeriesCollection.Add(ChartType)` without a name. The series named by a literal
saves on a chart added with sample data.

### SLIDES-CHART-SHEET-CREATED

`IChartDataWorkbook.GetCell` with a worksheet index or name the workbook does not have adds a
worksheet instead of failing: index 1 of a workbook of one sheet adds a second `Sheet1`, the name
`sheet1` beside `Sheet1` adds `sheet1`, and a null name adds a sheet whose name is null. The
embedded workbook is saved with these names, the null one empty, although sheet names are unique
without regard to case and never empty.

### SLIDES-CHART-CELL-RANGE

`IChartDataWorkbook.GetCell` takes row -1, row 1048576 and column 16384, which lie outside a
worksheet, and the embedded workbook is saved with cells `A0`, `A1048577` and `XFE1`.

### SLIDES-CHART-CELL-READ

Reading an empty cell through `IChartDataWorkbook.GetCell` changes the saved embedded workbook:
two saves write the same worksheet part, but after `GetCell(0, 300, 70)` the part holds a cell
`BS301`.

### SLIDES-CHART-POINT-REMOVE

On a chart none of whose point values has been read, removing a data point
(`IChartDataPointCollection.RemoveAt` or `Remove`, or `IChartDataPoint.Remove`) lowers `Count` but
keeps the point: a PPTX save writes it, and the first point value read afterwards brings it back.
Removing point 2 of the 4 of a sample chart's series leaves 3; the saved chart reopens with 4, and
once a value is read the series in memory has 4 points again, with the indexes 0, 1, 3 and 3.
After any point value has been read, the same calls remove the point.

### SLIDES-CHART-POINT-BY-INDEX

`IChartDataPointCollection.GetOrCreateDataPointByIdx` adds empty points up to the index, as
documented, but a PPTX save drops them: a series of 4 points has 11 once point 10 is created, and
4 once saved and reopened.

### SLIDES-CHART-MIXED-ORDER

A chart whose series have different types reopens with its series in another order: a pie series
added after the three column series of a sample chart is the first series once the chart is saved
as PPTX and reopened.

### SLIDES-CHART-DATA-SETTER

The `IBaseChartValue.Data` setter treats a string by where the value comes from: a point value
read from a worksheet cell writes it into the cell, a point value given as a literal
(`DoubleLiterals`) throws `ArgumentException` ("Assigned value is not appropriate to
DataSourceType."), and a series name read from a worksheet cell throws the same
`ArgumentException`.

### SLIDES-CHART-UNUSED-VALUE

A point value member the chart type does not use, such as `IChartDataPoint.XValue` of a column
chart, has no cell and reads `Data` as null, and setting its `Data` throws
`NullReferenceException` instead of an exception that names the cause.

### SLIDES-CATEGORY-AXIS-DATE

`IAxis.CategoryAxisType` set to `CategoryAxisType.Date` on the horizontal axis of a column chart
with text categories, as `AddChart` creates it with sample data, reads `Date` in memory, but a
PPTX save writes a text axis (`c:catAx`, no `c:dateAx`), so the reopened axis reads `Text`.

### SLIDES-SLIDE-NUMBER-SETTER

`ISlide.SlideNumber` reads the slide's index plus `IPresentation.FirstSlideNumber`, but its setter
moves the slide to the index of the value minus 1, as if numbering started at 1. With
`FirstSlideNumber` 5 and three slides, setting the first slide's number to 7, the last slide's
number, throws `PptxEditException`, and setting it to 4 throws the same exception after changing
`FirstSlideNumber` to 4.

### SLIDES-INSERT-CLONE-RANGE

`ISlideCollection.InsertClone` takes any index from 0 to `Count`, and refuses a larger one with
`PptxEditException` "Index must lay within [0..Count-1] range.", a range that leaves out `Count`.

### SLIDES-SECTION-INSERT-END

`ISectionCollection.AddEmptySection(name, index)` refuses the index `Count` with
`PptxEditException`, while the engine's other insert calls, such as
`ISlideCollection.InsertEmptySlide`, take `Count` as the end; only `AppendEmptySection` adds a
section at the end.

### SLIDES-TABLE-INDEX-MESSAGE

`ITable[column, row]` out of range throws `IndexOutOfRangeException` with messages that give
different upper bounds: in a table of 3 columns and 2 rows, column 3 is refused with "value of
index = 3 is out of range 0..3", a range that includes it, and row 2 with "value of index = 2 is
out of range 0..1".

### SLIDES-BULLET-CHAR-UNSET

`IBulletFormat.Char` of a paragraph that sets no bullet reads U+FFFF, a Unicode noncharacter,
while the effective bullet character is U+2022. Its documentation names no value for an unset
character, as that of `IBulletFormat.Height` names `float.NaN`.

### SLIDES-FOOTER-LAYOUT-ONLY

`ISlideHeaderFooterManager.SetFooterVisibility(true)`, `SetSlideNumberVisibility(true)` and
`SetDateTimeVisibility(true)` on a slide whose layout has no such placeholder return without an
error and change nothing, even when the master has the placeholder: `IsFooterVisible`,
`IsSlideNumberVisible` and `IsDateTimeVisible` still read false, and the slide gets no shape.

### SLIDES-PARENT-GROUP-ROOT

`IShape.ParentGroup` of a shape that is not grouped (`IsGrouped` false) is the slide's root group
shape (`OfficeInteropShapeId` 1), which is not among the slide's shapes; the documentation says it
is null for a shape that is not grouped.

### SLIDES-UNIQUEID-TAGS

Once the `IShape.UniqueId` of one shape has been read, saving writes a custom tag part
(`ppt/tags/tagN.xml`) holding the unique id for every shape of the presentation, those of its
masters and layouts too, and every later save of the file keeps them. A new presentation with two
shapes is saved without such a part before the read, and with more parts than the slide has
shapes after it.

### SLIDES-ENVIRONMENT-TAGS

Every presentation the engine creates or opens has five custom tags in
`IPresentation.CustomData.Tags` that neither the file nor the caller set, each named `AS_` and a
word: the .NET version, the operating system and its version, and the engine's release date,
title and version. A PowerPoint file without tags opens with them, and a save writes all five even
after one is removed, so each saved file names the operating system that saved it.

### SLIDES-DUPLICATE-SHAPE-ID

Loading a slide on which two shapes share an id (`p:cNvPr id`) gives the second one the slide's
highest id plus 1, without a warning through `LoadOptions.WarningCallback`: shapes of ids 2, 2 and
4 load as 2, 5 and 4.

### SLIDES-IMAGE-PART-WARNING

Loading a presentation whose image part is cut short or empty reports no warning through
`LoadOptions.WarningCallback`, while a missing image part is reported (`DataLoss`, "Empty image
part.").

### SLIDES-IMAGE-EMPTY-PART

A presentation whose image part is empty (0 bytes) opens, but the image's `IPPImage.Width` and
`Height` throw `EndOfStreamException` ("Unable to read beyond the end of the stream."); its
`BinaryData` reads 0 bytes.

### SLIDES-ACTIVEX-EMPTY-SAVE

An ActiveX control part with an all-zero `classid` and no properties loads as a control, and
saving the presentation throws `NullReferenceException`; with one property, it saves.

### SLIDES-IMAGE-UNKNOWN-BYTES

`IImageCollection.AddImage(byte[])` takes bytes in no image format it decodes, an SVG document
among them, without an error or a warning. The image reads `ContentType` `image/unknown` and the
size 0 × 0, a picture frame of it draws nothing, and a PPTX save stores it as
`ppt/media/image1.image` of the content type `image/unknown`. The same SVG given as an `ISvgImage`
(`AddImage(ISvgImage)`) draws.

The CLI does not add an SVG file as a picture.

### SLIDES-ODP-EMPTY-DECK

A presentation without slides saved as ODP reopens with one slide; saved as PPTX, it reopens with
none.

### SLIDES-ODP-MIMETYPE

An ODP save writes the package's `mimetype` entry compressed and after other entries, while
OpenDocument requires it to be the first entry, stored without compression.

### SLIDES-ODP-LENGTH-PRECISION

An ODP save writes lengths in centimetres rounded to 0.001 cm (about 0.03 pt), so shape positions
reopen changed: shapes at X 205.5, 41.5, 312 and 100.25 pt are written as `7.249cm`, `1.464cm`,
`11.006cm` and `3.536cm` and reopen at 205.4962, 41.50179, 312.00046 and 100.23929 pt. A PPTX save
keeps them exactly. The engine has no save options for ODP.

### SLIDES-MD-LINE-BREAK

Saving a presentation as Markdown (`SaveFormat.Md`) drops a line break inside a paragraph: a text
frame whose text is `First line\vSecond line` is written as `First lineSecond line`, the two lines
joined with nothing between them.

## Aspose.Words 26.9.0

Reproductions: [WordsKnownIssueTests](tests/Aspose.Cli.Product.Words.Tests/WordsKnownIssueTests.cs).

### WORDS-TABLE-STYLE-RESET

Setting a table's style (`Table.Style`, `StyleName` or `StyleIdentifier`) clears every setting the
table makes itself, not only the borders, cell margins and cell spacing the style decides: its
alignment and left indent, text wrapping and position, right-to-left layout, title and
description and style options, each row's heading, row-break and hidden setting, and each cell's
vertical alignment, which the document stores apart from the style it names.

### WORDS-LAYOUT-TABLE-GRID

Reading `Document.PageCount` lays the document out, and the layout rewrites the column widths of
each table's grid (`w:tblGrid`), which the next save writes: a document read for its page count
and saved differs from one only saved.

### WORDS-EMPTY-TABLE-DROPPED

A save does not keep a table without rows, or a row without cells; a table that ended its story
leaves an empty paragraph there.

### WORDS-EVALUATION-TRACKED-SAVE

Without a license, `Document.Save` throws `ArgumentException` "Cannot insert a node of this type
at this location" when `StartTrackRevisions` is in effect and the document holds revisions; after
`StopTrackRevisions`, or with a license, the same document saves.

### WORDS-ADJACENT-TABLES

Two tables with nothing between them are saved as two and opened as one.

### WORDS-ODT-FIELDS

Saving to ODT or OTT drops the results of `PAGE` and `NUMPAGES` fields, and drops formula (`=`)
fields.

### WORDS-HEADER-EMPTY-LINKED

`HeaderFooter.IsLinkedToPrevious` reads true for a header or footer that holds no paragraph,
although the section has one of its own of that type.

### WORDS-HORIZONTAL-MERGE-SPAN

A horizontal merge (`CellFormat.HorizontalMerge` `First` and `Previous`) is saved as one cell
spanning the merged ones: once opened again the row has one cell fewer, whose `HorizontalMerge` is
`None`.

### WORDS-TEXT-COMMENTS

Saving to plain text or Markdown writes each comment's text into the body, as a paragraph beside
the one that anchors the comment, where it reads as document text. `TxtSaveOptions` and
`MarkdownSaveOptions` have no option to leave comments out.

### WORDS-TEXT-DELETIONS

Saving a document with tracked changes to plain text or Markdown writes deleted and moved-from
text beside the text that replaces it, so the output reads as neither the original nor the
revised document.

### WORDS-PAGE-FIELD-LAYOUT

A page field (`PAGE`, `NUMPAGES`, `SECTIONPAGES`, `PAGEREF`) that `DocumentBuilder.InsertField`
inserts after the document was laid out, for example by reading `Document.PageCount`, gets an
empty result, and `Field.Update` leaves it empty until `Document.UpdatePageLayout` rebuilds the
layout.

### WORDS-PDF-HEADER-FOOTER

Loading a PDF guesses its headers and footers. In a PDF of one page they are written into the body
as ordinary paragraphs, and the rebuilt layout can push them onto a page of their own: a one-page
PDF with a footer page number loads as two pages. In a longer PDF, text repeated at the top or
bottom of the pages becomes a real header or footer, but a number in it that matches a page number
becomes a `PAGE` field, so a footer reading "Version 1  Page 1" turns into two page fields and
reads "Version 2" on the second page. `PdfLoadOptions` has only `PageIndex`, `PageCount` and
`SkipPdfImages`, so no option keeps headers and footers as text.

### WORDS-PDF-CJK-LINE-END

Loading a PDF ends each line of Chinese, Japanese or Korean text with a space, so "甲乙双方"
wrapped after "甲" loads as "甲 乙双方", and it may merge the lines of separate paragraphs, such as
a heading and the clause after it, into one paragraph. A space belongs between Latin words, not
inside East Asian text, which has none. `PdfLoadOptions` has no option for either.

### WORDS-PDF-ENCRYPTION

`FileFormatUtil.DetectFileFormat` reports `IsEncrypted` false for every PDF, including one that
needs a password to open and one with only an owner password, although loading the first without
a password, or either with a wrong one, is refused.

### WORDS-CJK-LINE-BREAK

Page layout breaks East Asian lines against the line-breaking rules the paragraph turns on
(`ParagraphFormat.FarEastLineBreakControl`). Text whose East Asian language is not Chinese,
Japanese or Korean, such as the English that a new `Document` and many converted documents
declare, may start a line with "，" or "。" while the current culture is Chinese, and on a Windows
set to Chinese whatever the current culture is, en-US included (seen on a Windows whose display
language and regional format are Chinese). In a document without Word 2013 compatibility mode,
East Asian text drawn in one font for all scripts, as PDF loading writes it, breaks only at spaces
after Latin text, so "人民币 1,920,000.00 元（大写：…）" ends its line after the number although
part of the rest would fit.

### WORDS-TRACKED-DETACHED-TEXT

While `Document.StartTrackRevisions` is on, setting `Run.Text` on a run that is not in the
document tree, such as a copy from `Node.Clone`, throws `NullReferenceException`.

### WORDS-REGEX-TIMEOUT

`Range.Replace(Regex, string, FindReplaceOptions)` ignores the expression's `MatchTimeout`: an
expression that backtracks catastrophically, such as `(a+)+$` on a run of `a` that ends in another
character, runs until it finishes, which can take hours.

### WORDS-REGEX-ANCHORS

`Range.Replace(Regex, string, FindReplaceOptions)` matches `^` only at the start of the range,
even with `(?m)`, and `$` at no paragraph end, even in the range of one paragraph, so an
expression anchored at each paragraph replaces at most the first one.

### WORDS-MD-FOOTNOTE-HEADING

Loading Markdown takes a heading that follows a footnote definition after a blank line, such as
`# B` after `[^1]: Note one.`, into the footnote's text instead of starting a new heading,
although CommonMark ends the definition at that unindented line. A paragraph of ordinary text
after the definition ends it as expected.

### WORDS-MD-NUMBER-ESCAPE

Saving to Markdown escapes a paragraph or table cell that starts with digits and a period or
closing parenthesis as an ordered-list marker, whatever follows: `22.75` becomes `22\.75`, which
renders right but gives a script that reads the bytes a backslash, although CommonMark takes
digits and a period as a marker only before a space or the line end; and `22) total` becomes
`\22) total`, whose backslash before a digit CommonMark keeps as text. `MarkdownSaveOptions` has
no option to turn the escaping off.

### WORDS-TEXT-FOOTNOTE-BODY

Saving a document as plain text (`SaveFormat.Text`) writes a footnote's text into the body where
its reference stands, and ends the line there: a paragraph `Body` + footnote `Note text.` +
` continues.` is written as `Body Note text.` and, on the next line, ` continues.`

### WORDS-HTML-SVG-SKIP

Loading HTML, the engine asks `HtmlLoadOptions.ResourceLoadingCallback` for each image that an SVG
image of the page names in an `<image>` element (`href` or `xlink:href`). When the callback
answers with `ResourceLoadingAction.Skip`, the official way to leave a resource out, `new Document`
throws `FileCorruptedException` "The document appears to be corrupted and cannot be loaded.",
whose inner exception is an `ArgumentNullException` for `inArray`, whether the image is a network
address, a missing file or a file that exists. Answered with `Default`, the page loads, even when
the engine then finds no image; so it does when the callback supplies the image's bytes
(`UserProvided`), when the image is a `data:` URI, and when the SVG has no `<image>`.

The CLI answers `Skip` for every resource it does not supply, so `words convert` of such a page
reports `FILE_CORRUPT`.

## Memory on untrusted documents

An SDK expands a document in memory while it loads it, before `--max-input-bytes` can count what
is in it, and nothing limits that memory. `--max-input-bytes` admits a document by the size of its
file, which a document that compresses well passes: a 300 KB DOCX holding 100 MB of XML reaches
1.4 GB before `INPUT_BUDGET_EXCEEDED` refuses it, and a 500 KB XLSX of 8 million cells reaches
0.76 GB and loads.
