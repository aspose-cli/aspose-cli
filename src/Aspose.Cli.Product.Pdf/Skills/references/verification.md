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

Choose ranges from the inspected page count, check query truncation, and
compare extracted text with the expected content.

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
