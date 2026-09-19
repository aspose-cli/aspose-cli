# Edit a deck safely

Read the current state, apply one atomic batch to a new file, then read back
and review the result:

```powershell
aspose-cli slides inspect deck.pptx --preview --output json
aspose-cli slides edit deck.pptx --ops deck-ops.json --out deck.revised.pptx --output json
aspose-cli slides query slides deck.revised.pptx --slides 1- --scope full --notes --output json
aspose-cli review deck.revised.pptx --out deck.review --output json
```

Do not overwrite the source unless the user requested an in-place edit; then
use `--in-place --backup`.
