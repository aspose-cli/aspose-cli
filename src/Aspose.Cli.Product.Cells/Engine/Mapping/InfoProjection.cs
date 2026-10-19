using Aspose.Cells;
using Aspose.Cells.Pivot;
using Aspose.Cells.Tables;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Builds the <c>cells inspect</c> projection: the workbook summary and the
/// optional detail sections (defined names, formula errors, fonts, tables,
/// charts, pivots, validations, sheet layouts). Detail scans are budgeted so a pathological
/// workbook cannot make <c>info</c> unaffordable.
/// </summary>
internal static class InfoProjection
{
    public static (CellsWorkbookSummary Summary, Warning? ListTruncated) Summarize(
        ResourceBudgetLedger budgets, Workbook workbook, CellsInspectRequest request)
    {
        var sheets = new List<CellsSheetInfo>(workbook.Worksheets.Count);
        for (int index = 0; index < workbook.Worksheets.Count; index++)
        {
            sheets.Add(BuildSheetInfo(workbook.Worksheets[index], index, request));
        }

        IReadOnlyList<CellsCellError>? formulaErrors = null;
        Warning? errorsTruncated = null;
        if (WantsDetail(request, CellsInspectDetails.Errors))
        {
            (formulaErrors, int total) = ScanFormulaErrors(budgets, workbook);
            if (total > formulaErrors.Count)
            {
                errorsTruncated = EnvelopeParts.ListTruncated(
                    "workbook.formulaErrors", formulaErrors.Count, total, FormulaErrorsCappedHint);
            }
        }

        var summary = new CellsWorkbookSummary
        {
            Name = Path.GetFileName(request.Input),
            SheetCount = sheets.Count,
            Sheets = sheets,
            HasMacros = workbook.HasMacro,
            DefinedNameCount = DefinedNames.Of(workbook).Count(),
            StructureProtected = Sheets.StructureProtected(workbook),
            StructurePasswordProtected = Sheets.StructurePasswordProtected(workbook),
            Author = Normalize(workbook.BuiltInDocumentProperties.Author),
            Title = Normalize(workbook.BuiltInDocumentProperties.Title),
            DefaultFont = DefaultFont(workbook),
            DefinedNames = WantsDetail(request, CellsInspectDetails.Names) ? BuildDefinedNames(workbook) : null,
            FormulaErrors = formulaErrors,
            Fonts = WantsDetail(request, CellsInspectDetails.Fonts) ? FontOps.UsedFonts(workbook) : null,
            Tables = WantsDetail(request, CellsInspectDetails.Tables) ? BuildTables(workbook) : null,
            Charts = WantsDetail(request, CellsInspectDetails.Charts) ? BuildCharts(workbook) : null,
            Pivots = WantsDetail(request, CellsInspectDetails.Pivots) ? BuildPivots(workbook) : null,
            Validations = WantsDetail(request, CellsInspectDetails.Validation) ? BuildValidations(workbook) : null,
            ConditionalFormats = WantsDetail(request, CellsInspectDetails.ConditionalFormats) ? ObjectProjection.ConditionalFormats(budgets, workbook) : null,
            Sparklines = WantsDetail(request, CellsInspectDetails.Sparklines) ? ObjectProjection.Sparklines(budgets, workbook) : null,
            Images = WantsDetail(request, CellsInspectDetails.Images) ? ObjectProjection.Images(workbook) : null,
            Comments = WantsDetail(request, CellsInspectDetails.Comments) ? ObjectProjection.Comments(budgets, workbook) : null,
            Hyperlinks = WantsDetail(request, CellsInspectDetails.Hyperlinks) ? ObjectProjection.Hyperlinks(budgets, workbook) : null,
            Layouts = WantsDetail(request, CellsInspectDetails.Layout) ? SheetLayoutProjection.Build(budgets, workbook) : null,
        };

        return (summary, errorsTruncated);
    }

    /// <summary>The workbook's default font.</summary>
    internal static CellsDefaultFontInfo DefaultFont(Workbook workbook) =>
        new() { Font = workbook.DefaultStyle.Font.Name, FontSize = workbook.DefaultStyle.Font.DoubleSize };

    /// <summary>Whether a sheet is protected with a password.</summary>
    internal static bool PasswordProtected(Worksheet sheet) => sheet.IsProtected && sheet.Protection.IsProtectedWithPassword;

    private static bool WantsDetail(CellsInspectRequest request, string detail) =>
        request.Details is { } details && details.Contains(detail);

