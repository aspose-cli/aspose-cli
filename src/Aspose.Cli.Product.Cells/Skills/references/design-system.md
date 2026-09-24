# The design system

Workbook standards (`aspose-cli docs workbook-standards`) are the floor: widths,
number formats, formulas-not-hardcodes, validation, print. This document is
the design pass on top — the difference between a functional workbook and a
deliverable that reads as professional work the moment it opens. Run it for
any file a human will open; skip it for machine-to-machine data files. Every
recipe is one executable ops batch — fold them into your larger batches —
and the pass is only done after the eyes loop (section 12).

## 1. When this applies — and when the file overrides you

Two regimes:

- **Files you create** — apply this system in full.
- **Existing files with an established look — THE FILE WINS.** Detect its
  conventions before the first mutation and reuse them; impose nothing.

Detection is two cheap reads:

```sh
aspose-cli cells inspect book.xlsx --detail fonts --output json
aspose-cli cells query range book.xlsx --sheet Data --range A1:I3 --scope styles --output json
```

`--scope styles` maps each cell to a deduplicated `styles` pool — sample the
header rows' fills, fonts and number formats. Headers sharing a fill and a
font are a design language: take the exact hexes from the pool, the font, the
header treatment, and apply THIS document only where the file is silent.
Default remnants everywhere — Arial 10, no fills, General formats — mean no
established look, and the design pass applies. Never restyle a sheet you
were asked to edit two cells in.

## 2. Design tokens

One set of tokens per workbook, used everywhere; <= 8 distinct colors total.
Usage is 60-30-10: neutrals ~60% of the inked surface, brand ~30%,
semantic/accent the last 10%. A color difference must mean a meaning
difference — decoration is not a meaning.

**Ink layer** (cell TEXT color in models and registers humans edit; a
presentation dashboard keeps formula ink black):

| token | hex | means |
|-------|-----|-------|
| input | `#0000FF` | hardcoded input / historical actual |
| formula | `#000000` | same-sheet formula |
| cross-sheet | `#008000` | formula pulling from another sheet |
| external | `#7030A0` | link to another workbook |

**Theme layer** (titles, headers, charts, tabs):

| token | hex | use |
|-------|-----|-----|
| primary | `#1F3864` | titles, header fills, KPI numbers, dashboard tab |
| accent | `#4472C4` | main chart series, sparklines, data bars, input tabs |
| neutrals | `#FFFFFF` `#F7F7F7` `#F2F2F2` `#D9D9D9` `#808080` `#404040` | background / banding / zone fill / border lines / footnote text / dark body |

**Chart series** S1-S6 (medium intensity, distant hues): `#4472C4` blue,
`#ED7D31` orange, `#A5A5A5` gray (prior year / baseline), `#FFC000` gold,
`#5B9BD5` light blue, `#70AD47` green (skip when the same chart already uses
semantic red/green). **Semantic state**: good `#548235`, bad `#C00000`, warn
`#BF8F00` text on `#FFF2CC` fill. Red/green are reserved for variance meaning
— never decoration — and never the only cue (pair with an arrow or a sign).

**Type scale** — one font family, hierarchy by size/weight/gray only:

| level | spec |
|-------|------|
| workbook title | 16pt bold, primary |
| section / block title | 12-13pt bold |
| body and data | 11pt regular |
| KPI number | 18-24pt bold, primary or semantic |
| footnote / unit note / source | 8-9pt regular, `#808080` |

`set_default_font` is the FIRST op of every new workbook — column width units
are measured in the Normal font, so the font comes before content and widths.
Calibri 11 for Latin content (the one probe-measured metric-neutral swap from
the engine's Arial 10 default); DengXian or Microsoft YaHei for CJK-primary
workbooks — either rescales rendered widths, which is exactly why it must
come first, and CJK renders are judged at >= 150 dpi only:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"set_default_font","name":"Calibri","size":11}
]}'
# CJK-primary workbook instead:
#   {"op":"set_default_font","name":"DengXian","size":11}
```

## 3. Title band

Every deliverable sheet opens with a title band: row 1 the title, row 2 a
gray subtitle that declares WHAT, IN WHAT UNIT, and WHEN — the unit is stated
once here, never repeated per cell ("Revenue in US$ 000s · FY2026 Jan-Jun",
"单位: 人民币千元 · 2026年1-6月"). Row 3 stays empty as a spacer. Do NOT merge
cells for the title — a merge breaks fill-down, sorting and range selection
forever after; a left-aligned title in the leftmost content cell overflows
across empty neighbors and looks identical:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"set_values","sheet":"Dashboard","range":"B1","values":[["Sales Performance"]]},
  {"op":"set_values","sheet":"Dashboard","range":"B2","values":[["Revenue in US$ 000s · FY2026 Jan–Jun · source: CRM export"]]},
  {"op":"format_range","sheet":"Dashboard","range":"B1","style":{"bold":true,"size":16,"color":"#1F3864"}},
  {"op":"format_range","sheet":"Dashboard","range":"B2","style":{"size":9,"color":"#808080"}},
  {"op":"resize_rows","sheet":"Dashboard","from":1}
]}'
```

