# PDF verification

For an existing user PDF, preserve one baseline before the first edit. Apply
related operations in one batch with `--verify`.

Verification reopens the output, reads affected pages, and renders a bounded
set of changed pages. Complete delivery checks explicitly:

```powershell
aspose-cli pdf inspect report.final.pdf --detail metadata permissions forms signatures attachments outline fonts --output json
aspose-cli pdf query pages report.final.pdf --pages 1-5 --mode layout --output json
aspose-cli pdf render report.final.pdf --pages 1-5 --to png --dpi 150 --out verify.png --output json
aspose-cli pdf query search report.final.pdf --pattern DRAFT --output json
```

Actually inspect the rendered images at wide and narrow viewport sizes. For
CJK text use at least 150 DPI. Validate archival output separately. For
redaction, require search-zero and raw-byte absence evidence in addition to
visual inspection.
