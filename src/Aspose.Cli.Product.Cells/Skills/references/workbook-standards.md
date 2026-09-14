# Workbook standards

These are delivery standards, not decoration: the floor between a data dump
and a workbook a professional can use. When a file already has a look,
match its existing conventions instead of imposing these. The design pass
on top of this floor — tokens, title bands, the border hierarchy, charts,
dashboards — is `aspose-cli docs design-system`.

Recipes below assume a report book (`Sales` summary, `Config` assumptions,
`Data` dataset); adjust names and ranges. Each is one atomic `edit` batch —
fold them into a larger batch. In-place edits to a user-supplied file take
the backup copy first (skill, Editing section).

## Column widths

Widths are saved in the file; nothing re-fits when the user opens it, and a
value wider than its column shows truncated or as `###`. Every column a
human reads gets an explicit width (characters): label columns 24–32, dates
and formatted numbers 11–14, short codes 6–10. Omitting `width` auto-fits
to today's content — a floor, not a standard: next month's entry still
truncates.

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"resize_columns","sheet":"Sales","from":"A","width":28},
  {"op":"resize_columns","sheet":"Sales","from":"B","width":8},
  {"op":"resize_columns","sheet":"Sales","from":"C","to":"G","width":12}
]}'
```

## Number formats

Raw numbers are not deliverable. Set formats with `format_range`'s
`numberFormat` (an Excel format code):

| data | format code |
|------|-------------|
| money in financial tables | `#,##0.00;(#,##0.00);"-"` — thousands, negatives in parentheses, zeros as `-` |
| counts | `#,##0;(#,##0)` |
| percentages | `0.0%;(0.0%)` — one decimal |
| dates | `yyyy-mm-dd` — unambiguous in every locale |
| years | `0`, or text — `2,026` is never a year |

The report-grade codes layered on these (variance `+0.0%;-0.0%;0.0%`,
multiples `0.0"x"`, summary money with `-` zeros) are `aspose-cli docs
design-system`, Number and unit discipline.

Money, counts and percentages each carry an explicit negative section, and
all three use the same one. A format code with no `;` section signs its
negatives with a leading minus, so mixing the two conventions prints the same
fact two ways in adjacent columns — `(170.00)` next to `-1.4%` — leaving the
reader to work out that nothing is wrong. Pick the parentheses the money
format already sets and give the others the matching section. Rendered and
checked at a negative of each: `(170.00)`, `(1,234)`, `(1.4%)`.

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"format_range","sheet":"Sales","range":"E2:F12","style":{"numberFormat":"#,##0.00;(#,##0.00);\"-\""}},
  {"op":"format_range","sheet":"Sales","range":"D2:D11","style":{"numberFormat":"#,##0;(#,##0)"}},
  {"op":"format_range","sheet":"Sales","range":"C2:C9","style":{"numberFormat":"yyyy-mm-dd"}},
  {"op":"format_range","sheet":"Sales","range":"G2:G9","style":{"numberFormat":"0.0%;(0.0%)"}}
]}'
```

A format only bites on a real value: `set_values` types exactly what you
send, so `"2026-04-03"` stays text and ignores a date format. Write dates
as `=DATE(2026,4,3)` formulas or date serials — `query range` shows
`"t": "datetime"` when you got it right.

The CSV import is the exception, and the contrast decides how much work you
owe: `convert` coerces ISO-8601 dates into real datetimes *and* applies
`yyyy-mm-dd` itself, so a CSV-sourced date column arrives finished.

```sh
printf 'id,opened,amount\n1,2026-03-02,1200\n' > tickets.csv
aspose-cli cells convert tickets.csv --to xlsx
aspose-cli cells query range tickets.xlsx --sheet tickets --range B2 --scope full --output json
# -> {"v":"2026-03-02","t":"datetime"}, style numberFormat "yyyy-mm-dd"
```

The identical string through `set_values` stays `"t": "string"` forever. Only
ISO-8601 input is verified here; ambiguous forms like `03/04/2026` are not —
`query range` the column back and check `t` rather than assuming either way.

## Fonts and headers

Pick one font family and make it the workbook default as **step 0** —
`set_default_font` rewrites the Normal style every unstyled cell derives
from, and column width units are measured in that font, so it comes before
content and widths (Arial 10 → Calibri 11 is the probe-measured
metric-neutral swap; other fonts rescale rendered widths). Then still set
the family explicitly on the ranges you author — belt and braces for files
that predate the default change, where cells styled under the old default
keep their concrete font. Mixed families across the ranges you wrote read
as carelessness. Header rows are bold, filled dark, white text, slightly
taller, and frozen so they survive scrolling:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"set_default_font","name":"Calibri","size":11},
  {"op":"format_range","sheet":"Sales","range":"A1:G12","style":{"font":"Calibri","size":11}},
  {"op":"format_range","sheet":"Sales","range":"A1:G1","style":{"bold":true,"bg":"#1F4E79","color":"#FFFFFF"}},
  {"op":"resize_rows","sheet":"Sales","from":1,"height":20},
  {"op":"freeze_panes","sheet":"Sales","cell":"A2"}
]}'
```

