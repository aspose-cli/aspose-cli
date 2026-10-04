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
  `append_document`. A tracked batch can also hold `add_comment` and
  `remove_comments`: a comment is a review annotation of its own, so it is added
  or removed outright and is never listed as a revision. Every other operation
  would change the document without a revision, so a tracked batch that
  contains one fails with `OPTION_INVALID` before anything changes; run it in a
  separate batch.

## Text

- `set_text` replaces the inline content of paragraphs and keeps their style;
  use `set_table_cell` for table cells. With `"at": {"bookmark": "Name"}`, or
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
  key for one of the row's placeholders fails the operation, naming an extra
  key close to the placeholder as a likely misspelling; extra keys are
  otherwise ignored, and no items removes the template row. A tracked batch records
  each copy as a row insertion and the template row as a deletion.
- Inline Markdown loads local resources under the edited document's directory;
  remote and escaping resources are omitted and reported.

## Operation index

| Task | Operations |
|---|---|
| Change text | `replace_text` (literal or regex, by scope), `set_text` (paragraph or bookmark text), `set_table_cell` (one 1-based cell), `repeat_table_row` (one copy of a `{{key}}` template row per item) |
| Add content | `insert_paragraphs` (styled paragraphs, list items), `insert_markdown`, `insert_table`, `insert_image`, `insert_hyperlink`, `insert_field`, `insert_toc`, `insert_bookmark` (a paragraph's visible text), `append_document` |
| Remove content | `delete_blocks` |
| Styles and formatting | `set_style` (apply an existing style), `define_style` (create or update one), `format_text` (runs of target blocks), `format_table` (how one table breaks across pages), `apply_list` (one new bullet or numbered list), `set_default_font` |
| Sections and pages | `insert_break` (page break, or split the section), `add_section`, `delete_section` (never the last one), `set_page_setup`, `set_header`, `set_footer`, `set_page_numbers`, `add_watermark` (text in `font`; East Asian text otherwise in the default East Asian font of a Chinese, Japanese or Korean document, or else Microsoft YaHei), `remove_watermark` |
| Review annotations | `add_comment`, `remove_comments`, `accept_revisions`, `reject_revisions` |
| Fields and data | `update_fields` (tables of contents, or every field and the layout), `mail_merge` ([mail merge](mail-merge.md)) |
| Document state | `set_properties`, `protect`, `unprotect` |

## Recipe: insert several pieces in reading order

Insertions at the same anchor stack against it: each `after` insertion lands
directly after the anchor, ahead of earlier ones, and each `before` insertion
lands directly before it, behind earlier ones. To keep the batch order as the
reading order, anchor every piece `before` the block that should follow them.
A table read with `query blocks` can be written back with its `rowCount`,
`columnCount` and `cells` unchanged. `insert_table`'s `style` names an existing
table style; `inspect --detail tables` shows the `style` each table uses, so a
new table can match the document's tables.

```json
{
  "ops": [
    { "op": "insert_paragraphs", "at": { "find": "Revenue increased" }, "position": "before",
      "paragraphs": [ { "text": "Key figures", "style": "Heading 3" }, { "text": "Figures are in thousands." } ] },
    { "op": "insert_table", "at": { "find": "Revenue increased" }, "position": "before",
      "rowCount": 2, "columnCount": 2, "cells": [ [ "Metric", "Value" ], [ "Revenue", "120" ] ],
      "style": "Table Grid" }
  ]
}
```

Inserted paragraphs and tables take their font from styles: a paragraph without
`style`, and a table's text, use Normal. A template that sets its font only on
runs, such as Microsoft YaHei over a Normal in another font, therefore gives
inserted text, CJK text in particular, a different font. Compare
`inspect --detail fonts` of the template and the output. To match, put the body
font into the styles first, in the same batch: `define_style` on `Normal`
changes Normal and the styles based on it, and `set_default_font` changes every
paragraph and character style. Both set the Latin and the East Asian font.

```json
{
  "ops": [
    { "op": "define_style", "name": "Normal", "font": "Microsoft YaHei" },
    { "op": "insert_paragraphs", "at": { "block": 2 }, "position": "after",
      "paragraphs": [ { "text": "New clause text." } ] }
  ]
}
```

## Tables across pages

`format_table` sets how the one table `at` addresses breaks across pages;
omitted settings keep their values:

- `keepTogether: true` keeps the whole table on one page when it fits: no row
  splits, and every paragraph keeps with the next except the last paragraph of
  each last-row cell. `false` clears keep-with-next on those same paragraphs
  and leaves the row setting; pass `allowRowBreakAcrossPages: true` as well to
  let rows split again. A table taller than a page still breaks.
- `allowRowBreakAcrossPages` sets whether each row's text may split.
- `headerRowCount` repeats the first N rows as a heading on every page the
  table spans and clears the other rows; `0` clears all, and more than the
  table's rows fails.
- `keepWithNext` keeps the table on the page of the paragraph that follows it.
  A caption or heading before the table stays with it through its own
  paragraph style's keep-with-next setting, which heading styles usually have.

`itemsAffected` counts the table's rows, or 1 when only `keepWithNext` is
set. `--track-changes` cannot record it.

```json
{ "ops": [ { "op": "format_table", "at": { "find": "Action items" }, "keepTogether": true, "headerRowCount": 1 } ] }
```

## Headers, footers and page numbers

`set_header` and `set_footer` replace the selected kind, including its fields,
in one section or every section. Their content is exactly one of `markdown` or
`paragraphs`, and these `paragraphs` are plain strings, one per paragraph, such
as `["Contract C-2026-014", "Confidential"]`; `insert_paragraphs` instead takes
`{"text": ..., "style": ...}` objects. Apply footer text before `set_page_numbers`.
Page numbering targets only the primary header or footer, reuses its first
PAGE field or appends one in a new paragraph, and keeps the other content. A
`start` restarts numbering in each selected section; name a `section` when only
one should restart.

## Protection and encryption

- Editing restrictions (`protect`) are not encryption: they guide Word's user
  interface and do not bind the CLI. Editing a restricted document succeeds,
  reports `PROTECTION_NOT_ENFORCED` and keeps the restrictions in a Word
  format output. `unprotect`
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
An `--out` in `txt`, `md`, `html` or `html-fixed` reports `LOSSY_CONVERSION`:
such a format cannot hold every Word feature, such as styles, headers or
fields. Any output other than a Word format, `rtf`, `odt` or `ott` also
reports `LOSSY_CONVERSION` for the tracked changes it cannot keep as revisions,
and any output other than a Word format, PDF included, for the editing
restrictions it cannot keep; restrict a PDF with the `encrypt` operation of
`aspose-cli pdf edit`.
A `txt` or `md` output also writes comment text into the body and
deleted text beside inserted text, each reported by its own `LOSSY_CONVERSION`
(`words convert` reports them too); add `remove_comments`, and
`accept_revisions` or `reject_revisions` as the reviewer decides, to the same
batch to leave them out. `--verify` adds the semantic checks described in
[verification](verification.md); it does not render pages and cannot be used
for outputs that are not documents.
