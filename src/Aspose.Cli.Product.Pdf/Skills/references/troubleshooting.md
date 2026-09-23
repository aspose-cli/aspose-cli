# PDF troubleshooting

- `PASSWORD_REQUIRED` / `PASSWORD_INVALID`: use `--password-env` or
  `--password-stdin`; never inline a password.
- `FORM_XFA_UNSUPPORTED`: XFA can be inspected but not filled, flattened or
  exported as AcroForm data in this build.
- `PAGE_RANGE_INVALID`: use 1-based ranges such as `1-3,7,9-`.
- `RENDER_TOO_LARGE`: lower DPI or render fewer pages.
- `REMOTE_RESOURCES_BLOCKED`: this warning describes the HTML resource
  callback's decision. The pinned PDF importer can fetch linked images or CSS
  outside that callback, so the warning is not proof of isolation. Use trusted
  HTML inputs only.
- Relative HTML image and stylesheet paths resolve against the original HTML
  directory, including during supervised execution. Keep permitted resources
  beneath that directory; do not widen access to work around an omitted resource.
- `OUTPUT_EXISTS`: choose another path or explicitly pass `--overwrite`.
- `EVAL_MODE`: disclose evaluation limits and the visible watermark. After a
  license change, start the matching preview again to select the new license.
- PDF/A validation issues: inspect `valid`, `issues` and `truncated`; command
  success and conversion success alone do not establish conformance.

Use `aspose-cli doctor`, `aspose-cli license status --output json`,
`aspose-cli fonts check report.pdf`, and
`aspose-cli capabilities --output json` for environment diagnosis.
