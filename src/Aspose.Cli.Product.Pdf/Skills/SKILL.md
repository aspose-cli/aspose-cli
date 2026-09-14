---
name: aspose-cli-pdf
description: High-fidelity PDF automation with bounded inspection, page assembly, forms, redaction, security, PDF/A validation, rendering, and verification.
---

# Aspose PDF

Use `aspose-cli pdf` for PDF-native fixed-layout workflows. Use explicit
`aspose-cli words convert input.pdf --to docx` only when the intended result
is an editable word-processing document.

## Safe workflow

1. Inspect structure and security first:
   `aspose-cli pdf inspect input.pdf --preview --detail metadata permissions forms signatures attachments outline fonts --output json`.
2. Read only required pages:
   `aspose-cli pdf query pages input.pdf --pages 1-5 --mode layout --max-chars 20000 --output json`.
3. Preserve one baseline before editing an existing user PDF.
4. Apply related changes in one `pdf edit` batch. Use `--verify`; use
   `--best-effort` only when partial output is explicitly acceptable.
5. Read back affected pages, search for expected or removed text, render at
   least the changed pages, and inspect the images.
6. Validate the requested PDF/A profile separately; conversion success does
   not imply conformance.
7. Disclose `EVAL_MODE`, evaluation watermarks, lossy conversion, suspected
   scanned pages, signature invalidation, remote-resource blocking and
   best-effort table extraction.

Passwords and owner secrets must come from `--password-env`,
`--password-stdin`, or operation `*PasswordEnv` fields. Never put secrets in
ops JSON, logs, result envelopes, or preview session state.

Sign only the final verified artifact with `pdf sign`; pass the PKCS#12
password through `--certificate-password-env`, then confirm it with
`pdf inspect --detail signatures`. See `references/forms-security.md`.

## Preview and licensing

`aspose-cli preview input.pdf --open --output json` resolves valid PDF content
to the PDF product and its default `pages` view. It provides page navigation, zoom, live edit activity and
last-good recovery without Words or spreadsheet controls. Agents must use
static `pdf query pages`, `pdf render`, `pdf query search`, and `pdf validate` results for
delivery evidence. See `references/preview.md`.

Install a PDF-only license with
`aspose-cli license install Aspose.PDF.lic --product pdf`, set
`ASPOSE_PDF_LICENSE_PATH`, or use a shared Aspose.Total license through
`ASPOSE_LICENSE_PATH`. Inspect the `pdf` entry in the `products` array from
`aspose-cli license status --output json`; sibling product status is not PDF
status. Re-run the preview command after a license change; see
`references/preview.md` for session reuse and evaluation-output limits.

See `references/editing.md`, `references/forms-security.md`,
`references/pdf-standards.md`, `references/verification.md`, and
`references/troubleshooting.md`.

Worked example: `examples/assemble-review-report`.

## Visual delivery gate

1. Understand the audience, document purpose, viewing or print context, compliance target, and requested scope before assembling or editing the PDF.
2. For an existing user PDF, preserve unrelated pages, geometry, metadata, forms, attachments, security, and navigation; change only the requested scope. Warn before any requested action that invalidates signatures.
3. Run `aspose-cli review <artifact> --out <fresh-review-dir> --output json` for every PDF deliverable, using a fresh output directory for each round. Use its findings to target inspection, never as a substitute for opening pages or for separate PDF/A, signature, permission, or redaction validation.
4. Actually open every visual artifact produced by review, one by one, then every page for a new or assembled PDF and every changed or affected page for a scoped edit. Check page order and size, crop and rotation, text clipping, images, tables, headers and footers, links, fields, annotations, redaction appearance, contrast, and print readability at useful zoom levels.
5. Fix defects, reopen, render, validate, and run review again. Stop after at most three visual correction rounds and report any unresolved defect.
6. Do not claim a visual pass when page inspection is unavailable, any required page/artifact was not opened, or coverage is incomplete. State exact page coverage and mark the remainder partial or skipped.
7. Report evaluation results separately from licensed results. Disclose `EVAL_MODE`, evaluation watermarks, page limits, signature state, and standards-validation results for each affected artifact.

## External resources

The custom HTML resource callback uses verified local reads and shared budgets. However, the pinned Aspose.PDF.Drawing 26.5.0 importer can fetch linked images and CSS outside that callback. PDF HTML creation therefore cannot currently guarantee network or filesystem resource isolation. Use trusted HTML inputs only; the callback warning is not proof that external access was prevented.
