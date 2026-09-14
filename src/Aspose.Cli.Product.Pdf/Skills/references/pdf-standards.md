# PDF standards and conversion

Use `pdf convert --to pdfa-1b|pdfa-2b|pdfa-3b` to produce an archival candidate,
then independently run `pdf validate --profile ...` on that output. Require
`valid: true`; a completed validation command can return `valid: false`.
The issue list is capped at 100 entries, with `truncated` indicating more.

```powershell
aspose-cli pdf convert report.pdf --to pdfa-2b --out report.archive.pdf --output json
aspose-cli pdf validate report.archive.pdf --profile pdfa-2b --output json
```

PDF/A conversion uses the engine's delete-on-conversion-error policy, so
unsupported content can be removed. Compare the candidate with the original
before delivery. `pdf validate` checks only the selected PDF/A profile; it is
not a signature, redaction, permission or linearization validator.

The exact `--to` ids for `pdf convert` are `docx`, `xlsx`, `pptx`, `html`,
`epub`, `txt`, `md`, `svg`, `xps`, `pdfa-1b`, `pdfa-2b`, `pdfa-3b`, `png`,
`jpeg`, and `tiff`. Optional `--pages` selects physical pages. PNG, JPEG and
SVG exports produce one file per selected page; TIFF produces one multipage
file. Read the result's `outputs` for the actual paths. `pdf render` supports
only `png`, `jpeg`, and `svg`; use `pdf convert --to tiff` for TIFF.

Conversions to document, text and HTML formats are structurally lossy: PDF
has fixed pages while those formats have different semantic models. Render
and inspect results before delivery. Raster outputs do not retain selectable
text or interactive PDF features. Do not assume SVG or XPS preserves editable
document structure, forms, annotations or signatures.

Table extraction is best effort. Extracted tables include page and rectangle
context, but the current `confidence` value is a fixed 0.5, not a calibrated
accuracy score. Verify extracted values against the source.

## Linearization limitation

The pinned Aspose.PDF.Drawing 26.5.0 SDK has a confirmed licensed-save defect:
linearized output can declare a `/L` length different from the actual file
length, and reopening can report `IsLinearized == false`. The `linearize`
operation remains exposed, but a successful edit, render or PDF/A validation
does not prove Fast Web View conformance. Check the saved structure separately
when linearization is a delivery requirement. This SDK limitation is separate
from the CLI documentation and the supported conversion-format list.
