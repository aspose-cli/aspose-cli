# Verifying spreadsheet work

Assume there are problems; your job is to find them. A clean exit code
means the command ran, not that the workbook is right — the first build
is almost never right. Every check below is cheap next to delivering a
broken workbook.

When each tier applies:

| tier | run it |
|------|--------|
| 1 — values | after every write, no exceptions |
| 2 — visual | anything visual changed, or a human will open the file |
| 3 — semantic | once, before delivery |
| 4 — session diff | you edited a user's file under the backup protocol |

Each tier catches what the previous one structurally cannot see. Run them
in order; stop and fix at the first finding, then re-run the tier.

## Built-in edit verification

`cells edit --verify` compares a private input snapshot with the exact staged
output, scans saved formula errors, and stages visible-sheet images before the
output set is published. It uses the same candidate with `--timeout` and MCP.
The evidence directory defaults to `.aspose-verify/<output-name>` beside the
output. A custom `--verify-dir` must share its filesystem and a writable common
transaction directory with the document.

Inspect `verification.ok`, `issues` and the images. Semantic findings can commit
with exit 8; a reopen error, rendering execution error, budget failure or
cancellation aborts publication. Empty sheets are reported as missing visual
evidence. `--verify` requires final recalculation and cannot accompany
`--dry-run` or `--no-recalc`.

## Tier 1 — values

Windowed `query range` of every range you changed, in the right scope:

```
aspose-cli cells query range book.xlsx --sheet Sales --range E2:E6 --scope values --output json
aspose-cli cells query range book.xlsx --sheet Sales --range E2:E6 --scope formulas --output json
```

- Read reported values with `query range` after a recalculating `edit` —
  never infer the saved result from arithmetic alone.
- For formulas, check both faces: `--scope formulas` shows the formula
  text (`f`), `--scope values` its computed result. Both are real engine
  state. Queries do not recalculate: for imported workbooks or edits using
  `--no-recalc`, recalculate before treating formula caches as current.
- A cell of type `error` (`#DIV/0!`, `#REF!`, ...) in the window is a
  finding: fix it now, don't wait for Tier 3 to catch it.
- After structural edits (insert/delete rows or columns, sorts), re-read
  a formula range you did NOT touch as well — its references should have
  shifted with the structure, and Tier 4 will confirm nothing else moved.
- Keep windows small and targeted; if a read chains a `next` command,
  execute it verbatim.

## Tier 2 — the visual pass

Mandatory when the change touched anything visual — column widths,
styles, charts, merges, number formats, conditional formats, print
setup — or when you are delivering a workbook a human will open.

Why this tier exists: `query range` returns full cell values, so it structurally
cannot see truncation, overlap, or a chart plotting nonsense. Only a
render can.

How:

```
aspose-cli cells render book.xlsx --all-sheets --out check.png --dpi 192
aspose-cli cells render book.xlsx --sheet Sales --range A1:G20 --out zoom.png --dpi 192
```

- One `--all-sheets` render per iteration is the whole-workbook look:
  one PNG per visible sheet (`check.Data.png`, `check.Summary.png`, …),
  and you LOOK at every one — a defect on a sheet you did not render is
  a defect you ship. Hidden sheets are not rendered; an empty or
  unrenderable sheet is skipped and named in a `SHEETS_SKIPPED` warning,
  which you read instead of assuming a file exists per sheet.
- Big files: window the sheets in question with `--sheet`/`--range`
  (e.g. `--range A1:G20`) instead of rendering thousands of rows — the
  spot check, and the strict-width view (below).
- Write to a temp/scratch directory, not the user's folder.
- DPI: keep the default 192. Latin-only content can drop to 96-150 for a
  smaller image; anything non-Latin needs >= 150 and is safest at 192 —
  below that the glyphs change identity (next section).
- Then OPEN the PNGs with your image-reading tool and actually look.
- For a deliverable a human opens, the look includes the design self-grade:
  the finishing-pass checklist in `aspose-cli docs design-system` (title band,
  header contrast, number discipline, chart hygiene). This tier owns the
  render mechanics — DPI floors, the strict `--range` view, `--all-sheets` —
  and that checklist owns the design bar; fix and re-render until both pass.

