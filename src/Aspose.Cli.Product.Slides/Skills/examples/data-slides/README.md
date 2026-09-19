# Build data slides

Create a deck from an outline, add a chart or table with one atomic edit batch,
and check the saved shapes and their appearance:

```powershell
aspose-cli slides create metrics.pptx --from-markdown metrics.md --template default-16x9.pptx --output json
aspose-cli slides edit metrics.pptx --ops data-ops.json --out metrics.review.pptx --output json
aspose-cli slides query slides metrics.review.pptx --scope shapes --output json
aspose-cli review metrics.review.pptx --out metrics.review --output json
```

Keep series and categories bounded. Prefer one clear visual claim per slide.

The slide query confirms chart and table shapes. Native chart series, categories
and table-cell text are not included in the read/search projection; check their
rendered labels and values against the source data in the review images.
