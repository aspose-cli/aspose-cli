# PDF verification

For an existing user PDF, preserve one baseline before the first edit. Apply
related operations in one batch with `--verify`.

The save path reopens the PDF. `--verify` then renders up to three touched
pages at 150 DPI, using page 1 when no touched page is recorded. It does not
compare expected text or inspect every page. Read `verification.ok`, `issues`,
`readBackPages` and `renders`; verification runs after the PDF is published,
so a failed verification can leave a saved output and return partial exit 8.
Complete delivery checks explicitly:

```powershell
aspose-cli pdf inspect report.final.pdf --detail metadata permissions forms signatures attachments outline fonts --output json
aspose-cli pdf query pages report.final.pdf --pages 1-5 --mode layout --output json
aspose-cli pdf render report.final.pdf --pages 1-5 --to png --dpi 150 --out verify.png --output json
aspose-cli pdf query search report.final.pdf --pattern DRAFT --output json
```

Choose ranges from the inspected page count. Rendering defaults to page 1;
use `--all-pages` for every page, or `--pages` for a range, never both.
Multiple-page renders add `.pN` before the output extension; inspect the
returned output paths and open every required image. For CJK text use at
least 150 DPI. Compare extracted text with the expected content and check
query truncation rather than treating successful rendering as semantic proof.

Validate archival output separately and require `valid: true`. For redaction,
cover all relevant pages, require the expected search results and inspect the
rendered regions; check images, annotations, metadata and attachments as
needed. Raw-byte absence of a known phrase is supplementary evidence because
PDF text may be encoded or compressed. Neither search nor `--verify` provides
OCR or a complete redaction certification.
