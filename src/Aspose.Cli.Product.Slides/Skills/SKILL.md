---
name: aspose-cli-slides
description: Create, inspect, edit, convert and review PowerPoint presentations with the local Aspose CLI. Markdown outlines fill real template layouts, edits are atomic batches, and review renders every slide for visual checks.
---

# Aspose Slides

Use `aspose-cli slides` for PPTX, PPT, ODP and related presentation formats. The shared rules
(session start, reading windows, operation batches, secrets, review protocol, licensing, errors)
are in `aspose-cli docs overview`; this Skill adds only what is specific to presentations.
`aspose-cli capabilities slides` lists the commands, formats, review checks and codes, and
`aspose-cli schema v2/slides/ops --operation <op>` describes each operation's fields.
A deck exported to PDF is a PDF: edit that file with the PDF Skill (`aspose-cli docs pdf/overview`).

## Workflow

1. Clarify audience, purpose, talk length, screen ratio and requested scope.
2. **New deck:** write a Markdown outline and author it into a template
   ([outline authoring](references/outline-authoring.md)). Omit `--template` to use the
   built-in 16:9 design.

   ```powershell
   aspose-cli slides create deck.pptx --from-markdown outline.md --template brand.pptx --output-format json
   ```

3. **Existing deck:** inspect the structure, then read only the slides you need. A slide cut
   short by `--max-chars` has `contentTruncated: true`; `window.next` reads it again, with a
   doubled budget when it alone exceeded the budget.

   ```powershell
   aspose-cli slides inspect deck.pptx --sample --detail layouts --detail fonts --detail notes --output-format json
   aspose-cli slides read deck.pptx --slides 1-5 --content full --notes --output-format json
   ```

4. Put all related changes in one `slides edit` batch
   ([Slides editing](references/editing.md)):

   ```powershell
   aspose-cli slides edit deck.pptx --ops deck-ops.json --out deck.revised.pptx --verify --output-format json
   ```

5. Read the changes back and review every slide ([Slides verification](references/verification.md),
   then the checklist in `aspose-cli docs verification`):

   ```powershell
   aspose-cli review deck.revised.pptx --out deck.review-1 --output-format json
   ```

## Addressing

- A slide is addressed by its 1-based `slide` number or its stable `slideId`.
- A shape is addressed within its slide by `shapeId` (persistent, slide-scoped), by
  case-sensitive `shapeName`, or by `placeholder` role (`title`, `body`, `subtitle`, `footer`,
  `date`, `slide-number`), the roles `slides read` reports.
- `slides read --content shapes` reports every one of these names exactly as the
  operations accept them; `inspect` reports slide ids and shape counts, not shape ids.

## Design: the template owns the look

Fonts, colors, backgrounds and placeholder geometry come from the template's theme, masters and
layouts. Fix a look by choosing or correcting the template, never by restyling text run by run.
Details: [design system](references/design-system.md).

## Evaluation mode

Without a Slides license (`aspose-cli docs licensing`), reads return text longer than five
characters cut short and warn `EVALUATION_INPUT_TRUNCATED`, while the presentations, PDFs and
images it saves keep the full text. Each evaluation save adds another watermark text box to every slide, which `review`
reports as `SLIDES_EVALUATION_WATERMARK`; a license does not remove them, so rebuild the deck
with a license. Details: [evaluation mode](references/troubleshooting.md#evaluation-mode).

## Common mistakes

- Taking shape ids from `slides inspect`: read them with `slides read --content shapes`, and read
  them again after copying or importing slides.
- Restyling text with `format_shape` slide by slide instead of fixing the template once.
- Splitting related changes over several edits; one batch can add a slide and fill it.
- Trusting `--verify` alone: it does not read back formatting, geometry, inserted objects, table
  cells or chart data; read those with `slides read --content full` and look at the review.
- Using `slides search` for chart text or alternative text: it does not see them.
- Expecting speaker notes in a PDF or other export: read them with `slides read --notes`.
- Delivering a converted file without its warnings: `HIDDEN_SLIDES_SKIPPED`, `ENCRYPTION_REMOVED`
  and `LOSSY_CONVERSION` change what the output holds
  ([outputs](references/editing.md#outputs-and-conversion)).
- Passing different `--font-dir` folders to `create`, `edit`, `convert`, `render` and `review`:
  saving shrinks auto-fit text with the fonts it finds.

## References

- [Slides editing](references/editing.md): addressing in a batch, operations by task, text,
  notes, charts, media, and what each output format keeps
- [Outline authoring](references/outline-authoring.md): the Markdown grammar and how it maps to layouts
- [Design system](references/design-system.md): choosing the template and content rules
- [Slides verification](references/verification.md): what `--verify` reads back, content reads,
  review checks, chart fidelity
- [Slides preview](references/preview.md): what the viewer shows
- [Slides troubleshooting](references/troubleshooting.md): failed or odd-looking edits and
  evaluation-mode output

Examples: [deck from outline](examples/deck-from-outline/README.md),
[edit a deck safely](examples/edit-deck-safely/README.md),
[data slides](examples/data-slides/README.md).
