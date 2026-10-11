# Build a deck from an outline

Copy this Skill to a fresh writable directory, then change into
examples/deck-from-outline. [notes.md](notes.md) contains a synthetic quarterly update.

```powershell
aspose-cli slides create qbr.pptx --from-markdown notes.md --output-format json
aspose-cli slides inspect qbr.pptx --sample --detail layouts fonts notes --output-format json
aspose-cli review qbr.pptx --out qbr.review --output-format json
```

The result has three slides: Quarterly update, Highlights and Next steps.
The built-in design supplies the 16:9 size, theme and layout geometry.
Open every image the review lists before delivery and disclose evaluation output.
