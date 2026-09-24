# Slides verification

Every `slides edit` reopens its output before publishing it. Verification of
content and appearance is a separate, explicit step.

## Content

```powershell
aspose-cli slides inspect deck.revised.pptx --preview --detail layouts fonts notes sections properties --output json
aspose-cli slides query slides deck.revised.pptx --slides 1- --scope full --notes --output json
aspose-cli slides query search deck.revised.pptx --pattern TODO --scope all --output json
```

Check `contentTruncated` on bounded reads. Table cells, group children and
SmartArt nodes are part of their shape's text; chart titles, labels and series are
not, so confirm them visually.

## Appearance

```powershell
aspose-cli review deck.revised.pptx --out deck.review-1 --output json
```

Open every image under the review directory, one by one. `review.json` lists
findings (outside shapes, small text, overlaps, blank or duplicate slides) and
`coverage.complete`; when coverage is incomplete, say so. Check clipping,
overflow, contrast, missing glyphs, distorted images, chart categories, table
legibility, footer placement, hidden slides and ordering.

Fix and review again into a new directory, for at most three rounds, then
report any remaining defects.

Fonts delivered beside the deck, such as brand fonts on a machine without
them, reach the engine only through `--font-dir`, which adds to the system
fonts. Pass the same directories to `fonts check`, `review`, and
`slides render`, `convert`, `create` and `edit` (saving shrinks text that
auto-fits its shape with the fonts' metrics), so the delivered output uses the
fonts the check saw:

```powershell
aspose-cli fonts check deck.revised.pptx --font-dir fonts --output json
aspose-cli slides convert deck.revised.pptx --to pdf --font-dir fonts --output json
```

## Evaluation

Evaluation output carries `EVAL_MODE` and may replace or truncate text. Claim
licensed results only when the CLI reports Slides as licensed.

`slides render` and PNG/JPEG conversion export images for delivery (192 DPI by
default; a 720-by-405-point slide becomes 1920 by 1080 pixels). `slides render`
takes the format from `--to`, or from the `--out` extension when `--to` is
omitted, and refuses a `--to` that disagrees with the `--out` extension.

## Existing chart fidelity

An unrelated edit still passes the presentation through the native SDK's full
save path. The pinned Aspose.Slides SDK can change an untouched chart's
automatic title layout, axis scale and color behavior during a plain load/save
(a known SDK defect). Inspect
existing native charts independently in PowerPoint before publishing a revised
template. Successful reopen and review coverage do not establish unchanged
appearance. The CLI does not rewrite imported chart defaults to hide this SDK
limitation.
