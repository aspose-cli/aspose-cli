# PDF troubleshooting

- `PASSWORD_REQUIRED` / `PASSWORD_INVALID`: use `--password-env` or
  `--password-stdin`; never inline a password.
- `FORM_XFA_UNSUPPORTED`: XFA can be inspected but not filled, flattened or
  exported as AcroForm data in this build.
- `PAGE_RANGE_INVALID`: use 1-based ranges such as `1-3,7,9-`.
- `RENDER_TOO_LARGE`: lower DPI or render fewer pages.
- `REMOTE_RESOURCES_BLOCKED`: a local HTML image or stylesheet was missing or
  outside the HTML directory and was left out; review the incomplete output.
- `FEATURE_UNSUPPORTED` naming a network address: HTML and Markdown inputs may
  not name any network address, including hyperlinks and addresses in text,
  because the pinned PDF importer requests them before the CLI can refuse them.
  Remove the address, or save the resource beside the input and reference it by
  relative path.
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
