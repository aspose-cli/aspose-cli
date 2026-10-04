# Verifying spreadsheet work

The delivery checklist, the review protocol and font checks are shared by
every product: `aspose-cli docs verification`. This page adds the workbook
tiers and the render facts that decide what a Cells check can see.

| tier | run it |
|------|--------|
| 1 values | after every write |
| 2 visual | anything visual changed, or a person will open the file |
| 3 semantic | once, before delivery |
| 4 session diff | you edited a user's file under `--backup` |

Each tier catches what the previous one cannot see. Stop and fix at the first
finding, then run the tier again. `cells edit --verify` already performs the
value and formula diff and the formula-error scan for one edit
(`aspose-cli docs cells/editing`, Built-in verification); it produces no
images.

## Tier 1: values

Read every changed range back:

```
aspose-cli cells query range book.xlsx --sheet Sales --range E2:E6 --scope values --output json
aspose-cli cells query range book.xlsx --sheet Sales --range E2:E6 --scope formulas --output json
```

- Report numbers from these reads, never from your own arithmetic.
- A formula has two faces: `--scope formulas` adds its text (`f`), `--scope
  values` shows its result as Excel shows it on opening: stored, or calculated
  when the workbook asks for that (`FORMULAS_CALCULATED_ON_OPEN`).
- A cell of type `error` (`#DIV/0!`, `#REF!`) is a finding now.
- After inserting or deleting rows or columns, or sorting, also read a formula
  range you did not touch: its references should have moved with the
  structure.

## Tier 2: visual

`query range` returns full values, so it cannot see truncation, overlap or a
chart plotting the wrong block. Only a render can:

```
aspose-cli review book.xlsx --out book.review-1 --output json
aspose-cli cells render book.xlsx --sheet Sales --range A1:G20 --out zoom.png --dpi 192
```

`review` renders one 192 DPI image per visible sheet; hidden sheets are not
reviewed. A sheet too large for one image contributes its first rows, and the
`CELLS_SHEET_PARTIALLY_RENDERED` warning names how many; the other sheets are
still reviewed. Cells checks report codes such as `CELLS_FORMULA_ERROR`,
`CELLS_POPULATED_COLUMNS_NARROW` and `CELLS_VALUES_CLIPPED` (text cut off by
the next cell, or a number too wide to show in full, measured with Excel's
column widths). The check compares the width the engine measures for each
value with its column. The engine measures East Asian text in a font without
East Asian glyphs (Calibri, Arial) narrower than auto-fit and the sheet image
draw it (known issue CELLS-WIDTH-EAST-ASIAN), so such text can be cut off
unlisted, or listed while the image shows it whole; judge it in the image, and
give a column that still cuts it off after auto-fit an explicit width, or use
a font with East Asian glyphs such as Microsoft YaHei. For a file a person opens, the look also grades the design
checklist in `aspose-cli docs cells/design-system`, section 12. Render large
sheets as windows with `--sheet` and `--range`.

### Judge widths from a `--range` render

A range render and a full-sheet render do not share a geometry. Measured on
four width-12 columns with row 1 at 22pt, at 192 DPI:

| render | content box |
|--------|-------------|
| `--range A1:D1` | 712 x 59 px |
| full sheet | 749 x 57 px |

The range box matches Excel's width formula (`12 chars x 7 + 5` px per column
at 96 DPI). The full-sheet render is 5.2% wider and 3.4% shorter, which hides
truncation: a `Headcount plan` header in a width-12 column renders
`Headcount pla` under `--range` and whole in the full sheet. `convert --to
pdf` hides it the same way. Judge width, truncation and `###` from a
`--range` render; use full-sheet images for layout, chart placement and page
flow.

### Non-Latin text: never look below 150 DPI

At 96-120 DPI the thin horizontal strokes of CJK glyphs fall under one pixel
and fade to near white. What survives is a different, real character:

| intended | reads as at 96-120 DPI |
|----------|------------------------|
| 天 | 大 |
| 东 | 乐 |
| 方 | 万 |
| 无 | 尢 |
| `2026年7月17日` | `2026年/月17日` |

A digit inside a CJK string renders through the substituted CJK font and
loses its strokes too, while a Latin-only `2026-07-17` beside it stays crisp.
At 150 and 192 DPI the strokes are solid. When text looks corrupt, query the
cell first; if the value is right, check the DPI, font availability and layout
before diagnosing corruption. `fonts check` answers whether declared font
names are installed, not whether every glyph renders: a CJK sheet declaring
only Arial reports `allAvailable: true` and renders through substitution.

While looking, check for:

- clipped text or `###` (from a `--range` render);
- title rows with shaved glyph tops: a larger font does not re-fit its row
  (`aspose-cli docs cells/workbook-standards`, Fonts and headers);
- overlapping labels, broken merges or header layout;
- chart series, legend and axes that do not match the source data;
- surviving placeholders (TBD, TODO, `{{...}}`, xxx);
- tofu boxes or `□□□` in non-Latin text.

Gridlines in the view never appear in renders; renders follow print layout,
and on-screen Excel can differ in zoom and gridline shading.

## Tier 3: semantic

```
aspose-cli cells inspect book.xlsx --detail errors --output json
aspose-cli cells query search book.xlsx --pattern "TBD|TODO|xxx|\{\{" --regex --output json
```

`workbook.formulaErrors` must be empty; guard a division that can meet zero
or an empty cell with `IFERROR` or an `IF` on the denominator. The search
covers every sheet and matches values; `--scope both` also searches formula
text. `hits` must be empty, or each hit is an intentional token you explain.
Formula errors are not the only wrong numbers: a `SUM` over the wrong range
returns a plausible value, so spot-check two or three computed cells against
what they should roughly be.

## Tier 4: session diff

```
aspose-cli cells compare book.backup.xlsx book.xlsx --output json
```

- It compares stored cell values and formula text only (`--compare values`
  ignores formula text). Dates compare as serial numbers, strings ordinally,
  and empty cells, empty strings, numbers, Booleans and errors are distinct.
- It cannot see styling, widths, heights, charts, images, page setup,
  validation or freeze panes. After a formatting, chart or layout session,
  `identical: true` is the correct result; confirm that work from the
  `sizeBytes` change and a Tier 2 render, and never report "nothing changed"
  from the diff.
- Read the whole list. Any difference you did not intend, such as a shifted
  formula or a cleared cell, is a finding to fix before delivery.
- A renamed sheet pairs with the sheet it was (they keep one internal sheet
  id): `status: "renamed"` with its old name in `from` and its cell changes,
  in compare and in `--verify` alike. The pairing is meaningful only for two
  versions of the same workbook; two unrelated workbooks can pair unrelated
  sheets that happen to share an id.
- `--max-diffs` caps the listed cells; the summary still counts every
  difference and `LIST_TRUNCATED` reports the omission.
- Report the summary (sheets modified, cells differing) as value and formula
  changes, with the backup path.
