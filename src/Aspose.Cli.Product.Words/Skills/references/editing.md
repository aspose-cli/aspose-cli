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
  table of contents, are blocks too.
- A header or footer field whose result changes from page to page (`PAGE`,
  `NUMPAGES`, `SECTIONPAGES`) reads as its placeholder, such as `{PAGE}`.
  Read comments and footnotes with `inspect --detail comments` or
  `search --scope`, and tracked changes with `inspect --detail revisions`
  ([revisions](revisions.md)).
- A search pattern anchored with `^` must allow for the number of a numbered
  paragraph, and `set_text` changes only the text after it.
- `search --pattern ".+" --regex --scope all` returns the text of every scope
  line by line.

`words read --blocks 1- --max-blocks 30` reads the first 30 blocks, or all of
a shorter document. `--section` with `--blocks` reads only that section's
blocks in the range; block numbers run through the whole document, so a range
with none of them is `BLOCK_NOT_FOUND`, whose hint names the section's blocks.

`extract --what text` writes the visible text of every block to one file, one
line per paragraph and per table row, whose cells are separated by tabs. It
holds the body only: no headers or footers, footnotes, endnotes or comments,
and it does not say they were left out. `extract --what comments` writes the
comments with their author and block. `extract --what tables` writes each
body table to its own CSV file (`table-001.csv` and on); a cell's paragraphs
and line breaks become lines inside its field, and cells a merge covers stay
as empty fields. Cell text is written as it is, so Excel
evaluates a cell that starts with `=`, `+`, `-` or `@` as a formula; import
such files as text when that matters.

## Addresses

- `find` matches blocks whose visible text contains the given text, and
  `heading` does the same among heading paragraphs. When nothing matches,
  `ANCHOR_NOT_FOUND` suggests a close heading, or names the
  block that holds the longest leading part of the text, heading or not,
  ignoring spaces and preferring body text to table of contents entries. Prefer
  bookmarks and headings in automation; read block numbers again immediately
  before using them.
- There is no character-offset addressing.
- In `format_text`, with `"regex": true` a pattern picks one match among
  several, such as `(?<=and )Party B` for the second "Party B" of "Party A
  pays Party B, and Party B signs.".
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
  such as a block of a removed section, fails when the batch reaches it. An
  earlier operation that failed deleted nothing, so it refuses nothing. Later
  section numbers never shift to a different original section.
- `--track-changes` records content insertions and deletions: `replace_text`,
  `set_text`, `insert_*` (page breaks only, not section breaks),
  `delete_blocks`, `set_table_cell`, `repeat_table_row`, `import_sections`. A
  tracked batch can also hold `add_comment` and `delete_comments`: a comment is
  a review annotation of its own, so it is added or removed outright and is
  never listed as a revision. Every other operation would change the document
  without a revision, so a tracked batch that contains one fails with
  `OPTION_INVALID` before anything changes; run it in a separate batch.

## Text

- `set_text` replaces the inline content of paragraphs. Use `set_table_cell`
  for table cells. With `"target": {"bookmark": "Name"}`, or
  `--set bookmark:Name=text`, it replaces only the text the bookmark encloses,
  anywhere including table cells: the bookmark and the rest of its paragraph
  remain, the new text takes the format of the bookmark's first run, and a
  bookmark spanning several paragraphs becomes one paragraph.
- `replace_text`'s new text takes the format of the text where the match
  starts; when that text has no Chinese, Japanese or Korean characters, such
  as an amount in digits, East Asian characters of the new text take the East
  Asian font of the nearest East Asian text in the paragraph.
