# Build a deck from an outline

Author a Markdown outline into the bundled template (or the user's brand
template), inspect the result, and review every slide:

```powershell
aspose-cli slides create qbr.pptx --from-markdown notes.md --template default-16x9.pptx --output json
aspose-cli slides inspect qbr.pptx --preview --detail layouts fonts notes --output json
aspose-cli review qbr.pptx --out qbr.review --output json
```

`default-16x9.pptx` is `assets/templates/default-16x9.pptx` in this Skill.
Open every image the review lists before delivery and disclose evaluation
status.
