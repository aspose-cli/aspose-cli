# PDF troubleshooting

- `PASSWORD_REQUIRED` / `PASSWORD_INVALID`: use `--password-env` or
  `--password-stdin`; never inline a password.
- `FORM_XFA_UNSUPPORTED`: XFA can be inspected but not filled, flattened or
  exported as AcroForm data in this build.
- `PAGE_RANGE_INVALID`: use 1-based ranges such as `1-3,7,9-`.
- `RENDER_TOO_LARGE`: lower DPI or render fewer pages.
- `REMOTE_RESOURCES_BLOCKED`: a local HTML image or stylesheet was missing or
  outside the HTML directory and was left out; review the incomplete output.
- `FEATURE_UNSUPPORTED` naming a network address or script: HTML, Markdown and SVG
  image inputs, and the stylesheets and SVG files they load, may not name any
  network address, including hyperlinks and addresses in text, or contain script,
  event-handler attributes or `javascript:` URLs, because the pinned PDF engine
  requests them, or runs the script, before the CLI can refuse them. Remove the
  address, or save the resource beside the input and reference it by relative path.
  For trusted HTML, `--allow-network-resources` lets the import fetch them, and
  `aspose-cli words convert page.html --to pdf` converts without any request.
- `NETWORK_RESOURCES_REQUESTED`: `--allow-network-resources` let the HTML importer
  request the listed addresses, and the output contains what they returned. Review
  it. Combine the option with `--timeout`: each unanswered request can hold the
  import for up to 100 seconds. Markdown input refuses the option with
  `OPTION_INVALID`.
- `FEATURE_UNSUPPORTED` naming a Markdown reference: the PDF Markdown importer reads
  files with no resource policy, so every image, stylesheet and SVG file it could
  load, and those they reference, must be an existing ordinary file beneath the
  Markdown file's directory. Absolute paths, `file:` URIs, `..` escapes (also
  percent- or entity-encoded), links and junctions, missing files and non-raster
  `data:` URIs are refused, including in code examples. The importer resolves the
  Markdown's relative paths against the working directory: run the command from the
  Markdown file's directory.
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
