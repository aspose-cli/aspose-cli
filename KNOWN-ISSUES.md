# Known issues

Confirmed defects in the commercial Aspose SDKs this CLI runs on, and how the CLI handles each.
How they are reproduced and retired: [CONTRIBUTING.md](CONTRIBUTING.md#known-sdk-issues).

## Aspose.Cells 26.9.0

### CELLS-SVG-EGRESS

- **Defect:** adding an SVG picture requests the images its `xlink:href` attributes name while
  the engine rasterizes the picture's fallback image; `WorkbookSettings.ResourceProvider` is
  never consulted.
- **CLI behavior:** `insert_image` refuses an SVG that names a network address, and a compressed
  SVG, with `FEATURE_UNSUPPORTED`.
- **Workaround:** embed the SVG's images as `data:` URIs, or insert a raster image.
- **Reproduction:** [CellsKnownIssueTests](tests/Aspose.Cli.Product.Cells.Tests/CellsKnownIssueTests.cs)

### CELLS-SPARKLINE-APOSTROPHE

- **Defect:** `SparklineGroups.Add(type, dataRange, isVertical, locationRange)` throws
  `Invalid "'"` for a data range on a sheet whose name has an apostrophe, however it is quoted.
- **CLI behavior:** none visible; `add_sparkline` builds the group from its parts, with the same
  result.
- **Reproduction:** [CellsKnownIssueTests](tests/Aspose.Cli.Product.Cells.Tests/CellsKnownIssueTests.cs)

### CELLS-COPY-NAME-SCOPE

- **Defect:** `Worksheet.Copy` from another workbook, when the copied sheet uses a
  workbook-level name that the destination also defines and the source's definition refers to
  another sheet, adds the source's definition as a name scoped to the destination's first sheet.
  Once saved, that sheet's own formulas read the added name instead of the workbook's, and the
  copied sheet's formulas read the destination's definition.
- **CLI behavior:** `import_sheet` refuses, with `OPS_INVALID`, a source that defines a
  workbook-level name this workbook defines differently, unless the name refers only to the
  imported sheet.
- **Workaround:** rename or delete the name in one of the workbooks, or import the cells with
  `import_range`.
- **Reproduction:** [CellsKnownIssueTests](tests/Aspose.Cli.Product.Cells.Tests/CellsKnownIssueTests.cs)

### CELLS-COPY-EXTERNAL-CACHE

- **Defect:** `Worksheet.Copy` and `Range.Copy` from another workbook, when a copied formula
  reads a third workbook through a link that caches no values (such as a link written while the
  linked file was absent), give the destination's link an empty cache. The formula evaluates to
  `#REF!` in the source but, once the destination is calculated, reads the linked cells as empty,
  usually as 0.
- **CLI behavior:** `import_sheet` and `import_range` with `"content": "all"` warn
  `EXTERNAL_LINK_CACHE_MISSING` with the copied cells whose result changed this way;
  `--verify` reports it as a verification issue, so the edit exits 8.
- **Workaround:** replace those formulas with `set_formula` or `set_values`, or save the source
  in Excel with the linked workbook available so the link caches its values.
- **Reproduction:** [CellsKnownIssueTests](tests/Aspose.Cli.Product.Cells.Tests/CellsKnownIssueTests.cs)

### CELLS-LINK-RELATIVE-TARGET

- **Defect:** a formula that reads a workbook by its full path, when that workbook is in the
  folder the edited workbook was opened from, gets a link to the file name alone. An xlsx output
  stores it as the relative target `fx.xlsx` with the relationship type `xlPathMissing`, so once
  saved to another folder the link no longer names the file in the input's folder. How Excel
  resolves an `xlPathMissing` target is unverified.
- **CLI behavior:** `cells edit` warns `EXTERNAL_LINK_RELATIVE` with the stored targets when the
  batch adds a link whose target is a file name without a folder.
- **Workaround:** keep the linked workbook in the output's folder, or bring its values in with
  `import_range` instead of a link.
- **Reproduction:** [CellsKnownIssueTests](tests/Aspose.Cli.Product.Cells.Tests/CellsKnownIssueTests.cs)

### CELLS-WIDTH-EAST-ASIAN

- **Defect:** `Cell.GetWidthOfValue` (and `CellsHelper.GetTextWidth`) measures East Asian
  characters with the metrics of the cell's font when that font has no glyphs for them, such
  as Calibri or Arial, while `AutoFitColumn` and rendering draw them with a fallback font. The
  measured width is then up to half the drawn width, or, for mixed text such as
  `配件 Accessories`, can exceed the auto-fitted column by a pixel.
- **CLI behavior:** review's `CELLS_VALUES_CLIPPED` measures with `GetWidthOfValue`, so it can
  miss East Asian text that is cut off or list mixed text that fits; when its samples hold East
  Asian text, its message says the measurement is unreliable for them and asks to check the
  sheet image.
- **Workaround:** give such text a font with East Asian glyphs (for example Microsoft YaHei),
  which all three measure alike, or set the column width explicitly.
- **Reproduction:** [CellsKnownIssueTests](tests/Aspose.Cli.Product.Cells.Tests/CellsKnownIssueTests.cs)

### CELLS-OVERFLOW-EDGE

- **Defect:** the page layout of `SheetRender` (`OnlyArea = false`) cuts text that spills over
  empty cells when its end falls on a column edge: it stops drawing at that edge instead of
  continuing into the next empty cell, which can cut away part of the last character. The page
  layout sizes columns apart from the cell model (a few percent wider with Microsoft YaHei), so
  the edge falls elsewhere in the print-area layout of `OnlyArea = true`, which also draws text
  that ends on an edge in full.
- **CLI behavior:** `cells render` of a whole sheet and review's sheet images can cut such text,
  while `cells render --range` shows it whole; PDF export lays out its own pages, so check its
  pages the same way. No public API measures the page layout, so review lists text that spills
  over empty cells and ends within about 6% of a column edge, the most page layout moves an
  edge, with the info finding `CELLS_TEXT_OVERFLOWS`, and asks to check its end in the image.
- **Workaround:** widen the column the text starts in by one or two characters, or wrap the
  text.
- **Reproduction:** [CellsKnownIssueTests](tests/Aspose.Cli.Product.Cells.Tests/CellsKnownIssueTests.cs)

### CELLS-PIVOT-TOTAL-CAPTION

- **Defect:** a pivot with a column field and two or more value fields labels each value
  field's grand-total column with the word of `PivotGlobalizationSettings.GetTextOfTotal`
  before the caption (`Total Sum of Amount`); overriding the word gives `汇总 求和项:金额`, never
  Excel's Simplified Chinese form `求和项:金额汇总`, and no pivot caption property sets it.
- **CLI behavior:** `create_pivot` keeps the engine's `Total <caption>` in every caption
  language; `aspose-cli docs cells/editing` says so.
- **Workaround:** leave out the column field, so the value fields are the columns and the one
  grand-total row reads `总计`, or summarize with `SUMIFS` formulas.
- **Reproduction:** [CellsKnownIssueTests](tests/Aspose.Cli.Product.Cells.Tests/CellsKnownIssueTests.cs)

## Aspose.PDF.Drawing 26.8.0

### PDF-HTML-EGRESS

- **Defect:** the HTML importer requests http(s) stylesheets and images, and runs script, before
  it calls `HtmlLoadOptions.CustomLoaderOfExternalResources`. The Markdown importer and SVG images
  (`Image.File`, `ImageStamp`) have no resource hook: they fetch remote resources and read any
  local file a reference names, and Markdown resolves relative paths against the process working
  directory.
- **CLI behavior:** HTML, Markdown and SVG input that names a network address or contains script
  is refused with `FEATURE_UNSUPPORTED` before the engine reads it, and Markdown also unless every
  file it references is an ordinary file beneath its directory. For trusted HTML,
  `pdf create --from-html --allow-network-resources` lets the importer fetch and lists every
  address in `NETWORK_RESOURCES_REQUESTED`.
- **Workaround:** keep resources beside the input and reference them by relative path, or
  convert HTML with `aspose-cli words convert`, which makes no network request.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

### PDF-MOVE-BOOKMARK

- **Defect:** destination getters read an omitted (null) coordinate as 0 and no public API writes
  one (`XYZExplicitDestination` with `NaN` writes an invalid `NaN` token), so a destination that
  named a moved page cannot always be rebuilt exactly; the SDK has no page move.
- **CLI behavior:** `move_pages` retargets navigation to and from the moved pages exactly, except
  a destination with a coordinate of 0, which it leaves without a target and counts in
  `NAVIGATION_DEGRADED`.
- **Workaround:** re-create the counted bookmarks and links with `add_bookmark` and `add_link`.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

### PDF-PDFA-ATTACHMENT-TYPE

- **Defect:** `Document.Convert` to PDF/A-3B gives every attachment that has no media type the
  type `application/pdf`, whatever its content.
- **CLI behavior:** `pdf convert --to pdfa-3b` labels such an attachment
  `application/octet-stream` unless its content is a PDF; a declared type is kept.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

### PDF-OUTLINE-DELETE-TITLE

- **Defect:** `OutlineItemCollection.Delete()` removes bookmarks by title rather than the item
  it is called on: it removes the first bookmark with that title it finds, which may be a
  different one, and when bookmarks at different levels share the title it can remove several.
- **CLI behavior:** none visible; `delete_bookmarks` gives each selected bookmark a unique title
  before deleting it, so exactly the selected bookmarks and their children are removed.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

### PDF-NAMES-WITHOUT-DESTS

- **Defect:** `Document.NamedDestinations.Names` and `Count` throw `NullReferenceException` when
  the catalog's name tree has no `Dests` entry, for example a tree that holds only attachments.
- **CLI behavior:** none visible; such a document is read as having no named destinations, so
  `pdf edit`, `pdf merge` and `pdf convert --to pdfa-*` accept it.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

### PDF-TAGGED-CONTENT-WRITES

- **Defect:** reading `Document.TaggedContent` rewrites the loaded document: it sets an empty
  title to `Tagged PDF`, creates XMP metadata dated now that declares PDF/UA (`pdfuaid:part` 1)
  and drops the PDF/A identification, so `IsPdfaCompliant` turns false.
- **CLI behavior:** none visible; `pdf inspect` reads the structure tree after every other
  property and never saves the inspected document.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

### PDF-IMPORT-INFO-PLACEHOLDER

- **Defect:** the HTML and Markdown importers set the document title, author and subject to
  `Aspose`, whatever the source states; the HTML `title` element is ignored.
- **CLI behavior:** `pdf create --from-html` takes the title from the HTML `title` element and
  `--from-text` Markdown leaves it empty; both leave the author and subject empty.
- **Workaround:** set the other fields with a `set_metadata` edit.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

### PDF-HTML-FORM-NAMES

- **Defect:** the HTML importer keeps the `name` of single-line text inputs only. It names check
  boxes, radio groups, selects, text areas and buttons itself (such as `field_#0`, `field_1` and
  `radio`) and drops their `value` attributes: a check box is checked with `Yes` and radio buttons
  take the values `Item0`, `Item1` and so on. `HtmlLoadOptions` has no option to keep them.
- **CLI behavior:** `pdf create --from-html` warns `LOSSY_CONVERSION` with the generated names;
  `pdf query forms` gives every field's `page` and `rect`.
- **Workaround:** match each generated field to the label beside it by its `rect` before filling.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

### PDF-HTML-FORM-INPUTS

- **Defect:** the HTML importer drops inputs of type `email`, `tel`, `url`, `time`,
  `datetime-local`, `month`, `week`, `color`, `range` and `file`: they become no form field.
- **CLI behavior:** `pdf create --from-html` counts such `<input>` tags in the HTML and names
  their types in its `LOSSY_CONVERSION` warning.
- **Workaround:** give those inputs `type="text"`.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

### PDF-HTML-CHECKBOX-BOX

- **Defect:** the HTML importer gives a check box neither a border nor a border colour, so its
  appearances draw no box: an unchecked box shows nothing and a checked one only its check
  mark. Radio buttons get a black border.
- **CLI behavior:** `pdf create --from-html` gives such a check box a black one-point border, so
  the engine draws its box.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

### PDF-PDFA-RADIO-APPEARANCE

- **Defect:** the HTML importer writes a radio group's field into its page's annotations with an
  appearance stream of its own. `Document.Convert` to PDF/A returns success and leaves it, and
  validation of the saved file then fails clause 6.3.3 (the appearance of a button field must be
  a subdictionary of states).
- **CLI behavior:** `pdf convert --to pdfa-*` validates the file it converted and fails with
  `PDFA_CONVERSION_FAILED` when it does not conform, naming in the hint the check boxes and radio
  groups on the pages of appearance problems that have one appearance for all their states.
- **Workaround:** flatten the named fields (`flatten_forms`) before converting.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

### PDF-TEXT-GAP-SPACE

- **Defect:** text extraction and `TextFragmentAbsorber` read a horizontal gap of about a
  sixth of an em or more between two text runs as a space, and search the text with those
  spaces; no `TextSearchOptions` or `TextExtractionOptions` setting turns this off. Word leaves
  such a gap between East Asian text and digits or Latin letters (automatic spacing), so
  `2026年10月31日` in a Word-made PDF extracts as `2026年 10月 31日` (two spaces in the plain
  mode) and a search for it finds nothing.
- **CLI behavior:** literal patterns of `pdf query search` and `redact_text`, and the
  `redact_text` verification, also match with up to two spaces, never a line break, wherever an
  East Asian character meets another character. A `redact_text` operation that matches nothing
  is named in a `REDACTION_NO_MATCH` warning.
- **Workaround:** in a regular expression, allow the gaps with ` *`.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

### PDF-ATTACHMENT-NAME-OPENS-FILE

- **Defect:** the `FileSpecification.Name` setter opens the file its value names, relative to
  the process working directory, for reading and writing. It throws when that file is open
  elsewhere, for example as the stream the specification was built from.
- **CLI behavior:** none visible; `add_attachment` names the attachment through the
  `FileSpecification` constructor and never sets `Name`, so the working directory is not read.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

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
  not prove that nothing moved. `review` reports `PDF_TEXT_COVERED` on every page where text
  lies under a redaction cover. The moved text is still in the file but no longer visible.
- **Workaround:** redact the source document and create the PDF again. With only the PDF, the
  values can all be removed, with `redact_text` or with `redact_area` on search rectangles
  without a margin, the areas of a line from right to left, but the text that follows them on
  their lines stays hidden under the cover.
- **Reproduction:** [PdfKnownIssueTests](tests/Aspose.Cli.Product.Pdf.Tests/PdfKnownIssueTests.cs)

## Aspose.Slides.NET6.CrossPlatform 26.9.0

### SLIDES-AUTOFIT-RECT

- **Defect:** in a text frame that shrinks its text on overflow, `IParagraph.GetRect` reports
  laid-out lines that end past the right edge of the frame once the text overflows, while
  rendering shrinks the text and draws every line inside the frame. No public API exposes the
  font scale that rendering applies.
- **CLI behavior:** `review` does not report `SLIDES_TEXT_OVERFLOWS_SHAPE` for a shape that
  shrinks its text on overflow, as Markdown body placeholders do; text cut off by a slide edge is
  still reported.
- **Workaround:** check such shapes in the rendered review images.
- **Reproduction:** [SlidesKnownIssueTests](tests/Aspose.Cli.Product.Slides.Tests/SlidesKnownIssueTests.cs)

### SLIDES-CHART-TITLE

- **Defect:** loading a chart whose automatic title is implicit (no `c:title`,
  `autoTitleDeleted` 0) reports a title that overlays the plot, and saving writes it, so the plot
  area grows and its automatic axis scale can change.
- **CLI behavior:** every edit, conversion and rendering of such a chart warns
  `CHART_TITLE_OVERLAID` with its slide.
- **Workaround:** inspect those charts in PowerPoint and restore their title layout there.
- **Reproduction:** [SlidesKnownIssueTests](tests/Aspose.Cli.Product.Slides.Tests/SlidesKnownIssueTests.cs)

### SLIDES-CJK-FALLBACK

- **Defect:** when a run's font has no glyph for a Chinese, Japanese or Korean character, the
  engine picks a fallback font per glyph and alternates between Japanese and Chinese fonts inside
  one word (MS Gothic and SimSun on Windows), so rendered and exported text mixes stroke weights.
  PowerPoint draws the run in one East Asian font.
- **CLI behavior:** every loaded presentation gets fallback rules for the CJK ranges that name the
  fonts of its script (kana: Japanese, Hangul: Korean, a Traditional Chinese language tag:
  Traditional Chinese, otherwise Simplified Chinese); the engine uses the first installed one.
  The rules affect rendering only and are not saved.
- **Workaround:** give the template's theme an East Asian font that has the glyphs.
- **Reproduction:** [SlidesKnownIssueTests](tests/Aspose.Cli.Product.Slides.Tests/SlidesKnownIssueTests.cs)

### SLIDES-FALLBACK-STDOUT

- **Defect:** every rendering or export that uses a font fallback rule writes
  `Updating of Inner rules` to `Console.Out`.
- **CLI behavior:** none visible; every Slides engine call runs with `Console.Out` muted, so the
  JSON result and the render worker's messages stay intact.
- **Reproduction:** [SlidesKnownIssueTests](tests/Aspose.Cli.Product.Slides.Tests/SlidesKnownIssueTests.cs)

### SLIDES-FONT-REGISTRY

- **Defect:** font initialization casts every value under
  `HKCU\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts` to `string`, so a `REG_DWORD` or
  `REG_BINARY` value written by other software throws `InvalidCastException`.
- **CLI behavior:** a Slides command that uses fonts fails with `FEATURE_UNSUPPORTED`, naming each
  such value, before the engine starts.
- **Workaround:** remove the named values; Windows writes only string values there.
- **Reproduction:** [SlidesKnownIssueTests](tests/Aspose.Cli.Product.Slides.Tests/SlidesKnownIssueTests.cs)

## Aspose.Words 26.9.0

### WORDS-TEXT-COMMENTS

- **Defect:** saving to plain text or Markdown writes each comment's text into the body, as a
  paragraph beside the one that anchors the comment, where it reads as document text.
  `TxtSaveOptions` and `MarkdownSaveOptions` have no option to leave comments out.
- **CLI behavior:** `words convert` and `words edit` with a `txt` or `md` output warn
  `LOSSY_CONVERSION` with the number of comments written into the body.
- **Workaround:** add `remove_comments` to a `words edit` batch whose `--out` is the text file.
- **Reproduction:** [WordsKnownIssueTests](tests/Aspose.Cli.Product.Words.Tests/WordsKnownIssueTests.cs)

### WORDS-TEXT-DELETIONS

- **Defect:** saving a document with tracked changes to plain text or Markdown writes deleted and
  moved-from text beside the text that replaces it, so the output reads as neither the original
  nor the revised document.
- **CLI behavior:** `words convert` and `words edit` with a `txt` or `md` output warn
  `LOSSY_CONVERSION` when the saved document has tracked deletions or moves.
- **Workaround:** add `accept_revisions` or `reject_revisions`, as the reviewer decides, to a
  `words edit` batch whose `--out` is the text file.
- **Reproduction:** [WordsKnownIssueTests](tests/Aspose.Cli.Product.Words.Tests/WordsKnownIssueTests.cs)

### WORDS-PAGE-FIELD-LAYOUT

- **Defect:** a page field (`PAGE`, `NUMPAGES`, `SECTIONPAGES`, `PAGEREF`) that
  `DocumentBuilder.InsertField` inserts after the document was laid out, for example by reading
  `Document.PageCount`, gets an empty result, and `Field.Update` leaves it empty until
  `Document.UpdatePageLayout` rebuilds the layout.
- **CLI behavior:** none visible; `insert_field` rebuilds the layout and updates such a field
  when it has no result.
- **Reproduction:** [WordsKnownIssueTests](tests/Aspose.Cli.Product.Words.Tests/WordsKnownIssueTests.cs)

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
- **Workaround:** read the headers, footers and fields with `words inspect --detail sections
  fields`, then restore them with `set_header`, `set_footer` or `set_page_numbers` and remove
  body paragraphs that held them with `delete_blocks`.
- **Reproduction:** [WordsKnownIssueTests](tests/Aspose.Cli.Product.Words.Tests/WordsKnownIssueTests.cs)

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
- **Reproduction:** [WordsKnownIssueTests](tests/Aspose.Cli.Product.Words.Tests/WordsKnownIssueTests.cs)

### WORDS-CJK-LINE-BREAK

- **Defect:** page layout breaks East Asian lines against the line-breaking rules the paragraph
  turns on (`ParagraphFormat.FarEastLineBreakControl`). Text whose East Asian language is not
  Chinese, Japanese or Korean, such as the English that a new `Document` and many converted
  documents declare, may start a line with "，" or "。". And in a document without Word 2013
  compatibility mode, East Asian text drawn in one font for all scripts, as PDF loading writes
  it, breaks only at spaces after Latin text, so "人民币 1,920,000.00 元（大写：…）" ends its
  line after the number although the rest would fit in part.
- **CLI behavior:** the built-in design declares Chinese as its East Asian language, so
  documents created without `--template` follow the rules. Other documents keep their own
  language and compatibility settings, which no operation changes; the Words Skill tells
  reviewers to disclose such lines.
- **Reproduction:** [WordsKnownIssueTests](tests/Aspose.Cli.Product.Words.Tests/WordsKnownIssueTests.cs)

### WORDS-TRACKED-DETACHED-TEXT

- **Defect:** while `Document.StartTrackRevisions` is on, setting `Run.Text` on a run that is not
  in the document tree, such as a copy from `Node.Clone`, throws `NullReferenceException`.
- **CLI behavior:** none visible; `insert_paragraphs` and `set_text` set the text of the run they
  copy, from the paragraph before the insertion point or from the replaced text, with tracking
  paused, and the run's insertion is then tracked as usual.
- **Reproduction:** [WordsKnownIssueTests](tests/Aspose.Cli.Product.Words.Tests/WordsKnownIssueTests.cs)
