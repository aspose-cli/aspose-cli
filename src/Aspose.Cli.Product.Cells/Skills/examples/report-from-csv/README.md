# Example: CSV → formatted report with totals and a chart → PDF

Inputs: [sales.csv](sales.csv), [ops.json](ops.json). Run from this directory.

```sh
# 1. CSV becomes a real workbook; its data sheet is named sales.
aspose-cli cells convert sales.csv --to xlsx --out report.xlsx --overwrite

# 2. One atomic batch: totals row+column, header styling, widths, chart.
aspose-cli cells edit report.xlsx --ops ops.json --in-place --output json

# 3. Verify numerically (totals are engine-recalculated)...
aspose-cli cells query range report.xlsx --sheet sales --range E1:E6 --scope formulas --output json

# 4. ...and visually, then deliver as PDF.
aspose-cli cells render report.xlsx --sheet sales --range A1:L20 --out report.png --overwrite
aspose-cli cells convert report.xlsx --to pdf --sheet sales --overwrite
```

The batch recalculates after styling and before auto-fitting columns, so
widths account for the formatted totals.

Expected outcome: `report.xlsx` has a bold header on a dark fill, a
`Total` column E with `=SUM(B2:D2)` filled down (E2 = 4450), a totals
row 6, a column chart titled "Quarterly Sales", and `report.pdf` matches
the rendered layout. In evaluation mode every produced file additionally
carries the Aspose evaluation watermark — tell your user.
