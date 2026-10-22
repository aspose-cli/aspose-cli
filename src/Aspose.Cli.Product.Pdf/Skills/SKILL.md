---
name: aspose-cli-pdf
description: Inspect, assemble, edit, fill, redact, secure, sign, convert, validate and review PDF documents with the local Aspose CLI. Edits are atomic batches that reopen their output before publishing, and review renders every page for visual checks.
---

# Aspose PDF

Use `aspose-cli pdf` for fixed-layout PDF work. The shared rules (session start,
reading windows, operation batches, secrets, review protocol, licensing, errors)
are in `aspose-cli docs overview`; this Skill adds only what is specific to PDF.
`aspose-cli capabilities pdf` lists the commands, formats, operations and codes,
and `aspose-cli schema v2/pdf/<id>` describes each result and the operations.

For an editable word-processing document, use `aspose-cli words convert input.pdf --to docx`
and the Words Skill: it rebuilds flowing paragraphs and warns `LOSSY_CONVERSION`,
since headers and footers are guessed and pages may be added. `pdf convert --to docx`
keeps each line in a positioned frame that is hard to edit, and a Words command
that saves that docx embeds its fonts in full, grows it by megabytes and warns
`FONTS_EMBEDDED`.

Pages are 1-based physical positions. Rectangles are points (72 per inch) with a
top-left origin against the visible, rotated page box.

## Workflow

1. Clarify audience, viewing or print context, compliance target and scope.
2. Inspect structure and security, then read only the pages you need:

   ```powershell
   aspose-cli pdf inspect input.pdf --sample --detail properties --detail permissions --detail forms --detail signatures --detail attachments --detail outline --detail fonts --output-format json
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
6. Verify before delivery ([verification](references/verification.md)), then
   follow the checklist in `aspose-cli docs verification`. `aspose-cli preview open report.pdf`
   shows a person the `pages` view (`aspose-cli docs preview`).

## Creating PDFs

`pdf create` reads its one source as the option names it, whatever the file's
extension: `--from-markdown` lays out Markdown, a `#` line as a heading;
`--from-text` lays out plain text line by line, `#` and all; `--from-html` takes
HTML, whose `<title>` becomes the PDF title and whose form controls become
fields ([forms](references/forms-security.md)). A PDF made from HTML or Markdown
has no author or subject until a `set_properties` edit sets them.

`--from-images` puts each image on its own page, without distortion
([images](references/pdf-standards.md)); `IMAGE_RESOLUTION_LOW` names a page
whose image shows blurred.

## Sources that can reach the network

HTML and SVG inputs reach no network: the CLI blocks each request and lists it in
`REMOTE_RESOURCES_BLOCKED`. Markdown is refused with `FEATURE_UNSUPPORTED` when it
references a network address or a file outside its own directory. Copy
resources into the input's folder or a folder below it, reference them by relative path, and run
`pdf create --from-markdown` from the Markdown file's directory.
[Troubleshooting](references/troubleshooting.md) lists each case and its remedy,
including `--allow-network-resources` for trusted HTML.

## Evaluation mode

The shared rules are in `aspose-cli docs licensing`. Without a PDF license,
Aspose.PDF reads only the first 4 pages of a document. `inspect` of a longer PDF
warns `EVALUATION_INPUT_TRUNCATED` and still lists every bookmark and attachment
and counts every form field; `--detail forms` leaves out the `page` of a field on
a later page. Any other command that needs a later page fails with
`EVALUATION_LIMIT` (exit 7, `details.limit` 4) before writing anything, `render`
of every page included: limit it with `--pages 1-4`, keep the inputs of a `merge`
to 4 pages in total, or tell the user a PDF license is needed. A `convert` of a
longer PDF to a document format fails even with `--pages`. `review` reports a
saved notice as `PDF_EVALUATION_WATERMARK`, and a licensed command whose output
keeps one on its first 4 pages warns `EVALUATION_INPUT_MARKED`.

## Common mistakes

- Reading `--pages` past the end fails with `PAGE_NOT_FOUND`: take the count
  from `pdf inspect`, or use an open range such as `--pages 3-`.
- Expecting headings to be bookmarks: `pdf split --by bookmarks` refuses a
  document without them; add one per part with `add_bookmark` first.
- Converting to a `.pdf` `--out` without `--to`: name the profile, such as
  `--to pdfa-2b`, then require `valid: true` from `pdf validate`.
- Encrypting with `pdf convert`: it refuses `--encrypt-env`. Encrypt with
  `pdf edit`, and encrypt a copy of a PDF/A file, never the archive itself.
- Trusting the black box: confirm a redaction with `pdf search`, and act on
  `OP_NO_EFFECT`, `REDACTION_TEXT_MOVED`, `REDACTION_NEARBY_TEXT_REMOVED` and
  `PDF_TEXT_COVERED`
  ([redaction](references/editing.md)).
- Guessing rectangles: take them from a `pdf search` hit's `rect`, a field's
  `rect` or a `pdf render --grid` image.
- Numbering pages before inserting or deleting them: `add_page_numbers` takes
  `{N}` from the page count when it runs.
- Editing after signing or PDF/A conversion: edits can invalidate signatures
  (`SIGNATURE_INVALIDATED`) and conformance; convert and sign last.
- Passing on `extract --what forms --form-format json` as field data: its names
  differ from the inspect result's; read values with `pdf inspect --detail forms`.

## References

- [Editing](references/editing.md): operations by task, targets and ordering,
  navigation after page moves, redaction and scanned pages
- [Forms, encryption and signing](references/forms-security.md): filling fields,
  HTML form controls, passwords and permissions, signatures
- [PDF/A and conversion](references/pdf-standards.md): profiles, validation,
  images to PDF, other formats and table extraction
- [Verification](references/verification.md): what `--verify` reads back,
  content, navigation and appearance checks, PDF review findings
- [Troubleshooting](references/troubleshooting.md): refused network sources,
  permissions the engine does not enforce

Example: [assemble a review-ready report](examples/assemble-review-report/README.md).
