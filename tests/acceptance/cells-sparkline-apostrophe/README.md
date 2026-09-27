# Cells sparkline apostrophe acceptance: CELLS-SPARKLINE-APOSTROPHE

This explicitly invoked gate asserts that the one-call `SparklineGroups.Add` accepts a data range
on a sheet whose name contains an apostrophe, correctly quoted. It
**fails on Aspose.Cells 26.9.0** in both licensed and evaluation modes. It is outside normal
regression discovery under `tests/acceptance`, adds no project or dependency, and neither skips
failures nor accepts the broken result as its baseline.

## Run

Use PowerShell 7 after a normal repository build. A license is optional; the defect does not
depend on it. Run from the repository root:

```powershell
./tests/acceptance/cells-sparkline-apostrophe/reproduce.ps1 `
  -SdkDir ./src/Aspose.Cli/bin/Release/net10.0 `
  -OutputDirectory ./artifacts/cells-sparkline-apostrophe/run-01
```

Choose a fresh output directory on every run. Through the public SDK only, the script adds one
line sparkline for three placements: data and group on `O'Brien`, data on `O'Brien` with the
group on `Dash`, and data on `Data` with the group on `O'Brien`:

```csharp
sheet.SparklineGroups.Add(SparklineType.Line, "'O''Brien'!A1:C1", false, CellArea.CreateCellArea(4, 4, 4, 4));
```

`result.json` records the SDK version and hash and each case's outcome. The script returns **0
when every case succeeds**, **1 when a case throws `Invalid "'"`** and **2 when the check could
not run**.

## Observed behavior

With SDK 26.9 the two cases whose data lies on `O'Brien` throw `CellsException: Invalid "'"`,
whether the name is quoted, doubled or bare; the third succeeds. An unqualified range throws the
same on a host sheet whose name has an apostrophe. `SparklineCollection.Add` on a
group made by `SparklineGroups.Add(type)` accepts the same range, but that group has no colours
until a preset style is set, and reading its `PresetStyle` first throws `NullReferenceException`.

## Handling

KNOWN-ISSUES.md describes the CLI's handling of this defect under the gate's id, and the code
that handles it names the id. While this gate exits 1 the release proceeds; when it exits 0,
delete the issue, its handling and this gate. Do not make it pass by rewriting input or output,
or by weakening its assertion.

Official references:

- [Sparklines](https://docs.aspose.com/cells/net/using-sparklines/)
- [SparklineGroupCollection.Add](https://reference.aspose.com/cells/net/aspose.cells.charts/sparklinegroupcollection/add/)
