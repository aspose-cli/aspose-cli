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
[ ] Leftovers: `<product> search` finds no TBD / TODO / {{...}}
    placeholders
[ ] Product checks: the semantic checks the product Skill names pass
[ ] Visual: `<product> render` ran and every image it wrote was looked at
[ ] Warnings: evaluation marks, removed encryption and lossy conversion
    disclosed with the delivered file
```

## Looking at the output

```text
aspose-cli <product> render <file> --out <image> --output-format json
```

Render the output with the product's `render` command and open every image
it writes with your image-reading tool. Check clipping, overlap, missing
glyphs, contrast, charts against their data, and page or slide order. Never
claim a visual pass for an image you did not open.

## Fonts

Missing fonts are substituted silently and change text metrics, line breaks
and pagination.

Fonts delivered beside the document reach the engine only through
`--font-dir <dir>`, which adds to the system fonts and can repeat. Pass the
same directories to every command whose output
depends on layout; each command's `--help` shows whether it accepts `--font-dir`.
`aspose-cli fonts list` shows each engine's font sources and default fallback
font.

## Honest limits

- A render shows the engine's print layout; the user's application can differ
  in minor ways such as zoom or theme fonts.
- Semantic checks catch errors, not wrong but valid content; spot-check a few
  computed or rewritten values against what they should be.
- Reports to the user state what was checked and what was not: coverage,
  capped lists (`LIST_TRUNCATED`), font substitution and evaluation marks.
