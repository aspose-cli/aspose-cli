# Assemble a review-ready report

This workflow merges a cover and report, numbers pages, stamps a visible DRAFT
mark, fills the cover form when fields are present, produces a PDF/A-2b
candidate, and validates it. It is “signed-ready”; cryptographic signing is a
separate final approval step.

```powershell
aspose-cli pdf inspect cover.pdf --detail forms signatures --output json
aspose-cli pdf merge cover.pdf body.pdf --out assembled.pdf --output json
aspose-cli pdf edit assembled.pdf --ops assemble-ops.json --out review.pdf --verify --output json
aspose-cli pdf edit review.pdf --ops cover-form-ops.json --out review.filled.pdf --output json
aspose-cli pdf convert review.filled.pdf --to pdfa-2b --out review.archive.pdf --output json
aspose-cli pdf validate review.archive.pdf --profile pdfa-2b --output json
aspose-cli pdf render review.archive.pdf --pages 1-3 --to png --dpi 150 --out review.png --output json
```

If `pdf inspect` reports no AcroForm, omit the form-fill step rather than
inventing fields. Inspect every rendered page and disclose evaluation
watermarks when applicable.
