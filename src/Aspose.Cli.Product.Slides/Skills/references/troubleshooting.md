# Slides troubleshooting

The error envelope, exit codes, not-found details and general diagnosis are shared: see
`aspose-cli docs troubleshooting`.

- `SLIDE_NOT_FOUND`, `SHAPE_NOT_FOUND`, `PLACEHOLDER_NOT_FOUND`, `LAYOUT_NOT_FOUND`: pick a name
  from `details.available`. `slides query slides --scope shapes` lists each shape's `shapeId`,
  `shapeName` and `placeholder`; `slides inspect --detail layouts` lists layout names.
- `OPS_INVALID` for a shape name or placeholder role that several shapes on the slide share:
  address the shape by its `shapeId`.
- `CHART_DATA_INVALID`: the chart type or data source is outside what `update_chart_data`
  supports (Charts in [Slides editing](editing.md)); recreate the chart with `insert_chart`.
- `REMOTE_RESOURCES_BLOCKED`: a linked picture, linked media file or external chart workbook
  was left out of the output. Network addresses are never fetched; only ordinary files beneath
  the presentation's directory are read. Embed the media, or place the file beside the deck and
  link it by relative path, then review the incomplete rendering.
- A template-based deck looks wrong: check which masters and layouts were kept and whether
  `append_presentation` used `keep-source` or `use-dest` as its `masterPolicy`.
  `use-dest` and `apply_layout` replace a slide's own background with its layout's, and a
  `SLIDE_BACKGROUND_RESET` warning names the slides that had one; a later `set_background`
  sets one again.
- Rendering fails: run `aspose-cli fonts check deck.pptx --output json`, render fewer slides,
  and look for malformed embedded media.
