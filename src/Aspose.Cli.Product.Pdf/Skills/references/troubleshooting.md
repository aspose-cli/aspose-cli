# PDF troubleshooting

- `PASSWORD_REQUIRED` / `PASSWORD_INVALID`: use `--password-env` or
  `--password-stdin`; never inline a password.
- `FORM_XFA_UNSUPPORTED`: XFA is read-only in this build; do not treat it as AcroForm.
- `PAGE_RANGE_INVALID`: use 1-based ranges such as `1-3,7,9-`.
- `RENDER_TOO_LARGE`: lower DPI or render fewer pages.
- `REMOTE_RESOURCES_BLOCKED`: external HTML assets are denied; copy required
  resources beside the HTML input or use a separately verified local cache.
- `OUTPUT_EXISTS`: choose another path or explicitly pass `--overwrite`.
- `EVAL_MODE`: disclose evaluation limits and the visible watermark.
- PDF/A validation issues: use the reported profile and issues; do not claim
  conformance from conversion success alone.

Use `aspose-cli doctor`, `aspose-cli license status --output json`,
`aspose-cli fonts check report.pdf`, and
`aspose-cli capabilities --output json` for environment diagnosis.
