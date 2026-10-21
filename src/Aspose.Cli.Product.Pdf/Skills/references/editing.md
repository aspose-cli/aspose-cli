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

- Pages: `insert_page`, `import_pages` (pages of another PDF),
  `delete_pages`, `move_pages`, `rotate_pages` (absolute clockwise `rotation`; 0 sets a page upright),
  `set_page_size`, `crop_pages` (the visible crop box; `box: "media"` sets the physical
  media box).
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
that later operations name. `pdf inspect` and `pdf read` report targets
under the names the operations take: each page's `page`, each label range's
`startPage`, `numberStyle`, `prefix` and `start`, each page's `pageSize`
and `rotation`, and each bookmark's `page` and `bookmark`, the address of 1-based
positions joined by `/` that `delete_bookmarks.bookmarks` and `add_bookmark.parent`
accept: `"2/1"` is the first child of the second top-level bookmark. Titles are
never parsed, so bookmarks with duplicate or empty titles are addressed the same
way; `add_bookmark` refuses a title that contains `/`.
A PDF created from Markdown has no bookmarks for its headings, and
`pdf split --by bookmarks` refuses a document without bookmarks: add one for the
first page of each part first, such as
`{"op":"add_bookmark","title":"Part 1","page":1}`.

Bookmarks follow the same rule as pages. `add_bookmark` always adds the last child
of its parent, or the last top-level bookmark, so it never renumbers
existing bookmarks. Like `delete_pages.pages`, `delete_bookmarks.bookmarks`
resolves every address against the outline as it stands before the operation,
then removes them all with their children; a missing address removes none of
them, and omitting `bookmarks` removes every bookmark. List every bookmark to
delete in one `delete_bookmarks` operation, with addresses read from one
`pdf inspect --detail outline`; list a parent without its
children. Later operations of the batch see the renumbered outline; put
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
key must not be empty, hold characters beyond Latin-1 (Aspose.PDF writes such a
name garbled; the value may hold any text), name a standard entry, in any case, nor
start with `info:` or `xmp:`; a custom entry reads back under its own key.

Rectangles must lie within the visible page box. Reinspect geometry and review
after changing crop, size or rotation, because later rectangles follow the new
box. A `crop_pages` rectangle is measured on the page as the crop finds it, so a
second crop cuts into the first; `pdf inspect --sample` reports each page's
`mediaBox` and `cropBox` as seen, with the page's rotation, both measured from
the top-left corner of the media box.

`set_page_labels` changes navigation labels, not visible page text, and updates
only the ranges it names. `add_page_numbers` stamps visible text: `{n}` counts
the selected pages from `start`, and `{N}` is the document's page count when the
operation runs, so number pages after inserting or deleting them.

## Navigation after page moves

`move_pages` keeps the bookmarks, local links and named destinations that lead to
or from the moved pages, with their destination type, coordinates and zoom,
except a destination with a coordinate of 0: the SDK reads an omitted coordinate
as 0, so the CLI cannot rebuild it exactly and leaves it without a target.
`delete_pages` leaves navigation to deleted pages without a target. `pdf merge`
points each copied bookmark at its page with Fit zoom (other locations and zooms
are lost) and does not carry named destinations. `pdf split` does the same with
the bookmarks of each part's pages; a bookmark whose parent opens a page of
another part moves up in its place, and links to another part lead nowhere.
Each part keeps the page labels its pages had, such as `South-1` to `South-3`,
the input's document information and every attachment of the input. A merge
keeps the document information of the first input and the page labels of each
input; once any input has labels, an input without them numbers its pages from 1.
It keeps every attachment of each input, an attachment whose name and contents an
earlier input already gave only once; an attachment named like one of an earlier
input but with other contents is left out with a `LOSSY_CONVERSION` warning that
names it: rename it in its input and merge again. `pdf inspect --detail attachments`
lists two attachments of the same name as two entries, each with the `sizeBytes`,
`mimeType` and `description` the file states for it.
Each publishes its output with
a `NAVIGATION_DEGRADED` warning that counts the entries that no longer lead where
they did. Re-create them with `add_bookmark` and `add_hyperlink`, or reorder pages
before adding navigation, then follow the destination checks in
[verification](verification.md).

## Redaction

