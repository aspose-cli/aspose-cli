# Build a deck from an outline

Turn a Markdown outline into a presentation, inspect it, and render every slide:

```powershell
aspose-cli slides create qbr.pptx --from-markdown notes.md --size 16x9 --output json
aspose-cli slides inspect qbr.pptx --preview --detail masters layouts fonts notes --output json
aspose-cli slides render qbr.pptx --all-slides --to png --width 1600 --out qbr.png --output json
```

Inspect all rendered images before delivery and disclose evaluation status.