The `resize_rows` with no `height` auto-fits row 1 to the 16pt title —
forgetting it ships shaved glyph tops (`aspose-cli docs workbook-standards`,
Fonts). For a heavier, document-like opening, swap the title's style for a
full-width band: `{"bg":"#1F3864","color":"#FFFFFF","bold":true,"size":16}`
over `A1:Q1` — fill and text color as a pair, per the next section.

## 4. Header treatment

Header rows are bold, white, on the primary fill — **fill and font color are
one decision, always set as a pair**. A dark fill with default black text (or
white text on a default fill) is the single most common contrast failure.
Headers get a taller row (20-22pt at 11pt body), a medium rule below, and a
freeze exactly at the label/data boundary:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"format_range","sheet":"Data","range":"A1:I1","style":{"bold":true,"bg":"#1F3864","color":"#FFFFFF","wrap":true}},
  {"op":"resize_rows","sheet":"Data","from":1,"height":22},
  {"op":"set_borders","sheet":"Data","range":"A1:I1","edges":["bottom"],"style":"medium","color":"#1F3864"},
  {"op":"freeze_panes","sheet":"Data","cell":"A2"}
]}'
```

The fixed 22 holds ONE line. `wrap` is there so a long header folds instead
of truncating — but once a header actually wraps, auto-fit the header row
(`resize_rows` with `from: 1` and no `height`) in the same batch that sets
the final column widths: measured at 192 dpi, a wrapped second line under a
fixed 22 renders clipped, while the auto-fit grows the row so two- and
three-line headers come back whole. The data area never reuses the header's
dark fill — it is structure, not a palette entry for content.

## 5. Border hierarchy

Borders are a five-level language. Meaning comes from restraint: whitespace
first, lines second, fills last. Never wrap every block in a thick black
frame, and never draw vertical lines inside a data table — alignment and
column gaps carry the vertical structure.

| level | line | where |
|-------|------|-------|
| L0 | none | default; blocks separate by an empty row/column |
| L1 | hair or thin `#D9D9D9`, horizontal only | row separation inside data |
| L2 | thin `#000000` top | above subtotal rows |
| L3 | thin top + **double bottom** | grand-total rows (the accounting close) |
| L4 | medium `#1F3864` bottom | under header rows, under a title band |

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"set_borders","sheet":"Data","range":"A2:I7","edges":["horizontal"],"style":"hair","color":"#D9D9D9"},
  {"op":"set_borders","sheet":"Data","range":"A8:I8","edges":["top"],"style":"thin"},
  {"op":"set_borders","sheet":"Data","range":"A8:I8","edges":["bottom"],"style":"double"}
]}'
```

Grand totals are additionally bold; internal operating sheets may swap L3
for a full-row highlight fill — lines for external deliverables, fills for
internal ones, never both. `hair` renders identically to `thin` below ~150
dpi (probe-measured: both are one pixel at 96, and only at 192 does `thin`
double): verify the L1/L2 distinction at 192.

## 6. Tables, banding and in-table emphasis

Row-oriented registers a human will filter become a native table — the style
brings its own header treatment and banding, so do not paint a second header
over it:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"create_table","sheet":"Data","range":"A1:I7","name":"Orders","style":"TableStyleMedium2"}
]}'
```

