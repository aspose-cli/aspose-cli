# PDF live preview

For normal agent work, start the shared managed lifecycle:

```powershell
aspose-cli preview report.pdf --open --output json
aspose-cli preview status --output json
aspose-cli preview stop <id> --output json
```

The `.pdf` extension routes to PDF and selects the `pages` view. The product
shell exposes PDF pages, dimensions, navigation, zoom, render progress and
last-good failure recovery. It does not show Words headings/revisions or Cells
worksheets/formulas. Passwords use standard environment or stdin sources and
never enter result envelopes or session markers.

The browser is for human inspection; agents should rely on deterministic
`pdf query pages`, `pdf render`, `pdf query search`, and `pdf validate` output.
