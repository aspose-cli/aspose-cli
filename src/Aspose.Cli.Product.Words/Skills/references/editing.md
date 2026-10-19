# Editing

`words edit --ops` applies one operation batch; batches, `--best-effort`,
`--dry-run`, outputs and secrets work as `aspose-cli docs editing` describes.
Each operation's fields, types, defaults and allowed values come from the
generated schema:

```powershell
aspose-cli schema v2/words/ops --operation insert_table
```

## Reading the document

Windows, paging and compact output in general are in `aspose-cli docs
reading`. What Words adds:

- The paragraphs and tables inside a block-level content control, such as a
  table of contents, are blocks too. `inspect` names the `scope` of each
  image, field and revision, and `search` the `scope` and `section` of each
  hit; neither gives a `block` in headers and footers.
- `inspect --detail sections` lists the headers and footers each section
  defines, by `location` (`header`, `footer`) and `kind` (`primary`, `first`,
  `even`, the `kind` of `set_header` and `set_footer`), with their
  `paragraphs`; a kind a section does not define continues from the previous
  section. A search hit in one names its `location` and `kind` too.
- A header or footer field whose result changes from page to page (`PAGE`,
  `NUMPAGES`, `SECTIONPAGES`) reads as its placeholder, such as `{PAGE}`.
  Read comments and footnotes with `inspect --detail comments` or
  `search --scope`, and tracked changes with `inspect --detail revisions`
  ([revisions](revisions.md)).
- A numbered paragraph's block `text` leaves its number out, but search hit
  `context` and patterns, `find`/`heading` addresses, extracted text and table
  `values` start with it. A search pattern anchored with `^` must allow for
  the number, and `replace_text` and `set_text` change only the text after it.
- `search --scope` and `replace_text` share one scope vocabulary: `body`,
  `headers-footers`, `footnotes` (with endnotes), `comments` and `all`.
  `search --pattern ".+" --regex --scope all` returns the text of every scope
  line by line, each hit naming its `scope`, within the `--max-hits` window.
- A table block's `rowCount`, `columnCount` and `values` and a paragraph's
  `text` and `style` are written back through `insert_table` and
  `insert_paragraphs` under the same names.

`words read --blocks 1- --max-blocks 30` reads the first 30 blocks, or all of
a shorter document; a closed range such as `1-30` must lie inside the
document, or it is `BLOCK_NOT_FOUND`. `--content outline|text|full` chooses
how much of each block is projected. `--max-chars` bounds the sum of
paragraph, table-cell and run text; addressing and formatting metadata do not
count against it. Check `window.truncated` and each block's
`contentTruncated`, and run `window.next` as given: it resumes at the block
the budget cut short and doubles `--max-chars` when that block alone exceeded
it. `--section` with `--blocks` reads only that section's blocks in the
range; block numbers run through the whole document, so a range with none of
them is `BLOCK_NOT_FOUND`, whose hint names the section's blocks.

`extract --what text` writes the visible text of every block to one file, one
line per paragraph and per table row, whose cells are separated by tabs. It
holds the body only: no headers or footers, footnotes, endnotes or comments,
and it does not say they were left out. `extract --what comments` writes the
comments with their author and block. `extract --what tables` writes each
body table to its own CSV file (`table-001.csv` and on, UTF-8; add `--bom`
for a byte order mark, so Excel reads Chinese text) and reports its `block`;
a cell's paragraphs and line breaks become lines inside its field, and cells
a merge covers stay as empty fields. Cell text is written as it is, so Excel
evaluates a cell that starts with `=`, `+`, `-` or `@` as a formula; import
such files as text when that matters.

## Addresses

