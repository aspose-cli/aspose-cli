# Assemble a review-ready report

Copy this Skill to a fresh writable directory, then change into
examples/assemble-review-report. The bundled [cover.html](cover.html) is a cover
page with the form fields `ReportTitle` and `PreparedFor`, and
[body.md](body.md) holds synthetic quarterly figures.

The workflow creates both PDFs, merges them, stamps page numbers and a DRAFT
watermark with [assemble-ops.json](assemble-ops.json), fills the cover form with
[cover-values.json](cover-values.json), produces a PDF/A-2b candidate, validates
it and reviews it.

```powershell
aspose-cli pdf create cover.pdf --from-html cover.html --output json
aspose-cli pdf create body.pdf --from-text body.md --output json
aspose-cli pdf query forms cover.pdf --output json
aspose-cli pdf merge cover.pdf body.pdf --out assembled.pdf --output json
aspose-cli pdf edit assembled.pdf --ops assemble-ops.json --out review.pdf --output json
aspose-cli pdf edit review.pdf --ops cover-values.json --out review.filled.pdf --output json
aspose-cli pdf convert review.filled.pdf --to pdfa-2b --out review.archive.pdf --output json
aspose-cli pdf validate review.archive.pdf --profile pdfa-2b --output json
aspose-cli pdf query pages review.archive.pdf --output json
aspose-cli review review.archive.pdf --out review.archive.review --output json
```

With your own documents, read the field names from `pdf query forms` and adjust
`cover-values.json`; when the form is XFA or has no fields, skip the fill and
convert `review.pdf` instead.

The archive has two pages, each stamped `Page n of 2` and DRAFT, and the cover
fields hold the filled values. Require `valid: true` from validation, compare
the archive with `review.filled.pdf` because PDF/A conversion can remove content,
open every page image the review lists, and disclose evaluation output when
applicable. Sign, if required, only after these checks pass.
