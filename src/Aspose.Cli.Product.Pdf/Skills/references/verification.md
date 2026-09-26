# PDF verification

Every `pdf edit` reopens its output before publishing it and reports
`mutation.verification: "reopened"`. Content, standards and appearance are
verified explicitly.

## Content

```powershell
aspose-cli pdf inspect report.final.pdf --detail metadata permissions forms signatures attachments outline fonts --output json
aspose-cli pdf query pages report.final.pdf --pages 1-5 --mode layout --output json
aspose-cli pdf query search report.final.pdf --pattern DRAFT --output json
```

Choose ranges from the inspected page count and compare extracted text with the
expected content. When window.truncated is true, run window.next for the
rest; a search's 
ext adds --skip past the hits already returned.

After `move_pages`, also verify each affected bookmark, local link and named
destination against the original target content, including destination type,
coordinates, zoom and inherited/null values. Reopening or correct page order does not
prove navigation preservation. A `NAVIGATION_DEGRADED` warning counts the entries
the edit or merge left without their exact target; its absence covers only the
page each entry reaches, not its location or zoom. The pinned
SDK's typed coordinate getters also collapse null and zero; getter equality alone
cannot certify those semantics. This limitation is separate from ordinary text and
page rendering checks.

For redaction, cover all relevant pages, require the expected search results
and inspect the redacted regions in review; check images, annotations,
metadata and attachments as needed. Raw-byte absence of a known phrase is
supplementary evidence only, because PDF text may be encoded or compressed.
Search provides no OCR and no redaction certification.

## Standards

Validate archival output separately and require `valid: true`; see
[PDF/A and conversion](pdf-standards.md).

## Appearance

```powershell
aspose-cli review report.final.pdf --out report.review-1 --output json
```

Open every page image under the review directory, one by one. `review.json`
lists findings and `coverage.complete`; when coverage is incomplete, say so.
Check page order and size, crop and rotation, clipping, images, tables,
headers and footers, fields, annotations, redaction appearance and contrast.
Fix and review again into a new directory, for at most three rounds, then
report any remaining defects.

A font the PDF uses without embedding it is drawn from the fonts installed
here. For fonts delivered beside the document, pass the same `--font-dir` to
`fonts check`, `review`, and `pdf create`, `render`, `convert`, `edit` and
`sign`; it adds to the system fonts. HTML and text sources draw their fonts from
it too, so a CSS `font-family` resolves to the delivered font. PDF/A conversion
must embed every font, so it fails with `PDFA_CONVERSION_FAILED` while one is
missing here. Text operations that name a `font`, and the appearance of a
visible signature, need that font here.

```powershell
aspose-cli fonts check report.pdf --font-dir fonts --output json
aspose-cli pdf convert report.pdf --to pdfa-2b --font-dir fonts --output json
```

A valid blank or image-only page may have no font resources. Font checking then
returns an empty fonts array. Review still renders every selected page and
reports PDF_PAGE_WITHOUT_READABLE_CONTENT for an unscanned page without text;
that finding is a reason to inspect the page, not an internal error.

For supporting attachments, compare each extracted file with the original bytes.
The extraction result reports actual written sizes only after the complete output
set has been published; a later extraction failure must leave no partial set.
