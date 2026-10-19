# Slides editing

Batch semantics (atomic by default, `--best-effort`, `--dry-run`, output and backups) and field
discovery are shared: see `aspose-cli docs editing`. Each operation's fields, types, defaults
and allowed values come from its generated schema:

```powershell
aspose-cli schema v2/slides/ops --operation insert_chart
```

## Addressing within a batch

- Operations run in order, and each one's slide and shape targets resolve against the
  presentation as the earlier operations left it. A slide inserted earlier in the batch is
  addressed by its new number, so one batch can add a slide and fill it; after a move or a
  deletion, `slide` numbers count the slides as they now stand. Moving a slide keeps its
  `slideId`, so `slideId` keeps addressing the same slide wherever it moves.
- `shapeId` is persistent within its slide, not a position or a presentation-wide counter; pair
  it with that slide's `slide` or `slideId`. Reading a different slide window does not change
  ids. Read new ids after copying or importing slides.
- Shape selectors address top-level shapes; a group is one shape and its children are not
  addressed separately. A `shapeName` or `placeholder` role that several shapes on the slide
  share is refused; use the `shapeId`.

An outcome's `targets` (`aspose-cli docs editing`) address slides, shapes, notes, sections,
properties and the slide size: `slide/3`, `slide/3/shape/7`, `slide/3/notes`, `section/Intro`,
`property/title`, `presentation/slide-size`. `slidesTouched` lists the numbers of the
output's changed slides. A section's address is its name as `slides inspect --detail sections`
reports it, with only a slash, a percent sign, a control character and white space at either
end written as `%XX` of their UTF-8 bytes: `section/Q3 Results`, `section/业绩回顾`,
`section/Plan%2FBudget`.

## Operations by task

| Task | Operations |
| --- | --- |
| Slide order and structure | `insert_slide`, `copy_slide`, `move_slide`, `delete_slides`, `insert_section`, `import_slides` |
| Layout and canvas | `set_layout`, `set_slide_size`, `set_background`, `set_footer`, `set_transition`, `set_slide_hidden` |
| Text | `set_title`, `set_body`, `set_text`, `replace_text`, `set_table_cell`, `set_notes` |
| Objects | `insert_image`, `insert_shape`, `insert_table`, `insert_chart`, `delete_shape`, `set_shape_bounds`, `format_shape` |
| Data | `set_chart_data` |
| Document | `set_properties` |

Inserted shapes, pictures, tables and charts are named by their geometry or kind and their
shapeId, such as `rectangle 5`. `set_shape_bounds` moves or resizes a shape in place, keeping its id, name,
style and text; omitted sides keep their values. Use it when a review finding asks to move or
enlarge a shape. `insert_table` reports a table whose rows grow past its `rect` as a
`TABLE_OVERFLOW` warning; `format_shape` on a table styles every cell, so a smaller `fontSize`
lets a long table fit. The warning measures the table as `insert_table` left it, so it stays
when a later operation of the same batch shrinks the text; review the slide instead. On a chart, `format_shape` styles the title, legend, axis and
data-label text. A picture has no text, so a text style on it fails with `OPS_INVALID`.
The `format_shape` `line` is only the solid outline color; no operation sets an outline's
width, dash or transparency.

No operation adds, changes or deletes comments: `slides read --content full` reports the
comments a deck already has.

## Text and notes

- `set_title`, `set_body` and `set_text` fill placeholders the layout already styles; prefer
  them to `format_shape`, which overrides every run of one shape.
- `format_shape` `font` names one font for Latin, East Asian and complex-script text;
  `latinFont` and `eastAsianFont` set the font of their script in its place. `slides read
  --content full` reports a `font` given alone as each run's `font`; given with `latinFont`, it
  reads back as `complexScriptFont`, while `font` reports the Latin font. To return text to the
  template's fonts, see [design system](design-system.md).
- A `set_body` paragraph with a `list` is an item at the list's `level`, which starts at 1 for the top level, with the bullet the
  placeholder gives that level (a bullet of its own where the level has none), or numbered with
  `"kind": "number"`; a paragraph without `list` is
  plain, as a Markdown paragraph is. `slides read --content full` reports each paragraph's
  `list` the same way. `set_text` keeps the placeholder's bullets on every line.
