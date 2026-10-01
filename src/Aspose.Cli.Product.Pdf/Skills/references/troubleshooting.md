# PDF troubleshooting

The error envelope, exit codes and shared codes are in
`aspose-cli docs troubleshooting`. PDF-specific codes:

- `FORM_XFA_UNSUPPORTED`: XFA can be inspected but not filled, flattened or
  exported as AcroForm data.
- `PAGE_RANGE_INVALID`: use 1-based ranges such as `1-3,7,9-`.
- `BOOKMARK_NOT_FOUND`: `details.requested` is the bookmark index and
  `details.availableCount` counts the bookmarks at the level where it ran out;
  the hint names that level and its valid range.
- `ATTACHMENT_NOT_FOUND`, `FIELD_NOT_FOUND`: `details.available` lists
  attachment names or full field names.
- `PDFA_CONVERSION_FAILED`: `error.details.problems` lists what the conversion
  could not fix; a font the document uses that is missing here also fails it.
- `FEATURE_UNSUPPORTED` naming a network address or script: HTML, Markdown and SVG
  image inputs, and the stylesheets and SVG files they load, may not name any
  network address, including hyperlinks and addresses in text, or contain script,
  event-handler attributes or `javascript:` URLs, because the PDF engine requests
  them, or runs the script, before the CLI can refuse them. Remove the address, or
  save the resource beside the input and reference it by relative path. For
  trusted HTML, `--allow-network-resources` lets the import fetch them, and
  `aspose-cli words convert page.html --to pdf` converts without any request.
- `NETWORK_RESOURCES_REQUESTED`: `--allow-network-resources` let the HTML importer
  request the listed addresses, and the output contains what they returned; review
  it. Combine the option with `--timeout`: each unanswered request can hold the
  import for up to 100 seconds. Markdown input refuses the option with
  `OPTION_INVALID`.
- `FEATURE_UNSUPPORTED` naming a Markdown reference: every image, stylesheet and
  SVG file the Markdown could load, and those they reference, must be an existing
  ordinary file beneath the Markdown file's directory. Absolute paths, `file:`
  URIs, `..` escapes (also percent- or entity-encoded), links and junctions,
  missing files and non-raster `data:` URIs are refused, including in code
  examples. Run the command from the Markdown file's directory.
- `REMOTE_RESOURCES_BLOCKED`: a local HTML image or stylesheet was missing or
  outside the HTML directory and was left out; review the incomplete output.
  Relative paths resolve against the HTML file's directory; keep resources
  beneath it rather than widening access.
- `OUTPUT_EXISTS` from `split` or `extract --out-dir`: one existing file refuses
  the whole run and nothing is published; `--overwrite` replaces only the files
  the command writes.