**A bigger font does not re-fit its row.** `format_range {"size":16}` on a
title leaves the row at its old height and the render comes back with the
glyph tops shaved off — on every sheet you did it to. `query range` cannot catch
this: the value is perfectly intact. Auto-fit the row by OMITTING `height`:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"resize_rows","sheet":"Sales","from":1}
]}'
```

Verified by render: a clipped 16pt title comes back whole. Note this inverts
the column rule above — a row's height should track its font, so auto-fit is
the standard for any row whose font you enlarged, while an explicit `height`
(the `20` above, chosen for 11pt) is a promise you must revisit the moment
the font grows. Fix the row in the same batch that sets the size.

The full header treatment — the medium rule under the header, wrap plus a
row auto-fit for long names, the type scale above 11pt body — is `aspose-cli
docs design-system`, Header treatment.

"One font per workbook" now has a real lever: the Normal style is Arial 10
until `set_default_font` changes it, and every cell without an explicit
font follows the new default — verify with a render, where the letterforms
change. What you cannot chase to zero is the *declared* list: the file
keeps an inert record of the pre-swap default, so expect it listed next to
your family even when every visible cell is yours (measured: a new
workbook whose first batch sets Calibri still declares both) — that is a
leftover, not a stray cell:

```sh
aspose-cli cells inspect book.xlsx --detail fonts --output json   # workbook.fonts -> ["Arial","Calibri"]
```

## Formulas, not hardcodes

If a number can be computed from other cells, it must be a formula — a
hardcoded aggregate breaks the workbook silently the first time an input
changes. Name reused assumptions with `define_name` and reference the name
in formulas; when a constant must stay hardcoded, record its source with
`add_comment` on the cell.

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"set_formula","sheet":"Sales","range":"F11","formula":"=SUM(F2:F9)"},
  {"op":"define_name","name":"VatRate","refersTo":"Config!$B$1"},
  {"op":"set_values","sheet":"Sales","range":"A12","values":[["VAT due"]]},
  {"op":"set_formula","sheet":"Sales","range":"F12","formula":"=F11*VatRate"},
  {"op":"add_comment","sheet":"Sales","cell":"E8","text":"Refund priced at the 2026 list rate — source: pricing sheet v3.","author":"agent"}
]}'
```

## Input cells get validation

Any cell a human will type into gets `set_validation` — a list of the legal
values, or numeric bounds. It protects the person who opens the file in
Excel: they get a dropdown of the legal values and a rejection on a typo
instead of a downstream `#N/A` hunt.

