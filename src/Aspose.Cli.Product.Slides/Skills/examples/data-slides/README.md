# Build data slides

Copy this Skill to a fresh writable directory, then change into
examples/data-slides. [metrics.md](metrics.md) creates two synthetic slides;
[data-ops.json](data-ops.json) adds a native column chart to the second slide.

```powershell
aspose-cli slides create metrics.pptx --from-markdown metrics.md --template ../../assets/templates/default-16x9.pptx --output json
aspose-cli slides edit metrics.pptx --ops data-ops.json --out metrics.review.pptx --output json
aspose-cli slides query slides metrics.review.pptx --scope shapes --output json
aspose-cli review metrics.review.pptx --out metrics.review --output json
```

The chart shows North, South, East and West revenue of 120, 90, 75 and 60
synthetic units. Native chart series and categories are not included in the
read/search projection; check rendered labels and values against
`data-ops.json`. Open every review image and disclose evaluation output.