Redaction is destructive: keep the baseline, apply `redact_text` or
`redact_area`, then verify with `pdf search` and `review`. `redact_text`
works on extractable text and performs no OCR; it matches any case unless
`caseSensitive` is `true`. A black rectangle or zero search
hits alone is not proof that all sensitive content was removed; a raw-byte
absence check for a known literal is additional evidence only, because PDF text
can be encoded or compressed. Every edit leaves out the objects that nothing in
the document references, so a field `flatten_forms` flattened or an attachment
`delete_attachment` deleted no longer holds its value in the file. An `OP_NO_EFFECT` warning names each
`redact_text` operation that removed nothing: search for the text it targets
with `pdf search`, and when it shows on the page but is not found, widen
the pattern into a regular expression or cover the text with `redact_area`.

On the pages it searches, `redact_text` also clears the whole value of each
form field whose value matches and removes each annotation whose text matches.
Before publishing, every edit with `redact_text`, with or without `--verify`,
searches the output again: the text of those pages, their fields and their
annotations, and, for an operation without `pages`, the bookmark titles, the
attachment names and descriptions, and every document information entry, under
any key, and XMP value the document held; what the save itself writes, such as
the producer and the modification date, is the engine's and is not searched.
Where its text is still found, nothing is written, also not with `--in-place`
or `--backup`: the command exits 8 with `verification.ok: false` and a
`REDACTED_TEXT_FOUND` issue whose `location` names each place,
`attachment/all` for an attachment whose name matches. Take the text out there,
for example with `set_properties`, `clear_properties`, `delete_bookmarks` or
`delete_attachment`, and redact again. An operation limited to `pages` does
not search the bookmarks, properties or attachments. A pattern that also
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
names each `redact_text` or `redact_area` operation and the pages on which
this happened. The check follows runs that keep their whole text, so a run
that the redaction cut can move unnoticed: no warning does not prove that
nothing moved. `review` reports `PDF_TEXT_COVERED` on every page where text lies
under such a cover. The moved text is still in the file and searchable, and
nothing in the PDF can move it back: to keep the line visible, redact the
source document and create the PDF again.

When only the PDF exists, the values can still all be removed, at the cost of
the text that follows them on their lines:

- Prefer `redact_text`. Each operation searches the page as the earlier ones
  left it and removes its matches on a page from the last to the first, so
  every match is still where it was found.
- To cover text with `redact_area`, take each rectangle from the `rect` of a
  `pdf search` hit as it is. The cover spans the whole rectangle, and that of
  `redact_text` each whole match, so nothing of a scanned image under the text
  shows. On closely set lines the engine also removes characters of a
  neighbouring line that a cover overlaps; a `REDACTION_NEARBY_TEXT_REMOVED`
  warning names each operation and page with the number of such characters and
  lines, never the text itself.
  Do not widen an area there. Confirm a redaction by
  `pdf search` no longer finding the text, not by the black box.
  Order the areas of one line from right to left, because each removal moves
  the rest of the line and an area taken from the original would then miss
  part of the value it was meant to cover.

Then run `pdf search` for each value to confirm that none is left, and
`review` the output: tell the user which pages `PDF_TEXT_COVERED` names, because
their text is hidden there.

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

1. Render the page with a coordinate grid. `--grid 50` draws a line every 50
   points and labels every 100 points along the top and left edges; the
   result's `grid` states the spacing.
2. Read the rectangle from the labels: `x` and `y` are the distances in points
   from the top-left corner of the visible page (after rotation and crop), and
   `width` and `height` extend right and down, exactly as `redact_area.rect`
   takes them. On a scan, add a margin of a few points on every side.
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
`--grid` takes 10 to 500 points and applies to PNG and JPEG output.

## Other effects

`optimize_document` can lower image quality and unembed fonts; compare file sizes and
look at the affected pages. It always stores identical streams once, so a merge
of files that each embed the same font keeps one copy; image settings do not
shrink a file whose size is its fonts (`pdf inspect --detail fonts` shows
`embedded` and `subset`). `removeUnusedObjects` removes images a page lists but
never draws. `unembedFonts` removes the fonts altogether, and the
text then renders only where the reader has them installed (a CJK font on
another operating system usually is not): tell the user before sending such a
file outside. `add_attachment` stores the source file name, never
its local path, when `name` is omitted.
