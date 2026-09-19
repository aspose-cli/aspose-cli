# Financial models

This layer is for formula-driven analytical models: budgets, forecasts,
3-statement models, DCF-style valuations, scenario analysis. For trackers,
CSV reports and dashboards go back to `aspose-cli docs workbook-standards` —
and everything taught there (widths, number formats, the color language,
print delivery) is inherited here, not repeated. (This document is also
served offline as `aspose-cli docs financial-models`.) Recipes below build a
small 3-statement model — `model.xlsx`, sheets `Assumptions` / `PnL` /
`Summary`; every recipe ran against the real engine, and every quoted
number is an engine read-back, not arithmetic.

## The three zones

Every model separates Inputs (assumption sheets), Calc (statement sheets)
and Outputs (summary sheets). The convention is carried by sheet NAMES and
tab colors, plus the cell color language from workbook-standards — the
ink tokens (blue input / black formula / green cross-sheet) are
standardized in `aspose-cli docs design-system`, Design tokens:

```sh
aspose-cli cells create model.xlsx --sheets "Assumptions,PnL,Summary"
```

Prefer plain names — `&` or spaces force quoting everywhere (`='P&L'!B3*2`,
`--set "'P&L'!B3=42"`; both verified, both noise). `protect_sheet` on Calc
and Output sheets keeps humans out in Excel while Assumptions stays open:
zone granularity is sheet granularity, so no-per-cell-lock never bites.

The rule that makes the zones real: **Calc sheets contain zero hardcoded
numbers** — every figure is a formula or comes from Assumptions. The audit
is executable — read the calc block in full scope and demand `f` on every
non-empty cell:

```sh
aspose-cli cells query range model.xlsx --sheet PnL --range B4:D14 --scope full --output json
```

Adopt ONE sign convention up front, FAST-style, and state it on the
Assumptions sheet: either all flows positive with labeled deductions, or
costs negative throughout — never mixed, so a reader never guesses whether
to add or subtract a line. The negative-parentheses number formats
(`aspose-cli docs design-system`, Number and unit discipline) then read
unambiguously.

A cell with `v` but no `f` is a hardcode: move it to Assumptions. To catch
a magic constant buried in formula text (`=B7*0.25` instead of
`=B7*TaxRate`), sweep formulas for the literal — must return zero hits:

```sh
aspose-cli cells query search model.xlsx "0.25" --in formulas --output json
```

## Build order

Assumptions first, then statements along the dependency chain, Summary
last. Edits recalculate at the end by default. Queries read stored formula
results, so do not treat an imported cache or an edit made with `--no-recalc`
as freshly calculated.

In fact the whole model fits in ONE atomic batch: ops apply in order and
recalculation runs once at the end, so a `define_name` mid-batch resolves
in formulas set later in the same batch, across sheets. Verified:

```sh
aspose-cli cells edit model.xlsx --in-place --ops '{"ops":[
  {"op":"set_values","sheet":"Assumptions","range":"A3","values":[["Revenue Y1",1000],["Growth rate",0.08]]},
  {"op":"define_name","name":"RevenueY1","refersTo":"Assumptions!$B$3"},
  {"op":"define_name","name":"GrowthRate","refersTo":"Assumptions!$B$4"},
  {"op":"set_formula","sheet":"PnL","range":"B4","formula":"=RevenueY1"},
  {"op":"set_formula","sheet":"PnL","range":"C4:D4","formula":"=B4*(1+GrowthRate)"}
]}'
aspose-cli cells query range model.xlsx --sheet PnL --range B4:D4 --output json
```

reads back 1000, 1080, 1166.4 — computed, cross-sheet, one recalc, and
atomic: any bad op leaves the file untouched. Batch at least per sheet;
never one command per cell.

## Named assumptions

Any assumption referenced three or more times gets a `define_name` with a
sheet-qualified absolute `refersTo` (`Assumptions!$B$4`). Formulas then
read as the model's own language — `=B4*(1+GrowthRate)`,
`=NPV(DiscountRate,PnL!B9:D9)` — instead of `=B4*(1+Assumptions!$B$4)`.
Confirm with `--scope formulas` that the name really landed in the formula
text (`"f": "=RevenueY1"`) and computes. Audit the list before delivery —
`workbook.definedNames` holds every name and its `refersTo`:

```sh
aspose-cli cells inspect model.xlsx --detail names --output json
```

