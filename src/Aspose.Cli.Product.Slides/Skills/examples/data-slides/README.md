# Build data slides

Copy this Skill to a fresh writable directory, then change into
examples/data-slides. [metrics.md](metrics.md) creates two synthetic slides;
[data-ops.json](data-ops.json) adds a native column chart to the second slide.

```powershell
aspose-cli slides create metrics.pptx --from-markdown metrics.md --output json
aspose-cli slides edit metrics.pptx --ops data-ops.json --out metrics.review.pptx --output json
aspose-cli slides query slides metrics.review.pptx --scope shapes --output json
aspose-cli slides convert metrics.review.pptx --to md --slides 2 --out metrics.review.md --output json
aspose-cli review metrics.review.pptx --out metrics.review --output json
```

The chart shows North, South, East and West revenue of 120, 90, 75 and 60
synthetic units. Native chart series and categories are not included in the
read/search projection; `metrics.review.md` lists the chart's values by category
and series, so compare them with `data-ops.json`, then check the rendered labels
and bars. Open every review image and disclose evaluation output.