- A block address (`at` or `target`, as the operation's schema names it) is
  exactly one of `block`, `blocks`, `bookmark`, `heading` or `find`. `find` matches blocks whose visible text
  contains the given text, ignoring case, and `heading` does the same among
  heading paragraphs; `nth` picks the match (the first by default). When
  nothing matches, `ANCHOR_NOT_FOUND` suggests a close heading, or names the
  block that holds the longest leading part of the text, heading or not,
  ignoring spaces and preferring body text to table of contents entries. Prefer
  bookmarks and headings in automation; read block numbers again immediately
  before using them.
- Insertions take `position: before|after` at a block boundary; there is no
  character-offset addressing.
- `format_text` with `match` formats only the text it matches inside its
  target blocks, in the text `words search` reads there, text boxes included;
  the rest keeps its format. `regex`, `caseSensitive` and `wholeWord` match as
  they do in `replace_text`. With `"regex": true`
  a pattern picks one match among several, such as `(?<=and )Party B` for the
  second "Party B" of "Party A pays Party B, and Party B signs.". A `words
  search` hit's `occurrence` numbers it among the matches of the document.
- A block-level content control is a container: its paragraphs and tables are
  blocks, content inserted beside one of them stays inside the control,
  deleting all of them removes the control, and a section break cannot be
  placed inside it.

## Batch semantics

- Every address, including numbered sections, resolves to the document as
  it was before the first operation. An omitted section means every original
  section. Inserted content and sections cannot be targeted later in the same
  batch.
- An operation whose block, bookmark or section an earlier operation deleted,
  such as a block of a removed section, fails when the batch reaches it, and
  its error names it in `details.id`; with `--best-effort` only that
  operation fails. An earlier operation that failed deleted nothing, so it
  refuses nothing. Later section numbers never shift to a different original
  section.
- `--track-changes` requires `--author` and records content insertions and
  deletions: `replace_text`, `set_text`, `insert_*` (page breaks only, not
  section breaks), `delete_blocks`, `set_table_cell`, `repeat_table_row`,
  `import_sections`. A tracked batch can also hold `add_comment` and
  `delete_comments`: a comment is a review annotation of its own, so it is added
  or removed outright and is never listed as a revision. Every other operation
  would change the document without a revision, so a tracked batch that
  contains one fails with `OPTION_INVALID` before anything changes; run it in a
  separate batch.

## Text

- `set_text` replaces the inline content of paragraphs and keeps their style
  and paragraph format; as typing over it in Word does, the new text takes the
  font of the first text it replaces. Use `set_table_cell` for table cells.
  With `"target": {"bookmark": "Name"}`, or
  `--set bookmark:Name=text`, it replaces only the text the bookmark encloses,
  anywhere including table cells: the bookmark and the rest of its paragraph
  remain, the new text takes the format of the bookmark's first run, and a
  bookmark spanning several paragraphs becomes one paragraph.
- `replace_text` changes the text a reader sees: field results but never field
  codes, and never text a tracked change deletes. It replaces the matches
  `words search` finds with the same pattern and scope, so a regex `^` or `$`
  anchors at each paragraph, comment and note; a match that takes in a list
  number, a paragraph mark or a header or footer placeholder such as `{PAGE}`
  is left alone and not counted. The new text is inserted as
  text, so `^p` or `&p` adds no break, and takes the format of the text where
  the match starts; when that text has no Chinese, Japanese or Korean
  characters, such as an amount in digits, East Asian characters of the new
  text take the East Asian font of the nearest East Asian text in the
  paragraph. With `--track-changes` each match reads as a deletion followed
  by an insertion.
- `repeat_table_row` expands a template row of the table `at` addresses (such
  as `{"find":"{{code}}"}`): one copy per item, in order, in place of the
  template row. Without `row`, the template is the table's one row with a
  `{{key}}` placeholder (spaces inside the braces are allowed); pass the
  1-based `row` when no row or several rows have one. Items come inline as
  `items` or from `path` (a JSON array of flat objects, or CSV with a header
  row); a number or Boolean is its JSON text, and null or a missing CSV value
  is empty text. Each placeholder takes the
  item's value as literal text in the placeholder's formatting, and the copies
  keep the row's height, borders, shading and cell widths. An item without a
  key for one of the row's placeholders fails the operation, naming an extra
  key close to the placeholder as a likely misspelling; extra keys are
  otherwise ignored, and no items removes the template row. A tracked batch records
  each copy as a row insertion and the template row as a deletion.
- Inline Markdown loads local resources under the edited document's directory;
  remote and escaping resources are omitted and reported.
- Footnotes have no operation of their own: `insert_markdown` adds one from
  Markdown's footnote syntax, a `[^id]` reference in the text and a
  `[^id]: note text` line in the same `markdown`, such as
  `"markdown": "Certified under ISO 9001[^iso].\n\n[^iso]: Certificate CN-2026-001."`.
  `words create --from-markdown` reads the same syntax; put every definition
  at the end of the Markdown ([document standards](document-standards.md)).
  Read the notes back
  with `words search --pattern ".+" --regex --scope footnotes`.

## Operation index

The operations grouped by task; each one's description and fields are in
`aspose-cli schema v2/words/ops --operation <op>`.

| Task | Operations |
|---|---|
| Change text | `replace_text`, `set_text`, `set_table_cell`, `repeat_table_row` |
| Add content | `insert_paragraphs`, `insert_markdown` (also footnotes, see below), `insert_table`, `insert_image`, `insert_hyperlink`, `insert_field`, `insert_toc`, `insert_bookmark`, `import_sections` |
| Remove content | `delete_blocks` |
| Styles and formatting | `set_style`, `define_style`, `format_text`, `set_table_pagination`, `set_list`, `clear_list`, `set_default_font` |
| Sections and pages | `insert_break`, `insert_section`, `delete_section`, `set_page_setup`, `set_header`, `set_footer`, `set_page_numbers`, `set_watermark`, `delete_watermark` |
| Review annotations | `add_comment`, `delete_comments`, `accept_revisions`, `reject_revisions` |
| Fields and data | `refresh_fields`, `mail_merge` ([mail merge](mail-merge.md)) |
| Document state | `set_properties`, `protect_document`, `unprotect_document` |

## Recipe: insert several pieces in reading order

Insertions at the same anchor stack against it: each `after` insertion lands
directly after the anchor, ahead of earlier ones, and each `before` insertion
lands directly before it, behind earlier ones. To keep the batch order as the
reading order, anchor every piece `before` the block that should follow them.
`insert_table`'s `tableStyle` names an existing table style; `inspect --detail
tables` shows the `tableStyle` each table uses, so a new table can match the
document's tables.

```json
{
  "ops": [
    { "op": "insert_paragraphs", "at": { "find": "Revenue increased" }, "position": "before",
      "paragraphs": [ { "text": "Key figures", "style": "Heading 3" }, { "text": "Figures are in thousands." } ] },
    { "op": "insert_table", "at": { "find": "Revenue increased" }, "position": "before",
      "rowCount": 2, "columnCount": 2, "values": [ [ "Metric", "Value" ], [ "Revenue", "120" ] ],
      "tableStyle": "Table Grid" }
  ]
}
```

An `insert_paragraphs` paragraph without `style` continues the paragraph
before the insertion point, as Word's Enter does: it takes that paragraph's
indents, spacing and alignment and the direct run format of its last text
outside a field such as a hyperlink, such as font, size, color and bold,
without a character style, so a clause inserted between clauses matches them.
After a paragraph whose
style names another next style, such as a heading, it takes that style alone;
it continues no list, page break before or tracked change, and at the start of
the body or after a table it takes Normal.

A paragraph with a `list` (`{"level": 2}`, or with `"kind": "number"` for a
new numbered list) before or after a list item joins the list that
numbers that level there: the anchor's list when it defines the level, such as
a new clause 2.3 after 2.2, else, as in lists created from Markdown, where each
nesting level is a list of its own, the list of the nearest item at that level
under the same parent around the insertion point. Without `style` it continues
that list's nearest existing item at the level instead of the paragraph before,
so an item after a bold parent item is not bold when the level already has an
item there. When it has none, the first new item continues the paragraph
before, bold included, and the operation's later items at that level continue
the same paragraph. An item that continues an existing item takes that item's
left and first-line indents when it sets its own, as RTF conversions often do;
otherwise it follows the list level's indents like its neighbours. When no list
numbers the level there, or the anchor is no list item, it joins one new bullet
list shared by the operation's list paragraphs. Check `listLabel`,
`paragraphFormat` and `runs` in `words read --content full` against its
neighbours'.

`set_list` with `"kind": "number"` renumbers paragraphs as one new list:
`numberStyle` (`decimal`, `roman-lower`, `roman-upper`, `letters-lower` or
`letters-upper`) changes 1., 2., 3. into a., b., c., `numberFormat` sets the
text around the number, in which `%1` to `%9` stand for the numbers of levels
1 to 9, such as `"(%1)"` with `letters-lower` for (a), (b), (c) or `"%1.%2"`
with `decimal` at `"level": 2` for 1.1, 1.2, and `%%` for a percent sign, and
`start` sets the first number, such as 5 for 5., 6., 7. `numberFormat` shows no
level deeper than its own; without its own level's placeholder every item shows
the same label. A read reports all three in each item's `list`, and a
`numberFormat` it reports writes back unchanged; `listLabel` shows the result. `clear_list` takes the target
paragraphs out of their lists; their text and style stay.

A paragraph with `style`, inserted Markdown and a table's text take their font
from styles. A template that sets its font only on runs, such as Microsoft
YaHei over a Normal in another font, therefore gives such text, CJK text in
particular, a different font. Compare `inspect --detail fonts` of the template
and the output. To match, put the body font into the styles first, in the same
batch: `define_style` on `Normal`
changes Normal and the styles based on it, and `set_default_font` changes every
paragraph and character style. Their `font` sets the Latin and the East Asian
font; `latinFont` and `eastAsianFont` set one of them, as a template that sets
Chinese text in SimSun and English text in Times New Roman needs. Styles do not
reach a font set on the text itself, as chapters appended from RTF, HTML or
text usually carry (`inspect --detail fonts` still lists their fonts): give
those blocks `format_text` with the same `font`, `latinFont` and
`eastAsianFont` fields, for example
`{"op":"format_text","target":{"blocks":"26-36"},"latinFont":"Arial","eastAsianFont":"SimSun"}`. Read them
back as each run's `latinFont` and `eastAsianFont` in
`words read --content full`; its `font` is the East Asian font when the
run starts with a Chinese, Japanese or Korean character, otherwise the Latin
font.

```json
{
  "ops": [
    { "op": "define_style", "name": "Normal", "font": "Microsoft YaHei" },
    { "op": "insert_table", "at": { "block": 2 }, "position": "after",
      "rowCount": 1, "columnCount": 2, "values": [ [ "Term", "Value" ] ] }
  ]
}
```

## Tables across pages

`set_table_pagination` sets how the one table `target` addresses breaks across pages;
omitted settings keep their values:

- `keepTogether: true` keeps the whole table on one page when it fits: no row
  splits, and every paragraph keeps with the next except the last paragraph of
  each last-row cell. `false` clears keep-with-next on those same paragraphs
  and leaves the row setting; pass `allowRowBreakAcrossPages: true` as well to
  let rows split again. A table taller than a page still breaks.
- `allowRowBreakAcrossPages` sets whether each row's text may split.
- `headerRowCount` repeats the first N rows as a heading on every page the
  table spans and clears the other rows; `0` clears all, and more than the
  table's rows fails.
- `keepWithNext` keeps the table on the page of the paragraph that follows it.
  A caption or heading before the table stays with it through its own
  paragraph style's keep-with-next setting, which heading styles usually have.

`inspect --detail tables` reads `keepTogether`, `allowRowBreakAcrossPages`,
`headerRowCount` and `keepWithNext` back for each table.

```json
{ "ops": [ { "op": "set_table_pagination", "target": { "find": "Action items" }, "keepTogether": true, "headerRowCount": 1 } ] }
```

## Headers, footers and page numbers

`set_header` and `set_footer` replace the selected kind, including its fields,
in one section or every section. Their content is exactly one of `markdown` or
`paragraphs`, the same `{"text": ..., "style": ...}` objects `insert_paragraphs`
takes, without `list`, such as `[{"text": "Contract C-2026-014"}]`. A paragraph
without `style` keeps the alignment, style and font of the first paragraph it
replaces, including one a section continues from an earlier section, or takes
Word's Header or Footer style when there is none. Markdown takes the document's styles. Apply footer text before `set_page_numbers`.
`import_sections` brings the appended document's sections with the headers
and footers they define, and `formatting` decides only styles. A
section that defines no header or footer of a kind continues the one before
it, so after appending a chapter whose template had a header, the next
appended chapter without one shows that header too. Check the result with
`inspect --detail sections`; to give the whole document one header and
footer, follow with `set_header` and `set_footer` without a `section`.
Page numbering targets only the primary header or footer, reuses its first
PAGE field or appends one in a new paragraph, and keeps the other content. A
`start` restarts numbering in each selected section; name a `section` when only
one should restart.

## Protection and encryption

- Editing restrictions (`protect_document`) are not encryption: they guide Word's user
  interface and do not bind the CLI. Editing a restricted document succeeds,
  reports `PROTECTION_NOT_ENFORCED` and keeps the restrictions in a Word
  format output. `unprotect_document`
  with `passwordEnv` checks the password and fails with `PASSWORD_INVALID`
  when it is wrong; without `passwordEnv` it removes the restrictions whatever
  their password, so use it only when the user owns that decision.
- An encrypted input needs `--password-env` to open, and `inspect` reports
  it as `input.encrypted`. Its edited or converted output, the parts of a
  split and the redline of a comparison of it (as the original) keep the
  password when the output format supports encryption; `--encrypt-env`
  replaces it on `edit`, `convert` and `split`. A format that cannot be encrypted produces
  `ENCRYPTION_REMOVED`, and `--encrypt-env` with such a format is
  `OPTION_INVALID`. A read password for a plaintext input does not encrypt the
  output. A dry run does not report removed encryption.

## Save and verify

`edit` and `create` write only the formats that reopen as the same document:
the Word formats (`doc`, `dot`, `docx`, `docm`, `dotx`, `dotm`, and Flat OPC
or WordML `.xml`), `rtf`, `odt` and `ott`. Any other output, such as
`--out report.pdf` or `notes.md`, or an in-place edit of such an input, is
refused with `OPTION_INVALID` before anything is read; the hint names the
convert to run on the edited copy:

```powershell
aspose-cli words edit contract.docx --ops ops.json --out revised.docx --verify --output-format json
aspose-cli words convert revised.docx --to pdf --out revised.pdf --output-format json
```

Every output is reopened before publication. An `rtf`, `odt` or `ott` output
reports `LOSSY_CONVERSION` for the editing restrictions it cannot keep. An
`rtf` output stores each image once, in its own format such as PNG, without
the uncompressed metafile copy for old readers that would make it many times
larger; Word and LibreOffice read it, but a reader that knows only metafiles
shows no images.
`words convert` to `txt`, `md`, `html` or `html-fixed` reports
`LOSSY_CONVERSION`: such a format cannot hold every Word feature, such as
styles, headers or fields. A convert to any format other than those `edit`
writes also reports it for the tracked changes it cannot keep as revisions,
and to any non-Word format, PDF included, for the editing restrictions it
cannot keep; restrict a PDF with the `encrypt` operation of
`aspose-cli pdf edit`. A `txt` or `md` convert also writes comment text into
the body and deleted text beside inserted text, each reported by its own
`LOSSY_CONVERSION`; run `delete_comments`, and `accept_revisions` or
`reject_revisions` as the reviewer decides, in the edit before it to leave
them out. A `txt` output is the body text: it leaves out headers, footers and
page numbers, and lays each table row out on one line with its cells in
aligned columns. `--verify` adds the semantic checks described in
[verification](verification.md); it does not render pages.
