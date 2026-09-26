# Edit a deck safely

Copy this Skill to a fresh writable directory, then change into
examples/edit-deck-safely. [deck.md](deck.md) creates a synthetic source presentation;
[deck-ops.json](deck-ops.json) updates its second slide.

```powershell
aspose-cli slides create deck.pptx --from-markdown deck.md --output json
aspose-cli slides query slides deck.pptx --slides 2 --scope shapes --output json
aspose-cli slides edit deck.pptx --ops deck-ops.json --out deck.revised.pptx --output json
aspose-cli slides query slides deck.revised.pptx --slides 2 --scope full --notes --output json
aspose-cli review deck.revised.pptx --out deck.review --output json
```

The shape read shows the second slide's `title` and `body` placeholders, which the operations
address by role. The source keeps its Delivery plan title. The revised second slide is titled
Confirmed delivery plan, its body reads "Validation is complete; the release is approved.", and
its speaker note is "Launch approved." Open every review image before delivery and disclose
evaluation output.

For a user's existing deck, read its actual slide and shape addresses before editing. Use
`--in-place --backup` only when replacing the source was requested.
