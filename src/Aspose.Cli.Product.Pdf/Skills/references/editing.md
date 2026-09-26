# PDF editing

PDF operations address fixed pages, rectangles, annotations, form fields,
metadata and security. They do not use paragraphs, worksheets or formulas.
Page ranges are 1-based physical page positions. Each operation resolves its
range against the document as it exists at that point in the batch; earlier
insert, delete or move operations can change later targets.

Navigation limitation: `move_pages` reorders content but leaves bookmarks, local
links and named destinations that pointed at moved pages without a target, and
`delete_pages` does the same for deleted pages. `pdf merge` points each copied
bookmark at its page with Fit zoom (other locations and zooms are lost) and does
not carry named destinations. Both publish their output and say so: a
`NAVIGATION_DEGRADED` warning (it affects completeness) counts the bookmarks,
links and named destinations that no longer lead where they did. When it
appears, re-create the affected entries with `add_bookmark`/`add_link`, or
reorder pages before adding navigation, then follow the destination checks in
[verification](verification.md).

`set_page_labels` changes navigation labels, not visible page text. Its
`ranges[].startPage` is also 1-based; `startingValue` is the first label value
(default 1). Supported styles are `arabic`, `roman-upper`, `roman-lower`,
`letters-upper`, `letters-lower`, and `none`. Label ranges update the supplied
start pages; they do not reset every existing range. `add_page_numbers` stamps
visible text: `{n}` counts selected pages from `start`, while `{N}` is the
current document's total page count.

`pdf inspect` and `pdf query pages` report these targets under the names the
operations take: each page's `page`, each label range's `startPage`, `style`,
`prefix` and `startingValue`, and each bookmark's `page` and `path`, the
slash-separated title path that `delete_bookmarks.path` and
`add_bookmark.parent` accept.

Edit `rect` values are `{x,y,width,height}` in points (72 points per inch),
with a top-left origin against the currently visible, rotated CropBox (or
MediaBox when uncropped). Search rectangles use the same coordinates.
Coordinates must
be non-negative, dimensions positive, and the rectangle within page bounds.
Reinspect geometry and review after changing crop, size or rotation. The
`rotate_pages` angle sets the rotation in degrees (`90`, `180` or `270`).
`set_page_size` takes the exact names `A3`, `A4`, `Letter` or `Legal` and
scales content only with `scaleContent: true`. `add_link` requires an absolute
`http`, `https` or `mailto` URL. Cropping changes the visible box; use redaction
operations when content must be removed.

Create one ops file and apply it atomically:

```powershell
aspose-cli pdf edit report.pdf --ops report-ops.json --out report.review.pdf --output json
```

Use `--in-place --backup` for an intentional in-place edit. A normal operation
failure publishes no PDF. `--best-effort` permits a partial batch to be saved;
inspect every operation outcome, and expect exit 8 when failures remain.
Use `--dry-run` to apply the batch in memory without publishing files. Use `--if-match <sha256>` or
the ops envelope's `ifMatch` to reject a changed baseline.

Redaction is destructive. Preserve a baseline, apply `redact_text` or
`redact_area`, then verify with `pdf query search` and `review`.
`redact_text` works on extractable text and does not perform OCR. Regex search
and redaction preserve context and use a one-second regex timeout; expressions
that match zero characters are rejected. A raw-byte absence check for a known
literal is additional evidence only: PDF text can be encoded or compressed.
A black rectangle or zero search hits alone is not proof that all sensitive
content was removed.

`optimize` can change image quality and font embedding. Compare file sizes and
visually inspect affected pages.

For `add_attachment`, `name` is the stable embedded-file name; when omitted,
the CLI uses only the source file name and never stores its local path in attachment metadata.

The exact operation contract is available offline:

```powershell
aspose-cli schema v2/pdf/ops
aspose-cli docs pdf/ops
```

Every edit reopens the staged document before it is published.
