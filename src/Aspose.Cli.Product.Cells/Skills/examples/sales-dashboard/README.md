# Example: messy CSV → sales dashboard (KPI cards, charts, verified delivery)

Inputs: [sales.csv](sales.csv), [ops-structure.json](ops-structure.json),
[ops-data.json](ops-data.json), [ops-dashboard.json](ops-dashboard.json),
[ops-fix.json](ops-fix.json). Run from this directory.

Goal: the design-system deliverable for an open-ended "make me a sales
dashboard" — a `Data` sheet (the register as a filterable table) plus a
`Dashboard` sheet (title band, 4-KPI row with sparklines, two charts),
verified with the eyes loop. `sales.csv` is 36 orders, Jan–Jun 2026,
unsorted, no amount column, General formats everywhere:

```
order_id,date,category,region,owner,qty,unit_price
SO-2026-014,2026-03-08,Hardware,West,Priya Nair,16,395
SO-2026-002,2026-01-15,Software,East,Maria Silva,3,1150
...
```

```powershell
# 1. CSV becomes a workbook (sheet is named after the file: "sales").
aspose-cli cells convert sales.csv --to xlsx --out dashboard.xlsx --overwrite --output json

# 2. Structure: default font FIRST, real sheet names, sort, derived columns.
aspose-cli cells edit dashboard.xlsx --ops ops-structure.json --in-place --output json
```

The sort runs while the range is still pure values — never `sort_range` over
live formulas — and only then do the Amount and Month formulas land.

```powershell
# 3. The Data register: table, formats, widths, freeze, data bar, print.
aspose-cli cells edit dashboard.xlsx --ops ops-data.json --in-place --output json

# 4. The Dashboard: title band, summary blocks (SUMIFS/COUNTIFS), 4 KPI
#    cards + sparklines, two charts with cosmetics, chrome, print.
aspose-cli cells edit dashboard.xlsx --ops ops-dashboard.json --in-place --output json
```

The region block is listed ASCENDING (West 22,700 → North 67,535) because a
bar chart plots the first source row at the bottom — that order is what makes
the ranking read largest-first top-down (`aspose-cli docs cells/design-system`, Charts).

```powershell
# 5. Verify values: every reported number comes from the engine.
aspose-cli cells query range dashboard.xlsx --sheet Dashboard --range B4:K6 --scope values --output json

# 6. The eyes loop: render at 192 dpi and actually LOOK.
aspose-cli cells render dashboard.xlsx --sheet Dashboard --out scratch/dash-1.png --overwrite --output json
```

The read returns the KPI row computed by the engine: revenue `156460`,
orders `36`, avg order `4346.11`, top region `North`, deltas `+12.3%` /
`+14.3%` / `-1.7%` (June average order dipped — the red path is real data,
not a styling demo). Looking at `dash-1.png` against the design-system
checklist finds two real defects: the 16pt title's glyph tops are shaved
(row 1 kept its default height — the "f" of "Performance" loses its
ascender) and the Revenue KPI shows `######` (six figures at 20pt do not
fit width 12). Fix both, re-render, look again:

```powershell
# 7. Fix what the render showed, then re-check.
aspose-cli cells edit dashboard.xlsx --ops ops-fix.json --in-place --output json
aspose-cli cells render dashboard.xlsx --sheet Dashboard --out scratch/dash-2.png --overwrite --output json
```

`dash-2.png` ticks every box: `156,460` fully drawn, the title whole, the
sparklines showing the April dip the charts also show (also render `Data` —
`--range A1:I12` — for the register's banded table, ISO dates, formatted
money and data bars). A licensed save also keeps `Dashboard` as the active
sheet, so both Excel and the Cells Preview open on the summary; an evaluation
save activates its own warning sheet instead, and the preview then opens on
the first sheet, `Dashboard`. Then the semantic
gate and the print delivery:

```powershell
# 8. Zero formula errors before delivery.
aspose-cli cells inspect dashboard.xlsx --detail errors --output json

# 9. Print-accurate PDF (proves the page setup, not just the screen look).
aspose-cli cells convert dashboard.xlsx --to pdf --out dashboard.pdf --overwrite --output json
```

Measured outcome: `workbook.formulaErrors` is empty; the PDF pages are A4
landscape — the Dashboard fitted to one, the 36-row register flowing across
two with the header row repeated by `titleRows` (evaluation mode appends
pages for its warning sheets). `Dashboard` (tab
`#1F3864`, first) opens on a title band, four KPI cards whose numbers are
engine-computed formulas (spot-check: B5 `156,460` equals the region
block's sum), deltas colored by sign, two token-styled charts with the
biggest region on top; `Data` (tab `#808080`) is a filterable banded table
— dates `yyyy-mm-dd`, money with `(negatives)` and `-` zeros, frozen
header, print-ready. Report to the user: what was built, the engine-read
KPI values, and — in evaluation mode — that every produced file carries
the Aspose evaluation watermark.

Learn more: `aspose-cli docs cells/design-system` (tokens, recipes, the finishing
checklist), `aspose-cli docs cells/verification` (the verification tiers).
