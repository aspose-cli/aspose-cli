# Editing

`words edit --ops` applies one operation batch; batches, `--best-effort`,
`--dry-run`, outputs and secrets work as `aspose-cli docs editing` describes.
Each operation's fields, types, defaults and allowed values come from the
generated schema:

```powershell
aspose-cli schema v2/words/ops --operation insert_table
```

## Addresses

- A block address (`at`, or `target` for `apply_list`, `delete_blocks`,
  `format_text` and `set_style`) is exactly one of `block`, `blocks`,
  `bookmark`, `heading` or `find`. `find` matches blocks whose visible text
  contains the given text, ignoring case, and `heading` does the same among
  heading paragraphs; `nth` picks the match (the first by default). Prefer
  bookmarks and headings in automation; read block numbers again immediately
  before using them.
- Insertions take `position: before|after` at a block boundary; there is no
  character-offset addressing.
- A block-level content control is a container: its paragraphs and tables are
  blocks, content inserted beside one of them stays inside the control,
  deleting all of them removes the control, and a section break cannot be
  placed inside it.

## Batch semantics

- Every address, including numbered sections and `add_section.after`,
  resolves to the document as it was before the first operation. An omitted
  section means every original section. Inserted content and sections cannot
  be targeted later in the same batch.
- Overlapping deletes, delete-then-reference and other invalid dependencies
  fail during preflight, also with `--best-effort`. Removing a section
  invalidates references to its original blocks; later section numbers never
  shift to a different original section.
- `--track-changes` requires `--author` and records content insertions and
  deletions: `replace_text`, `set_text`, `insert_*` (page breaks only, not
  section breaks), `delete_blocks`, `set_table_cell`, `repeat_table_row`,
  `append_document`, `add_comment` and `remove_comments`. Every other operation would change the
  document without a revision, so a tracked batch that contains one fails with
  `OPTION_INVALID` before anything changes; run it in a separate batch.

## Text

- `set_text` replaces the inline content of paragraphs and keeps their style;
  use `set_table_cell` for table cells. With a `bookmark` target, or
  `--set bookmark:Name=text`, it replaces only the text the bookmark encloses,
  anywhere including table cells: the bookmark and the rest of its paragraph
  remain, the new text takes the format of the bookmark's first run, and a
  bookmark spanning several paragraphs becomes one paragraph.
- `replace_text` changes the text a reader sees: field results but never field
  codes, and never text a tracked change deletes.
- `repeat_table_row` expands a template row of the table `at` addresses (such
  as `{"find":"{{code}}"}`): one copy per item, in order, in place of the
  template row. Without `row`, the template is the table's one row with a
  `{{key}}` placeholder (spaces inside the braces are allowed); pass the
  1-based `row` when no row or several rows have one. Items come inline as
  `items` or from `path` (a JSON array of flat objects, or CSV with a header
  row); a null or missing CSV value is empty text. Each placeholder takes the
  item's value as literal text in the placeholder's formatting, and the copies
  keep the row's height, borders, shading and cell widths. An item without a
  key for one of the row's placeholders fails the operation; extra keys are
  ignored, and no items removes the template row. A tracked batch records
  each copy as a row insertion and the template row as a deletion.
- Inline Markdown loads local resources under the edited document's directory;
  remote and escaping resources are omitted and reported.

## Operation index

| Task | Operations |
|---|---|
| Change text | `replace_text` (literal or regex, by scope), `set_text` (paragraph or bookmark text), `set_table_cell` (one 1-based cell), `repeat_table_row` (one copy of a `{{key}}` template row per item) |
| Add content | `insert_paragraphs` (styled paragraphs, list items), `insert_markdown`, `insert_table`, `insert_image`, `insert_hyperlink`, `insert_field`, `insert_toc`, `insert_bookmark` (a paragraph's visible text), `append_document` |
| Remove content | `delete_blocks` |
| Styles and formatting | `set_style` (apply an existing style), `define_style` (create or update one), `format_text` (runs of target blocks), `apply_list` (one new bullet or numbered list), `set_default_font` |
| Sections and pages | `insert_break` (page break, or split the section), `add_section`, `delete_section` (never the last one), `set_page_setup`, `set_header`, `set_footer`, `set_page_numbers`, `add_watermark`, `remove_watermark` |
| Review annotations | `add_comment`, `remove_comments`, `accept_revisions`, `reject_revisions` |
| Fields and data | `update_fields` (tables of contents, or every field and the layout), `mail_merge` ([mail merge](mail-merge.md)) |
| Document state | `set_properties`, `protect`, `unprotect` |

## Recipe: insert several pieces in reading order

Insertions at the same anchor stack against it: each `after` insertion lands
directly after the anchor, ahead of earlier ones, and each `before` insertion
lands directly before it, behind earlier ones. To keep the batch order as the
reading order, anchor every piece `before` the block that should follow them.
A table read with `query blocks` can be written back with its `rows`,
`columns` and `cells` unchanged.

```json
{
  "ops": [
    { "op": "insert_paragraphs", "at": { "find": "Revenue increased" }, "position": "before",
      "paragraphs": [ { "text": "Key figures", "style": "Heading 3" }, { "text": "Figures are in thousands." } ] },
    { "op": "insert_table", "at": { "find": "Revenue increased" }, "position": "before",
      "rows": 2, "columns": 2, "cells": [ [ "Metric", "Value" ], [ "Revenue", "120" ] ] }
  ]
}
```

## Headers, footers and page numbers

`set_header` and `set_footer` replace the selected kind, including its fields,
in one section or every section. Apply footer text before `set_page_numbers`.
Page numbering targets only the primary header or footer, reuses its first
PAGE field or appends one in a new paragraph, and keeps the other content. A
`start` restarts numbering in each selected section; name a `section` when only
one should restart.

## Protection and encryption

- Editing restrictions (`protect`) are not encryption: they guide Word's user
  interface and do not bind the CLI. Editing a restricted document succeeds,
  reports `PROTECTION_NOT_ENFORCED` and keeps the restrictions. `unprotect`
  with `passwordEnv` checks the password and fails with `DOCUMENT_PROTECTED`
  when it is wrong; without `passwordEnv` it removes the restrictions whatever
  their password, so use it only when the user owns that decision.
- An encrypted input needs `--password-env` to open. Its output keeps the
  password when the output format supports encryption; `--encrypt-env`
  replaces it. A format that cannot be encrypted produces
  `DOCUMENT_ENCRYPTION_REMOVED`, and `--encrypt-env` with such a format is
  `OPTION_INVALID`. A read password for a plaintext input does not encrypt the
  output. A dry run does not report removed encryption.

## Save and verify

Every output that can be loaded as a document is reopened before publication.
`--verify` adds the semantic checks described in
[verification](verification.md); it does not render pages and cannot be used
for outputs that are not documents.
