# Slides troubleshooting

- `SLIDE_NOT_FOUND`, `SHAPE_NOT_FOUND`, `PLACEHOLDER_NOT_FOUND` and
  `LAYOUT_NOT_FOUND` list the available slide ids, shape ids or names,
  placeholder roles or layout names in `details.available` and the closest ones
  in `details.suggestions`; a slide number past the end reports
  `details.availableCount`. `slides query slides --scope shapes` lists each
  shape's id, name and role.
- A shape name or placeholder role that several shapes on the slide share is
  refused with `OPS_INVALID`; address the shape by its `shape` id.
- If rendering fails, run `fonts check` for the Slides product, reduce the
  selected slide set and inspect the source for malformed embedded media.
- If a template-based deck looks wrong, confirm which masters and layouts were
  retained and whether append operations used `keep-source` or `use-dest`.
- If evaluation text is replaced, do not treat inspection or conversion as
  complete; apply a valid Slides or Total license and retry.
- `REMOTE_RESOURCES_BLOCKED`: a linked picture, linked media file or external
  chart workbook was left out of the output. Network addresses are never
  fetched; only ordinary files beneath the presentation's directory are read.
  Embed the media, or place the file beside the deck and link it by relative
  path, then review the incomplete render.
- `OUTPUT_EXISTS` from `slides extract`: a file the command would write already
  exists in `--out-dir`, and nothing was published. `--overwrite` replaces the
  files the command writes; other files in the directory stay.
- If preview reload fails, fix or restore the source file. The browser keeps
  serving the last good snapshot until a valid save succeeds.

```powershell
aspose-cli doctor --output json
aspose-cli fonts check deck.pptx --output json
aspose-cli license status --output json
```
