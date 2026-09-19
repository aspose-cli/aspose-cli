# Assemble a review-ready report

Supply `cover.pdf` and `body.pdf`; they are input documents, not bundled files.
This workflow merges them, numbers pages, stamps a visible DRAFT mark, optionally
fills the cover form, produces a PDF/A-2b candidate, and validates it. Signing,
if required, follows successful content and standards checks.

The optional form-fill step uses the bundled `cover-values.json` operation
batch. Inspect the form first and adjust its exact field names and values.
This example expects `ReportTitle` and `PreparedFor`; if either is absent or
the form is XFA, omit the fill step and use `review.pdf` instead of
`review.filled.pdf` in the conversion command.

```powershell
aspose-cli pdf inspect cover.pdf --detail forms permissions signatures --output json
aspose-cli pdf merge cover.pdf body.pdf --out assembled.pdf --output json
aspose-cli pdf edit assembled.pdf --ops assemble-ops.json --out review.pdf --output json
aspose-cli pdf query forms review.pdf --output json
aspose-cli pdf edit review.pdf --ops cover-values.json --out review.filled.pdf --output json
aspose-cli pdf convert review.filled.pdf --to pdfa-2b --out review.archive.pdf --output json
aspose-cli pdf validate review.archive.pdf --profile pdfa-2b --output json
aspose-cli review review.archive.pdf --out review.archive.review --output json
```

Require `valid: true` from validation. Compare the archive candidate with the
review PDF because PDF/A conversion may remove unsupported content. Open every
page image the review lists and disclose evaluation watermarks when
applicable. Visible page numbers use `{n}` and `{N}`; they do not change PDF
navigation labels.
