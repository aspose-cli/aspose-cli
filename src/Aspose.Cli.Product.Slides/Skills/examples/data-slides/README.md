# Build data slides

Create a deck from an outline, add a chart or table with one atomic edit batch,
and verify the saved data labels and rendering:

```powershell
aspose-cli slides create metrics.pptx --from-markdown metrics.md --size 16x9 --output json
aspose-cli slides edit metrics.pptx --ops data-ops.json --out metrics.review.pptx --verify --output json
aspose-cli slides query search metrics.review.pptx --pattern Revenue --scope shapes --output json
aspose-cli slides render metrics.review.pptx --all-slides --to png --width 1600 --out metrics.png --output json
```

Keep series and categories bounded. Prefer one clear visual claim per slide.
