# PDF editing

`pdf edit` applies one ops batch; atomicity, `--best-effort`, `--dry-run`,
`--if-match`, `--verify`, output and backups work as `aspose-cli docs editing`
describes; [verification](verification.md) lists what `--verify` checks.
PDF operations address fixed pages, rectangles, annotations, form fields,
metadata and security, never paragraphs, worksheets or formulas. Print one
operation's fields, types, defaults and allowed values with
`aspose-cli schema v2/pdf/ops --operation <op>`.

```powershell
aspose-cli schema v2/pdf/ops --operation add_page_numbers --output-format json
aspose-cli pdf edit report.pdf --ops report-ops.json --out report.review.pdf --verify --output-format json
```

## Operations by task

- Pages: `insert_page`, `import_pages`,
  `delete_pages`, `move_pages`, `rotate_pages`,
  `set_page_size`, `crop_pages`.
- Stamps: `add_header_text`, `add_footer_text`, `add_page_numbers`,
  `add_watermark_text`, `add_watermark_image`, `add_stamp_image`.
- Navigation: `add_bookmark`, `delete_bookmarks`, `add_hyperlink`,
  `set_page_labels`.
- Forms: `set_form_field`, `flatten_forms`
  ([forms and security](forms-security.md)).
- Security: `encrypt_document`, `decrypt_document`.
- Redaction: `redact_text`, `redact_area`.
- Document: `set_properties`, `clear_properties`, `add_attachment`,
  `delete_attachment`, `optimize_document`.

## Targets and ordering

Each operation resolves its pages against the document as the earlier
operations of the batch left it: an insert, delete or move changes the pages
that later operations name.

A missing address in `delete_bookmarks.bookmarks` removes none of
them. List every bookmark to
delete in one `delete_bookmarks` operation, with addresses read from one
`pdf inspect --detail outline`. Later operations of the batch see the renumbered outline; put
`add_bookmark` operations that name a parent before the deletion, or reinspect
and edit in a second batch.

`itemsAffected` and `targets` work as `aspose-cli docs editing` describes. PDF
targets name pages as `page/<n>`, bookmarks as `bookmark/<address>`, and
`property/<key>`, `attachment/<name>`, `page-label/<start page>`,
`field/<name>` and `security/encryption`. A deleted page or bookmark keeps the
address it had before the deletion, and a deleted bookmark counts with each of
its children. A page counts once however much changed on it, such as every
match a redaction removed there.

Aspose.PDF writes `creator`, `producer` and `modificationDate` into every PDF
it saves, so no operation removes or sets them: `clear_properties` leaves them
out of its `targets` and warns `METADATA_REWRITTEN`. A `set_properties` `custom`
key must not hold characters beyond Latin-1 (Aspose.PDF writes such a
name garbled; the value may hold any text).

Reinspect geometry and review
after changing crop, size or rotation, because later rectangles follow the new
box. A second crop cuts into the first.

`set_page_labels` updates
only the ranges it names. In `add_page_numbers`, `{N}` is the document's page count when the
operation runs, so number pages after inserting or deleting them. A PDF converted
from a Word document or a spreadsheet often numbers its pages in its footer
already: read a page with `pdf read --pages 1` before numbering, and act on
`PAGE_NUMBERS_ALREADY_PRESENT`.

## Navigation after page moves

