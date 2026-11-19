# Build a slide

Copy this Skill to a fresh writable directory, then change into examples/build-a-slide.
[add-ops.json](add-ops.json) adds a text box, a two-by-two table and notes to the first slide of
a new deck, and a slide on the "Title and Content" layout after it;
[fill-ops.json](fill-ops.json) fills the table, the notes and the new slide's placeholders at the
addresses the first edit returned.

```powershell
aspose-cli slides create deck.pptx --output-format json
aspose-cli slides read deck.pptx --kind slide --kind layout --output-format json
aspose-cli slides edit deck.pptx --ops add-ops.json --out deck.1.pptx --output-format json
aspose-cli slides edit deck.1.pptx --ops fill-ops.json --out deck.2.pptx --output-format json
aspose-cli slides render deck.2.pptx --slides 1-2 --out deck.png --output-format json
```

The engine's new deck has one slide, `slide/256`, on the Blank layout, and the layouts the read
lists; `2147483650` is "Title and Content". The first edit's `ops` give the new objects'
addresses: the text box `slide/256/shape/2`, the table `slide/256/shape/3`, the notes
`slide/256/notes` and the new slide `slide/257`, whose layout gave it a `Title` and an `Object`
placeholder. The second edit sets text at those addresses; in a deck of your own, take them from
your first edit's result. A shape's text separates paragraphs with `\r`. Look at the rendered
images before handing the deck over.