- A shape's text in `slides read`, `slides search` and `extract --what text` includes its table
  cells, group children and SmartArt nodes. Extracted text and notes put each paragraph on its
  own line; lines are separated by line feeds. `extract --what text` holds neither a shape's
  alternative text nor a chart's text: `slides read --content shapes` reports alternative
  text as each shape's `altText`, and `--content full` reports each chart's data.
- `replace_text` matches within one paragraph at a time, in shapes and speaker notes. Only the
  matched characters change: the replacement takes the formatting of the first matched
  character, and other runs keep theirs. Its `itemsAffected` counts the shapes and notes whose
  text changed, not the replacements made in them. To check the replacements, run the edit
  with `--verify`, which compares how often the find text and the replacement occur in the
  output with what the operation did. In evaluation mode a paragraph that evaluation mode cut
  short is left as it is, and the result warns `EVALUATION_INPUT_TRUNCATED`.
- `set_notes` replaces speaker-note text. `inspect --detail notes` reports only presence and
  character counts; read note text with `slides read --notes` or `extract --what notes`.
- `set_footer` uses the layout's own footer, number and date placeholders; their position and
  style come from the template. A layout without them, as title layouts often hide footers,
  shows nothing, so its slides are left out of the operation's `targets`.
- `slides read --content shapes` reports each shape's `altText`, which `replace_text` does not
  reach. To replace a picture, `delete_shape` it and `insert_image` the new one at its `rect`
  with an `altText`.

## Charts

`set_chart_data` writes into the chart's embedded workbook. Existing series keep their
fills, markers, data labels and number formats; added series and points take the chart's
automatic style, and surplus categories, points and series are removed from the end. Omitted
`series` keep their values, so a categories-only update must keep the category count. Scatter
charts have no categories: pass `series` with matching `xValues` and `values`.

It supports bar, column, line, area, pie, doughnut, radar and scatter charts whose data lives
in the embedded workbook. Other charts (bubble, stock, surface, mixed scatter and category
series, external or literal data, multi-level categories) fail with `CHART_DATA_INVALID` and
stay unchanged; recreate them with `insert_chart`.

New charts reserve space for their title and legend; a new pie colors each slice and names the
categories in its legend. A new column or line chart slants its category labels by 45 degrees
when, at the chart's text size, they would leave no room between them; to keep them level,
widen the chart or shorten the categories. Their text keeps the chart style's color, unless the theme's
background color contrasts more with the slide's solid background than the theme's text color
does: then a chart on a dark slide gets that light text. `format_shape` restyles it. Adding a second series to a chart without a legend creates one
outside the plot. For non-negative bar and column data, the automatic value
axis starts at zero, saved as a fixed minimum of `0`. Later data updates keep every stored axis
limit, so new negative values or values beyond a fixed range can be clipped: adjust the axes in
a presentation editor or recreate the chart, then review the rendering. A successful update
does not establish that every value is visible.

## Media

This build cannot insert audio or video or render MP4. `extract --what media --slides 2-3`
writes the media those slides show (pictures, picture fills, backgrounds, audio and video, not
master or layout art); each item keeps its presentation-wide `index`.

## Outputs and conversion

- Only PPTX and PPTM outputs can carry a password; `--encrypt-env` with any other `create`,
  `edit` or `convert` output is `OPTION_INVALID`. Without it, a PPTX or PPTM output of an
  encrypted deck keeps its password, and any other document output is written without one and
  warns `ENCRYPTION_REMOVED`; the slide images `render` writes do not warn.
- A `convert` without `--slides` to a format other than PPT, PPTX, PPTM or ODP leaves out the
  hidden slides, as PowerPoint's export does, and warns `HIDDEN_SLIDES_SKIPPED` with their
  numbers; the hidden slides `--slides` names are converted. `render` and `review` draw hidden
  slides and warn `HIDDEN_SLIDES_RENDERED` with their numbers: unhide them with
  `set_slide_hidden`, or leave them out of what you report as the deck.
- An ODP output turns native charts into pictures and drops slide transitions and sections, and
  warns `LOSSY_CONVERSION` (SLIDES-ODP-NATIVE-CONTENT); keep the PPTX for further edits.
- A PDF or other export holds the slides alone, with no handout or notes-page layout, so
  speaker notes are not in it; read them with `slides read --notes` or
  `slides extract --what notes`.