It does not protect you. Validation is a rule stored in the file, not a
write-time check — the CLI's own writes ignore it. `--set "Sales!B2=Bogus"`
on a list-validated cell exits 0 and stores `Bogus`, rule still intact. Your
own writes stay yours to verify (`aspose-cli docs verification`).

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"set_validation","sheet":"Sales","range":"B2:B9","type":"list","listItems":["HW","SW","SVC"],"inputMessage":"Product line code"},
  {"op":"set_validation","sheet":"Config","range":"B1","type":"decimal","operator":"between","value1":"0","value2":"1","errorMessage":"VAT rate is a fraction between 0 and 1"}
]}'
```

## Conditional formatting, sparingly

Highlight only exceptions a reader must act on — a sheet where everything
is colored says nothing. A rule's `style` is differential: it overrides the
fields you set on matching cells and leaves the rest — a money or percent
column keeps its number format and font under the highlight. Use
`colorScale` or `dataBar` when the message is magnitude rather than a
threshold; an `iconSet` rule (`arrows3`, `trafficLights3`, …) reads well on
a short status or delta column, but its thresholds are automatic terciles —
use it only when "top/middle/bottom third" is actually the message, and
never beside a color scale saying the same thing.

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"add_conditional_format","sheet":"Sales","range":"F2:F9",
   "rule":{"kind":"cellValue","operator":"lessThan","value1":"1000"},
   "style":{"color":"#9C0006","bg":"#FFC7CE"}}
]}'
```

`value1`/`value2` take a literal **or** an `=`-led formula — the second form
is how a threshold stops being a constant. The formula anchors on the range's
top-left cell and shifts per cell exactly like `set_formula` fill semantics,
so mixed anchors (`$B2`) express a per-row, cross-column rule: flag every
actual above *its own row's* target, in one op.

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"add_conditional_format","sheet":"Sales","range":"A2:A4",
   "rule":{"kind":"cellValue","operator":"greaterThan","value1":"=$B2"},
   "style":{"color":"#9C0006","bg":"#FFC7CE"}}
]}'
```

`A2` compares against `B2`, `A3` against `B3`, `A4` against `B4`. When the
highlight should cover the whole ROW rather than the compared cell, use the
`formula` rule kind over the full-width range with the tested column
`$`-anchored (`{"kind":"formula","value1":"=$F2=\"OVERDUE\""}` over
`A2:F100`) — the recipe is in `aspose-cli docs editing`. Render and look before
trusting the anchors: a stray `$` on the row number silently pins every
comparison to one cell, and the rule still applies cleanly — it just
answers a different question than you asked.

## Real tables for datasets

Row-oriented data becomes a native table: filter dropdowns, banded styling
and structured references in one op. For a plain range that only needs the
dropdowns, use `set_autofilter` — never both on the same range.

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"create_table","sheet":"Data","range":"A1:D13","name":"Orders","style":"TableStyleMedium2"}
]}'
```

## Print-ready delivery

When the user will print or send the file as a report, set the page as part
of delivery, not as an afterthought. Two shapes cover most cases. A short
summary sheet — portrait, fitted to one page, a page footer:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"set_page_setup","sheet":"Sales","orientation":"portrait","paperSize":"a4",
   "fitToWidth":1,"fitToHeight":1,"footer":"page &P of &N"},
  {"op":"set_print_area","sheet":"Sales","range":"A1:G12"}
]}'
```

A tall data table — one page wide, as many pages tall as it takes
(`"fitToHeight":0`), header row repeated on every page:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"set_page_setup","sheet":"Data","orientation":"landscape","fitToWidth":1,"fitToHeight":0},
  {"op":"set_print_area","sheet":"Data","range":"A1:D13","titleRows":"1:1"}
]}'
```

`margins` (inches) and `scale` are set the same way; prove the pagination
with a PDF convert before delivery.

## The model color language

