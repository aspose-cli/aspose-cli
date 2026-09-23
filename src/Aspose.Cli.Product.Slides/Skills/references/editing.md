# Slides editing

All addresses resolve against the original presentation before the first
operation is applied. Every operation validates that its original slides and
shapes still belong to the presentation before modifying anything. A deleted
slide or shape cannot be targeted later in the batch; multi-target operations
validate every target before changing any. Moving a slide preserves its original
identity. Deleting the final slide also fails. Content inserted earlier in a batch cannot be addressed
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

`set_footer` shows footer text, slide numbers or dates through the layout's own
placeholders; their position and style come from the template.

The current operation vocabulary is available offline:

```powershell
aspose-cli schema v2/slides/ops
aspose-cli docs slides/ops
```

Video and audio insertion or MP4 rendering are not supported by this build. Existing embedded
media can be inventoried and extracted, but must not be silently synthesized.

For `update_chart_data`, omitted series retain their existing values, including
scatter X/Y coordinates. A categories-only update must keep matching lengths.
Explicit scatter series require matching `xValues` and `values`.

New charts reserve space for their title and legend. Adding a second series to a
chart without a legend creates a legend outside the plot. Data updates preserve
an existing chart's explicit title and legend overlay settings. For non-negative
bar and column data, automatic value axes start at zero in the correct orientation;
explicit value-axis limits remain unchanged.

The CLI's zero baseline is saved as an explicit fixed minimum of `0`. Later
data-only updates keep all stored axis limits, including that zero. New negative
values or values outside a fixed range can therefore be clipped. Adjust the axes
in a presentation editor or recreate the chart for the new data, then render and
review it before delivery. A successful data update does not establish that every
value is visible.
