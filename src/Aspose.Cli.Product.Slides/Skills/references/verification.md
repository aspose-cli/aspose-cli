# Slides verification

The delivery checklist and review protocol are shared: see `aspose-cli docs verification`.
This page covers what is specific to presentations.

## Read-back of an edit

`slides edit --verify` reads the staged output back and reports `verification` for the slides
each operation addressed when the batch started. It reads back the slide order (`insert_slide`,
`delete_slides`, `move_slide`, `copy_slide`, `import_slides`), titles, shape text, body
paragraphs with their `list`, speaker notes, layouts, hidden slides, footers,
transitions, sections, document properties, the slide size, and for `replace_text` how often
its find text and replacement occur in its scope. `format_shape`, `set_shape_bounds`,
`set_background`, `delete_shape`, the inserted pictures, shapes, tables and charts,
`set_table_cell` and `set_chart_data` are not read back: check them with the content reads and
the review below. `checkedOps` lists only the operations whose effect was read back, and an
operation a later one of the batch changed again is left out, except for the slide order and
the sections: their expectations build on each other, so an earlier `insert_slide`,
`import_slides`, `move_slide` or `insert_section` is checked with the latest one. An empty list with `ok: true`
means nothing was read back, not that every operation was confirmed. In evaluation mode no text
is read back.

## Content

```powershell
aspose-cli slides inspect deck.revised.pptx --sample --detail layouts fonts notes sections properties --output-format json
aspose-cli slides read deck.revised.pptx --slides 1- --content full --notes --output-format json
aspose-cli slides search deck.revised.pptx --pattern TODO --scope all --output-format json
```

`--max-chars` bounds all returned text, including paragraphs, table cells, chart labels and notes; addressing and
formatting metadata do not count.

### Charts and tables

`slides read --content full` reports each table's `rowCount`, `columnCount` and cell `values`
by row, and each chart's `chart` with its kind, title, categories and series values, under
the names `insert_table`, `set_table_cell`, `insert_chart` and `set_chart_data` write; the
read schema (`aspose-cli schema v2/slides/read-result`) describes every field. A Markdown
conversion reads both back too, each table as a Markdown table and each chart as a table of
its values:

```powershell
aspose-cli slides convert deck.revised.pptx --to md --slides 4-6 --out deck.revised.md --output-format json
```

Compare these values with the ones the batch wrote before you report them. In evaluation mode
titles and table cells come back truncated; chart values stay whole. The review images remain
the check for how a chart draws its values:
axis ranges, labels and clipping.

## Appearance

```powershell
aspose-cli review deck.revised.pptx --out deck.review-1 --output-format json
```

The Slides checks (`SLIDES_*` in `capabilities` under `review.checks`) flag shapes outside the
slide, text below 12 pt, overlapping shapes, covered charts, laid-out text running into a table
or chart or hidden by an opaque shape in front of it, however small (`SLIDES_TEXT_OVERLAPS_OBJECT`, judged from where the text actually sits, not from its
often much taller placeholder), laid-out text cut off by a slide edge (`SLIDES_TEXT_OUTSIDE_SLIDE`,
for example a long title in a bottom-anchored placeholder that wraps upward off the slide) or
spilling out of a shape that neither grows to fit it nor shrinks it on overflow
(`SLIDES_TEXT_OVERFLOWS_SHAPE`), text whose color has a contrast below 3:1 with the one solid
color behind it: the shape's own fill, a filled shape beneath it on the slide or in its
layout's or master's art, or the slide background (`SLIDES_TEXT_LOW_CONTRAST`, for example a
dark template title on a slide given a dark background, or white text merged onto a white
slide; a chart is judged by the text color it states, and text over a picture, a gradient or
a filled shape covering less than half of it is not judged), empty
placeholders that PowerPoint shows as prompts while editing (`SLIDES_PLACEHOLDER_EMPTY`), blank or
duplicate slides and content density (judged by object count alone on a slide whose text
evaluation mode replaced), and a slide that carries the watermark text box of a save without a
license (`SLIDES_EVALUATION_WATERMARK`, in any license mode; the box is left out of the other
checks). Text in rotated shapes and vertical text is not measured. A
finding names each shape with its `shapeId`, which edit operations address together with the
slide in its `location`.
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

Every edit and conversion passes the presentation through the SDK. It turns a chart's implicit
automatic title into one drawn over the plot, which enlarges the plot area and can change its
automatic axis scale; a `CHART_TITLE_OVERLAID` warning locates each affected chart. Inspect
those charts in PowerPoint before publishing a revised template; a successful reopen and
complete review coverage do not establish unchanged appearance.
