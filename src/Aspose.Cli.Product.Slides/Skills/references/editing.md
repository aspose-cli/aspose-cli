# Slides editing

Batch semantics (atomic by default, `--best-effort`, `--dry-run`, output and backups) and field
discovery are shared: see `aspose-cli docs editing`. Each operation's fields, types, defaults
and allowed values come from its generated schema:

```powershell
aspose-cli schema v2/slides/ops --operation insert_chart
```

## Addressing within a batch

- Every slide and shape target resolves against the presentation as it was before the first
  operation. Content inserted earlier in the batch cannot be addressed later in it; a slide or
  shape deleted earlier fails the operation that targets it. Moving a slide keeps its `slideId`.
- `shapeId` is persistent within its slide, not a position or a presentation-wide counter; pair
  it with that slide's `slide` or `slideId`. Reading a different slide window does not change
  ids. Read new ids after duplicating slides or appending a presentation.
- Shape selectors address top-level shapes; a group is one shape and its children are not
  addressed separately. A `shapeName` or `placeholder` role that several shapes on the slide
  share is refused; use the `shapeId`.

## Operations by task

| Task | Operations |
| --- | --- |
| Slide order and structure | `add_slide`, `duplicate_slide`, `move_slide`, `delete_slides`, `add_section`, `append_presentation` |
| Layout and canvas | `apply_layout`, `set_slide_size`, `set_background`, `set_footer`, `set_transition`, `set_slide_hidden` |
| Text | `set_title`, `set_body`, `set_text`, `replace_text`, `set_table_cell`, `set_notes` |
| Objects | `insert_image`, `insert_shape`, `insert_table`, `insert_chart`, `delete_shape`, `set_shape_bounds`, `set_shape_style` |
| Data | `update_chart_data` |
| Document | `set_properties` |

Inserted shapes, pictures, tables and charts are named by kind and shapeId, such as
`rectangle 5`. `set_shape_bounds` moves or resizes a shape in place, keeping its id, name,
style and text; omitted sides keep their values. Use it when a review finding asks to move or
enlarge a shape.

## Text and notes

- `set_title`, `set_body` and `set_text` fill placeholders the layout already styles; prefer
  them to `set_shape_style`, which overrides every run of one shape.
- A shape's text in `query slides`, `query search` and `extract --what text` includes its table
  cells, group children and SmartArt nodes. Extracted text and notes put each paragraph on its
  own line; lines are separated by line feeds.
- `replace_text` matches within one paragraph at a time, in shapes and speaker notes. Only the
  matched characters change: the replacement takes the formatting of the first matched
  character, and other runs keep theirs. Evaluation mode reads text longer than five
  characters cut short, so there `replace_text` fails with `EVALUATION_LIMIT` instead of
  matching nothing.
- `set_notes` replaces speaker-note text. `inspect --detail notes` reports only presence and
  character counts; read note text with `query slides --notes` or `extract --what notes`.
- `set_footer` uses the layout's own footer, number and date placeholders; their position and
  style come from the template. A layout without them, as title layouts often hide footers,
  shows nothing, so its slides are left out of the operation's `targets`.
- `query slides --scope shapes` reports each shape's `altText`, which `replace_text` does not
  reach. To replace a picture, `delete_shape` it and `insert_image` the new one at its `rect`
  with an `altText`.

## Charts

`update_chart_data` writes into the chart's embedded workbook. Existing series keep their
fills, markers, data labels and number formats; added series and points take the chart's
automatic style, and surplus categories, points and series are removed from the end. Omitted
`series` keep their values, so a categories-only update must keep the category count. Scatter
charts have no categories: pass `series` with matching `xValues` and `values`.

It supports bar, column, line, area, pie, doughnut, radar and scatter charts whose data lives
in the embedded workbook. Other charts (bubble, stock, surface, mixed scatter and category
series, external or literal data, multi-level categories) fail with `CHART_DATA_INVALID` and
stay unchanged; recreate them with `insert_chart`.

New charts reserve space for their title and legend; a new pie colors each slice and names the
categories in its legend. Adding a second series to a chart without a legend creates one
outside the plot. For non-negative bar and column data, the automatic value
axis starts at zero, saved as a fixed minimum of `0`. Later data updates keep every stored axis
limit, so new negative values or values beyond a fixed range can be clipped: adjust the axes in
a presentation editor or recreate the chart, then review the rendering. A successful update
does not establish that every value is visible.

## Media

This build cannot insert audio or video or render MP4. `extract --what media --slides 2-3`
writes the media those slides show (pictures, picture fills, backgrounds, audio and video, not
master or layout art); each item keeps its presentation-wide `index`.
