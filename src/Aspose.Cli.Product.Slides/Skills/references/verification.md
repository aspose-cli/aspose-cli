# Slides verification

Every `slides edit` reopens its output before publishing it. Verification of
content and appearance is a separate, explicit step.

## Content

```powershell
aspose-cli slides inspect deck.revised.pptx --preview --detail layouts fonts notes sections properties --output json
aspose-cli slides query slides deck.revised.pptx --slides 1- --scope full --notes --output json
aspose-cli slides query search deck.revised.pptx --pattern TODO --scope all --output json
```

Check `contentTruncated` on bounded reads. Chart series and table-cell text are
not part of the read projection; confirm them visually.

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

## Evaluation

Evaluation output carries `EVAL_MODE` and may replace or truncate text. Claim
licensed results only when the CLI reports Slides as licensed.

`slides render` and PNG/JPEG conversion export images for delivery (192 DPI by
default; a 720-by-405-point slide becomes 1920 by 1080 pixels).