    private static CellsSheetInfo BuildSheetInfo(Worksheet sheet, int index, CellsInspectRequest request)
    {
        RangeRef? usedRange = Sheets.UsedRange(sheet);

        return new CellsSheetInfo
        {
            Name = sheet.Name,
            Position = index + 1,
            UsedRange = usedRange is { } range ? A1.FormatRange(range) : null,
            RowCount = usedRange?.RowCount ?? 0,
            ColumnCount = usedRange?.ColumnCount ?? 0,
            Visibility = Sheets.Visibility(sheet),
            Protected = sheet.IsProtected,
            PasswordProtected = PasswordProtected(sheet),
            ChartCount = sheet.Charts.Count,
            PivotTableCount = sheet.PivotTables.Count,
            // A used range implies data; its (0,0) origin makes End the old max row/column.
            Preview = request.IncludePreview && usedRange is { } previewRange
                ? BuildPreview(sheet, previewRange, request.SampleRows)
                : null,
        };
    }

    private static IReadOnlyList<IReadOnlyList<string?>> BuildPreview(
        Worksheet sheet, RangeRef range, int previewRows)
    {
        int rowCount = Math.Min(previewRows, range.End.Row + 1);
        int columnCount = Math.Min(range.End.Column + 1, CellsSheetInfo.PreviewColumns);

        var rows = new List<IReadOnlyList<string?>>(rowCount);
        for (int row = 0; row < rowCount; row++)
        {
            var values = new string?[columnCount];
            for (int column = 0; column < columnCount; column++)
            {
                // CheckCell avoids materializing cells that were never written.
                Cell? cell = sheet.Cells.CheckCell(row, column);
                values[column] = cell?.DisplayStringValue;
            }

            rows.Add(values);
        }

        return rows;
    }

    private static IReadOnlyList<CellsDefinedNameInfo> BuildDefinedNames(Workbook workbook) =>
        [.. DefinedNames.Of(workbook).Select(DefinedNames.Info)];

    /// <summary>How to read formula errors beyond a capped list.</summary>
    internal const string FormulaErrorsCappedHint =
        "Sheets scanned after the cap are not represented; read a sheet with "
            + "'cells read --sheet <name>' and look for cells of type error.";

    /// <summary>Counts every stored error under the work budget and returns the first bounded sample.</summary>
    internal static (IReadOnlyList<CellsCellError> Errors, int Total) ScanFormulaErrors(
        ResourceBudgetLedger budgets, Workbook workbook)
    {
        const int maxErrors = 1000;
        var errors = new List<CellsCellError>();
        int total = 0;
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            foreach (Cell cell in StoredCells.InAddressOrder(budgets, sheet, "formula-errors"))
            {
                budgets.Deadline.ThrowIfExpired("formula-errors");
                if (cell.Type != CellValueType.IsError) { continue; }
                total++;
                if (errors.Count < maxErrors)
                {
                    errors.Add(new CellsCellError
                    {
                        Sheet = sheet.Name,
                        Cell = A1.FormatCell(new CellRef(cell.Row, cell.Column)),
                        Error = cell.StringValue ?? "#ERROR!",
                    });
                }
            }
        }
        return (errors, total);
    }

    private static IReadOnlyList<CellsTableInfo> BuildTables(Workbook workbook) =>
        [.. workbook.Worksheets.Cast<Worksheet>().SelectMany(static sheet => sheet.ListObjects.Cast<ListObject>()
            .Select(table => ObjectProjection.Table(sheet, table)))];

    private static IReadOnlyList<CellsChartInfo> BuildCharts(Workbook workbook) =>
        [.. workbook.Worksheets.Cast<Worksheet>().SelectMany(static sheet => Enumerable.Range(0, sheet.Charts.Count)
            .Select(index => ObjectProjection.Chart(sheet, sheet.Charts[index], index + 1)))];

    private static IReadOnlyList<CellsPivotInfo> BuildPivots(Workbook workbook) =>
        [.. workbook.Worksheets.Cast<Worksheet>().SelectMany(static sheet => sheet.PivotTables.Cast<PivotTable>()
            .Select(pivot => ObjectProjection.Pivot(sheet, pivot)))];

    // Budgeted like the formula-error scan: a very large validation set is capped.
    private static IReadOnlyList<CellsValidationInfo> BuildValidations(Workbook workbook) =>
        [.. workbook.Worksheets.Cast<Worksheet>()
            .SelectMany(static sheet => sheet.Validations.Cast<Validation>().Select(validation => ObjectProjection.Validation(sheet, validation)))
            .OfType<CellsValidationInfo>()
            .Take(1000)];

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
