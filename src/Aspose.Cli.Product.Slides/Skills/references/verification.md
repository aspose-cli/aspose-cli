# Slides verification

`slides edit --verify` is opt-in and cannot be combined with `--dry-run`.
It reopens the saved presentation and renders at most 12 surviving touched
slides, or the first slide when none were touched. Check `verification.ok`,
`verification.issues`, `verification.readBackSlideIds` and `verification.renders`
for the actual outcome and coverage; `verification.visualReviewRequired` marks
incomplete coverage. Verification runs after save and does not replace read-back
or visual inspection.

Continue with bounded read-back and renders:

```powershell
aspose-cli slides inspect deck.revised.pptx --preview --detail masters layouts fonts notes sections properties --output json
aspose-cli slides query slides deck.revised.pptx --slides 1- --scope full --notes --output json
aspose-cli slides query search deck.revised.pptx --pattern TODO --scope all --output json
aspose-cli slides render deck.revised.pptx --all-slides --to png --width 1600 --out review.png --output json
```

Inspect every produced image. Check clipping, overflow, contrast, missing
glyphs, distorted images, chart categories, table legibility, footer placement,
hidden slides and ordering. Repeat after corrections, for at most three visual
correction rounds, then report any remaining defects.

Evaluation output must disclose `EVAL_MODE`; input text may also be replaced or
truncated. Licensed results may be claimed only when the CLI reports the Slides
product as licensed.

PNG and JPEG conversion use the same 192 DPI default as `slides render` (a
720-by-405-point slide becomes 1920 by 1080 pixels). Use `slides render` with
`--width` or `--dpi` when a different raster size is required. Both paths check
single-image and total-batch pixel budgets before publishing any output files.