`move_pages` keeps the bookmarks, local links and named destinations that lead to
or from the moved pages, with their destination type, coordinates and zoom. The
SDK reads an omitted coordinate as 0: an XYZ destination whose left reads 0 is
kept with a left of 0, which opens the page at much the same place, but one whose
top reads 0, or a fit to the page's height whose left reads 0, is left without a
target, because the CLI would have to invent the position.
`delete_pages` leaves navigation to deleted pages without a target. `pdf merge`
points each copied bookmark at its page with Fit zoom (other locations and zooms
are lost), also one that opened it through a named destination, and does not
carry named destinations. `pdf split` does the same with
the bookmarks of each part's pages; a bookmark whose parent opens a page of
another part moves up in its place, and links to another part lead nowhere.
Each part keeps the page labels its pages had, such as `South-1` to `South-3`,
the input's document information and every attachment of the input. A merge
keeps the document information of the first input and the page labels of each
input; once any input has labels, an input without them numbers its pages from 1.
It keeps every attachment of each input, an attachment whose name and contents an
earlier input already gave only once. `pdf inspect --detail attachments`
lists two attachments of the same name as two entries.
A merge keeps each page's size and warns `PAGE_SIZES_MIXED`, listing the sizes,
when the inputs' paper differs, such as A4 and Letter: set one paper size in the
sources before converting them to PDF.
Each publishes its output with
`NAVIGATION_DEGRADED` warnings that count the entries that no longer lead where
they did, by why. Entries that still lead nowhere although the output holds
their page, such as the links and named destinations a merge or a part does
not carry, have `affectsCompleteness: true`. Entries that led to a page
the command was asked to leave out (deleted by the batch, not selected by
`--pages`, in another part of a split) have a warning without it, and so do
bookmarks that still open their page, only as a whole rather than at their place
in it: no operation sets that place, so tell the user if it matters. The
warnings of `pdf edit` name the bookmarks by their address in the output's
outline, as a `delete_bookmarks` operation that deletes no bookmark still
leading to a page; remove the ones you no longer need with it. A bookmark that
lost its page but holds bookmarks that still lead to theirs is named apart,
since deleting it deletes them too: keep it or tell the user.
Follow the destination checks in
[verification](verification.md).

## Redaction

Redaction is destructive: keep the baseline, apply `redact_text` or
`redact_area`, then verify with `pdf search` and `review`. `pdf search` reads
page text, which holds no form field's value: when it warns
`FORM_VALUES_NOT_IN_TEXT`, read the values of the fields it names with
`pdf inspect --detail forms`. One batch can fill a form, redact it, number the
pages and encrypt the result:

```json
{ "ops": [
  { "op": "set_form_field", "name": "applicant", "value": "Li Lei" },
  { "op": "redact_text", "find": "1[3-9]\\d{9}", "regex": true },
  { "op": "add_page_numbers", "text": "{n} / {N}" },
  { "op": "encrypt_document", "userPasswordEnv": "PASSWORD", "ownerPasswordEnv": "OWNERPASSWORD", "permissions": { "print": true } }
] }
```

A black rectangle or zero search
hits alone is not proof that all sensitive content was removed; a raw-byte
absence check for a known literal is additional evidence only, because PDF text
can be encoded or compressed. A field `flatten_forms` flattened or an attachment
`delete_attachment` deleted no longer holds its value in the file.

Before publishing, every edit with `redact_text`, with or without `--verify`,
searches the output again: the text of the pages it searched, their fields and their
annotations, and, for an operation without `pages`, the bookmark titles, the
attachment names and descriptions, and every document information entry, under
any key, and XMP value the document held; what the save itself writes, such as
the producer and the modification date, is the engine's and is not searched.
A pattern that also
matches a property the document holds, such as `\d` and a date in its XMP
metadata, refuses the edit until `clear_properties` comes before `redact_text`
in the same batch, or a more specific pattern leaves the property alone.
`--dry-run` writes nothing
and does not search the output, so it does not show that a redaction would
publish.

The engine removes redacted text without keeping its width, so text on the
same line that was written to follow it, without a position of its own, moves
left under the cover and no longer shows on the page. Word-made PDFs write
their lines that way, so labels and punctuation after a value, such as
`，手机号：`, disappear under the black box. A `REDACTION_TEXT_MOVED` warning
names each operation and page where that happened, and with `--verify` the
edit exits 8 on it while still writing the output. The check follows runs that keep their whole text, so a run
that the redaction cut can move unnoticed: no warning does not prove that
nothing moved. The moved text is still in the file and searchable, and
nothing in the PDF can move it back: to keep the line visible, redact the
source document and create the PDF again.

