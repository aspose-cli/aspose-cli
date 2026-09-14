# PDF editing

PDF operations address fixed pages, rectangles, annotations, form fields,
metadata and security. They do not use paragraphs, worksheets or formulas.
Page ranges are 1-based physical page positions. Each operation resolves its
range against the document as it exists at that point in the batch; earlier
insert, delete or move operations can change later targets.

`set_page_labels` changes navigation labels, not visible page text. Its
`ranges[].startPage` is also 1-based; `startingValue` is the first label value
(default 1). Supported styles are `arabic`, `roman-upper`, `roman-lower`,
`letters-upper`, `letters-lower`, and `none`. Label ranges update the supplied
start pages; they do not reset every existing range. `add_page_numbers` stamps
visible text: `{n}` counts selected pages from `start`, while `{N}` is the
current document's total page count.

Edit `rect` values are `{x,y,width,height}` in points (72 points per inch),
with a top-left origin against the current page dimensions. Coordinates must
be non-negative, dimensions positive, and the rectangle within page bounds.
Reinspect geometry and render after changing crop, size or rotation. The
`rotate_pages` angle sets the rotation; `set_page_size` scales content only
with `scaleContent: true`. Cropping changes the visible box; use redaction
operations when content must be removed.

Create one ops file and apply it atomically:

```powershell
aspose-cli pdf edit report.pdf --ops report-ops.json --out report.review.pdf --verify --output json
```

Use `--in-place --backup` for an intentional in-place edit. A normal operation
failure publishes no PDF. `--best-effort` permits a partial batch to be saved;
inspect every operation outcome, and expect exit 8 when failures remain.
Use `--dry-run` to apply the batch in memory without publishing files;
`--dry-run` cannot be combined with `--verify`. Use `--if-match <sha256>` or
the ops envelope's `ifMatch` to reject a changed baseline.

Redaction is destructive. Preserve a baseline, apply `redact_text` or
`redact_area`, then verify with `pdf query search` and page renders.
`redact_text` works on extractable text and does not perform OCR. A raw-byte
absence check for a known literal is additional evidence only: PDF text can
be encoded or compressed. A black rectangle or zero search hits alone is
not proof that all sensitive content was removed.

`optimize` can change image quality and font embedding. Compare file sizes and
visually inspect affected pages. `linearize` requests Fast Web View delivery
structure; the pinned SDK has a known save defect, so do not claim successful
linearization from command success. See `pdf-standards.md`.

For `add_attachment`, `name` is the stable embedded-file name; when omitted,
the CLI uses only the source file name and never stores its local path in attachment metadata.

The exact operation contract is available offline:

```powershell
aspose-cli schema v2/pdf/ops
aspose-cli docs pdf/ops
```
