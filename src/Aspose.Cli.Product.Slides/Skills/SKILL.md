---
name: aspose-cli-slides
description: High-fidelity presentation automation with bounded slide inspection, outline authoring, atomic editing, rendering, conversion, extraction, and visual verification.
---

# Aspose Slides

Use `aspose-cli slides` for slide, layout, shape, chart, table and speaker-note
workflows. Presentation addresses are 1-based slide numbers or stable slide and
shape ids; they are not worksheet cells, document paragraphs or PDF pages.

## Safe workflow

1. Inspect structure before content:
   `aspose-cli slides inspect deck.pptx --preview --detail masters layouts fonts notes comments sections properties --output json`.
2. Read only the required slide window:
   `aspose-cli slides query slides deck.pptx --slides 1-5 --scope full --notes --max-chars 20000 --output json`.
3. Preserve one baseline before editing an existing user deck.
4. Put related changes in one `slides edit` batch. Use `--verify`; use
   `--best-effort` only when partial output is explicitly acceptable.
5. Read back affected slides, search for expected text, render every changed
   slide and inspect the images at a useful size.
6. Confirm masters, layouts, slide size, hidden-slide state, fonts, notes and
   output format after save.
7. Disclose `EVAL_MODE`, evaluation watermark or truncation, lossy conversion,
   and any fallback fonts or unsupported media behavior.

Passwords must come from `--password-env`, `--password-stdin` or
`--encrypt-env`. Never put secrets in ops JSON, logs, result envelopes or
preview session state.

## Preview and licensing

`aspose-cli preview deck.pptx --open --output json` resolves supported
presentation content to Slides and its default `slides` view. It provides thumbnails, slide
navigation, live edit activity and last-good recovery without spreadsheet,
word-processing or PDF controls. Agents use static `slides query slides`,
`slides render`, `slides query search` and verification results as delivery evidence.

Install a Slides-only license with
`aspose-cli license install Aspose.Slides.lic --product slides`, set
`ASPOSE_SLIDES_LICENSE_PATH`, or use a shared Aspose.Total license through
`ASPOSE_LICENSE_PATH`. Inspect the `slides` entry from
`aspose-cli license status --output json`; one product's failure does not
describe sibling products.

See `references/editing.md`, `references/outline-authoring.md`,
`references/design-system.md`, `references/verification.md`,
`references/preview.md`, and `references/troubleshooting.md`.

Worked examples: `examples/deck-from-outline`,
`examples/edit-deck-safely`, and `examples/data-slides`.

## Visual delivery gate

1. Understand the audience, presentation purpose, delivery setting, screen ratio, talk length, and requested scope before authoring or editing the deck.
2. For an existing user deck, preserve unrelated slides, masters, layouts, theme, notes, media, animations, and speaker intent; change only the requested scope.
3. Run `aspose-cli review <artifact> --out <fresh-review-dir> --output json` for every presentation and exported deliverable, using a fresh output directory for each round. Treat findings as a review queue, not as proof that the deck has been seen.
4. Actually open every visual artifact produced by review, one by one, then every slide render for a new deck and every changed or affected slide for a scoped edit; also open every page of PDF exports. Check narrative flow, title hierarchy, alignment, spacing, overflow, contrast, font fallback, charts and tables, image crops, footers, slide numbers, consistency, and readability at presentation distance.
5. Fix defects, reopen, render, and run review again. Stop after at most three visual correction rounds and report remaining issues rather than endlessly polishing.
6. Do not claim a visual pass when slide/page inspection is unavailable, any required artifact was not opened, or coverage is incomplete. State exact slide coverage and mark the rest partial or skipped.
7. Report evaluation results separately from licensed results. Disclose `EVAL_MODE`, watermarks, truncation, font/media fallback, and lossy conversion for every affected artifact.
