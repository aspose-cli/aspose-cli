# PDF standards and conversion

Use `pdf convert --to pdfa-1b|pdfa-2b|pdfa-3b` to produce an archival candidate,
then independently run `pdf validate --profile ...` on that output. Treat the
validation result and its issue list as the conformance evidence.

```powershell
aspose-cli pdf convert report.pdf --to pdfa-2b --out report.archive.pdf --output json
aspose-cli pdf validate report.archive.pdf --profile pdfa-2b --output json
```

Conversions to DOCX, XLSX, PPTX, HTML, EPUB or Markdown are structurally lossy:
PDF has fixed pages while those formats have different semantic models. Render
and inspect results before delivery. Image and SVG conversion preserves page
appearance but does not preserve selectable text, forms or annotations.

Table extraction is best effort. Every extracted table carries page and
rectangle context plus a confidence value; do not silently promote uncertain
cells into authoritative numbers.
