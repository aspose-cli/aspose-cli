---
name: aspose-cli-words
description: High-fidelity Word-processing automation with bounded reading, atomic editing, rendering, comparison, extraction, and verification.
---

# Aspose Words

Use `aspose-cli words` for DOC/DOCX, RTF, ODT, HTML, Markdown, PDF and related document workflows.

## Safe workflow

1. Inspect before reading: `aspose-cli words inspect input.docx --detail outline sections fields bookmarks comments --output json`.
2. Read only the required block window: `aspose-cli words query blocks input.docx --blocks 1-30 --scope full --output json`.
3. For edits, create one atomic ops document and prefer `--in-place --backup --verify`, or write to `--out`.
4. Read back changed blocks and inspect every verification render.
5. Run `words compare` when semantic evidence against a baseline matters.
6. Disclose `EVAL_MODE`, tracked changes, signatures, lossy conversion, macro loss, font substitution and layout warnings.

## Live preview

Use `aspose-cli preview input.docx --open` for the managed product-routed
lifecycle. It selects Words and the `document` view from the file, returns
immediately, and is managed with `preview status|stop`. See
`references/preview.md`.

Blocks are only top-level body paragraphs and tables. Block addresses are resolved against the original document once per batch, so inserted content cannot be targeted later in that batch.

## Licensing

Without a Words license, output carries evaluation warnings and may contain
watermarks or evaluation text. Install a Words-only license with
`aspose-cli license install Aspose.Words.lic --product words`, set
`ASPOSE_WORDS_LICENSE_PATH`, use a shared `ASPOSE_LICENSE_PATH`, or pass
`--license <path>`. Always inspect the `words` entry from
`aspose-cli license status --output json`; a licensed Cells entry does not
mean Words is licensed.

See `references/editing.md`, `references/document-standards.md`, `references/revisions.md`, `references/mail-merge.md`, `references/verification.md`, and `references/troubleshooting.md`.

Worked examples:

- `examples/report-from-markdown`
- `examples/edit-contract-safely`
- `examples/mail-merge-letters`

## Visual delivery gate

1. Understand the audience, document purpose, reading or print context, document standard, and requested scope before drafting or editing.
2. For an existing user document, preserve unrelated text, styles, sections, headers and footers, fields, notes, revisions, comments, and document settings; change only the requested scope.
3. Run `aspose-cli review <artifact> --out <fresh-review-dir> --output json` for every document and exported deliverable, using a fresh output directory for each round. Use its findings to focus inspection, never as a substitute for opening pages or separately checking revisions, signatures, fields, and standards requirements.
4. Actually open every visual artifact produced by review, one by one, then every rendered page for a new document and every changed or reflow-affected page for a scoped edit; open every page of PDF exports. Check heading hierarchy, typography, paragraph flow, widows and orphans, tables, lists, image placement, captions, cross-references, headers and footers, page numbers, section breaks, clipping, and tracked-change/comment visibility.
5. Fix defects, reopen, render, and run review again. Stop after at most three visual correction rounds and report any remaining issue.
6. Do not claim a visual pass when page inspection is unavailable, any required page/artifact was not opened, or coverage is incomplete. State exact page coverage and mark the remainder partial or skipped.
7. Report evaluation results separately from licensed results. Disclose `EVAL_MODE`, watermarks or evaluation text, truncation, font substitution, macro or format loss, and signature/revision state for each affected artifact.

## External resources

External document resources are limited to verified ordinary local files beneath the input directory. Network, data, UNC, device, linked and escaping references are omitted. Reads are capped at 256 resources, 32 MiB each and 128 MiB total, and also consume the invocation input, memory and time budgets. Shared budget failures abort the operation. Omitted resources produce a completeness warning. Platforms without a verified file-handle boundary omit all external resources.
