---
name: aspose-cli-slides
description: Create, inspect, edit, convert and review PowerPoint presentations with the local Aspose CLI. Markdown outlines fill real template layouts, edits are atomic batches, and review renders every slide for visual checks.
---

# Aspose Slides

Use `aspose-cli slides` for PPTX, PPT, ODP and related presentation formats. The shared
workflow, batch semantics, review protocol, licensing and error envelope are in the platform
Skill: start with `aspose-cli docs overview`.

## Addressing

- A slide is addressed by its 1-based `slide` number or its stable `slideId`.
- A shape is addressed within its slide by `shapeId` (persistent, slide-scoped), by
  case-sensitive `shapeName`, or by `placeholder` role (`title`, `body`, `subtitle`, `footer`).
- `slides query slides --scope shapes` reports every one of these names exactly as the
  operations accept them; `inspect` reports slide ids and shape counts, not shape ids.

## Workflow

1. Clarify audience, purpose, talk length, screen ratio and requested scope.
2. **New deck:** write a Markdown outline and author it into a template
   ([outline authoring](references/outline-authoring.md)). Omit `--template` to use the
   built-in 16:9 design.

   ```powershell
   aspose-cli slides create deck.pptx --from-markdown outline.md --template brand.pptx --output json
   ```

3. **Existing deck:** inspect the structure, then read only the slides you need. A truncated
   read reports `window.next`; run it verbatim.

   ```powershell
   aspose-cli slides inspect deck.pptx --preview --detail layouts fonts notes --output json
   aspose-cli slides query slides deck.pptx --slides 1-5 --scope full --notes --output json
   ```

4. Put all related changes in one `slides edit` batch
   ([Slides editing](references/editing.md)):

   ```powershell
   aspose-cli slides edit deck.pptx --ops deck-ops.json --out deck.revised.pptx --output json
   ```

5. Verify before delivery ([Slides verification](references/verification.md), then the
   checklist in `aspose-cli docs verification`).

## Design: the template owns the look

Fonts, colors, backgrounds and placeholder geometry come from the template's theme, masters and
layouts. Fix a look by choosing or correcting the template, never by restyling text run by run.
One claim per slide, at most six bullets and two levels; split a slide rather than shrink it.
Details: [design system](references/design-system.md).

## Slides-specific rules

- The license product id is `slides`: `aspose-cli license install Aspose.Slides.lic --product slides`.
  Evaluation output is watermarked (`aspose-cli docs licensing`). Evaluation mode reads text
  longer than five characters cut short, so reads, reviews, `extract` of text or notes and
  Markdown conversion warn `EVAL_INPUT_TRUNCATED`; the presentations, PDFs and images it saves
  keep the full text. Each evaluation save adds another watermark text box to every slide, which
  reads list as content. A review in evaluation mode leaves those boxes out and counts them in its
  `excludedEvaluationWatermarks` coverage metric; a licensed review judges them like any shape
  (for example `SLIDES_TEXT_OVERLAPS_OBJECT` over a table), so rebuild the deck with a license.
- Only PPTX and PPTM outputs can carry a password; `--encrypt-env` with any other `create`,
  `edit` or `convert` output is `OPTION_INVALID`.
- Chart titles, labels and data are not text: `query`, `search` and `replace_text` never see
  them, so confirm charts in the rendered review images.

## References

- [Slides editing](references/editing.md): addressing, operations by task, text, notes and chart semantics
- [Outline authoring](references/outline-authoring.md): Markdown to layouts
- [Design system](references/design-system.md): templates and content rules
- [Slides verification](references/verification.md): content reads, review checks, chart fidelity
- [Slides preview](references/preview.md): what the viewer shows
- [Slides troubleshooting](references/troubleshooting.md): Slides error codes

Examples: [deck from outline](examples/deck-from-outline/README.md),
[edit a deck safely](examples/edit-deck-safely/README.md),
[data slides](examples/data-slides/README.md).
