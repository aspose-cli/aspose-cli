# Edit a deck safely

Keep a baseline, apply one atomic batch, then read and render the result:

```powershell
aspose-cli slides inspect deck.pptx --preview --output json
aspose-cli slides edit deck.pptx --ops deck-ops.json --out deck.revised.pptx --verify --output json
aspose-cli slides query slides deck.revised.pptx --slides 1- --scope full --notes --output json
aspose-cli slides render deck.revised.pptx --all-slides --to png --width 1600 --out review.png --output json
```

Do not overwrite the source unless the user requested an in-place edit and a
backup was created first.
