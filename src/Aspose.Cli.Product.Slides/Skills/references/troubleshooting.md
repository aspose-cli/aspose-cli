# Slides troubleshooting

- If a slide, shape or layout is not found, rerun `slides inspect` or
  `slides query slides` and use the returned stable ids or advertised layout names.
- If rendering fails, run `fonts check` for the Slides product, reduce the
  selected slide set and inspect the source for malformed embedded media.
- If a template-based deck looks wrong, confirm which masters and layouts were
  retained and whether append operations used `keep-source` or `use-dest`.
- If evaluation text is replaced, do not treat inspection or conversion as
  complete; apply a valid Slides or Total license and retry.
- If preview reload fails, fix or restore the source file. The browser keeps
  serving the last good snapshot until a valid save succeeds.

```powershell
aspose-cli doctor --output json
aspose-cli fonts check deck.pptx --product slides --output json
aspose-cli license status --output json
```
