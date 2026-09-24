---
name: aspose-cli-pdf
description: Inspect, assemble, edit, fill, redact, secure, sign, convert, validate and review PDF documents with the local Aspose CLI. Edits are atomic batches that reopen their output before publishing, and review renders every page for visual checks.
---

# Aspose PDF

Use `aspose-cli pdf` for fixed-layout PDF work. When the goal is an editable
word-processing document, convert explicitly with
`aspose-cli words convert input.pdf --to docx`. Pages are 1-based physical
positions; rectangles are points with a top-left origin.

## Workflow

1. Clarify audience, viewing or print context, compliance target and scope.
2. Inspect structure and security, then read only the pages you need:

   ```powershell
   aspose-cli pdf inspect input.pdf --preview --detail metadata permissions forms signatures attachments outline fonts --output json
   aspose-cli pdf query pages input.pdf --pages 1-5 --mode layout --output json
   ```

3. Put all related changes in one atomic `pdf edit` batch. Write to `--out`,
   or use `--in-place --backup` when replacing the user's file is intended.
   Every edit reopens its output before publishing it.

   ```powershell
   aspose-cli pdf edit input.pdf --ops ops.json --out output.pdf --output json
   ```

4. Convert to PDF/A, then validate the result separately; conversion success
   does not imply conformance.
5. Sign only the final, verified artifact with `pdf sign`, passing the
   certificate password through `--certificate-password-env`.

`pdf create --from-html` and Markdown `--from-text` refuse any input that names a
network address, hyperlinks included, with `FEATURE_UNSUPPORTED`: the PDF importer
requests network resources before the CLI can refuse them. Save required images
and CSS beside the input and reference them by relative path. The Markdown
importer reads local images without the CLI's local-resource guard, so use
trusted Markdown only.

## Verify before delivery

1. **Content:** read changed pages back with `pdf query pages`, and use
   `pdf query search` for expected, removed or placeholder text. For
   redaction, search every relevant page; search is not OCR.
2. **Standards and security:** require `valid: true` from `pdf validate`, and
   `valid: true` for signatures from `pdf inspect --detail signatures`.
3. **Visual:** run `aspose-cli review output.pdf --out <new-dir> --output json`,
   then open every page image it lists, one by one. Use `review.json` findings
   to focus, not as a substitute for looking.
4. Fix, then review again into a fresh directory. Stop after three rounds and
   report what remains. Never claim a visual pass for pages you did not open;
   state the exact page coverage.

Details: [verification](references/verification.md).

## Licensing

Without a PDF license, output is watermarked and results carry `EVAL_MODE`;
disclose that with every delivered file. Install a license with
`aspose-cli license install Aspose.PDF.lic --product pdf` and check the `pdf`
entry of `aspose-cli license status --output json`.

Passwords come from `--password-env`, `--password-stdin` or operation
`*PasswordEnv` fields; never put secrets in ops JSON.

## References

- [Editing and the ops vocabulary](references/editing.md) (`aspose-cli schema v2/pdf/ops`)
- [Forms, encryption and signing](references/forms-security.md)
- [PDF/A and conversion](references/pdf-standards.md)
- [Verification](references/verification.md)
- [Live preview for a human](references/preview.md)
- [Troubleshooting](references/troubleshooting.md)

Example: [assemble a review-ready report](examples/assemble-review-report/README.md).
