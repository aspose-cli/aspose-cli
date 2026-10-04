# PDF verification

Follow the delivery checklist and review protocol in `aspose-cli docs verification`.
Every `pdf edit` reopens its output before publishing it; this page adds the
PDF evidence to collect.

## Edit verification

`pdf edit --verify` reads the staged output back against the effect of each
applied operation. Any issue makes `verification.ok` false and the command exit
8; the output is still published so you can inspect it. Each issue message
starts with the ids and names of the operations it concerns, and never repeats
redacted text. `verification.checkedOps` lists, in batch order, the operations
whose every recorded effect was read back; an operation it omits was not fully
checked, even when part of its effect was.

| Operation | Read back | Issue code |
| --- | --- | --- |
| `set_form_field` | the field holds the value set, or is clear after `null` | `PDF_FIELD_VALUE_MISMATCH` |
| `flatten_forms` | no field of a flattened name remains, or no field at all without `fields` | `PDF_FIELD_NOT_FLATTENED` |
| `redact_text` | the pattern no longer matches the text of its pages | `PDF_REDACTED_TEXT_FOUND` |
| `add_bookmark` | its index holds its title and page | `PDF_BOOKMARK_MISMATCH` |
| `add_bookmark`, `delete_bookmarks` | the bookmark count | `PDF_BOOKMARK_MISMATCH` |
| `set_metadata` | each document information entry set | `PDF_METADATA_MISMATCH` |
| `add_attachment`, `remove_attachment` | an attachment of the name holds the embedded length, or none has the name | `PDF_ATTACHMENT_MISMATCH` |
| `insert_blank_page`, `insert_pages_from`, `delete_pages`, `move_pages` | the page count | `PDF_PAGE_COUNT_MISMATCH` |

`PDF_VERIFICATION_INCOMPLETE` reports a page a `redact_text` pattern could not be
checked on within its time budget; search that page with `pdf query search`.
A `redact_text` pattern that matched nothing passes its check, so every edit,
verified or not, names such operations in a `REDACTION_NO_MATCH` warning: the
text it was meant to remove may still be on the page under a different
extracted form ([redaction](editing.md#redaction)). Likewise, a
`REDACTION_TEXT_MOVED` warning names the redactions that moved the rest of a
line under their cover, which no read-back check catches; review those pages.

The batch is checked as a whole: only the last value set for a field, entry or
attachment is checked, the value of a flattened field or a deleted page's fields is not, a
bookmark position is not checked after `delete_bookmarks` renumbers the outline,
and page-scoped redactions and bookmark pages are not checked after a page
operation renumbers the pages. `add_attachment` with a name the document already
uses adds a second attachment of that name. Other operations (stamps,
watermarks, links, `redact_area`, page geometry, page labels,
`remove_metadata`, encryption, `optimize`) have no reliable read-back and are
not checked: render or search the output for them. In evaluation mode the
matches inside the watermark sentence the engine stamps on each page do not
count as remaining redacted text, and a check that has to read a page after the
fourth fails the command with `EVALUATION_LIMIT` and publishes nothing.

## Content

```powershell
aspose-cli pdf inspect report.final.pdf --detail metadata permissions forms signatures attachments outline fonts --output json
aspose-cli pdf query pages report.final.pdf --pages 1-5 --mode layout --output json
aspose-cli pdf query search report.final.pdf --pattern DRAFT --output json
```

Choose page ranges from the inspected page count and compare the text with the
expected content. Search is not OCR. `metadata` reports the values stored in the
file; `aspose-cli schema v2/pdf/pdf-info` describes how its dates are written.

For redaction, search every relevant page, inspect the redacted regions in the
review images, and check images, annotations, metadata and attachments as
needed. Search provides no redaction certification.

After `move_pages`, `delete_pages`, `pdf merge` or `pdf split`, check each affected bookmark,
local link and named destination against the original target, including
destination type, coordinates and zoom. A `NAVIGATION_DEGRADED` warning counts
the entries left without their exact target; its absence covers only the page
each entry reaches, not its location or zoom.

For attachments, compare each extracted file with the original bytes.

## Standards and signatures

Require `valid: true` from `pdf validate` for archival output
([PDF/A and conversion](pdf-standards.md)) and for every signature in
`pdf inspect --detail signatures` ([forms and security](forms-security.md)).

## Appearance

```powershell
aspose-cli review report.final.pdf --out report.review-1 --output json
```

Check page order and size, crop and rotation, clipping, images, tables, headers
and footers, stamps, form field appearances, annotations, redaction appearance
and contrast. PDF review findings worth acting on:

- `PDF_FONTS_NOT_EMBEDDED`: a font the PDF uses without embedding it is drawn
  from the fonts installed here. Pass the delivered fonts with the same
  `--font-dir` to `fonts check`, `review` and `pdf create`, `render`, `convert`,
  `edit` and `sign`; HTML and text sources resolve CSS `font-family` from it too.
  Text operations that name a `font`, and a visible signature, need that font here.
- `PDF_FORM_APPEARANCE_REVIEW_REQUIRED`: open the pages with fields and look for
  stale, clipped or missing values.
- `PDF_EVALUATION_WATERMARK`: a run without a license saved its watermark into
  the page, and a license does not remove it; regenerate the file from its
  original inputs with the license before delivery.
- `PDF_PAGE_WITHOUT_READABLE_CONTENT` and `PDF_PAGE_UTILIZATION_LOW`: a page
  with no or very little text, no images covering a quarter of it and that does
  not look scanned; look at it, it may be unintentionally blank or sparse. A blank or
  image-only page also has no font resources, so `fonts check` returns an empty
  `fonts` array for it.

```powershell
aspose-cli fonts check report.pdf --font-dir fonts --output json
```
