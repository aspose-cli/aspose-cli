# PDF verification

Follow the delivery checklist and review protocol in `aspose-cli docs verification`.
Every `pdf edit` reopens its output before publishing it; this page adds the
PDF evidence to collect. `aspose-cli capabilities pdf` lists the verification and
review codes.

## Edit verification

`pdf edit --verify` reads the staged output back against the effect of each
applied operation. When it finds an issue, the output is still published so you
can inspect it. Each issue message starts with the ids and names of the
operations it concerns, and never repeats redacted text; its `location` is the
address the operation's `targets` use. `verification.checkedOps` lists, in batch
order, the operations whose every recorded effect was read back; an operation it
omits was not fully checked, even when part of its effect was.

| Operation | Read back |
| --- | --- |
| `set_form_field` | the field holds the value set, or is clear after `null` |
| `flatten_forms` | no field of a flattened name remains, or no field at all without `fields` |
| `redact_text` | its text no longer matches the text of its pages |
| `add_bookmark` | its address holds its title and page, and the bookmark count |
| `delete_bookmarks` | the bookmark count |
| `set_properties` | each document information entry set |
| `add_attachment`, `delete_attachment` | an attachment of the name holds the embedded length, or none has the name |
| `insert_page`, `import_pages`, `delete_pages`, `move_pages` | the page count |

A page whose `redact_text` match could not be checked within its time budget is
reported as not checked; search that page with `pdf search`. A `redact_text`
that matched nothing passes its check, and no check catches text a redaction
moved under its cover; act on `OP_NO_EFFECT`, `REDACTION_TEXT_MOVED` and the
review's covered-text finding ([redaction](editing.md#redaction)).

The batch is checked as a whole: only the last value set for a field, entry or
attachment is checked, the value of a flattened field or a deleted page's fields is not, a
bookmark position is not checked after `delete_bookmarks` renumbers the outline,
and page-scoped redactions and bookmark pages are not checked after a page
operation renumbers the pages. `add_attachment` with a name the document already
uses adds a second attachment of that name. Other operations (stamps,
watermarks, links, `redact_area`, page geometry, page labels,
`clear_properties`, encryption, `optimize_document`) are not checked: render,
inspect or search the output for them. In evaluation mode the matches inside the
watermark sentence the engine stamps on each page do not count as remaining
redacted text.

## Content

```powershell
aspose-cli pdf inspect report.final.pdf --detail properties --detail permissions --detail forms --detail signatures --detail attachments --detail outline --detail links --detail fonts --output-format json
aspose-cli pdf read report.final.pdf --pages 1-5 --content layout --output-format json
aspose-cli pdf search report.final.pdf --pattern DRAFT --output-format json
```

Choose page ranges from the inspected page count and compare the text with the
expected content. Search is not OCR. `aspose-cli schema v2/pdf/inspect-result`
describes every inspected field, including how property dates are written.

For redaction, search every relevant page, inspect the redacted regions in the
review images, and check images, annotations, properties and attachments as
needed. Search provides no redaction certification.

After `move_pages`, `delete_pages`, `pdf merge` or `pdf split`, check each affected bookmark,
local link and named destination against the original target, including
destination type, coordinates and zoom. The absence of a `NAVIGATION_DEGRADED`
warning covers only the page each entry reaches, not its location or zoom.

For attachments, compare each extracted file with the original bytes. `extract --what attachments`
writes them as `attachment-0001` and on, in name order, never under the name the PDF gives;
each output's `name` is that name, an untrusted value, and a file whose extension is not a
document, image or text one is written as `.bin`.

## Standards and signatures

Require `valid: true` from `pdf validate` for archival output
([PDF/A and conversion](pdf-standards.md)), and `intact: true` for every
signature in `pdf inspect --detail signatures`, which says nothing changed after
signing, not that the signer is trusted ([forms and security](forms-security.md)).

## Appearance

```powershell
aspose-cli review report.final.pdf --out report.review-1 --output-format json
aspose-cli fonts check report.pdf --font-dir fonts --output-format json
```

To show a person the PDF, `aspose-cli preview open report.pdf` opens the `pages`
view: pages, thumbnails, the size of the page in view, zoom and a mark on what a
change touched (`aspose-cli docs preview`).

Check page order and size, crop and rotation, clipping, images, tables, headers
and footers, stamps, form field appearances, annotations, redaction appearance
and contrast. The engine drops some thin glyph strokes, such as the underscores
of a signature line, when it renders below about 300 DPI. Review images are
150 DPI, scaled down from a 300 DPI render unless the page is too large for
that, so such a line shows as a light grey line; `pdf render` draws exactly the
`--dpi` it is given, so render with `--dpi 300` to look at thin lines or small
print closely.

What the PDF review findings ask of you:

A font the PDF uses without embedding it is drawn from the fonts installed here
or in `--font-dir` (`aspose-cli docs verification`); HTML and text sources of
`pdf create` also resolve CSS `font-family` from `--font-dir`. Text operations that name a
`font`, and a visible signature, need that font here. A blank or image-only page
has no font resources, so `fonts check` returns an empty `fonts` array for it.

For a PDF with form fields, open the pages with fields and look for stale,
clipped or missing values.

A run without a license saves its watermark into the page; regenerate the file
as `aspose-cli docs licensing` says. The check matches the notice from "Created with Aspose.<product>"
to its "Copyright … Aspose Pty Ltd" in the page text, in the order it is drawn,
so a notice drawn over a title is found too; a PDF that `cells`, `words` or
`slides` saved is flagged as well, and the finding names the product whose
license is needed.

Text under an opaque box painted over it, such as the cover a redaction leaves,
means the file holds text the page does not show; after a redaction it is the
text the engine moved under the cover ([redaction](editing.md#redaction)).

A page with no or very little text, no image covering a quarter of it and no
look of a scan may be unintentionally blank or sparse; look at it. A page with no
extractable text and an image that fills its width or height is reported for
information ([scanned pages](editing.md#scanned-pages)).
