# PDF editing

PDF operations address fixed pages, rectangles, annotations, form fields,
metadata and security. They do not use paragraphs, worksheets or formulas.
Page ranges are 1-based and resolve against the original document before the
batch starts.

Create one ops file and apply it atomically:

```powershell
aspose-cli pdf edit report.pdf --ops report-ops.json --out report.review.pdf --verify --output json
```

Use `--in-place --backup` for an intentional in-place edit. A normal failure
writes no output. `--best-effort` commits successful operations and exits
8; reserve it for workflows that explicitly accept a partial result.

Redaction is destructive. Preserve a baseline, apply `redact_text` or
`redact_area`, then verify with `pdf query search`, page renders, and—when the source
phrase is known—a raw-byte absence check. A black rectangle alone is not proof
of removal.

`optimize` can change image quality and font embedding. Compare file sizes and
visually inspect representative pages. `linearize` changes delivery structure,
not visible content.

The exact operation contract is available offline:

For `add_attachment`, `name` is the stable embedded-file name; when omitted,
the CLI uses only the source file name and never stores its local path in attachment metadata.

```powershell
aspose-cli schema v2/pdf/ops
aspose-cli docs pdf/ops
```