A defined name no formula uses is dead decoration — delete it:

```sh
aspose-cli cells edit model.xlsx --in-place --ops '{"ops":[{"op":"delete_name","name":"OldRate"}]}'
```

`define_name` on an existing name repoints it in place (no error), so a
rename is delete + define + re-check every formula that used the old name.

## Balance and reconciliation checks

A model must audit itself: one check row per statement, one rollup cell on
the summary. `set_formula` over the row fills per period column (relative
references shift), and a conditional format makes a failure impossible to
miss:

```sh
aspose-cli cells edit model.xlsx --in-place --ops '{"ops":[
  {"op":"set_formula","sheet":"PnL","range":"B14:D14","formula":"=IF(ABS(B12-B13)<0.01,\"OK\",\"IMBALANCED\")"},
  {"op":"set_formula","sheet":"Summary","range":"B6","formula":"=IF(COUNTIF(PnL!B14:D14,\"IMBALANCED\")=0,\"OK\",\"IMBALANCED\")"},
  {"op":"add_conditional_format","sheet":"PnL","range":"B14:D14",
   "rule":{"kind":"cellValue","operator":"equal","value1":"IMBALANCED"},
   "style":{"color":"#9C0006","bg":"#FFC7CE"}}
]}'
```

Here B12 is closing cash and B13 equity, so the row proves the statements
articulate. The delivery gate greps for failures and must return zero hits:

```sh
aspose-cli cells query search model.xlsx "IMBALANCED|MISMATCH" --regex --output json
```

Keep the gate on the default values scope: the check formulas carry the
failure token in their TEXT by design, so `--in formulas` always "fails".
Verified: the clean model returns zero hits; hardcoding over one
closing-cash formula flipped two checks plus the rollup, and the gate
listed all three, sheet and cell.

## Scenario switching

One validated input cell selects the scenario; INDEX/MATCH rows pull the
active column from a scenario table (`C12:E12` headers, `C13:E13` values):

```sh
aspose-cli cells edit model.xlsx --in-place --ops '{"ops":[
  {"op":"set_values","sheet":"Assumptions","range":"C12","values":[["Base","Upside","Downside"],[0.08,0.15,0.02]]},
  {"op":"define_name","name":"Scenario","refersTo":"Assumptions!$B$10"},
  {"op":"set_validation","sheet":"Assumptions","range":"B10","type":"list","listItems":["Base","Upside","Downside"],"inputMessage":"Active scenario"},
  {"op":"set_values","sheet":"Assumptions","range":"B10","values":[["Base"]]},
  {"op":"set_formula","sheet":"Assumptions","range":"B4","formula":"=INDEX(C13:E13,MATCH(Scenario,$C$12:$E$12,0))"}
]}'
```

INDEX/MATCH computes correctly in this engine — flipping the one cell
re-prices the whole model:

```sh
aspose-cli cells edit model.xlsx --in-place --set "Assumptions!B10=Upside"
aspose-cli cells query range model.xlsx --sheet Summary --range B3 --output json
```

The summary NPV moved 301.27 → 320.98, and back when B10 returned to
`Base` — full precision against independent calculation. Caveat: validation constrains
humans in Excel, not programmatic writes; a typo'd scenario value lands
silently and turns MATCH into `#N/A` across every dependent cell — the
`cells inspect --detail errors` gate catches it.

### Restore the base scenario after a sweep

A sweep is a MUTATION, not a query. Every flip writes the file and leaves it
there, so reading three scenarios in a loop ships a model frozen on whichever
one you read last — usually Downside, and nothing in the delivery gates
notices. Restore the base as the final step, then prove it with a read-back:

```sh
aspose-cli cells edit model.xlsx --in-place --set "Assumptions!B10=Base"
aspose-cli cells query range model.xlsx --sheet Assumptions --range B10 --output json
aspose-cli cells query range model.xlsx --sheet Summary --range B3 --output json
```

Both reads must show the state you started from — the selector back on `Base`
and the summary back to its base number. Verified on a Base → Upside →
Downside sweep of a growth-rate model: the summary read 3246.4 / 3472.5 /
3060.4, the file stayed on Downside at 3060.4 after the loop, and the restore
put it back to 3246.4. The comparison table you built is the deliverable; the
file ships on Base.

## Sensitivity grids

