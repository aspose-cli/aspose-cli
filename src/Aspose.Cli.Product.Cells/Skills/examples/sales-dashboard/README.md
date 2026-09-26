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

```json
{"ops":[
  {"op":"set_default_font","font":"Calibri","size":11},
  {"op":"rename_sheet","sheet":"sales","to":"Data"},
  {"op":"add_sheet","name":"Dashboard","position":0},
  {"op":"set_values","sheet":"Data","range":"A1","values":[["Order id","Date","Category","Region","Owner","Qty","Unit price","Amount","Month"]]},
  {"op":"sort_range","sheet":"Data","range":"A2:G37","by":[{"column":"B","order":"asc"}]},
  {"op":"set_formula","sheet":"Data","range":"H2:H37","formula":"=F2*G2"},
  {"op":"set_formula","sheet":"Data","range":"I2:I37","formula":"=TEXT(B2,\"yyyy-mm\")"}
]}
```

The sort runs while the range is still pure values — never `sort_range` over
live formulas — and only then do the Amount and Month formulas land.

```powershell
# 3. The Data register: table, formats, widths, freeze, data bar, print.
aspose-cli cells edit dashboard.xlsx --ops ops-data.json --in-place --output json
```

```json
{"ops":[
  {"op":"create_table","sheet":"Data","range":"A1:I37","name":"Orders","style":"TableStyleMedium2"},
  {"op":"format_range","sheet":"Data","range":"B2:B37","style":{"numberFormat":"yyyy-mm-dd"}},
  {"op":"format_range","sheet":"Data","range":"G2:H37","style":{"numberFormat":"#,##0;(#,##0);\"-\""}},
  {"op":"add_conditional_format","sheet":"Data","range":"H2:H37","rule":{"kind":"dataBar","barColor":"#4472C4"}},
  {"op":"resize_columns","sheet":"Data","from":"A","to":"I","width":12},
  {"op":"resize_columns","sheet":"Data","from":"A","width":13},
  {"op":"resize_columns","sheet":"Data","from":"F","to":"G","width":10},
  {"op":"freeze_panes","sheet":"Data","cell":"A2"},
  {"op":"set_tab_color","sheet":"Data","color":"#808080"},
  {"op":"set_page_setup","sheet":"Data","orientation":"landscape","paperSize":"a4","fitToWidth":1,"fitToHeight":0},
  {"op":"set_print_area","sheet":"Data","range":"A1:I37","titleRows":"1:1"}
]}
```

```powershell
# 4. The Dashboard: title band, summary blocks (SUMIFS/COUNTIFS), 4 KPI
#    cards + sparklines, two charts with cosmetics, chrome, print.
aspose-cli cells edit dashboard.xlsx --ops ops-dashboard.json --in-place --output json
```