`TableStyleMedium2` (accent blue) and `TableStyleMedium9` are the two
token-compatible defaults. For a report table that must stay a plain range,
band manually — a formula rule, striping every other row:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"add_conditional_format","sheet":"Data","range":"A2:I7","rule":{"kind":"formula","value1":"=MOD(ROW(),2)=0"},"style":{"bg":"#F7F7F7"}}
]}'
```

Banding OR air — never both: either zebra stripes at default row height, or
no stripes and generous rows (18-20pt at 11pt body). Emphasis inside a table
is semantic and scarce (a sheet where everything is highlighted says
nothing), and the rules below are a menu, not a stack — a `dataBar` under a
`topBottom` font color on the same column paints the number over the bar,
busily (measured); pick the one that carries the message:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"add_conditional_format","sheet":"Data","range":"H2:H7","rule":{"kind":"dataBar","barColor":"#4472C4"}},
  {"op":"add_conditional_format","sheet":"Data","range":"A2:I7","rule":{"kind":"formula","value1":"=$H2>5000"},"style":{"bg":"#FFF2CC"}},
  {"op":"add_conditional_format","sheet":"Data","range":"H2:H7","rule":{"kind":"topBottom","rank":3},"style":{"bold":true,"color":"#548235"}}
]}'
```

- The money/magnitude column of a register gets a `dataBar` in the accent —
  the one-glance answer to "where is the weight".
- A `formula` rule with a `$`-pinned column (`=$H2>5000`) highlights the
  WHOLE row from one cell's condition — the classic status-row pattern
  (`=$F2="OVERDUE"`). The formula anchors on the range's top-left cell and
  shifts per row; `value1` must be `=`-led. Where a row matches two fill
  rules, the later-added rule's fill wins over the zebra stripe.
- `topBottom` (`rank`, plus `percent`/`bottom`) flags the top or bottom N
  without hardcoding a threshold.
- `iconSet` (`arrows3`, `trafficLights3`, `symbols3`, `rating4`, `rating5`)
  suits bounded scores and RAG status — icons survive grayscale printing and
  colorblindness, the preferred second cue next to red/green (section 9 puts
  `arrows3` on KPI deltas).

## 7. Number and unit discipline

`aspose-cli docs workbook-standards` carries the base table (money with decimals,
counts, percentages, dates, years). The design pass adds the report-grade
codes and the consistency rules:

| data | format code | shows |
|------|-------------|-------|
| money, summary level | `#,##0;(#,##0);"-"` | `12,345` / `(12,345)` / `-` |
| variance / delta | `+0.0%;-0.0%;0.0%` | `+12.4%` / `-3.1%` |
| multiple | `0.0"x"` | `11.2x` |
| factor / ratio | `0.0000` | `1.0250` |
| year | `0` | `2026`, never `2,026` |
| date (data) | `yyyy-mm-dd` | locale-proof, sortable |

Rules: negatives in parentheses everywhere (never a bare minus in one column
and parentheses in the next); zeros display as `-`; one decimal-place count
per column; summary layers round (`3.8M`, not `3,848,305.93`); the unit is
declared once in the title band or header, not glued to every cell. A format
does not widen its column: `yyyy-mm-dd` needs width >= 11 or it renders
`########` until the finishing pass sets widths (section 12, step 2).

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"format_range","sheet":"Data","range":"G2:H8","style":{"numberFormat":"#,##0;(#,##0);\"-\""}},
  {"op":"format_range","sheet":"Data","range":"B2:B7","style":{"numberFormat":"yyyy-mm-dd"}},
  {"op":"format_range","sheet":"Config","range":"B1","style":{"numberFormat":"+0.0%;-0.0%;0.0%"}},
  {"op":"format_range","sheet":"Config","range":"B2","style":{"numberFormat":"0.0\"x\""}}
]}'
```

Pivot tables are not exempt — a pivot of formatted money that prints `17997`
fails the discipline. Give every value field its format at creation:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"add_sheet","name":"Pivot"},
  {"op":"create_pivot","sheet":"Pivot","sourceRange":"Data!A1:I7","at":"A1","rows":["Region"],"values":[{"field":"Amount","numberFormat":"#,##0;(#,##0);\"-\""}]}
]}'
```

## 8. Charts

Selection is by data shape, not preference:

| shape / task | use | not |
|--------------|-----|-----|
| time series, <= 12 periods | column (time on the x-axis) | pie, bar |
| time series, longer or multi-series | line | stacked area, 3-D |
| category comparison / ranking | bar, largest on top | pie, radar |
| share of a whole, <= 5 slices | pie (else a bar) | pie with > 5 slices |
| correlation | scatter | dual-axis overlay |
| single KPI now | KPI card + sparkline (section 9) | gauge |

