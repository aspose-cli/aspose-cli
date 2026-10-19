# Slides troubleshooting

The error envelope, exit codes, not-found details and general diagnosis are shared: see
`aspose-cli docs troubleshooting`. Every Slides code, with its exit code, is listed by
`aspose-cli capabilities slides`; this page says what to do about the ones a Slides edit
commonly meets.

A layout name an operation does not find fails with `LAYOUT_NOT_FOUND` and the presentation's
layout names in `details.available`; `slides inspect --detail layouts` lists them with their
masters.

A chart that cannot be updated in place fails with `CHART_DATA_INVALID`, whose message names
the cause; only then recreate it with `insert_chart` (Charts in [Slides editing](editing.md)).

When `replace_text` changes nothing, its `OP_NO_EFFECT` warning has `details.reason`
`no-match`: run `slides search` with the same pattern and scope. Chart text and alternative
text are never matched.

`REMOTE_RESOURCES_BLOCKED` means a linked picture, linked media file or external chart workbook
was left out of the output. Network addresses are never fetched; only ordinary files beneath
the presentation's directory are read. Embed the media, or place the file beside the deck and
link it by relative path, then review the incomplete rendering.

When a template-based deck looks wrong, check which masters and layouts were kept and whether
`import_slides` used `keep-source` or `use-destination` as its `formatting`.
`use-destination` and `set_layout` replace a slide's own background with its layout's, and a
`SLIDE_BACKGROUND_RESET` warning names the slides that had one; `set_background` sets one
again, for imported slides in a later batch.

## Evaluation mode

What every product shares is in `aspose-cli docs licensing`. In Slides:

- Reads cut text longer than five characters short, so reads, reviews and `extract` of text or
  notes warn `EVALUATION_INPUT_TRUNCATED`. A Markdown conversion holds a truncation notice in
  place of the cut text, which its `EVALUATION_MODE` message says without naming that text; the
  presentations, PDFs and images evaluation mode saves keep the full text.
- An edit that writes text (`set_title`, `set_body`, `set_text`, `set_table_cell`, `set_notes`)
  where the text then reads cut short counts it as changed and warns
  `EVALUATION_INPUT_TRUNCATED`, even when it wrote the same text again, since the cut read cannot
  show the difference.
- Each evaluation save adds another watermark text box to every slide, which reads list as
  content. A review reports each slide that carries one as `SLIDES_EVALUATION_WATERMARK`,
  licensed or not, and a licensed command whose output keeps them warns
  `EVALUATION_INPUT_MARKED`. A license does not remove them, so rebuild the deck with a license.
