# Slides verification

The delivery checklist and review protocol are shared: see `aspose-cli docs verification`.
This page covers what is specific to presentations.

## Read-back of an edit

`slides edit --verify` reads the staged output back and reports `verification` for the slides
each operation addressed when it ran; `--help` for `--verify` lists what it reads back, body
paragraphs with their `list` included. The
slide order covers `insert_slide`, `delete_slides`, `move_slide`, `copy_slide` and
`import_slides`; for `replace_text` it compares how often the find text and the replacement
occur in the operation's scope with what the operation did. `format_shape`, `set_shape_bounds`,
`set_background`, `delete_shape`, the inserted pictures, shapes, tables and charts,
`set_table_cell` and `set_chart_data` are not read back: check them with the content reads and
the review below.

`checkedOps` lists only the operations whose effect was read back, whether or not the output
holds it (each effect it does not hold is an issue). An operation a later one of the batch
changed again is left out, except for the slide order and the sections: their expectations
build on each other, so an earlier `insert_slide`, `import_slides`, `move_slide` or
`insert_section` is checked with the latest one. An empty list with `ok: true` means nothing was
read back, not that every operation was confirmed. In evaluation mode no text is read back.

## Content

```powershell
aspose-cli slides inspect deck.revised.pptx --sample --detail layouts --detail fonts --detail notes --detail sections --detail properties --output-format json
aspose-cli slides read deck.revised.pptx --slides 1- --content full --notes --output-format json
aspose-cli slides search deck.revised.pptx --pattern TODO --scope all --output-format json
```

### Charts and tables

`slides read --content full` reports each table's `rowCount`, `columnCount` and cell `values`
by row, and each chart's `chart` with its kind, title, categories and series values, under
the names `insert_table`, `set_table_cell`, `insert_chart` and `set_chart_data` write; the
read schema (`aspose-cli schema v2/slides/read-result`) describes every field. A Markdown
conversion reads both back too, each table as a Markdown table and each chart as a table of
its values. Each slide starts with `<!-- slide N -->`, where N is the slide's number in the
deck, also for a `--slides` selection:

```powershell
aspose-cli slides convert deck.revised.pptx --to md --slides 4-6 --out deck.revised.md --output-format json
```

Compare these values with the ones the batch wrote before you report them. In evaluation mode
titles and table cells come back truncated; chart values stay whole. The review images remain
the check for how a chart draws its values: axis ranges, labels and clipping.

## Appearance

```powershell
aspose-cli review deck.revised.pptx --out deck.review-1 --output-format json
```

`aspose-cli capabilities slides` lists the Slides checks under `review.checks`, each with its
code, severity and summary. What the summaries do not say:

- Text checks (`SLIDES_TEXT_OVERLAPS_OBJECT`, `SLIDES_TEXT_OUTSIDE_SLIDE`,
  `SLIDES_TEXT_OVERFLOWS_SHAPE`) judge the text from where it actually sits, not from its often
  much taller placeholder; a long title in a bottom-anchored placeholder that wraps upward off
  the slide is cut off by its edge. Text in rotated shapes and vertical text is not measured.
- `SLIDES_TEXT_LOW_CONTRAST` compares the text color with the one solid color behind it: the
  shape's own fill, a filled shape beneath it on the slide or in its layout's or master's art,
  or the slide background. A chart is judged by the text color it states; text over a picture,
  a gradient or a filled shape covering less than half of it is not judged.
- Content density is judged by object count alone on a slide whose text evaluation mode
  replaced. `SLIDES_EVALUATION_WATERMARK` is reported in any license mode, and the watermark box
  is left out of the other checks.
- A finding names each shape with its `shapeId`, which edit operations address together with
  the slide in its `location`, such as `slide/3`.

After fixing one kind of finding, a later round can focus on it while you still open every image:

```powershell
aspose-cli review deck.revised.pptx --out deck.review-2 --code SLIDES_SHAPE_OUTSIDE_SLIDE --code SLIDES_TEXT_TOO_SMALL --output-format json
```

Checks do not see everything. In every image, look at title hierarchy, text in rotated shapes,
text in tables, chart categories and labels, table legibility, image crops and distortion,
footer and slide-number placement, and hidden slides.

## Fonts

Saving shrinks text that auto-fits its shape using the fonts' metrics, so pass the same
`--font-dir` to `slides create`, `edit`, `convert` and `render` as to `fonts check` and `review`.

## Existing chart fidelity

Every edit and conversion passes the presentation through the SDK, which can change how a
chart's automatic title sits over its plot (SLIDES-CHART-TITLE). A `CHART_TITLE_OVERLAID`
warning locates each affected chart: inspect those charts in PowerPoint and restore their title
layout there before publishing a revised template; a successful reopen and complete review
coverage do not establish unchanged appearance.
