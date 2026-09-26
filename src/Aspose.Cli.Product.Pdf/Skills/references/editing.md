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
and `path`, the slash-separated title path that `delete_bookmarks.path` and
`add_bookmark.parent` accept.

Rectangles must lie within the visible page box. Reinspect geometry and review
after changing crop, size or rotation, because later rectangles follow the new
box.

`set_page_labels` changes navigation labels, not visible page text, and updates
only the ranges it names. `add_page_numbers` stamps visible text: `{n}` counts
the selected pages from `start`, and `{N}` is the document's page count when the
operation runs, so number pages after inserting or deleting them.

## Navigation after page moves

`move_pages` reorders content but leaves bookmarks, local links and named
destinations that pointed at moved pages without a target, and `delete_pages`
does the same for deleted pages. `pdf merge` points each copied bookmark at its
page with Fit zoom (other locations and zooms are lost) and does not carry named
destinations. Both publish their output with a `NAVIGATION_DEGRADED` warning
that counts the entries that no longer lead where they did. Re-create them with
`add_bookmark` and `add_link`, or reorder pages before adding navigation, then
follow the destination checks in [verification](verification.md).

## Redaction

Redaction is destructive: keep the baseline, apply `redact_text` or
`redact_area`, then verify with `pdf query search` and `review`. `redact_text`
works on extractable text and performs no OCR. A black rectangle or zero search
hits alone is not proof that all sensitive content was removed; a raw-byte
absence check for a known literal is additional evidence only, because PDF text
can be encoded or compressed.

## Other effects

`optimize` can lower image quality and unembed fonts; compare file sizes and
look at the affected pages. `add_attachment` stores the source file name, never
its local path, when `name` is omitted.
