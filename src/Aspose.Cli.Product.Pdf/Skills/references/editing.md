# PDF editing

`pdf edit` applies one ops batch; atomicity, `--best-effort`, `--dry-run`,
`--if-match`, output and backups work as `aspose-cli docs editing` describes.
PDF operations address fixed pages, rectangles, annotations, form fields,
metadata and security, never paragraphs, worksheets or formulas. Print one
operation's fields, types, defaults and allowed values with
`aspose-cli schema v2/pdf/ops --operation <op>`.

```powershell
aspose-cli schema v2/pdf/ops --operation add_page_numbers --output json
aspose-cli pdf edit report.pdf --ops report-ops.json --out report.review.pdf --output json
```

## Operations by task

- Pages: `insert_blank_page`, `insert_pages_from` (pages of another PDF),
  `delete_pages`, `move_pages`, `rotate_pages` (absolute angle),
  `set_page_size`, `crop_pages` (changes the visible box only).
- Stamps: `add_header_text`, `add_footer_text`, `add_page_numbers`,
  `add_watermark_text`, `add_watermark_image`, `add_stamp_image`.
- Navigation: `add_bookmark`, `delete_bookmarks`, `add_link`,
  `set_page_labels`.
- Forms: `set_form_field`, `flatten_forms`
  ([forms and security](forms-security.md)).
- Security: `encrypt`, `decrypt`.
- Redaction: `redact_text`, `redact_area`.
- Document: `set_metadata`, `remove_metadata`, `add_attachment`,
  `remove_attachment`, `optimize`.

## Targets and ordering

Each operation resolves its pages against the document as the earlier
operations of the batch left it: an insert, delete or move changes the pages
that later operations name. `pdf inspect` and `pdf query pages` report targets
under the names the operations take: each page's `page`, each label range's
`startPage`, `style`, `prefix` and `startingValue`, and each bookmark's `page`
and `index`, the 1-based positions joined by `/` that `delete_bookmarks.indexes`
and `add_bookmark.parent` accept: `"2/1"` is the first child of the second
top-level bookmark. Titles are never parsed, so bookmarks with duplicate titles,
titles containing `/` or empty titles are addressed the same way.

Bookmarks follow the same rule as pages. `add_bookmark` always adds the last child
of its parent, or the last top-level bookmark, so it never renumbers
existing bookmarks. Like `delete_pages.pages`, `delete_bookmarks.indexes`
resolves every index against the outline as it stands before the operation,
then removes them all with their children; a missing index removes none of
them. List every bookmark to delete in one `delete_bookmarks` operation, with
indexes read from one `pdf inspect --detail outline`; list a parent without its
children. Later operations of the batch see the renumbered outline; put
`add_bookmark` operations that name a parent before the deletion, or reinspect
and edit in a second batch.

Rectangles must lie within the visible page box. Reinspect geometry and review
after changing crop, size or rotation, because later rectangles follow the new
box.

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
are lost) and does not carry named destinations. Each publishes its output with
a `NAVIGATION_DEGRADED` warning that counts the entries that no longer lead where
they did. Re-create them with `add_bookmark` and `add_link`, or reorder pages
before adding navigation, then follow the destination checks in
[verification](verification.md).

## Redaction

Redaction is destructive: keep the baseline, apply `redact_text` or
`redact_area`, then verify with `pdf query search` and `review`. `redact_text`
works on extractable text and performs no OCR. A black rectangle or zero search
hits alone is not proof that all sensitive content was removed; a raw-byte
absence check for a known literal is additional evidence only, because PDF text
can be encoded or compressed.

### Scanned pages

A page without a text layer (`pdf query pages` reports
`SCANNED_PAGES_SUSPECTED`) can only be redacted with `redact_area`. Take its
rectangle from a gridded render, never by eye:

1. Render the page with a coordinate grid. `--grid 50` draws a line every 50
   points and labels every 100 points along the top and left edges; the
   result's `grid` states the spacing.
2. Read the rectangle from the labels: `x` and `y` are the distances in points
   from the top-left corner of the visible page (after rotation and crop), and
   `width` and `height` extend right and down, exactly as `redact_area.rect`
   takes them. Add a margin of a few points on every side.
3. Apply `redact_area` to a copy, never to the only original.
4. Render the result again with `--grid` at a higher `--dpi`, and check that
   nothing sensitive shows and that nothing that must stay is covered. Repeat
   from step 2 with a corrected rectangle when either check fails, then
   `review` the output.

```powershell
aspose-cli pdf query pages scan.pdf --output json
aspose-cli pdf render scan.pdf --pages 2 --grid 50 --dpi 150 --out scan.grid.png --output json
aspose-cli pdf edit scan.pdf --ops redact-ops.json --out scan.redacted.pdf --output json
aspose-cli pdf render scan.redacted.pdf --pages 2 --grid 50 --dpi 200 --out scan.redacted.grid.png --output json
```

The grid exists only in the rendered image; the PDF is never changed.
`--grid` takes 10 to 500 points and applies to PNG and JPEG output.

## Other effects

`optimize` can lower image quality and unembed fonts; compare file sizes and
look at the affected pages. It always stores identical streams once, so a merge
of files that each embed the same font keeps one copy; image settings do not
shrink a file whose size is its fonts (`pdf inspect --detail fonts` shows
`embedded` and `subset`). `unembedFonts` removes the fonts altogether, and the
text then renders only where the reader has them installed (a CJK font on
another operating system usually is not): tell the user before sending such a
file outside. `add_attachment` stores the source file name, never
its local path, when `name` is omitted.
