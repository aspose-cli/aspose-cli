# Example: CSV → formatted report with totals and a chart → PDF

Inputs: [sales.csv](sales.csv), [ops.json](ops.json). Run from this directory.

```powershell
# 1. CSV becomes a real workbook; its data sheet is named sales.
aspose-cli cells convert sales.csv --to xlsx --out report.xlsx --overwrite --output json

# 2. One atomic batch: totals row+column, header styling, widths, chart.
aspose-cli cells edit report.xlsx --ops ops.json --in-place --output json

# 3. Verify numerically (totals are engine-recalculated)...
aspose-cli cells query range report.xlsx --sheet sales --range E1:E6 --scope formulas --output json

# 4. ...and visually, then deliver as PDF.
aspose-cli review report.xlsx --out report.review --output json
aspose-cli cells convert report.xlsx --to pdf --sheet sales --overwrite --output json
```

This CSV is a plain table: its header is row 1 and step 1 reports no
`TEXT_TABLE_LAYOUT` warning. An export with a title or query-condition rows
before the header, empty rows or a trailing total row gets one such warning per
finding; shift the ranges in `ops.json` to the header and data rows the
messages name, and end sums before the total row
(`aspose-cli docs cells/troubleshooting`).

Auto-fitting columns calculates the batch's formulas first, so widths account
for the formatted totals.

Expected outcome: `report.xlsx` has a bold header on a dark fill, a
`Total` column E with `=SUM(B2:D2)` filled down (E2 = 4450), a totals
row 6, a column chart titled "Quarterly Sales", and `report.pdf` matches
the rendered layout. In evaluation mode every produced file additionally
carries the Aspose evaluation watermark — tell your user.