```json
{"ops":[
  {"op":"resize_columns","sheet":"Dashboard","from":"A","width":2},
  {"op":"resize_columns","sheet":"Dashboard","from":"B","to":"P","width":12},
  {"op":"set_values","sheet":"Dashboard","range":"B1","values":[["Sales Performance"]]},
  {"op":"format_range","sheet":"Dashboard","range":"B1","style":{"bold":true,"size":16,"color":"#1F3864"}},
  {"op":"set_values","sheet":"Dashboard","range":"B2","values":[["Revenue in US$ · Jan–Jun 2026 · source: sales.csv"]]},
  {"op":"format_range","sheet":"Dashboard","range":"B2","style":{"size":9,"color":"#808080"}},
  {"op":"set_values","sheet":"Dashboard","range":"B24","values":[["Month","2026-01","2026-02","2026-03","2026-04","2026-05","2026-06"],["Revenue (US$)",null,null,null,null,null,null],["Orders",null,null,null,null,null,null]]},
  {"op":"set_formula","sheet":"Dashboard","range":"C25:H25","formula":"=SUMIFS(Data!$H$2:$H$37,Data!$I$2:$I$37,C$24)"},
  {"op":"set_formula","sheet":"Dashboard","range":"C26:H26","formula":"=COUNTIFS(Data!$I$2:$I$37,C$24)"},
  {"op":"format_range","sheet":"Dashboard","range":"C25:H25","style":{"numberFormat":"#,##0;(#,##0);\"-\""}},
  {"op":"set_values","sheet":"Dashboard","range":"J24","values":[["Region","Revenue (US$)"],["West",null],["South",null],["East",null],["North",null]]},
  {"op":"set_formula","sheet":"Dashboard","range":"K25:K28","formula":"=SUMIFS(Data!$H$2:$H$37,Data!$D$2:$D$37,J25)"},
  {"op":"format_range","sheet":"Dashboard","range":"K25:K28","style":{"numberFormat":"#,##0;(#,##0);\"-\""}},
  {"op":"set_values","sheet":"Dashboard","range":"B4","values":[["Revenue (US$)",null,null,"Orders",null,null,"Avg order (US$)",null,null,"Top region"]]},
  {"op":"format_range","sheet":"Dashboard","range":"B4:K4","style":{"size":9,"color":"#808080"}},
  {"op":"set_formula","sheet":"Dashboard","range":"B5","formula":"=SUM(C25:H25)"},
  {"op":"set_formula","sheet":"Dashboard","range":"E5","formula":"=SUM(C26:H26)"},
  {"op":"set_formula","sheet":"Dashboard","range":"H5","formula":"=IFERROR(B5/E5,0)"},
  {"op":"set_formula","sheet":"Dashboard","range":"K5","formula":"=INDEX(J25:J28,MATCH(MAX(K25:K28),K25:K28,0))"},
  {"op":"format_range","sheet":"Dashboard","range":"B5:K5","style":{"bold":true,"size":20,"color":"#1F3864","numberFormat":"#,##0;(#,##0);\"-\""}},
  {"op":"set_formula","sheet":"Dashboard","range":"B6","formula":"=IFERROR(H25/G25-1,0)"},
  {"op":"set_formula","sheet":"Dashboard","range":"E6","formula":"=IFERROR(H26/G26-1,0)"},
  {"op":"set_formula","sheet":"Dashboard","range":"H6","formula":"=IFERROR((H25/H26)/(G25/G26)-1,0)"},
  {"op":"set_formula","sheet":"Dashboard","range":"K6","formula":"=IFERROR(MAX(K25:K28)/B5,0)"},
  {"op":"format_range","sheet":"Dashboard","range":"B6:H6","style":{"size":9,"numberFormat":"+0.0%;-0.0%;0.0%"}},
  {"op":"format_range","sheet":"Dashboard","range":"K6","style":{"size":9,"numberFormat":"0.0%"}},
  {"op":"add_conditional_format","sheet":"Dashboard","range":"B6:H6","rule":{"kind":"cellValue","operator":"greaterOrEqual","value1":"0"},"style":{"color":"#548235"}},
  {"op":"add_conditional_format","sheet":"Dashboard","range":"B6:H6","rule":{"kind":"cellValue","operator":"lessThan","value1":"0"},"style":{"color":"#C00000"}},
  {"op":"add_sparkline","sheet":"Dashboard","dataRange":"C25:H25","location":"C5","type":"line","color":"#4472C4"},
  {"op":"add_sparkline","sheet":"Dashboard","dataRange":"C26:H26","location":"F5","type":"column","color":"#4472C4"},
  {"op":"create_chart","sheet":"Dashboard","type":"column","dataRange":"B24:H25","at":"B8:H22","seriesInRows":true,"title":"Monthly revenue · US$ · Jan–Jun 2026","legend":{"visible":false},"axisTitles":{"category":"Month","value":"Revenue (US$)"},"seriesColors":["#4472C4"]},
  {"op":"create_chart","sheet":"Dashboard","type":"bar","dataRange":"J24:K28","at":"J8:P22","title":"Revenue by region · US$ · Jan–Jun 2026","legend":{"visible":false},"dataLabels":{"visible":true,"format":"#,##0"},"seriesColors":["#4472C4"]},
  {"op":"resize_rows","sheet":"Dashboard","from":5},
  {"op":"set_tab_color","sheet":"Dashboard","color":"#1F3864"},
  {"op":"set_sheet_view","sheet":"Dashboard","gridlines":false},
  {"op":"set_page_setup","sheet":"Dashboard","orientation":"landscape","paperSize":"a4","fitToWidth":1,"fitToHeight":1},
  {"op":"set_print_area","sheet":"Dashboard","range":"A1:P29"}
]}
```

The region block is listed ASCENDING (West 22,700 → North 67,535) because a
bar chart plots the first source row at the bottom — that order is what makes
the ranking read largest-first top-down (`aspose-cli docs cells/design-system`, Charts).

```powershell
# 5. Verify values: every reported number comes from the engine.
aspose-cli cells query range dashboard.xlsx --sheet Dashboard --range B4:K6 --scope values --output json

# 6. The eyes loop: render at 192 dpi and actually LOOK.
aspose-cli cells render dashboard.xlsx --sheet Dashboard --out scratch/dash-1.png --dpi 192 --overwrite --output json
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
aspose-cli cells render dashboard.xlsx --sheet Dashboard --out scratch/dash-2.png --dpi 192 --overwrite --output json
```

```json
{"ops":[
  {"op":"resize_columns","sheet":"Dashboard","from":"B","width":16},
  {"op":"resize_columns","sheet":"Dashboard","from":"E","width":16},
  {"op":"resize_columns","sheet":"Dashboard","from":"H","width":16},
  {"op":"resize_columns","sheet":"Dashboard","from":"K","width":16},
  {"op":"resize_rows","sheet":"Dashboard","from":1},
  {"op":"set_active_sheet","sheet":"Dashboard"}
]}
```

`dash-2.png` ticks every box: `156,460` fully drawn, the title whole, the
sparklines showing the April dip the charts also show (also render `Data` —
`--range A1:I12` — for the register's banded table, ISO dates, formatted
money and data bars). A licensed save also keeps `Dashboard` as the active
sheet, so both Excel and the Cells Preview open on the summary; an evaluation
save activates its own warning sheet instead. Then the semantic
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
