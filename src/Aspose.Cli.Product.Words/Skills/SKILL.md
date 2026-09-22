---
name: aspose-cli-words
description: Create, inspect, edit, compare, convert and review Word documents with the local Aspose CLI. Markdown or text content is poured into a template's styles, edits are atomic batches with semantic verification, and review renders every page for visual checks.
---

# Aspose Words

Use `aspose-cli words` for DOC/DOCX, RTF, ODT, HTML, Markdown, PDF and related
document workflows. A block is a top-level body paragraph or table, numbered
from 1.

`query blocks --max-chars` bounds the sum of returned paragraph, table-cell and
run text. Full scope counts repeated text/run projections separately; addressing
and formatting metadata do not consume this text budget. Inspect both
`window.truncated` and each block's `contentTruncated`. `next` selects unread
blocks; it does not resume inside a truncated block. Re-read that block with a
larger budget or extract text when complete content is required.

## Workflow

1. Clarify audience, purpose, reading or print context and requested scope.
2. **New document:** write Markdown and create it inside a template (see
   Design below):

   ```powershell
   aspose-cli words create report.docx --markdown report.md --template brand.docx --title "Quarterly Report" --output json
   ```

3. **Existing document:** inspect structure, then read only the blocks you need:

   ```powershell
   aspose-cli words inspect input.docx --detail outline sections fields bookmarks comments --output json
   aspose-cli words query blocks input.docx --blocks 1-30 --scope full --output json
   ```

4. Put all related changes in one atomic `words edit` batch with `--verify`.
   Write to `--out`, or use `--in-place --backup` when replacing the user's
   file is intended. Add `--track-changes --author "Name"` when the change must
   stay reviewable.

   ```powershell
   aspose-cli words edit input.docx --ops ops.json --out output.docx --verify --output json
   ```

5. Verify before delivery (below).

## Design: the template owns the look

- A template supplies styles, page setup, headers and footers; `--markdown` or
  `--text` supplies the body. Markdown headings map to Heading 1-6, quotes to
  Quote, lists to list paragraphs; only bold, italic and strike-through from
  the Markdown survive as direct formatting.
- Use the user's template when one exists; otherwise the bundled
  [default A4 template](assets/templates/default-a4.docx) (path relative to
  this Skill directory). Without `--template`, the SDK's plain default styles
  are used.
- Structure with built-in Heading styles; they drive navigation and TOC. Use
  `set_style`, `define_style`, `insert_toc` and `set_page_numbers` rather than
  run-by-run formatting.

More: [document standards](references/document-standards.md).

## Verify before delivery

1. **Content:** `--verify` reopens the staged file and reports
   `semanticChangesDetected`, field, revision and protection state and
   `issues`. Read changed blocks back with `words query blocks`. Use
   `words compare` against a revision-free baseline when a redline matters.
2. **Visual:** run `aspose-cli review output.docx --out <new-dir> --output json`,
   then open every page image it lists, one by one. Use `review.json` findings
   to focus, not as a substitute for looking.
3. Fix, then review again into a fresh directory. Stop after three rounds and
   report what remains.
4. Never claim a visual pass for pages you did not open. State the exact page
   coverage, and disclose tracked changes, signatures, macro loss, font
   substitution and lossy conversion.

Details: [verification](references/verification.md).

## Licensing

Without a Words license, output carries `EVAL_MODE` and may contain evaluation
text; disclose that with every delivered file. Install a license with
`aspose-cli license install Aspose.Words.lic --product words` and check the
`words` entry of `aspose-cli license status --output json`.

Passwords come from `--password-env` or `--password-stdin`; operation
passwords are environment variable names in `passwordEnv`.

## References

- [Editing and the ops vocabulary](references/editing.md) (`aspose-cli schema v2/words/ops`)
- [Document standards](references/document-standards.md)
- [Revisions and comparison](references/revisions.md)
- [Mail merge](references/mail-merge.md)
- [Verification](references/verification.md)
- [Live preview for a human](references/preview.md)
- [Troubleshooting](references/troubleshooting.md)

Examples: [report from Markdown](examples/report-from-markdown/README.md),
[edit a contract safely](examples/edit-contract-safely/README.md),
[mail-merge letters](examples/mail-merge-letters/README.md).
