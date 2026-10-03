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

## Appearance

```powershell
aspose-cli review deck.revised.pptx --out deck.review-1 --output json
```

The Slides checks (`SLIDES_*` in `capabilities` under `review.checks`) flag shapes outside the
slide, text below 12 pt, overlapping shapes, covered charts, laid-out text running into a table
or chart (`SLIDES_TEXT_OVERLAPS_OBJECT`, judged from where the text actually sits, not from its
often much taller placeholder), laid-out text cut off by a slide edge (`SLIDES_TEXT_OUTSIDE_SLIDE`,
for example a long title in a bottom-anchored placeholder that wraps upward off the slide) or
spilling out of a shape that does not grow to fit it (`SLIDES_TEXT_OVERFLOWS_SHAPE`), empty
placeholders that PowerPoint shows as prompts while editing (`SLIDES_PLACEHOLDER_EMPTY`), blank or
duplicate slides and content density (judged by object count alone on a slide whose text
evaluation mode replaced). Text in rotated shapes and vertical text is not measured.
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