- `repeat_table_row` expands a template row of the table `target` addresses
  (such as `{"find":"{{code}}"}`). Without `row`, the template is the table's
  one row with a `{{key}}` placeholder (spaces inside the braces are allowed).
  A missing CSV value is empty text. Extra keys are ignored. A tracked batch
  records each copy as a row insertion and the template row as a deletion.
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
| Add content | `insert_paragraphs`, `insert_markdown` (also footnotes, see above), `insert_table`, `insert_image`, `insert_hyperlink`, `insert_field`, `insert_toc`, `insert_bookmark`, `import_sections` |
| Remove content | `delete_blocks` |
| Styles and formatting | `set_style`, `define_style`, `format_text`, `set_table_style`, `set_table_pagination`, `set_list`, `clear_list`, `set_default_font` |
| Sections and pages | `insert_break`, `insert_section`, `delete_section`, `set_page_setup`, `set_header`, `set_footer`, `set_page_numbers`, `set_watermark`, `delete_watermark` |
| Review annotations | `add_comment`, `delete_comments`, `accept_revisions`, `reject_revisions` |
| Fields and data | `refresh_fields`, `mail_merge` ([mail merge](mail-merge.md)) |
| Document state | `set_properties`, `protect_document`, `unprotect_document` |

## Recipe: insert several pieces in reading order

Insertions at the same anchor stack against it: each `after` insertion lands
directly after the anchor, ahead of earlier ones, and each `before` insertion
lands directly before it, behind earlier ones. To keep the batch order as the
reading order, anchor every piece `before` the block that should follow them.

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
under the same parent around the insertion point. When the level has no item
there, the first new item continues the paragraph before, bold included, and
the operation's later items at that level continue the same paragraph. Check
`listLabel`, `paragraphFormat` and `runs` in `words read --content full`
against its neighbours'.

A paragraph with `style`, inserted Markdown and a table's text take their font
from styles. A template that sets its font only on runs, such as Microsoft
YaHei over a Normal in another font, therefore gives such text, CJK text in
particular, a different font. Compare `inspect --detail fonts` of the template
and the output. To match, put the body font into the styles first, in the same
batch: `define_style` on `Normal`
changes Normal and the styles based on it, and `set_default_font` changes every
paragraph and character style. Styles do not reach a font set on the text
itself, as chapters appended from RTF, HTML or text usually carry
(`inspect --detail fonts` still lists their fonts): give those blocks
`format_text` with the same `font`, `latinFont` and `eastAsianFont` fields, for
example
`{"op":"format_text","target":{"blocks":"26-36"},"latinFont":"Arial","eastAsianFont":"SimSun"}`.

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

A caption or heading before a table stays with it through its own paragraph
style's keep-with-next setting, which heading styles usually have.

```json
{ "ops": [ { "op": "set_table_pagination", "target": { "find": "Action items" }, "keepTogether": true, "headerRowCount": 1 } ] }
```

## Headers, footers and page numbers

A `set_header` or `set_footer` paragraph without `style` keeps the alignment,
style and font of the first paragraph it replaces, including one a section
continues from an earlier section. Apply footer text before `set_page_numbers`.
Neither writes a page number: when the primary header or footer they replace
held one (a PAGE, NUMPAGES or SECTIONPAGES field) and the batch leaves the
section's primary header and footer without one, the edit reports
`PAGE_NUMBERS_REMOVED`, naming each section that showed it, a later section
that continues it too. Its hint gives the `set_page_numbers` that numbers the
sections whose header or footer the operation wrote, which those that continue
it follow, and writes the number as it read; words beside a field such as
`DATE` it names instead, since no `text` writes that field. A first-page or even-page kind is not checked, since
no operation numbers those pages again.
After `import_sections`, check the result with `inspect --detail sections`; to
give the whole document one header and footer, follow with `set_header` and
`set_footer` without a `section`.
A `set_page_numbers` `start` restarts numbering in each selected section; name
a `section` when only one should restart. Its `text` replaces what the
paragraph that holds the number held, in that paragraph's font, so the number
stays in one line with its words. Moved to the other `location` without
`text`, the number takes its words along, such as `Page {n} of {N}`. A number
whose paragraph also holds another field, such as `DATE`, neither moves, with
`text` or without, nor takes `text` where it is, since a move clears that
paragraph and a text replaces it: rewrite that header or footer with
`set_header` or `set_footer` first, or leave the number where it is without
`text`.

