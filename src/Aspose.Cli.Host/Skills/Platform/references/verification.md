# Verifying before delivery

Assume the output has problems until the evidence says otherwise. A clean exit
code means the command ran, not that the document is right. The product
overview adds the checks specific to its documents.

## Delivery checklist

```text
[ ] Content: every changed range, page, slide or block read back; reported
    values came from the engine
[ ] Edits: every OP_NO_EFFECT warning explained; no change reported that was
    not read back
[ ] Leftovers: a search (`cells query search`, `pdf search`, `slides search`,
    `words search`) finds no TBD / TODO / {{...}} placeholders
[ ] Product checks: the semantic checks the product Skill names pass
[ ] Visual: review ran into a new directory and every image it lists was
    opened; its coverage is complete, or the gap is reported
[ ] Fonts: fonts check shows no missing font, or the substitution is reported
[ ] Session diff: against the backup, only intended changes remain
[ ] Warnings: evaluation marks, removed encryption and lossy conversion
    disclosed with the delivered file
```

## The review protocol

```text
aspose-cli review <file> --out <new-dir> --output-format json
```

`review` renders the document into an evidence directory: `index.html` for a
person, `review.json` (the same result the command prints) and one image per
sheet, page or slide. Without `--out` it writes `<filename>.review` beside the
source; write evidence to a scratch directory instead of the user's folder.
`aspose-cli schema v2/common/review` describes every field of the result.

1. Read the result. `artifacts[]` lists each image with the sheet, page or
   slide it shows. `findings[]` point at likely defects, each with its
   `severity`, `location`, `hint` and the `evidence` images it concerns.
2. Read `coverage`. `--max-items` caps the images; anything omitted is
   unreviewed, and incomplete coverage fails the review. `input.encrypted`
   says whether the reviewed file is encrypted, as evidence for a delivery
   that must be.
3. Open every evidence image with your image-reading tool and look. Findings
   do not replace looking. Check clipping, overlap, missing glyphs, contrast,
   charts against their data, and page or slide order.
4. Fix, then review again into a new directory, or pass `--overwrite` to
   replace the earlier evidence; `--overwrite` replaces only an empty
   directory or one that holds earlier evidence (`review.json`), never a
   folder of other files.
5. Stop after three rounds and report what remains.

`review` exits 8, with the evidence still written, when coverage is incomplete
or a reported finding has `error` severity. Never claim a visual pass for an
image you did not open; state the coverage you actually inspected.

## Review checks

Findings come from declared checks. `aspose-cli capabilities <product>` lists
the product's checks under `review.checks`, each with its code, severity and
summary.

`review --code <code>` (repeat it for several checks) reports only the findings
of the named checks, and among findings only theirs decide exit 8; incomplete
coverage still fails the review, and a missing or substituted font makes the
evidence incomplete whatever the filter. The result's `filter` says how many
findings were left out. The images are still the full set: a filtered review
narrows the findings, not the look.

## Fonts

Missing fonts are substituted silently and change text metrics, line breaks
and pagination.

```text
aspose-cli fonts check <file> --output-format json
```

`fonts check` reports each font the document uses, whether it is available
here and, when known, the font the engine draws instead; it exits 8 when a
font is not available. It answers whether the declared font names are
installed, not whether every glyph renders: non-Latin text in a font without
those glyphs can still pass, so the visual pass remains the proof. `review`
reports a missing or substituted font with the product's check, such as
`PDF_FONTS_MISSING_OR_SUBSTITUTED`.

Fonts delivered beside the document reach the engine only through
`--font-dir <dir>`, which adds to the system fonts and can repeat. Pass the
same directories to `fonts check`, `review` and every command whose output
depends on layout; `capabilities` shows which commands accept `--font-dir`.
`aspose-cli fonts list` shows each engine's font sources and default fallback
font.

## Honest limits

- A render shows the engine's print layout; the user's application can differ
  in minor ways such as zoom or theme fonts.
- Semantic checks catch errors, not wrong but valid content; spot-check a few
  computed or rewritten values against what they should be.
- Reports to the user state what was checked and what was not: coverage,
  capped lists (`LIST_TRUNCATED`), font substitution and evaluation marks.
