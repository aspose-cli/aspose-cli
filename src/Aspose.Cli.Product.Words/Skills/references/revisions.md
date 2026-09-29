# Revisions and comparison

Before editing, inspect `document.revisionsPresent`, `document.revisionAuthors`,
`document.protection` and `document.signed` in the `words inspect` result.
Editing a document that already has revisions reports `TRACKED_CHANGES_PRESENT`;
disclose them.

Use `--track-changes --author "Name"` when the requested edit must remain reviewable.
`set_table_cell` supports tracked replacement while retaining the cell's structure;
accept or reject revisions only when that review decision is explicitly requested.

## Listing revisions

`--detail revisions` lists every tracked change in document order, so "what did
the reviewer change?" needs no accepted or rejected copies:

```powershell
aspose-cli words inspect contract.docx --detail revisions --output json
```

```json
"revisions": [
  { "type": "deletion", "author": "Alice Legal", "date": "2026-09-01T10:30:00", "block": 1, "text": "thirty" },
  { "type": "insertion", "author": "Alice Legal", "date": "2026-09-01T10:30:00", "block": 1, "text": "sixty" }
]
```

- One entry is one change: adjacent runs and paragraph marks that one author
  inserted or deleted are listed together. `type` is `insertion`, `deletion`,
  `formatChange`, `styleDefinitionChange` or `moving`.
- `text` is the new text of an insertion and the original text of a deletion.
  Format and style definition changes have no `text`; read the `block` instead.
  A move is listed at its source and at its destination.
- `date` is the time the document records, without a time zone; it is absent
  when the document records none. `block` is absent for changes outside the
  body, such as in headers, and for style definitions.
- A replacement is a deletion followed by an insertion by the same author in the
  same block: read the pair above as "thirty" replaced by "sixty".
- Long texts are cut at 300 characters; read the block for the full text. More
  than 1000 changes carry a `LIST_TRUNCATED` warning.

## Comparing

`words compare` refuses inputs that already contain revisions (`DOCUMENT_HAS_REVISIONS`); list them with `--detail revisions` above. To compare anyway, make reviewed copies first, explicitly accept or reject revisions there, then compare:

```powershell
aspose-cli words compare original.docx changed.docx --out redline.docx --output json
```

Editing a signed document invalidates its signature and emits `SIGNATURE_INVALIDATED`; the resulting file must be reviewed and re-signed.