### Non-Latin text: never look below 150 DPI

Low DPI does not just soften CJK text — it silently rewrites it. A CJK
glyph's thin horizontal strokes fall under one pixel at 96-120 DPI and
anti-alias to near-white. The top stroke goes first, and what survives is a
different, real character:

| stroke lost at 96-120 DPI | intended | reads as |
|---------------------------|----------|----------|
| top horizontal | 天 | 大 |
| top horizontal | 东 | 乐 |
| top horizontal | 方 | 万 |
| top horizontal | 无 | 尢 |
| top bar of `7` | `2026年7月17日` | `2026年/月17日` |

Measured on `天` at 11pt: the darkest pixel anywhere in the glyph is 94/255
at 96 DPI and 75/255 at 120 DPI — no part of the character reaches black. At
150 and 192 the strokes hit 0. A digit inside a CJK string is dragged down
with it: the whole string renders through the substituted CJK font, so the
`7` in `7月7日` loses its bar, while a Latin-only `2026-07-17` in the next
cell stays crisp at the same DPI.

Why this bites the agent and not the file: the render looks like a font or
encoding fault, so a LOOK at 120 DPI invents corruption that is not in the
workbook — or misses real truncation while you chase it. Query the cell
back first. If its value is correct, check DPI, font availability and layout
before diagnosing corruption. Re-render at 192 and inspect the result.

### `--range` is the strict view; trust it over a full-sheet render

The two renders do not share a geometry. Measured on four columns of width
12 with row 1 at 22pt, at 192 DPI:

| render | content box |
|--------|-------------|
| `--range A1:D1` | 712 x 59 px |
| full sheet | 749 x 57 px |

The `--range` box matches Excel's own width formula exactly (`12 chars x 7 +
5` px per column at 96 DPI, x4 columns, doubled for 192). The full-sheet
render is 5.2% wider and 3.4% shorter — non-uniform, so it is not a zoom you
can correct for.

Width decides truncation, so those extra 5% hide it. A `Headcount plan`
header in a width-12 column renders `Headcount pla` — clipped, the truth —
under `--range`, and a plausible-looking `Headcount plan` in the full-sheet
render. A `convert --to pdf` hides it the same way.

Judge width, truncation and `###` from a `--range` render of the block in
question. Keep the full-sheet render for layout, chart placement and page
flow.

Checklist while looking:

- No clipped or truncated text (a value wider than its column) — from a
  `--range` render, the only view whose widths match Excel.
- No glyph tops shaved off a title row: enlarging a font does not re-fit its
  row (`aspose-cli docs workbook-standards`).
- No overlapping labels.
- Chart series, legend and axes plausible and matching the source data.
- No placeholder tokens surviving (TBD, TODO, `{{...}}`, xxx).
- Merged regions and header layout intact.
- Non-Latin text (CJK, Greek, Cyrillic) actually formed — not tofu boxes or
  `□□□`. `fonts check` structurally cannot see this (Honest limits). Rule out
  the DPI first: broken-looking strokes below 150 DPI are the render, not the
  file.

Delete the scratch image after looking.

## Tier 3 — semantic scan

Before delivery, the workbook must be free of formula errors:

```
aspose-cli cells inspect book.xlsx --detail errors --output json
```

`workbook.formulaErrors` must be empty. If a division can meet a zero or
an empty cell, guard it (`IFERROR`, or an `IF` on the denominator) rather
than shipping `#DIV/0!`.

Sweep for leftover placeholder tokens:

```
aspose-cli cells query search book.xlsx "TBD|TODO|xxx|\{\{" --regex --output json
```

`hits` must be empty — anything found is either unfinished work or an
intentional token to explain in your report. `query search` covers all sheets
by default (`--sheet` narrows it) and matches values; add `--in both` to
sweep formula text too.

## Tier 4 — the session diff

For an edit, `--verify` performs the value/formula diff, workbook-wide formula
scan, and 192 DPI visible-sheet renders in one call. You still must open the
returned images. Use the stable backup below for the final multi-edit session
inventory.

