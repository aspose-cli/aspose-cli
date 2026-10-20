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
aspose-cli cells read book.xlsx --sheet Sales --range E2:E6 --content values --output-format json
aspose-cli cells read book.xlsx --sheet Sales --range E2:E6 --content formulas --output-format json
```

- A formula has two faces: `--content formulas` adds its text (`f`), `--content
  values` shows its result as Excel shows it on opening: stored, or calculated
  when the workbook asks for that (`FORMULAS_CALCULATED_ON_OPEN`).
- A cell of type `error` (`#DIV/0!`, `#REF!`) is a finding now.
- After inserting or deleting rows or columns, or sorting, also read a formula
  range you did not touch: its references should have moved with the
  structure.

## Tier 2: visual

`read` returns full values, so it cannot see truncation, overlap or a
chart plotting the wrong block. Only a render can:

```
aspose-cli review book.xlsx --out book.review-1 --output-format json
aspose-cli cells render book.xlsx --range Sales!A1:G20 --out zoom.png
```

`review` renders one 192 DPI image per visible sheet; hidden sheets are not
reviewed. A sheet more than 10000 pixels on a side contributes its first
window of rows and columns, and the `SHEET_PARTIALLY_RENDERED` warning names the
window and gives the `cells render` command, with a sheet-qualified `--range`,
for the next one;
a sheet whose print area lists several areas contributes its first area, and the
same warning gives the command that renders each other one; the other sheets are
still reviewed.
Cells checks report codes such as `CELLS_FORMULA_ERROR`,
`CELLS_POPULATED_COLUMNS_NARROW` and `CELLS_VALUES_CLIPPED` (text cut off by
the next cell, or a number or date too wide to show in full, as `####` or
rounded, measured with Excel's column widths). `CELLS_VALUES_CLIPPED` can miss East Asian text in a font
without East Asian glyphs (Calibri, Arial): judge those columns in the image,
or use a font such as Microsoft YaHei. It does report the indented column
labels of a pivot table, which the engine cuts off at their column's edge and
`resize_columns` without `widthChars` leaves as they are: give those columns an
explicit `widthChars`. For a file a person opens, the look
also grades the design checklist in `aspose-cli docs cells/design-system`,
section 12. Render large sheets as windows with a sheet-qualified `--range`;
`cells render` warns `RENDER_IMAGE_LARGE` with such a window when an image is
more than 10000 pixels on a side.

### Judge widths from a `--range` render

A range render matches Excel's column widths; a full-sheet render is about
5% wider, which hides truncation: a `Headcount plan` header in a width-12
column renders `Headcount pla` under `--range` and whole in the full sheet.
`convert --to pdf` hides it the same way. Judge width, truncation and `###` from a
`--range` render; use full-sheet images for layout, chart placement and page
flow.

The reverse also happens: text that spills over empty cells can lose its last
character at a column edge in a full-sheet image while `--range` shows it
whole (`CELLS_TEXT_OVERFLOWS`).

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
At 150 and 192 DPI the strokes are solid. When text looks corrupt, read the
cell first; if the value is right, check the DPI, font availability and layout
before diagnosing corruption. A CJK sheet declaring only Arial passes
`fonts check` with `allAvailable: true` and renders through substitution.

While looking, check for:

- clipped text or `###` (from a `--range` render);
- title rows with shaved glyph tops: a larger font does not re-fit its row
  (`aspose-cli docs cells/workbook-standards`, Fonts and headers);
- overlapping labels, broken merges or header layout;
- chart series, legend and axes that do not match the source data;
- surviving placeholders (TBD, TODO, `{{...}}`, xxx);
- tofu boxes or `□□□` in non-Latin text.

## Tier 3: semantic

```
aspose-cli cells inspect book.xlsx --detail errors --output-format json
aspose-cli cells search book.xlsx --pattern "TBD|TODO|xxx|\{\{" --regex --output-format json
```

`workbook.formulaErrors` must be empty; guard a division that can meet zero
or an empty cell with `IFERROR` or an `IF` on the denominator. The search
covers every sheet and matches values; `--content all` also searches formula
text. `hits` must be empty, or each hit is an intentional token you explain.
Formula errors are not the only wrong numbers: a `SUM` over the wrong range
returns a plausible value, so spot-check two or three computed cells against
what they should roughly be.

## Tier 4: session diff

```
aspose-cli cells compare book.backup.xlsx book.xlsx --output-format json
```

- It compares stored cell values and formula text only (`--content values`
  ignores formula text). Dates compare by their stored value and read as
  ISO 8601, strings ordinally,
  and empty cells, empty strings, numbers, Booleans and errors are distinct.
- It cannot see styling, widths, heights, charts, images, page setup,
  validation or freeze panes. After a formatting, chart or layout session,
  `identical: true` is the correct result; confirm that work from the
  `sizeBytes` change and a Tier 2 render, and never report "nothing changed"
  from the diff. Read layout and page setup back with
  `cells inspect --detail layout`, and the other objects with their sections:
  `charts`, `pivots`, `tables`, `validation`, `conditional-formats`,
  `sparklines`, `images`, `comments` and `hyperlinks`, with `--detail` written
  once per section (`--detail charts --detail pivots`); the `x-readBack` of
  each operation field in `aspose-cli schema v2/cells/ops` names the property
  that reports it, or says that no read reports it yet, so confirm that field
  in a render.
- Read the whole list. Any difference you did not intend, such as a shifted
  formula or a cleared cell, is a finding to fix before delivery.
- A renamed sheet pairs with the sheet it was (they keep one internal sheet
  id): compare reports `status: "renamed"` with its old name in `from` and
  its cell changes; `--verify` lists it in `otherChanges` as `renamed`,
  without `from`, and its cell changes under the new name. The pairing is
  meaningful only for two versions of the same workbook; two unrelated workbooks can pair unrelated
  sheets that happen to share an id.
- Cells pair by address. When rows were inserted or deleted, `ROWS_SHIFTED`
  names where (`1 row inserted at right row 9`): the cells below compare with
  the row that held their address before, so those differences are not edits.
  Rows match by their content or, when their first cell holds a label no
  other row of the sheet starts with, by that label, so a statement whose
  values all changed still aligns on its line names. For a row-for-row list, apply the same `insert_rows` or `delete_rows` to a
  copy of the left workbook and compare the copy. Columns are not aligned.
- `--max-diffs` caps the listed cells; the summary still counts every
  difference and `LIST_TRUNCATED` reports the omission.
- Report the summary (sheets modified, cells differing) as value and formula
  changes, with the backup path.