In analytical and financial workbooks color is information: blue text =
hardcoded input, black text = formula, green text (`#008000`) = pulled from
another sheet, yellow fill = assumption pending review. Applied
consistently, a reader — or the next agent — can tell at a glance what is
safe to change.

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"format_range","sheet":"Sales","range":"D2:E9","style":{"color":"#0000FF"}},
  {"op":"format_range","sheet":"Config","range":"B1","style":{"color":"#0000FF","bg":"#FFF2CC"}}
]}'
```

## Limits

What the v2 ops cannot express — say so rather than faking it:

- Borders are no longer a limit: `set_borders` draws outline, inside-grid
  and single-edge borders per range — see `aspose-cli docs editing`; the
  L0-L4 hierarchy that gives each line weight a meaning (thin top over
  subtotals, double bottom under grand totals) is `aspose-cli docs
  design-system`, Border hierarchy.
- No per-cell lock: `protect_sheet` locks every cell (with verb-level
  `allow` exceptions), so "locked sheet except the input cells" is not
  expressible.
- Underline, strikethrough and indent are no longer limits: they are
  `format_range` style fields — see `aspose-cli docs editing`.
- Page `header`/`footer` set the center section only; left/right sections
  are not expressible.
- Sheet cosmetics are no longer a limit: `set_tab_color` and
  `set_sheet_view` (gridlines, zoom, headings) cover them — see
  `aspose-cli docs editing`. Note `set_sheet_view` gridlines are a VIEW
  setting: Excel and the live preview honor them, PNG renders never do
  (draw `set_borders` when a grid must appear in a render).
- No calculation settings (iterative calculation, manual mode).
- Conditional-format kinds are no longer the old short list: `formula`
  (whole-row highlighting via a `$`-anchored column), `topBottom` and
  `iconSet` join `cellValue`, `colorScale`, `dataBar`, `duplicates` — see
  `aspose-cli docs editing`. Still not expressible: text-contains /
  date-period / above-average rules, rule priority and stop-if-true, and
  custom icon thresholds or reversed icon order (iconSet thresholds are
  automatic).
- Chart cosmetics are no longer the old dead end: `create_chart` and
  `update_chart` take `legend` (visibility, right/bottom/top/left),
  `axisTitles` (rejected on `pie` — it has no axes), `seriesColors` and
  `dataLabels` (visibility + number format); `create_chart` applies a
  modern default look by itself (white plot, bottom legend, series
  palette), and `delete_chart` removes a chart — see `aspose-cli docs editing`.
  Still not expressible: fonts inside charts (title/axis/label typefaces
  and sizes), axis scale and bounds, and label content beyond the value
  (no category/percentage labels). Unknown operation or style fields are
  rejected with `OPS_INVALID`; use the schema's field names.
- `create_chart` plots ONE contiguous `dataRange`. A multi-area reference
  fails `OPS_INVALID` ("a range has at most one ':' separator"), so
  "header + two non-adjacent rows" (`A1:F1,A3:F4`) is not directly
  chartable — mirror the rows into a contiguous helper block with formulas
  and chart that, keeping the block live:

```sh
aspose-cli cells edit book.xlsx --in-place --ops '{"ops":[
  {"op":"set_formula","sheet":"Data","range":"A7:F7","formula":"=A1"},
  {"op":"set_formula","sheet":"Data","range":"A8:F8","formula":"=A2"},
  {"op":"set_formula","sheet":"Data","range":"A9:F9","formula":"=A4"},
  {"op":"create_chart","sheet":"Data","type":"column","dataRange":"A7:F9","at":"H2:P18",
   "title":"Revenue vs Profit","seriesInRows":true}
]}'
```

  The helper block is visible on the sheet — park it below the data or on a
  side sheet, and never hand-copy the values, or the chart silently stops
  tracking the model.
- The default (Normal) style is no longer immutable: `set_default_font`
  changes its font (and size) workbook-wide. Call it FIRST — column width
  units are measured in the Normal font, so a later swap rescales rendered
  widths (Arial 10 → Calibri 11 is the one probe-measured metric-neutral
  pair).
- `set_validation` constrains a human typing in Excel, not the CLI: an ops
  batch or `--set` writes any value into a validated cell and exits 0.

## Verify, then deliver

A standards pass is only done when verified: `render` each changed sheet
and look at the image, `query range` back computed cells, and run
`cells inspect --detail errors` for formula errors — the full loop is
`aspose-cli docs verification`.