`create_chart` already applies a modern default — the outer chart border is
removed, the plot area has no fill, gridlines are
`#D9D9D9` and value-axis only (horizontal on a column/line chart, vertical
on a bar), the value-axis line and tick marks are gone, the legend sits at
the bottom, bars are tighter, and series fall on a navy ramp (`#1F4E79`,
`#2E75B6`, `#9DC3E6`, `#D9D9D9`) — so do NOT re-specify those. What you DO
set:

- `title`, message-style: subject · measure + unit · period ("Monthly
  revenue · US$ 000s · FY2026 H1"), never "Chart 1".
- `axisTitles` carrying the units — and never on a pie: a pie has no axes
  and the CLI rejects the field (`OPS_INVALID`) instead of dropping it.
- `legend {"visible":false}` for a single series; multi-series legends stay
  at the engine's bottom default (`position` only for a real layout need).
- `seriesColors` when hue must carry a meaning: the accent `#4472C4` for a
  token-styled dashboard's main series, actual `#4472C4` vs plan/prior-year
  `#A5A5A5`, an alarm series `#C00000`. The default navy ramp is fine for a
  standalone chart; the S1-S6 tokens are for charts whose colors separate
  meanings.
- `dataLabels` only when the chart has <= 6 points, always with a `format`.

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"set_values","sheet":"Dashboard","range":"B30","values":[["Month","2026-01","2026-02","2026-03","2026-04","2026-05","2026-06"],["Revenue",180,210,235,228,265,300]]},
  {"op":"create_chart","sheet":"Dashboard","type":"column","dataRange":"B30:H31","at":"B8:H22","seriesInRows":true,"title":"Monthly revenue · US$ 000s · FY2026 H1","legend":{"visible":false},"axisTitles":{"category":"Month","value":"Revenue (US$ 000s)"},"seriesColors":["#4472C4"]}
]}'
```

(The source row is hardcoded here only to keep the recipe self-contained — in
real work it is a SUMIFS block, per the worked example: `aspose-cli docs
sales-dashboard`.) A ranking bar with direct labels instead of an axis — and
note the source order: **a bar chart plots the FIRST source row at the
BOTTOM** (measured: largest-first source order ships the largest bar at the
bottom), so list a ranking bar's rows ASCENDING and the ranking reads
largest-first top-down:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"set_values","sheet":"Dashboard","range":"B33","values":[["Region","Revenue"],["West",187],["South",233],["East",298],["North",412]]},
  {"op":"create_chart","sheet":"Dashboard","type":"bar","dataRange":"B33:C37","at":"J8:P22","title":"Revenue by region · US$ 000s · FY2026 H1","legend":{"visible":false},"dataLabels":{"visible":true,"format":"#,##0"},"seriesColors":["#4472C4"]}
]}'
```

Order the source rows when they are values; never `sort_range` over live
formulas (relative references scramble) — re-derive the block in order
instead. Charts meant to be compared must plot the same measure over the
same window so their auto scales agree (axis min/max is not an op field —
scale honesty lives in the data, or in one shared chart). A stale or
superseded chart is removed, not abandoned:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"delete_chart","sheet":"Dashboard","index":1}
]}'
```

## 9. Dashboard layout grid

One screen, Z-flow, most important number top-left:

```
row 1-2   title band + unit/period subtitle (section 3)
row 4-6   KPI cards: label (9pt gray) / big number (18-24pt bold) /
          delta (+0.0%, semantic color) — sparkline beside the number
row 8-22  chart band: ~8 columns x 15 rows per chart, aligned edges
row 24+   the summary blocks feeding the charts, or a detail table
          (large detail belongs on the Data sheet)