When you edited a user's file under the backup protocol (SKILL.md
section 5), the backup is the pre-session state — diff against it:

```
aspose-cli cells compare book.backup.xlsx book.xlsx --output json
```

- What it compares: cell VALUES and formula TEXT. Nothing else. `--compare`
  accepts only `values` or `formulas` (the default is both); `--compare
  styles` is a usage error.
- What it cannot see: styling (fonts, fills, number formats), column widths
  and row heights, charts and their titles, images, page setup, validation,
  freeze panes. A formatting-only session reports `identical: true,
  cellsDiffering: 0` while the file plainly changed — 7,237 -> 7,555 bytes in
  one measured case; rewriting a chart title moved 10,511 -> 10,573 bytes and
  still diffed as identical.
- Read the whole list. Anything in the diff you did not intend — a
  formula that shifted, a cell cleared by a careless range — means stop
  and fix before delivery.
- `identical: true` is a finding ONLY when you changed values or formulas and
  expected them to appear. After a formatting-only, chart-only or
  layout-only session it is the CORRECT result, not evidence that the edit
  went to the wrong file. Confirm those sessions from the `sizeBytes` move
  and the Tier 2 render instead — and never report "nothing changed" to the
  user on the strength of a diff over visual work.
- Include the summary (sheets modified, cells differing) in your report
  to the user — labelled as what it is: value and formula changes.

## Delivery checklist

```
[ ] Values: every changed range read back; all reported numbers came
    from the engine, not from your own arithmetic
[ ] Visual: rendered to PNG and actually looked, if anything visual
    changed or a human will open the file — no clipping, no overlap,
    charts plausible, layout intact
[ ] Semantic: cells inspect --detail errors -> workbook.formulaErrors is empty
[ ] Placeholders: search finds no TBD / TODO / {{...}} / xxx
[ ] Session diff: only intended changes; summary reported to the user
[ ] Evaluation mode: watermark disclosed to the user (EVAL_MODE warning)
```

## Honest limits

- A render shows print layout; on-screen Excel can differ in minor ways
  (zoom, gridline shading, theme fonts). Treat the render as the layout
  truth, not a pixel promise of the user's screen.
- Missing fonts substitute silently and change text metrics. When exact
  widths matter, check first:

```
aspose-cli fonts check book.xlsx
```

  Know what that answers: are the font NAMES this workbook declares
  installed here — not will every glyph render. The two come apart on
  non-Latin text, because a cell's script and its declared font are
  unrelated: a sheet of CJK or Greek text declaring only Arial (a font with
  no CJK glyphs) reports `allAvailable: true`, then renders through a silent
  substitution. For anything non-Latin, `allAvailable: true` means nothing on
  its own — Tier 2's render-and-LOOK is the only real check.

- In evaluation mode, produced files gain an "Evaluation Warning" sheet
  and a watermark: expect the extra sheet in `inspect` output and in renders
  of files you created. Do not try to delete it; disclose it instead.
- `--detail errors` catches formula errors, not wrong-but-valid numbers.
  A `=SUM` over the wrong range returns a plausible value — spot-check
  2-3 computed cells against your own expectation of what they should
  roughly be, by hand.

When a check fails and the fix is not obvious:
`aspose-cli docs troubleshooting` maps every error code to its recovery.

### Stored-value comparison

Comparison uses exact stored values, independently of display formatting. Dates compare as raw Excel serial numbers, including across 1900/1904 date systems. Strings use ordinal equality; empty cells, empty strings, numbers, booleans and errors are distinct. Each nonempty comparison side carries required `t` and canonical `v`; a formula with an empty cache retains `t: "empty"` and `f`.

The default `--compare formulas` compares stored values and formula text without recalculation. `--compare values` ignores formula text. `--max-diffs` limits listed cells across the whole workbook; the summary still counts every difference and `truncated` reports omitted entries. Enumeration is sparse and bounded across both files and all shared sheets. Budget exhaustion or cancellation fails the comparison instead of returning a partial total.