There is no native what-if data table — the pattern is explicit formulas,
which are also auditable. Each grid cell is self-contained: the output
formula with the row driver (`$A10`, column pinned) and the column driver
(`B$9`, row pinned) substituted in. One `set_formula` fills the grid from
the top-left formula:

```sh
aspose-cli cells edit model.xlsx --in-place --ops '{"ops":[
  {"op":"set_values","sheet":"Summary","range":"B9","values":[[0.05,0.10,0.15]]},
  {"op":"set_values","sheet":"Summary","range":"A10","values":[[800],[1000],[1200]]},
  {"op":"set_formula","sheet":"Summary","range":"B10:D12","formula":"=$A10*(1+B$9)^2"}
]}'
```

Read the grid back and check opposite corners against expectation — here
Y3 revenue over Y1 revenue × growth: 882 / 1058 / 1323 / 1587, all exact,
and `--scope formulas` shows each cell holding its own shifted anchors.

## Functions that behave

| need | use | note |
|------|-----|------|
| regular annual flows | `NPV` / `IRR` | `NPV` discounts its FIRST argument one period — keep t=0 out: `=B2+NPV(0.1,B3:B6)` |
| dated, irregular flows | `XNPV` / `XIRR` | dates must be real datetimes — write them as `=DATE(y,m,d)` (workbook-standards) |
| lookups | `INDEX`/`MATCH` | survives inserted rows and columns; positional column-number lookups (VLOOKUP) break silently |
| weighted sums | `SUMPRODUCT` | one formula, no helper columns |
| every division | `IFERROR` or an `IF` on the denominator | a shipped `#DIV/0!` is a delivery failure |

Every row is engine-verified against independently computed expectations,
to full precision: flows (-1000, 300, 400, 500, 200) — NPV 1115.5659, IRR
15.3221%; the same flows on irregular dates (`=DATE(...)` in the dates
column, `=XNPV(0.1,B2:B6,A2:A6)`) — XNPV 230.7164, XIRR 29.0813%;
SUMPRODUCT and INDEX/MATCH exact; `IFERROR(1/0,"n/a")` returns the
fallback.

## No circular references

Iterative calculation is not exposed, so circular structures — interest on
average balance feeding the cash sweep that sets the balance — cannot be
switched on. Design acyclically instead: charge interest on the OPENING
balance, draw or repay the revolver in the NEXT period. This is also the
better model — a reviewer (or the next agent) can trace an acyclic chain
cell by cell.

## Delivery gates

The workbook-standards verification loop applies, plus the model-specific
gates — each executable, each with a hard pass condition:

```sh
aspose-cli cells inspect model.xlsx --detail errors --output json
aspose-cli cells query search model.xlsx "IMBALANCED|MISMATCH" --regex --output json
aspose-cli cells query range model.xlsx --sheet Summary --range B3:B6 --output json
aspose-cli cells query range model.xlsx --sheet Assumptions --range B10 --output json
aspose-cli cells render model.xlsx --sheet Summary --range A1:G20 --out summary-check.png --dpi 192
```

1. `workbook.formulaErrors` is empty — no `#N/A`, `#DIV/0!`, `#REF!`.
2. The check-token search returns zero hits.
3. Every summary and valuation cell read back — report these engine
   results after recalculation, not an assumed or stale cached value.
4. Swept the scenarios? The selector reads back on the base case, and the
   summary reads back at its base number (Scenario switching).
5. LOOK at the render with your image tool (truncation, layout, checks
   visibly OK). Keep `--dpi 192` and window with `--range`: a full-sheet
   render runs ~5% wide and hides truncation, and non-Latin labels below
   150 DPI change identity (`aspose-cli docs verification`).
6. Editing a user's model? The backup diff shows only intended changes — a
   formula replaced by a hardcode is visible as left-`f` / right-value-only.
   It compares values and formula text only, so a styling or chart pass on
   the model is expected to diff as `identical: true`.

The full protocol, tier by tier: `aspose-cli docs verification`.

## Limits

- No what-if data tables, no iterative calculation — the patterns in
  Sensitivity grids and No circular references are the replacements.
- `set_validation` and `protect_sheet` constrain humans in Excel, not
  engine writes (verified) — the model's real guards are the check rows
  and the delivery gates.
- Every function this document names evaluates correctly in the real
  engine; nothing in this layer had to be faked or approximated.
- Tab colors are no longer a limit (`set_tab_color`); the remaining
  cosmetic limits are inherited — see `aspose-cli docs workbook-standards`.
