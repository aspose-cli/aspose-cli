# Build a deck from an outline

Copy this Skill to a fresh writable directory, then change into
examples/deck-from-outline. [notes.md](notes.md) contains a synthetic quarterly update.

```powershell
aspose-cli slides create qbr.pptx --from-markdown notes.md --template ../../assets/templates/default-16x9.pptx --output json
aspose-cli slides inspect qbr.pptx --preview --detail layouts fonts notes --output json
aspose-cli review qbr.pptx --out qbr.review --output json
```

The result has three slides: Quarterly update, Highlights and Next steps.
The bundled template supplies the 16:9 size, theme and layout geometry.
Open every image the review lists before delivery and disclose evaluation output.
