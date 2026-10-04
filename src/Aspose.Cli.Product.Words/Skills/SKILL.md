---
name: aspose-cli-words
description: Create, inspect, edit, compare, convert and review Word documents with the local Aspose CLI. Markdown or text content is poured into a template's styles, edits are atomic batches with semantic verification, and review renders every page for visual checks.
---

# Aspose Words

Use `aspose-cli words` for DOC/DOCX, RTF, ODT, HTML, Markdown, PDF and related
word-processing documents. This Skill holds what is specific to Words; the
session start, the rules every product shares and the delivery checklist are in
`aspose-cli docs overview`.

A PDF input is rebuilt as flowing text: its headers and footers, such as page
numbers, become body text and the layout may add pages, which `convert`,
`split` and `edit` report as `LOSSY_CONVERSION` (known SDK issue
`WORDS-PDF-HEADER-FOOTER`); review the output and restore them with
`set_header`, `set_footer` or `set_page_numbers`.

## Document model

- A **block** is a body paragraph or table, numbered from 1. The paragraphs and
  tables inside a block-level content control, such as a table of contents,
  are blocks too. Images, fields, hyperlinks and breaks belong to their
  paragraph; `inspect` names the `scope` of each image, field and revision,
  and `query search` the `scope` and `section` of each hit; neither gives a
  `block` in headers and footers. A **section** carries page setup, headers and footers, numbered
  from 1. `inspect --detail sections` lists the headers and footers each
  section defines, by the `location` (`header`, `footer`) and `kind`
  (`primary`, `first`, `even`) that `set_header`, `set_footer` and
  `set_page_numbers` take, with their `paragraphs`; a kind a section does not
  define continues from the previous section. A search hit in one names its
  `location` and `kind` too.
- Block text, `--scope full` runs, search snippets and `heading`/`find`
  addresses use the text a reader sees: field results rather than field codes, without text a tracked
  change deletes, and without the comments and footnotes a paragraph anchors.
  Read those with `inspect --detail comments` or `query search --scope`, and
  tracked changes themselves with `inspect --detail revisions`
  ([revisions](references/revisions.md)).
- `query search --scope` and the `replace_text` op share one scope vocabulary:
  `body`, `headersFooters`, `footnotes` (with endnotes), `comments` and `all`.
- Read fields use the edit vocabulary: a table block's `rowCount`,
  `columnCount` and `cells` and a paragraph's `text` and `style` can be
  written back through `insert_table` and `insert_paragraphs` under the same
  names.

## Reading

```powershell
aspose-cli words inspect input.docx --detail outline sections fields bookmarks comments --output json
aspose-cli words query blocks input.docx --blocks 1-30 --scope full --output json
aspose-cli words query search input.docx --pattern "notice period" --scope all --output json
```

`--scope outline|text|full` chooses how much of each block is projected.
`--max-chars` bounds the sum of paragraph, table-cell and run text; addressing
and formatting metadata do not count against it. Check `window.truncated` and
each block's `contentTruncated`, and run `window.next` as given: it resumes at
the block the budget cut short and doubles `--max-chars` when that block alone
exceeded it. `--section` with `--blocks` reads only that section's blocks in
the range; block numbers run through the whole document, so a range with none
of them is `BLOCK_NOT_FOUND`, whose hint names the section's blocks. `extract --what text` writes the visible text of every block to one
file, one line per paragraph. Windows, paging and compact output in general:
`aspose-cli docs reading`.

## Workflow

1. **New document:** write Markdown and create it inside a template (see
   Design below).

   ```powershell
   aspose-cli words create report.docx --markdown report.md --template brand.docx --title "Quarterly Report" --output json
   ```

2. **Existing document:** inspect structure and revision state, then read only
   the blocks you need (above).
3. **Edit:** put all related changes in one `words edit` batch with `--verify`;
   add `--track-changes --author "Name"` when the change must stay reviewable.

   ```powershell
   aspose-cli words edit input.docx --ops ops.json --out output.docx --verify --output json
   ```

   Filling a template's `{{placeholder}}` text: `replace_text` covers only the
   body unless it names `"scope": "all"`, and templates often keep the contract
   number or date in a header. A table with one template row for a list of
   items, such as products, takes `repeat_table_row`: it copies that row per
   item with its formatting and column widths, so never rebuild such a table
   with `insert_table`.

   ```json
   {"ops":[
     {"op":"replace_text","find":"{{contract_no}}","replace":"C-2026-014","scope":"all"},
     {"op":"repeat_table_row","at":{"find":"{{code}}"},"items":[
       {"code":"A-100","name":"Widget","qty":"2"},
       {"code":"B-200","name":"Gadget","qty":"1"}]}
   ]}
   ```

   Then confirm `words query search output.docx --pattern "{{" --scope all`
   finds nothing (the pattern is literal unless `--regex`).

4. **Verify:** read the changed blocks back, then review every page
   ([verification](references/verification.md)).

## Design: the template owns the look

- A template supplies styles, page setup, headers and footers; `--markdown` or
  `--text` supplies the body. Markdown headings map to Heading 1-6, quotes to
  Quote, lists to list paragraphs; only bold, italic and strike-through from
  the Markdown survive as direct formatting.
- Use the user's template when one exists; without `--template`,
  `words create` uses the built-in A4 design. `insert_markdown` and Markdown
  headers or footers import the same way, into the edited document's styles.
- Structure with built-in Heading styles; they drive navigation and the table
  of contents. Use `set_style`, `define_style`, `insert_toc` and
  `set_page_numbers` rather than run-by-run formatting.

More: [document standards](references/document-standards.md).

## Words disclosures

Besides the shared disclosures, tell the user about tracked changes and
comments left in the output, invalidated signatures (`SIGNATURE_INVALIDATED`),
dropped macros (`MACROS_DROPPED`; `inspect` reports `document.hasMacros`
before any conversion), removed encryption
(`DOCUMENT_ENCRYPTION_REMOVED`) and lossy conversion. In evaluation mode,
`EVAL_INPUT_TRUNCATED` means only part of an input, template or appended
document was loaded, or that the edited document grew past what evaluation
mode lays out and saves (about 200 paragraphs, as with a mail merge of many
records): the output then keeps only its first sections, ends with the
engine's truncation notice, and `--verify` reports `OUTPUT_TRUNCATED`. The
result is incomplete whatever `itemsAffected` says. A Words license is installed
with `aspose-cli license install Aspose.Words.lic --product words`; licensing
in general: `aspose-cli docs licensing`.

## References

- [Editing: addressing, batch semantics and the operation index](references/editing.md)
- [Document standards](references/document-standards.md)
- [Revisions and comparison](references/revisions.md)
- [Mail merge](references/mail-merge.md)
- [Verification](references/verification.md)
- [What the viewer shows](references/preview.md)
- [Words error codes](references/troubleshooting.md)

Examples: [report from Markdown](examples/report-from-markdown/README.md),
[edit a contract safely](examples/edit-contract-safely/README.md),
[mail-merge letters](examples/mail-merge-letters/README.md).
