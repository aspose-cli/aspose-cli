# Words live preview

For normal agent work, start the shared managed lifecycle:

```
aspose-cli preview contract.docx --open --output json
aspose-cli preview status --output json
aspose-cli preview stop <id> --output json
```

Content detection routes supported Word-processing input to Words, whose
product adapter selects the `document` view. The result identifies `product: "words"`; `status` and `stop`
operate across all file products in the same current-user CLI configuration.
Use `--product words` to choose Words explicitly for a supported input. Passwords use the standard `--password-env` or
`--password-stdin` sources and never enter the result or session marker.

The human browser shell is document-specific: it exposes pages, page
navigation, zoom, live edit progress, and affected-page emphasis. It never
shows spreadsheet formula or worksheet controls. Agents should not scrape
that dynamic shell; use the static `words query blocks`, `words render`, comparison,
and verification outputs as evidence.

The browser view is for a human; delivery verification still requires
read-back, semantic comparison when relevant, and inspection of rendered
verification pages.

Run `preview` again after changing a license. A matching session is reused only
when the applied license identity also matches. A valid change of source, path
or license contents restarts it with a new `id` and `pid` and `reused: false`,
keeping the URL unless a different port is requested. An invalid selected
license is rejected before the existing session is stopped.
