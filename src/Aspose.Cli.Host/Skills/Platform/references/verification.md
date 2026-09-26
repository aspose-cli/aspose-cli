# Verifying before delivery

Assume the output has problems until the evidence says otherwise. A clean exit
code means the command ran, not that the document is right. The product
overview adds the checks specific to its documents.

## Delivery checklist

```text
[ ] Content: every changed range, page, slide or block read back; reported
    values came from the engine
[ ] Leftovers: query search finds no TBD / TODO / {{...}} placeholders
[ ] Product checks: the semantic checks the product Skill names pass
[ ] Visual: review ran into a new directory and every image it lists was
    opened; coverage.complete is true, or the gap is reported
[ ] Fonts: fonts check shows no missing font, or the substitution is reported
[ ] Session diff: against the backup, only intended changes remain
[ ] Evaluation: any EVAL_MODE warning disclosed with the delivered file
```

## The review protocol

```text
aspose-cli review <file> --out <new-dir> --output json
```

`review` renders the document into a new evidence directory: `index.html`
for a person, `review.json` (the same result the command prints) and one
image per sheet, page or slide under `artifacts/`. Without `--out` it writes
`<filename>.review` beside the source; write evidence to a scratch directory
instead of the user's folder.

1. Read the result. `artifacts[]` lists each image (`role: "evidence"`) with
   the sheet, page or slide it shows in `label`. `findings[]` carry `code`,
   `severity` (`error`, `warning`, `info`), `message`, `location`, `hint` and
   the `evidence` images.
2. Read `coverage`: `expectedItems`, `renderedItems`, `omittedItems`,
   `truncated` and `complete`. `--max-items` (default 256) caps the images;
   anything omitted is unreviewed.
3. Open every evidence image with your image-reading tool and look. Findings
   point you at likely defects; they do not replace looking. Check clipping,
   overlap, missing glyphs, contrast, charts against their data, and page or
   slide order.
4. Fix, then review again into a new directory: an existing `--out` fails
   with `OUTPUT_EXISTS`, because evidence is never overwritten.
5. Stop after three rounds and report what remains.

`review` exits 8, with the evidence still written, when coverage is incomplete
or a reported finding has `error` severity. Never claim a visual pass for an
image you did not open; state the coverage you actually inspected.

## Review checks

Findings come from declared checks. `aspose-cli capabilities --output json`
lists each product's checks under `products[].review.checks`, each with
`code`, `severity` and `summary`; `aspose-cli capabilities <product>` narrows
the list.

`review --code <code>` (repeat it for several checks) reports only the findings
of the named checks, and among findings only theirs decide exit 8; incomplete
coverage still fails the review, and a missing or substituted font makes the
evidence incomplete whatever the filter. `review.json` then carries `filter` with the
`codes` and `omittedFindings`, the number of findings left out. The images are
still the full set: a filtered review narrows the findings, not the look.

## Fonts

Missing fonts are substituted silently and change text metrics, line breaks
and pagination.

```text
aspose-cli fonts check <file> --output json
```

`fonts check` reports `allAvailable` and, for each font the document uses,
`available` and, when a substitute is known, `substitutedBy`: the font the
engine draws instead. It answers
whether the declared font names are installed here, not whether every glyph
renders: non-Latin text in a font without those glyphs can still pass, so the
visual pass remains the proof. `review` reports a missing or substituted font
as `FONTS_MISSING_OR_SUBSTITUTED`.

Fonts delivered beside the document reach the engine only through
`--font-dir <dir>`, which adds to the system fonts and can repeat. Pass the
same directories to `fonts check`, `review` and every command whose output
depends on layout; `capabilities` shows which commands accept `--font-dir`.
`aspose-cli fonts list --product <product>` shows an engine's font sources and
default fallback font.

## Honest limits

- A render shows the engine's print layout; the user's application can differ
  in minor ways such as zoom or theme fonts.
- Semantic checks catch errors, not wrong but valid content; spot-check a few
  computed or rewritten values against what they should be.
- Reports to the user state what was checked and what was not: coverage,
  capped lists (`LIST_TRUNCATED`), font substitution and evaluation marks.
