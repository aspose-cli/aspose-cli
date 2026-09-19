# Slides editing

All addresses resolve against the original presentation before the first
operation is applied. Content inserted earlier in a batch cannot be addressed
by later operations in that same batch. Obtain stable `slideId` values from
`slides inspect` or `slides query slides`. Obtain `shapeId` values from
`slides query slides --scope shapes` or `--scope full`; `inspect` reports shape
counts, not shape ids.

A `shapeId` is a positive, persistent identifier within its slide, not a shape
position or a presentation-wide counter. Pass it as the operation's `shape` value
and pair it with that slide's `slide` or `slideId`. Reading a different slide
window does not change these ids. Read new ids after duplicating or importing shapes.
Shape selectors address top-level slide shapes, including a group as one shape;
nested group children are not separately projected or addressed. `shapeName`
matching is case-sensitive.

`set_notes` replaces the selected slide's speaker-note text. `inspect --detail notes`
reports only presence and character counts. Read note text with
`query slides --notes` (also included by `--scope full`) or `extract --what notes`.
Check `contentTruncated` on bounded slide reads.

Apply one atomic batch:

```powershell
aspose-cli slides edit deck.pptx --ops deck-ops.json --out deck.revised.pptx --output json
```

Use `--in-place --backup` only for an intentional in-place edit. An operation
failure normally writes no document output. `--best-effort` saves successful
operations even when others fail; those partial results exit 8. Reserve it for
workflows that explicitly accept partial delivery. Every save reopens the
output before it is published.

After `set_footer`, run `review` and open every affected slide.
Confirm that footer text, dates, and slide numbers are visible inside the slide
canvas; template placeholders can retain geometry from an earlier slide size.

The current operation vocabulary is available offline:

```powershell
aspose-cli schema v2/slides/ops
aspose-cli docs slides/ops
```

Video and audio insertion or MP4 rendering are not supported by this build. Existing embedded
media can be inventoried and extracted, but must not be silently synthesized.
