# Words live preview

For normal agent work, start the shared managed lifecycle:

```
aspose-cli preview contract.docx --open --output json
aspose-cli preview status --output json
aspose-cli preview stop <id> --output json
```

The file extension routes the session to Words and the product adapter selects
the `document` view. The result identifies `product: "words"`; `status` and `stop`
operate across Cells and Words, so one lifecycle is enough for mixed work.
Use `--product words` only when an ambiguous or unconventional extension needs
an explicit override. Passwords use the standard `--password-env` or
`--password-stdin` sources and never enter the result or session marker.

The human browser shell is document-specific: it exposes pages, page
navigation, zoom, live edit progress, and affected-page emphasis. It never
shows spreadsheet formula or worksheet controls. Agents should not scrape
that dynamic shell; use the static `words query blocks`, `words render`, comparison,
and verification outputs as evidence.

The browser view is for a human; delivery verification still requires
read-back, semantic comparison when relevant, and inspection of rendered
verification pages.
