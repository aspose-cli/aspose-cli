# Slides troubleshooting

The error envelope, exit codes, not-found details and general diagnosis are shared: see
`aspose-cli docs troubleshooting`.

- `LAYOUT_NOT_FOUND`: `slides inspect --detail layouts` lists layout names.
- `OPS_INVALID` for a shape name or placeholder role that several shapes on the slide share:
  address the shape by its `shapeId`.
- `CHART_DATA_INVALID`: the message names the cause; only a chart that cannot be updated in
  place needs `insert_chart` (Charts in [Slides editing](editing.md)).
- `REPLACE_NO_MATCH`: `replace_text` found nothing in its `scope` and changed nothing. Run
  `slides query search` with the same pattern and scope; chart text and alternative text are
  never matched.
- `REMOTE_RESOURCES_BLOCKED`: a linked picture, linked media file or external chart workbook
  was left out of the output. Network addresses are never fetched; only ordinary files beneath
  the presentation's directory are read. Embed the media, or place the file beside the deck and
  link it by relative path, then review the incomplete rendering.
- A template-based deck looks wrong: check which masters and layouts were kept and whether
  `append_presentation` used `keep-source` or `use-dest` as its `masterPolicy`.
  `use-dest` and `apply_layout` replace a slide's own background with its layout's, and a
  `SLIDE_BACKGROUND_RESET` warning names the slides that had one; `set_background` sets one
  again, for appended slides in a later batch.
- Rendering fails: run `aspose-cli fonts check deck.pptx --output json`, render fewer slides,
  and look for malformed embedded media.
