# Revisions and comparison

Before editing, inspect `document.revisionsPresent`, `document.revisionAuthors`,
`document.commentCount`,
`document.protection` and `document.signed` in the `words inspect` result.
Editing or converting a document that already has revisions reports
`TRACKED_CHANGES_PRESENT` while the output still contains them; disclose them.
Only a Word format, `rtf`, `odt` or `ott` output stores revisions. A batch that
accepts or rejects every revision does not report it. Any other output reports
`LOSSY_CONVERSION` instead: it cannot keep the changes as revisions, and a
`txt` or `md` output writes deleted text beside the inserted text.

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
  { "type": "deletion", "author": "Alice Legal", "date": "2026-09-01T10:30:00", "scope": "body", "block": 1, "text": "thirty" },
  { "type": "insertion", "author": "Alice Legal", "date": "2026-09-01T10:30:00", "scope": "body", "block": 1, "text": "sixty" }
]
```

- One entry is one change: adjacent runs and paragraph marks that one author
  inserted, deleted or moved are listed together, except inside comment text,
  where each run and paragraph mark is its own entry. `type` is `insertion`, `deletion`,
  `formatChange`, `styleDefinitionChange` or `moving`.
- `document.revisionCount` counts the revisions the document stores, one per
  run, paragraph mark or other changed node, so it is usually larger than the
  number of entries: a two-run insertion is one entry and two revisions.
- `scope` names the story that holds the change, as `query search` names its
  scopes: `body`, `headersFooters`, `footnotes` or `comments`. A change inside a
  comment's text belongs to the comment, not to the body around it, even though
  its `block` is the block that anchors the comment. Style definition changes
  have no `scope`.
- `text` is the new text of an insertion and the original text of a deletion.
  Format and style definition changes have no `text`; read the `block` instead.
  A move is listed twice, once at its source and once at its destination, each
  with the moved text. A paragraph mark inserted or deleted on its own, as when
  a paragraph is split or joined, has no `text`.
- `date` is the time the document records, without a time zone; it is absent
  when the document records none. `block` is the body block that holds the
  change or anchors the comment or note that holds it; it is absent in headers
  and footers and for style definitions.
- A replacement is a deletion followed by an insertion by the same author in the
  same block: read the pair above as "thirty" replaced by "sixty".
- Long texts are cut at 300 characters; read the block for the full text. More
  than 1000 changes carry a `LIST_TRUNCATED` warning.

## Comparing

`words compare` refuses inputs that already contain revisions (`DOCUMENT_HAS_REVISIONS`); list them with `--detail revisions` above. To compare anyway, make reviewed copies first, explicitly accept or reject revisions there, then compare:

```powershell
aspose-cli words compare original.docx changed.docx --out redline.docx --author "Legal Review" --output json
```

The redline's revisions are attributed to `--author`, or to `Aspose CLI`
without it.

The result counts the revisions by type and lists up to 50 `samples`, one per
revision run or paragraph mark, with the `type` names `--detail revisions`
uses; a paragraph mark's sample has no `text`.

Editing a signed document invalidates its signature and emits `SIGNATURE_INVALIDATED`; the resulting file must be reviewed and re-signed.
