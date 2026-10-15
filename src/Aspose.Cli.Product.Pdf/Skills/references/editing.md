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
`startPage`, `numberStyle`, `prefix` and `startingValue`, each page's `pageSize`
and `rotation`, and each bookmark's `page` and `bookmark`, the address of 1-based
positions joined by `/` that `delete_bookmarks.bookmarks` and `add_bookmark.parent`
accept: `"2/1"` is the first child of the second top-level bookmark. Titles are
never parsed, so bookmarks with duplicate or empty titles are addressed the same
way; `add_bookmark` refuses a title that contains `/`.

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

Each applied operation's `itemsAffected` counts the objects it changed and its
`targets` name them: pages as `page/<n>`, bookmarks as `bookmark/<address>`, and
`property/<key>`, `attachment/<name>`, `page-label/<start page>`,
`field/<name>` and `security/encryption`. A deleted page or bookmark keeps the
address it had before the deletion, and a deleted bookmark counts with each of
its children. A page counts once however much changed on it, such as every
match a redaction removed there. An operation that changed nothing reports 0
and an `OP_NO_EFFECT` warning. Past 100 targets, an operation reports one range
per unit that covers them, such as `page/1-150`.

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
Each part keeps the page labels its pages had, such as `South-1` to `South-3`.
Each publishes its output with
a `NAVIGATION_DEGRADED` warning that counts the entries that no longer lead where
they did. Re-create them with `add_bookmark` and `add_hyperlink`, or reorder pages
before adding navigation, then follow the destination checks in
[verification](verification.md).

## Redaction

Redaction is destructive: keep the baseline, apply `redact_text` or
`redact_area`, then verify with `pdf search` and `review`. `redact_text`
works on extractable text and performs no OCR; it matches case unless
`caseSensitive` is `false`. A black rectangle or zero search
hits alone is not proof that all sensitive content was removed; a raw-byte
absence check for a known literal is additional evidence only, because PDF text
can be encoded or compressed. An `OP_NO_EFFECT` warning names each
`redact_text` operation that removed nothing: search for the text it targets
with `pdf search`, and when it shows on the page but is not found, widen
the pattern into a regular expression or cover the text with `redact_area`.

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
  `pdf search` hit as it is. A redaction removes the characters whose middle
  lies in its rectangle, `redact_text` those of its matches, and its cover
  stops short of the characters around them, such as the lines above and
  below; a margin that takes in the middle of the full-width `，` or `：`
  beside a value removes it too. On closely set lines a margin also takes in
  the middle of the next line's characters and removes them: do not widen the
  area there. The cover can be smaller than the area, because it stops short
  of every character kept, and a removed character it no longer reaches, as
  in an area across lines or one that only grazes a character, gets a small
  cover of its own. Confirm a redaction by `pdf search` no longer finding the
  text, not by the black box.
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
`embedded` and `subset`). `unembedFonts` removes the fonts altogether, and the
text then renders only where the reader has them installed (a CJK font on
another operating system usually is not): tell the user before sending such a
file outside. `add_attachment` stores the source file name, never
its local path, when `name` is omitted.
