---
name: aspose-cli-slides
description: Create, inspect, edit, convert and review PowerPoint presentations with the local Aspose CLI. Markdown outlines fill real template layouts, edits are atomic batches, and review renders every slide for visual checks.
---

# Aspose Slides

Use `aspose-cli slides` for PPTX, PPT, ODP and related presentation formats.
Slides are addressed by 1-based number or stable `slideId`; shapes by `shapeId`
or case-sensitive `shapeName` within their slide.

`query slides --max-chars` bounds all returned title, text, run, requested note
and comment text, including repeated projections. Addressing and formatting
metadata do not consume this text budget. Inspect `window.truncated` and each
slide's `contentTruncated`. Run `next` verbatim: it reads the remaining slides
and starts again at a slide the budget cut short; when that slide alone exceeded
the budget, `next` doubles `--max-chars`.

## Workflow

1. Clarify audience, purpose, talk length, screen ratio and requested scope.
2. **New deck:** write a Markdown outline and author it into a template (see
   Design below):

   ```powershell
   aspose-cli slides create deck.pptx --from-markdown outline.md --template brand.pptx --output json
   ```

   Omit `--template` to use the built-in 16:9 design.

3. **Existing deck:** inspect structure, then read only the slides you need:

   ```powershell
   aspose-cli slides inspect deck.pptx --preview --detail layouts fonts notes --output json
   aspose-cli slides query slides deck.pptx --slides 1-5 --scope full --notes --output json
   ```

4. Put all related changes in one atomic `slides edit` batch. Write to `--out`,
   or use `--in-place --backup` when replacing the user's file is intended:

   ```powershell
   aspose-cli slides edit deck.pptx --ops deck-ops.json --out deck.revised.pptx --output json
   ```

5. Verify before delivery (below). Every edit reopens its output before
   publishing it, so a successful edit is a readable presentation.

## Design: the template owns the look

- Fonts, colors, backgrounds and placeholder geometry come from the template's
  theme, masters and layouts. Use the user's brand template when one exists;
  without `--template`, `slides create` uses the built-in 16:9 design.
- Markdown authoring fills layout placeholders: `#` becomes a Title Slide, `##`
  a Title and Content slide, and a slide with both text and an image uses Two
  Content. It sets no colors or fonts of its own: emphasis becomes bold or
  italic and code uses a monospace font. See
  [outline authoring](references/outline-authoring.md).
- Do not restyle text run by run to fix a look; choose or correct the template.
- One claim per slide. Keep at most six bullets and two levels; prefer a chart
  or small table over dense prose. Split a slide rather than shrink its text.

More: [design system](references/design-system.md).

## Verify before delivery

1. **Content:** read changed slides back with `slides query slides` and search
   for leftovers such as `TODO` with `slides query search`.
2. **Visual:** run `aspose-cli review deck.pptx --out <new-dir> --output json`,
   then open every image it lists, one by one. Use `review.json` findings
   (overflow, small text, overlaps, blank slides) to focus, not as a substitute
   for looking.
3. Fix, then run review again into a fresh directory. Stop after three rounds
   and report what remains.
4. Never claim a visual pass for slides you did not open. State the exact
   coverage from `coverage.complete` and the images inspected.

Details: [verification](references/verification.md).

## Licensing

Without a Slides license, output is watermarked and results carry `EVAL_MODE`;
disclose that with every delivered file. Install a license with
`aspose-cli license install Aspose.Slides.lic --product slides` and check the
`slides` entry of `aspose-cli license status --output json`.

Passwords come from `--password-env`, `--password-stdin` or `--encrypt-env`;
never put secrets in ops JSON.

## References

- [Editing and the ops vocabulary](references/editing.md) (`aspose-cli schema v2/slides/ops`)
- [Outline authoring](references/outline-authoring.md)
- [Design system](references/design-system.md)
- [Verification](references/verification.md)
- [Live preview for a human](references/preview.md)
- [Troubleshooting](references/troubleshooting.md)

Examples: [deck from outline](examples/deck-from-outline/README.md),
[edit a deck safely](examples/edit-deck-safely/README.md),
[data slides](examples/data-slides/README.md).
