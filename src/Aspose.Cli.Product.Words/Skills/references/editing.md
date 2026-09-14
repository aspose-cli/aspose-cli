# Editing

Use one `words edit --ops` batch. The batch is validated and applied in memory before a single atomic save.

```powershell
aspose-cli words edit contract.docx --ops update.json --out contract.review.docx --verify --output json
```

Inline Markdown uses the same guarded local-resource policy as file loading. Relative resources resolve beneath the edited document's directory; remote and escaping resources are omitted and reported. Shared resource-budget or cancellation failures stop the operation.

Targets accept one of `block`, `blocks`, `bookmark`, `heading`, or `find`, with optional 1-based `nth`. Prefer bookmarks and headings for durable automation; inspect current block numbers immediately before using numeric targets.

`set_text` accepts paragraphs only and preserves the paragraph style while replacing inline runs. Use `set_table_cell` for tables. Insertion ops require `position: before|after`; v2 deliberately has no character-offset addressing.

Passwords used by `protect` and `unprotect` are environment variable names in `passwordEnv`. Never put a resolved secret into JSON.

Discover the exact vocabulary with:

```powershell
aspose-cli schema v2/words/ops
```

## Address and batch semantics

- A block is only a top-level paragraph or table in a section body. Images,
  fields, hyperlinks and breaks belong to their paragraph.
- All addresses are resolved to original node identities before the first op
  runs. Inserted content cannot be targeted later in the same batch.
- Overlapping deletes, delete-then-reference and other invalid dependencies
  fail before a file is written.
- `--best-effort` saves successful operations even when others fail; those
  partial results exit 8. Without it, an operation failure aborts the batch.
- `--track-changes` requires `--author`. Comparison and tracking metadata use a
  fixed internal timestamp so JSON summaries remain deterministic.

## Op index

| Op | Purpose |
|---|---|
| `replace_text` | Replace literal or timeout-bounded regex matches in body, headers/footers, comments or all content; supports `maxReplacements`. |
| `set_text` | Replace one or more paragraph bodies while keeping paragraph style. |
| `insert_paragraphs` | Insert structured paragraphs before or after an original block. |
| `insert_markdown` | Import Markdown blocks before or after an original block. |
| `delete_blocks` | Delete original paragraph/table blocks after conflict validation. |
| `insert_break` | Insert a page or section break at a block boundary. |
| `insert_image` | Insert an inline or floating local image with optional dimensions. |
| `insert_table` | Insert a table with bounded row/column data. |
| `set_table_cell` | Replace one 1-based cell in a targeted table block. |
| `insert_toc` | Insert and update a TOC through the selected heading level. |
| `insert_bookmark` | Bookmark the complete visible text of a paragraph. |
| `insert_hyperlink` | Insert a hyperlink paragraph at a block boundary. |
| `insert_field` | Insert an explicit Word field code at a block boundary. |
| `add_section` | Add a section at the start, end or after a numbered section. |
| `delete_section` | Delete a section, but never the document's final section. |
| `set_page_setup` | Set size (`a3`, `a4`, `a5`, `letter`, or `legal`, lowercase only), orientation, margins and columns on one or all sections. |
| `set_header` | Replace primary, first-page or even-page header content. |
| `set_footer` | Replace primary, first-page or even-page footer content. |
| `set_page_numbers` | Reuse or append a PAGE field in the primary header/footer, preserving its other content; configure start/number style. |
| `format_text` | Apply font, size, emphasis, colour and highlight to target runs. |
| `set_style` | Apply an existing paragraph style to target paragraphs. |
| `define_style` | Create or update a named paragraph style. |
| `apply_list` | Apply bullet or numbered list formatting at levels 0–8. |
| `set_default_font` | Update paragraph and character style defaults. |
| `set_properties` | Set built-in and string custom document properties. |
| `add_watermark` | Add a text or local-image watermark. |
| `remove_watermark` | Remove the document watermark. |
| `protect` | Protect using an optional password read from `passwordEnv`. |
| `unprotect` | Remove protection using an optional password read from `passwordEnv`. |
| `accept_revisions` | Accept all revisions or only those by an author. |
| `reject_revisions` | Reject all revisions or only those by an author. |
| `add_comment` | Comment the complete visible text of a paragraph. |
| `remove_comments` | Remove all comments or those by an author. |
| `append_document` | Append a local document with source or destination styles. |
| `mail_merge` | Merge JSON-object-array or headered CSV data, including regions. |
| `update_fields` | Update TOC or all fields, then refresh page layout. |

## Headers, footers and page numbers

`set_header` and `set_footer` replace the selected kind, including its fields,
in one section or all sections when `section` is omitted. Apply footer text
before `set_page_numbers`. Page numbering targets only the primary header or
footer, reuses its first PAGE field or appends one in a new paragraph, and
preserves the other content. A supplied `start` restarts numbering in each
selected section; specify `section` when only one section should restart.

## Verification

`--verify` is opt-in and cannot be combined with `--dry-run`. It reopens the
saved file, checks field/revision/protection state, compares private document
copies and renders bounded page evidence. See `verification.md` for coverage
and result fields. Verification happens after save; a failed verification does
not mean the saved document was rolled back.
