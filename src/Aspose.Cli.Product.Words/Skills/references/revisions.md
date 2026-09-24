# Revisions and comparison

Before editing, inspect `document.revisionsPresent`, `document.revisionAuthors`,
`document.protection` and `document.signed` in the `words inspect` result.
Editing a document that already has revisions reports `TRACKED_CHANGES_PRESENT`;
disclose them.

Use `--track-changes --author "Name"` when the requested edit must remain reviewable.
`set_table_cell` supports tracked replacement while retaining the cell's structure;
accept or reject revisions only when that review decision is explicitly requested.

`words compare` refuses inputs that already contain revisions. Make reviewed copies first, explicitly accept or reject revisions there, then compare:

```powershell
aspose-cli words compare original.docx changed.docx --out redline.docx --output json
```

Editing a signed document invalidates its signature and emits `SIGNATURE_INVALIDATED`; the resulting file must be reviewed and re-signed.
