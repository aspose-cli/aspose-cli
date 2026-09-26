---
name: aspose-cli-pdf
description: Inspect, assemble, edit, fill, redact, secure, sign, convert, validate and review PDF documents with the local Aspose CLI. Edits are atomic batches that reopen their output before publishing, and review renders every page for visual checks.
---

# Aspose PDF

Use `aspose-cli pdf` for fixed-layout PDF work. The shared rules (session start,
reading windows, operation batches, secrets, review protocol, licensing, errors)
are in `aspose-cli docs overview`; this Skill adds only what is specific to PDF.
When the goal is an editable word-processing document, convert explicitly with
`aspose-cli words convert input.pdf --to docx`.

Pages are 1-based physical positions. Rectangles are points (72 per inch) with a
top-left origin against the visible, rotated page box.

## Workflow

1. Clarify audience, viewing or print context, compliance target and scope.
2. Inspect structure and security, then read only the pages you need:

   ```powershell
   aspose-cli pdf inspect input.pdf --preview --detail metadata permissions forms signatures attachments outline fonts --output json
   aspose-cli pdf query pages input.pdf --pages 1-5 --mode layout --output json
   ```

3. Put all related changes in one `pdf edit` batch
   ([editing](references/editing.md)):

   ```powershell
   aspose-cli pdf edit input.pdf --ops ops.json --out output.pdf --output json
   ```

4. Convert to PDF/A, then validate the result separately; conversion success
   does not imply conformance ([standards](references/pdf-standards.md)).
5. Sign only the final, verified file with `pdf sign`
   ([forms and security](references/forms-security.md)).
6. Verify before delivery ([verification](references/verification.md)).

## Sources that can reach the network

`pdf create --from-html`, Markdown `--from-text` and SVG images (`--from-images`,
`add_stamp_image`, `add_watermark_image`) refuse any input that names a network
address, hyperlinks included, or contains script, with `FEATURE_UNSUPPORTED`: the
PDF engine requests network resources before the CLI can refuse them. Compressed
SVG images are refused too. Save required images and CSS beside the input and
reference them by relative path. For trusted HTML only, `--allow-network-resources`
lets the importer fetch what the HTML names; the result lists every address in a
`NETWORK_RESOURCES_REQUESTED` warning, local references still stay beneath the HTML
directory, and `--timeout` bounds the fetches. `aspose-cli words convert page.html --to pdf`
makes no network request at all.

Markdown may reference only ordinary files beneath its own directory, including
the images, stylesheets and SVG files that raw HTML and CSS load; anything else is
refused before the import. The PDF Markdown importer resolves relative paths against
the working directory, so run `pdf create --from-text` from the Markdown file's
directory.

HTML form controls become AcroForm fields. A text `<input>` keeps its `name`;
other controls get generated names, so read them with `pdf query forms` before
filling.

## Preview and licensing

`aspose-cli preview report.pdf --open` shows the `pages` view: pages, thumbnails,
the size of the page in view, zoom and a mark on what a change touched
(`aspose-cli docs preview`). Without a PDF license, output is watermarked and
results carry `EVAL_MODE`; a license installed later does not remove watermarks
already saved into a PDF, so regenerate that file from its original inputs
(`aspose-cli docs licensing`).

## References

- [Editing](references/editing.md): operations by task, targets and ordering
  (`aspose-cli schema v2/pdf/ops --operation <op>` for fields)
- [Forms, encryption and signing](references/forms-security.md)
- [PDF/A and conversion](references/pdf-standards.md)
- [Verification](references/verification.md)
- [Troubleshooting](references/troubleshooting.md)

Example: [assemble a review-ready report](examples/assemble-review-report/README.md).
