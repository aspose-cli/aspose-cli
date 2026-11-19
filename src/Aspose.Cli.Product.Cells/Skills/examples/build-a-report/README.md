# Build a report

Copy this Skill to a fresh writable directory, then change into examples/build-a-report.
[data-ops.json](data-ops.json) names the new workbook's sheet, writes a table of four regions by
four quarters with a total formula per row, formats the header and the numbers, fits the columns,
freezes the header, calculates, and adds an empty column chart;
[chart-ops.json](chart-ops.json) gives the chart a series per region at the address the first
edit returned.

```powershell
aspose-cli cells create report.xlsx --output-format json
aspose-cli cells edit report.xlsx --ops data-ops.json --out report.1.xlsx --output-format json
aspose-cli cells edit report.1.xlsx --ops chart-ops.json --out report.2.xlsx --output-format json
aspose-cli cells read report.2.xlsx --at "sheet/Sales/range/A1:F5" --output-format json
aspose-cli cells convert report.2.xlsx --out report.pdf --output-format json
```

The engine's new workbook has one sheet, `Sheet1`; the first edit renames it, so later
operations address `sheet/Sales`. Its `ops` give the new chart's address, `sheet/Sales/chart/1`;
in a workbook of your own, take it from your first edit's result. `add series` with
`isVertical: false` adds one series per row of `B2:E5`, `series/0` to `series/3`. The `calculate`
action stores the totals' results, which the read returns in `F2:F5`. Look at the PDF before
handing the workbook over.
