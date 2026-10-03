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

3. Put all related changes in one `pdf edit` batch with `--verify`
   ([editing](references/editing.md)):

   ```powershell
   aspose-cli pdf edit input.pdf --ops ops.json --out output.pdf --verify --output json
   ```

   Without a license the engine reads only the first four pages, so an edit or
   check that has to read a later page fails with `EVALUATION_LIMIT` and
   publishes nothing.

4. Convert to PDF/A, then validate the result separately; conversion success
   does not imply conformance ([standards](references/pdf-standards.md)).
5. Sign only the final, verified file with `pdf sign`
   ([forms and security](references/forms-security.md)).
6. Verify before delivery ([verification](references/verification.md)).

## Sources that can reach the network

HTML, Markdown and SVG inputs are refused with `FEATURE_UNSUPPORTED` when they name a
network address or contain script, and Markdown also when it references a file
outside its own directory, because the PDF engine would fetch or read it before any
policy applies. Keep resources beside the input and
reference them by relative path, and run `pdf create --from-text` from the Markdown
file's directory. [Troubleshooting](references/troubleshooting.md) lists each refusal
and its remedy, including `--allow-network-resources` for trusted HTML.

HTML form controls become AcroForm fields. A text `<input>` keeps its `name`;
other controls get generated names, so read them with `pdf query forms` before
filling. The HTML `<title>` becomes the PDF title; a PDF made from HTML or
Markdown has no author or subject until a `set_metadata` edit sets them.

## Preview and licensing

`aspose-cli preview report.pdf --open` shows the `pages` view: pages, thumbnails,
the size of the page in view, zoom and a mark on what a change touched
(`aspose-cli docs preview`). Without a PDF license, output is watermarked and
results carry `EVAL_MODE`; a license installed later does not remove watermarks
already saved into a PDF, so regenerate that file from its original inputs
(`aspose-cli docs licensing`). Evaluation mode also reads only the first 4 pages
of a document. `inspect` of a longer PDF warns `EVAL_INPUT_TRUNCATED` and still
lists every bookmark and attachment and counts every form field; `query forms`
lists every field but leaves out the `page` of one on a later page and names it
in `EVAL_INPUT_TRUNCATED`. Any other command that needs a later page fails with
`EVALUATION_LIMIT` (exit 7) before writing anything.
Limit it with `--pages 1-4`, or tell the user a PDF license is needed.

## References

- [Editing](references/editing.md): operations by task, targets and ordering
  (`aspose-cli schema v2/pdf/ops --operation <op>` for fields)
- [Forms, encryption and signing](references/forms-security.md)
- [PDF/A and conversion](references/pdf-standards.md)
- [Verification](references/verification.md)
- [Troubleshooting](references/troubleshooting.md)

Example: [assemble a review-ready report](examples/assemble-review-report/README.md).