```

Spacing: column A stays a 2-3 wide margin; cards and charts align to shared
column edges; blocks separate by whitespace, not boxes. One KPI card:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"resize_columns","sheet":"Dashboard","from":"A","width":2},
  {"op":"resize_columns","sheet":"Dashboard","from":"B","width":16},
  {"op":"resize_columns","sheet":"Dashboard","from":"C","width":10},
  {"op":"set_values","sheet":"Dashboard","range":"B4","values":[["Revenue (US$ 000s)"]]},
  {"op":"format_range","sheet":"Dashboard","range":"B4","style":{"size":9,"color":"#808080"}},
  {"op":"set_formula","sheet":"Dashboard","range":"B5","formula":"=SUM(C31:H31)"},
  {"op":"format_range","sheet":"Dashboard","range":"B5","style":{"bold":true,"size":20,"color":"#1F3864","numberFormat":"#,##0;(#,##0);\"-\""}},
  {"op":"set_formula","sheet":"Dashboard","range":"B6","formula":"=IFERROR(H31/G31-1,0)"},
  {"op":"format_range","sheet":"Dashboard","range":"B6","style":{"size":9,"numberFormat":"+0.0%;-0.0%;0.0%"}},
  {"op":"add_conditional_format","sheet":"Dashboard","range":"B6","rule":{"kind":"cellValue","operator":"greaterOrEqual","value1":"0"},"style":{"color":"#548235"}},
  {"op":"add_conditional_format","sheet":"Dashboard","range":"B6","rule":{"kind":"cellValue","operator":"lessThan","value1":"0"},"style":{"color":"#C00000"}},
  {"op":"add_conditional_format","sheet":"Dashboard","range":"B6","rule":{"kind":"iconSet","iconSet":"arrows3"}},
  {"op":"add_sparkline","sheet":"Dashboard","dataRange":"C31:H31","location":"C5","type":"line","color":"#4472C4"},
  {"op":"resize_rows","sheet":"Dashboard","from":5}
]}'
```

The KPI number is a formula against the data — never a pasted value. The
delta carries three redundant cues (sign in the format, semantic color,
`arrows3` icon), so it survives grayscale and colorblind readers. A width-12
column shows a six-figure KPI at 20pt as `#####` (probe-measured); width 16
holds it — which is why the card columns above are 16. Sparkline types:
`line` for trend, `column` for period volumes, `winloss` for above/below. A
sparkline cell must fall inside the sheet's used range to show — beside a
KPI it always does. The final `resize_rows` (no height) auto-fits the 20pt
number's row.

Sheet chrome — tabs colored by role, reading order = tab order, dashboard
first, gridlines off on the dashboard only:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"set_tab_color","sheet":"Dashboard","color":"#1F3864"},
  {"op":"set_tab_color","sheet":"Config","color":"#4472C4"},
  {"op":"set_tab_color","sheet":"Data","color":"#808080"},
  {"op":"move_sheet","sheet":"Dashboard","position":0},
  {"op":"set_sheet_view","sheet":"Dashboard","gridlines":false},
  {"op":"set_page_setup","sheet":"Dashboard","orientation":"landscape","paperSize":"a4","fitToWidth":1,"fitToHeight":1},
  {"op":"set_print_area","sheet":"Dashboard","range":"A1:Q37"}
]}'
```

Honest caveat (probe-measured): `gridlines` is a VIEW setting — Excel and
the live preview honor it; a PNG `render` never draws view gridlines
either way. Set it for the human opening the file in Excel; judge the
rendered look by the borders and fills you actually drew.

## 10. Synthetic data quality

When the user asks for a demo/example sheet and supplies no data, the data
model IS the deliverable's credibility:

- A complete register, not three columns: id (`SO-2026-001` scheme), real
  date, category, region/segment, owner, quantity, unit price — and amount
  as a FORMULA (`=qty*price`), never a typed product.
- At least 24 rows over a believable period, with seasonality and an uneven
  distribution — reality is never uniform; a flat demo reads as fake.
- Names, products and currency match the request's locale — Chinese names
  and 人民币 for a Chinese-language request, not "John Smith" and `$`.
- At least one derived dimension (a month key, a margin, an age bucket) so
  the summary layer has something honest to aggregate.
- The deliverable for an open-ended "make me a sales sheet" is Detail +
  Dashboard — never a bare grid.

Seed of the register the recipes in this document run against (6 rows shown;
a real deliverable gets >= 24 — note dates are `=DATE()` formulas, because
`set_values` would store `"2026-01-12"` as text):

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"set_values","sheet":"Data","range":"A1","values":[["Order id","Date","Category","Region","Owner","Qty","Unit price","Amount","Month"]]},
  {"op":"set_values","sheet":"Data","range":"A2","values":[["SO-2026-001",null,"Hardware","North","Chen Wei",12,420,null,null],["SO-2026-002",null,"Software","East","Maria Silva",3,1150,null,null],["SO-2026-003",null,"Services","North","Chen Wei",8,260,null,null],["SO-2026-004",null,"Hardware","West","Priya Nair",20,395,null,null],["SO-2026-005",null,"Software","South","Tom Baker",5,1150,null,null],["SO-2026-006",null,"Services","East","Maria Silva",14,270,null,null]]},
  {"op":"set_formula","sheet":"Data","range":"B2","formula":"=DATE(2026,1,12)"},
  {"op":"set_formula","sheet":"Data","range":"B3","formula":"=DATE(2026,1,27)"},
  {"op":"set_formula","sheet":"Data","range":"B4","formula":"=DATE(2026,2,3)"},
  {"op":"set_formula","sheet":"Data","range":"B5","formula":"=DATE(2026,2,19)"},
  {"op":"set_formula","sheet":"Data","range":"B6","formula":"=DATE(2026,3,6)"},
  {"op":"set_formula","sheet":"Data","range":"B7","formula":"=DATE(2026,3,24)"},
  {"op":"set_formula","sheet":"Data","range":"H2:H7","formula":"=F2*G2"},
  {"op":"set_formula","sheet":"Data","range":"I2:I7","formula":"=TEXT(B2,\"yyyy-mm\")"},
  {"op":"set_formula","sheet":"Data","range":"H8","formula":"=SUM(H2:H7)"},
  {"op":"format_range","sheet":"Data","range":"H8","style":{"bold":true}}
]}'
```

