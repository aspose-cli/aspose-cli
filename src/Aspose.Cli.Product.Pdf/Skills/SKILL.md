---
name: aspose-cli-pdf
description: Inspect, assemble, edit, fill, redact, secure, sign, convert, validate and review PDF documents with the local Aspose CLI. Edits are atomic batches that reopen their output before publishing, and review renders every page for visual checks.
---

# Aspose PDF

Use `aspose-cli pdf` for fixed-layout PDF work. The shared rules (session start,
reading windows, operation batches, secrets, review protocol, licensing, errors)
are in `aspose-cli docs overview`; this Skill adds only what is specific to PDF.
`aspose-cli capabilities pdf` lists the commands, formats and codes, and
`aspose-cli schema v2/pdf/<id>` describes each result and the operations.
When the goal is an editable word-processing document, convert explicitly with
`aspose-cli words convert input.pdf --to docx`.

Pages are 1-based physical positions. Rectangles are points (72 per inch) with a
top-left origin against the visible, rotated page box.

## Workflow

1. Clarify audience, viewing or print context, compliance target and scope.
2. Inspect structure and security, then read only the pages you need:

   ```powershell
   aspose-cli pdf inspect input.pdf --sample --detail properties permissions forms signatures attachments outline fonts --output-format json
   aspose-cli pdf read input.pdf --pages 1-5 --content layout --output-format json
   ```

3. Put all related changes in one `pdf edit` batch with `--verify`
   ([editing](references/editing.md)):

   ```powershell
   aspose-cli pdf edit input.pdf --ops ops.json --out output.pdf --verify --output-format json
   ```

4. Convert to PDF/A, then validate the file you deliver; a later edit can break
   the conformance the conversion checked ([standards](references/pdf-standards.md)).
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

HTML form controls become AcroForm fields. A single-line text `<input>` keeps
its `name`; other controls get generated names and lose their `value`s, and
inputs of type `email`, `tel`, `url`, `time`, `datetime-local`, `month`, `week`,
`color`, `range`, `file`, `image` or `hidden` are dropped;
`LOSSY_CONVERSION` lists both. Use `type="text"` for an input that must stay a
field, place the picture of an image input with `add_stamp_image`, read the fields with `pdf inspect --detail forms` and match each field's `rect` to
its label before filling. The HTML `<title>` becomes the PDF title; a PDF made from HTML or
Markdown has no author or subject until a `set_properties` edit sets them.

`pdf create --from-images` puts each image on its own page of `--page-size`,
turned to landscape for an image wider than it is tall. The image keeps its
aspect ratio: it fills the space inside `--margins` in one dimension and is
centred in the other. A photo's EXIF orientation counts, and an SVG without
both a width and a height takes the proportions of its viewBox. A raster image
enlarged to fewer than 96 pixels per inch warns `IMAGE_RESOLUTION_LOW` with the
page and the pixels it needs: it shows blurred.

## Preview and licensing

`aspose-cli preview open report.pdf` shows the `pages` view: pages, thumbnails,
the size of the page in view, zoom and a mark on what a change touched
(`aspose-cli docs preview`). Without a PDF license, output is watermarked and
results carry `EVALUATION_MODE`; `review` reports such pages as
`PDF_EVALUATION_WATERMARK` ([verification](references/verification.md)), and a
licensed command whose output keeps such a notice on one of its first 4 pages
warns `EVALUATION_INPUT_MARKED`.
Evaluation mode also reads only the first 4 pages of a document. `inspect` of a
longer PDF warns `EVALUATION_INPUT_TRUNCATED` and still lists every bookmark and
attachment and counts every form field; `--detail forms` lists the fields but
leaves out the `page` of one on a later page. Any other command that needs a later page fails with
`EVALUATION_LIMIT` (exit 7, `details.limit` 4) before writing anything,
`render` of every page included. Limit it with `--pages 1-4`, keep the inputs of
a `merge` to 4 pages in total, or tell the user a PDF license is needed; a
`convert` of a longer PDF to a document format fails even with `--pages`,
since removing the other pages reads them.

## References

- [Editing](references/editing.md): operations by task, targets and ordering
  (`aspose-cli schema v2/pdf/ops --operation <op>` for fields)
- [Forms, encryption and signing](references/forms-security.md)
- [PDF/A and conversion](references/pdf-standards.md)
- [Verification](references/verification.md)
- [Troubleshooting](references/troubleshooting.md)

Example: [assemble a review-ready report](examples/assemble-review-report/README.md).