When only the PDF exists, the values can still all be removed, at the cost of
the text that follows them on their lines:

- Prefer `redact_text`. Each operation removes its matches on a page from the last to the first, so
  every match is still where it was found.
- To cover text with `redact_area`, take each rectangle from the `rect` of a
  `pdf search` hit as it is. The cover spans the whole rectangle, and that of
  `redact_text` each whole match, so nothing of a scanned image under the text
  shows. On closely set lines the engine also removes characters of a
  neighbouring line that a cover overlaps; a `REDACTION_NEARBY_TEXT_REMOVED`
  warning names each operation and page with the number of such characters and
  lines, never the text itself, and with `--verify` the edit exits 8 on it
  ([verification](verification.md)).
  Do not widen an area there. Confirm a redaction by
  `pdf search` no longer finding the text, not by the black box.
  Order the areas of one line from right to left, because each removal moves
  the rest of the line and an area taken from the original would then miss
  part of the value it was meant to cover.

Then run `pdf search` for each value to confirm that none is left in the page
text, and when it warns `FORM_VALUES_NOT_IN_TEXT`, check the values of the
fields it names with `pdf inspect --detail forms`. Then `review` the output:
tell the user which pages `PDF_TEXT_COVERED` names.

The engine reads a visible gap between characters as a space, so extracted
text can hold spaces the source never had: Word separates Chinese, Japanese or
Korean text from digits and Latin letters with such a gap, and `2026年10月31日`
extracts as `2026年 10月 31日`. Literal patterns of `redact_text` and
`pdf search` therefore also match with up to two spaces, never a line
break, wherever an East Asian character meets a character that is not East
Asian, such as a digit or Latin letter. A regular
expression is matched as written: allow the gaps with ` *`, for example
`合同 *PO-\d+`.

### Scanned pages

A page without a text layer (`pdf read` reports
`SCANNED_PAGES_SUSPECTED`, and `review` `PDF_PAGE_WITHOUT_TEXT_LAYER`) can only
be redacted with `redact_area`. Both name a page that has no extractable text
and one image spanning at least three quarters of its width or height and
covering at least a quarter of it, as a scan of usual page-like proportions
is when `pdf create --from-images` places it with margins of up to an inch
(72 points); a long, narrow image such as a receipt may cover less. Take its
rectangle from a gridded render, never by eye:

1. Render the page with a coordinate grid. `--grid 50` labels every 100 points along the top and left edges.
2. Read the rectangle from the labels. On a scan, add a margin of a few points on every side.
3. Apply `redact_area` to a copy, never to the only original.
4. Render the result again with `--grid` at a higher `--dpi`, and check that
   nothing sensitive shows and that nothing that must stay is covered. Repeat
   from step 2 with a corrected rectangle when either check fails, then
   `review` the output.

```powershell
aspose-cli pdf read scan.pdf --output-format json
aspose-cli pdf render scan.pdf --pages 2 --grid 50 --dpi 150 --out scan.grid.png --output-format json
aspose-cli pdf edit scan.pdf --ops redact-ops.json --out scan.redacted.pdf --output-format json
aspose-cli pdf render scan.redacted.pdf --pages 2 --grid 50 --dpi 200 --out scan.redacted.grid.png --output-format json
```

The grid exists only in the rendered image; the PDF is never changed.

## Other effects

After `optimize_document`, compare file sizes and
look at the affected pages. A PDF that `pdf create` writes embeds font subsets
still sized for the whole font, about 300 KB for a page of Chinese and English,
and a merge of such files keeps each one, since different subsets are different
streams. After optimizing, the page is about 50 KB; run it last, after stamping
text. A document with a form field to fill keeps its fonts whole, so a value can
still use any character: fill, then `flatten_forms` and `optimize_document` in
one batch to cut them. Identical streams are stored once. `unembedFonts` removes the fonts altogether, and the
text then renders only where the reader has them installed (a CJK font on
another operating system usually is not): tell the user before sending such a
file outside.