A `=DATE()` cell reads back `t: "number"` until its `yyyy-mm-dd` format
lands (section 7) — the serial is the real date either way, and `TEXT()`
month keys compute from it immediately.

## 11. Live demo protocol

When the user wants to SEE the work happen (or asks for a preview URL),
start the preview FIRST and hand over the URL before building anything:

```sh
aspose-cli preview deliverable.xlsx --open --output json
```

The command returns immediately; the startup envelope carries the `url` —
hand it over immediately. Then build in 3-5 `edit` batches, not one: data →
structure → formats → charts → polish. Each save updates the preview in
place: the changed cells flash where they are, and `--fx demo` adds a
pointer that travels to them — the batch sequence narrates the build.
End with the delivery summary. Section 12 still applies: the preview is the
user's view, the render+LOOK is yours. Details: `aspose-cli docs preview`.

## 12. The finishing pass (the eyes loop)

Mandatory before delivery, in this order — each step exists because a later
one depends on it:

1. `set_default_font` (new workbooks — actually the FIRST op, section 2)
2. column widths, then row heights/auto-fits
3. number formats (section 7)
4. borders (section 5)
5. freeze panes → tab colors + `move_sheet` order → `set_sheet_view`
6. print setup (`set_page_setup` + `set_print_area`, every deliverable sheet)
7. **review the workbook and LOOK at every sheet image** — then fix and
   review again until every box below ticks, for at most three rounds.
   Judge widths and truncation from a `--range` render of the block you
   changed.

```sh
aspose-cli review book.xlsx --out scratch/book.review-1 --output json
aspose-cli cells render book.xlsx --sheet Data --range A1:I12 --out scratch/data.png --dpi 192 --overwrite
aspose-cli cells inspect book.xlsx --detail errors --output json
```

Self-grade against this checklist while looking at the PNGs:

```
[ ] Title band: title + unit declaration + period on every deliverable sheet
[ ] No ####, no truncated text, no tofu boxes (CJK judged at >= 150 dpi)
[ ] One font family; hierarchy only by size/weight/gray
[ ] <= 8 colors, each with a meaning; no decoration-only color
[ ] Header contrast: dark fill always paired with white bold text
[ ] Number discipline: negatives (), zeros "-", consistent decimals,
    year as 2026, units declared once
[ ] Charts: message titles, no default "Chart 1", single series has no
    legend, labels only when <= 6 points, series colors deliberate
    (tokens or the default navy ramp — a hue change means a meaning)
[ ] Ranking bars read largest-first top-down (source rows ascending)
[ ] Totals: bold, L2/L3 borders (double under grand totals)
[ ] Freeze at the label/data boundary; print setup on deliverable sheets
[ ] Tabs: semantic names (no Sheet1), role colors, reading order
[ ] No empty sheets, no placeholder text, no leftover scratch cells
[ ] Spot-check: 2-3 summary numbers re-read from the engine match the
    detail they claim to summarize
```

The render mechanics — DPI floors, the strict `--range` view that exposes
truncation a full-sheet render hides, review coverage — are Tier 2 of
`aspose-cli docs verification`; this checklist is the design bar layered on top
of those tiers, not a replacement. `cells inspect --detail errors` must report zero
formula errors before anything ships.