## Protection and encryption

- Editing restrictions (`protect_document`) are not encryption: they guide Word's user
  interface and do not bind the CLI. Editing a restricted document succeeds,
  reports `PROTECTION_NOT_ENFORCED` and keeps the restrictions in a Word
  format output. `unprotect_document`
  with `passwordEnv` checks the password and fails with `PASSWORD_INVALID`
  when it is wrong; without `passwordEnv` it removes the restrictions whatever
  their password, so use it only when the user owns that decision.
- An encrypted input's edited or converted output, the parts of a split and
  the redline of a comparison of it (as the original) keep the password when
  the output format supports encryption. A read password for a plaintext input
  does not encrypt the output. A PDF input with only an owner password opens
  without `--password-env` and is still reported as `input.encrypted`; its
  output has no password to keep, so it is written without one and produces
  `ENCRYPTION_REMOVED`.
- A `doc` output can carry only legacy RC4 encryption; when the protection
  matters, deliver `docx`, which uses AES.

No Words command removes a password. For a copy that opens without one, write
Flat OPC, which cannot carry a password, and convert that:

```powershell
aspose-cli words convert locked.docx --password-env DOC_PASSWORD --to flatopc --out plain.xml --output-format json
aspose-cli words convert plain.xml --to docx --out plain.docx --output-format json
```

A PDF input can instead be decrypted first by a `decrypt_document` operation of
`aspose-cli pdf edit`, opened with its owner password.

`words convert --to pdf` cannot encrypt, so `--encrypt-env` with it is
`OPTION_INVALID`. For a PDF that needs a password to open, convert first:

```powershell
aspose-cli words convert contract.docx --to pdf --out contract.pdf --output-format json
```

then encrypt `contract.pdf` with an `encrypt_document` operation of
`aspose-cli pdf edit` (the PDF Skill, `aspose-cli docs pdf/forms-security`).
Use `--in-place` without `--backup`, which would keep the unencrypted PDF.

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
`rtf` output stores each image once, in its own format such as PNG, without the uncompressed metafile copy
for old readers that would make it many times larger; Word and LibreOffice
read it, but a reader that knows only metafiles shows no images.
`words convert` to `txt`, `md`, `html` or `html-fixed` reports
`LOSSY_CONVERSION`: such a format cannot hold every Word feature, such as
styles, headers or fields. A convert to any format other than those `edit`
writes also reports it for the tracked changes it cannot keep as revisions,
and to any non-Word format, PDF included, for the editing restrictions it
cannot keep; restrict a PDF with the `encrypt_document` operation of
`aspose-cli pdf edit`. An `epub`, `azw3` or `mobi` output has no headers or
footers, and a `txt` or `md` output is the body text alone: a convert to one
leaves out the headers, footers and page numbers and reports
`LOSSY_CONVERSION` naming the sections whose headers or footers some page
shows with text or drawings, a page number alone aside; read them on the
source with `words inspect --detail sections`, or keep a `docx` copy. A `txt`
or `md` convert also writes comment text into the body and
deleted text beside inserted text, each reported by its own
`LOSSY_CONVERSION`; run `delete_comments`, and `accept_revisions` or
`reject_revisions` as the reviewer decides, in the edit before it to leave
them out. A `txt` output lays each table row out on one line with its cells in
aligned columns. A convert to a page format (`pdf`, `xps`, `openxps`, `ps`,
`pcl`, `html-fixed`) and a `words render` draw the tracked changes on the pages as
markup and the comments in balloons in a margin that narrows the pages; each
is reported by its own `LOSSY_CONVERSION` (a render reports only the
comments), and the same edit before it leaves them out. `--verify` adds the
semantic checks described in [verification](verification.md); it does not
render pages.
