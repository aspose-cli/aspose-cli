# Revisions and comparison

Always inspect `revisionsPresent`, revision authors, protection and signature state before editing.

Use `--track-changes --author "Name"` when the requested edit must remain reviewable. Do not accept or reject existing revisions unless explicitly requested.

`words compare` refuses inputs that already contain revisions. Make reviewed copies first, explicitly accept or reject revisions there, then compare:

```powershell
aspose-cli words compare original.docx changed.docx --out redline.docx --output json
```

Editing a signed document invalidates its signature and emits `SIGNATURE_INVALIDATED`; the resulting file must be reviewed and re-signed.
