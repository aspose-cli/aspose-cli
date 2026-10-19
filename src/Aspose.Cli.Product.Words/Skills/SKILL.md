---
name: aspose-cli-words
description: Create, inspect, edit, compare, convert and review Word documents with the local Aspose CLI. Markdown or text content is poured into a template's styles, edits are atomic batches with semantic verification, and review renders every page for visual checks.
---

# Aspose Words

Use `aspose-cli words` for DOC/DOCX, RTF, ODT, HTML, Markdown, PDF and related
word-processing documents. This Skill holds what is specific to Words; the
session start, the rules every product shares and the delivery checklist are in
`aspose-cli docs overview`; page-level PDF work (fill, redact, sign) belongs
to the PDF Skill. Options and defaults: `aspose-cli capabilities words`;
operation fields: `aspose-cli schema v2/words/ops --operation <op>`.

## Workflow

1. **New document:** write Markdown and create it inside a template (Design).

   ```powershell
   aspose-cli words create report.docx --from-markdown report.md --template brand.docx --title "Quarterly Report" --output-format json
   ```

2. **Existing document:** inspect structure and revision state, then read only
   the blocks you need ([reading](references/editing.md#reading-the-document)).

   ```powershell
   aspose-cli words inspect input.docx --detail outline sections fields bookmarks comments --output-format json
   aspose-cli words read input.docx --blocks 1- --max-blocks 30 --content full --output-format json
   aspose-cli words search input.docx --pattern "notice period" --scope all --output-format json
   ```

3. **Edit:** put all related changes in one `words edit` batch with `--verify`
   ([editing](references/editing.md)); add `--track-changes --author "Name"`
   when the change must stay reviewable.

   ```powershell
   aspose-cli words edit input.docx --ops ops.json --out output.docx --verify --output-format json
   ```

   To fill a template, `replace_text` replaces `{{placeholder}}` text and
   `repeat_table_row` copies a table's template row once per item, keeping its
   formatting and column widths; never rebuild such a table with `insert_table`.

   ```json
   {"ops":[
     {"op":"replace_text","find":"{{contract_no}}","replace":"C-2026-014","scope":"all"},
     {"op":"repeat_table_row","at":{"find":"{{code}}"},"items":[
       {"code":"A-100","name":"Widget","qty":"2"},
       {"code":"B-200","name":"Gadget","qty":"1"}]}
   ]}
   ```

4. **Verify:** read the changed blocks back, confirm
   `words search output.docx --pattern "{{" --scope all` finds nothing (the
   pattern is literal unless `--regex`), then review every page ([verification](references/verification.md)).

## Document model

- A **block** is a body paragraph or table, numbered from 1 through the whole
  document; a **section** carries page setup, headers and footers. Images,
  fields, hyperlinks and breaks belong to their paragraph.
- Reads and `find`/`heading` addresses use the text a reader sees: field
  results, without deleted tracked text, comments or footnotes.
- A numbered paragraph's number, such as `6.2`, is its block's `listLabel`,
  not part of its `text`; search, `find` addresses and extracted text start
  with the number and a space, so `"find": "6.2 "` addresses clause 6.2.

## Design: the template owns the look

- A template supplies styles, page setup, headers and footers; `--from-markdown`
  or `--from-text` supplies the body, with headings as Heading 1-6 and only
  bold, italic and strike-through as direct formatting. Use the user's template
  when one exists; without `--template`, `words create` uses the built-in A4 design.
- Use built-in Heading styles, `set_style`, `define_style`, `insert_toc` and `set_page_numbers`
  rather than run-by-run formatting ([document standards](references/document-standards.md)).

## Common mistakes

- **Placeholders left in a header.** `replace_text` covers only the body
  unless it names `"scope": "all"`, and templates often keep the contract
  number or date in a header.
- **A `#` title in the table of contents.** Markdown has no Title: write the
  title as a plain first line, chapters as `#`, and `set_style` that line to
  `Title` in the batch that inserts the table of contents.
- **Markdown markers after CJK punctuation.** A closing `**` or `~~` right
  after `》` or `）` stays literal; write `<strong>`, `<em>` or `<del>` tags.
  Put footnote definitions (`[^1]: note`) at the end of the Markdown.
- **Addresses after an insertion.** Every address resolves against the
  document before the batch, and insertions at one anchor stack against it:
  anchor them all `before` the block that follows
  ([editing](references/editing.md#batch-semantics)).
- **Untrackable operations in a tracked batch.** With `--track-changes`, any
  operation other than content insertions, deletions and comments fails the
  batch with `OPTION_INVALID` ([editing](references/editing.md#batch-semantics)).
- **Editing straight to PDF or Markdown.** `edit` writes only formats that
  reopen as the same document; edit to `.docx`, then `words convert`.
- **Trusting a PDF input.** Its headers and footers are guessed and CJK lines
  gain spaces; check `inspect --detail sections fields` and search with a
  regex such as `甲 ?乙双方` ([troubleshooting](references/troubleshooting.md#a-pdf-input)).
- **Fonts that differ from the template.** Styled text takes its style's font;
  set the body font with `define_style` or `set_default_font` in the batch.
- **Accepting revisions to clean up a review.** Disclose tracked changes and
  comments and decide them only as the user says; `words compare` refuses
  inputs that still have them (`REVISIONS_UNRESOLVED`).
- **Unreported warnings.** Besides the shared disclosures, tell the user about
  `SIGNATURE_INVALIDATED`, `MACROS_DROPPED` and `FONTS_EMBEDDED`
  ([troubleshooting](references/troubleshooting.md#what-a-warning-asks-you-to-disclose)).

## References

- [Editing](references/editing.md): reading, addresses, batches, operations, tables, headers, protection, saving.
- [Document standards](references/document-standards.md): templates, the built-in design, Markdown, review.
- [Revisions and comparison](references/revisions.md): tracked changes and `words compare`.
- [Mail merge](references/mail-merge.md): merge data, regions, one file per record.
- [Verification](references/verification.md): `--verify`, reading `applied[]` back, review findings.
- [What the viewer shows](references/preview.md): the live preview.
- [Troubleshooting](references/troubleshooting.md): failed addresses, no-effect operations, PDF inputs, warnings, evaluation mode.

Examples: [report from Markdown](examples/report-from-markdown/README.md),
[edit a contract safely](examples/edit-contract-safely/README.md), [mail-merge letters](examples/mail-merge-letters/README.md).
