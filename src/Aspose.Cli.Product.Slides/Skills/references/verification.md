# Slides verification

The delivery checklist and review protocol are shared: see `aspose-cli docs verification`.
This page covers what is specific to presentations.

## Content

```powershell
aspose-cli slides inspect deck.revised.pptx --preview --detail layouts fonts notes sections properties --output json
aspose-cli slides query slides deck.revised.pptx --slides 1- --scope full --notes --output json
aspose-cli slides query search deck.revised.pptx --pattern TODO --scope all --output json
```

`--max-chars` bounds all returned title, text, run, note and comment text; addressing and
formatting metadata do not count. Check each slide's `contentTruncated`; `window.next` resumes
at a slide the budget cut short and doubles `--max-chars` when that slide alone exceeded it.

### Charts and tables

`query` reports a chart only as a shape and a table's cells as one text in reading order. A
Markdown conversion reads both back by position: each table as a Markdown table, row by row
and column by column, and each chart as its type, its title and a table of its values, one row
per category and one column per series:

```powershell
aspose-cli slides convert deck.revised.pptx --to md --slides 4-6 --out deck.revised.md --output json
```

Compare these values with the ones the batch wrote before you report them. Evaluation mode
cuts short text longer than five characters here too, such as titles and table cells, while
chart values stay whole. The review images remain the check for how a chart draws its values:
axis ranges, labels and clipping.

## Appearance

```powershell
aspose-cli review deck.revised.pptx --out deck.review-1 --output json
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
evaluation mode replaced). Text in rotated shapes and vertical text is not measured. A
finding names each shape with its `shapeId`, which edit operations address together with the
slide in its `location`.
After fixing one kind of finding, a later round can focus on it while you still open every image:

```powershell
aspose-cli review deck.revised.pptx --out deck.review-2 --code SLIDES_SHAPE_OUTSIDE_SLIDE --code SLIDES_TEXT_TOO_SMALL --output json
```

Checks do not see everything. In every image, look at title hierarchy, text in rotated shapes,
text in tables, image crops and distortion, chart categories and labels, table legibility, footer
and slide-number placement, contrast, missing CJK glyphs, hidden slides and ordering.

## Fonts

Saving shrinks text that auto-fits its shape using the fonts' metrics, so pass the same
`--font-dir` to `slides create`, `edit`, `convert` and `render` as to `fonts check` and `review`:

```powershell
aspose-cli fonts check deck.revised.pptx --font-dir fonts --output json
aspose-cli slides convert deck.revised.pptx --to pdf --font-dir fonts --output json
```

## Existing chart fidelity

Every edit and conversion passes the presentation through the SDK. It turns a chart's implicit
automatic title into one drawn over the plot, which enlarges the plot area and can change its
automatic axis scale; a `CHART_TITLE_OVERLAID` warning locates each affected chart. Inspect
those charts in PowerPoint before publishing a revised template; a successful reopen and
complete review coverage do not establish unchanged appearance.
