using System.Text.Json;
using Aspose.Cells;
using Aspose.Cells.Charts;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Contracts.Addressing;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Results;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Builds the <c>cells inspect</c> projection: the workbook summary and the
/// optional detail sections (defined names, formula errors, fonts, tables,
/// charts, pivots, validations). Detail scans are budgeted so a pathological
/// workbook cannot make <c>info</c> unaffordable.
/// </summary>
internal static class InfoProjection
{
    private const int MaxPreviewColumns = 20;

    public static (WorkbookSummary Summary, Warning? ListTruncated) Summarize(
        ResourceBudgetLedger budgets, Workbook workbook, string filePath, InfoRequest request)
    {
        var sheets = new List<SheetInfo>(workbook.Worksheets.Count);
        for (int index = 0; index < workbook.Worksheets.Count; index++)
        {
            sheets.Add(BuildSheetInfo(workbook.Worksheets[index], index, request));
        }

        IReadOnlyList<CellError>? formulaErrors = null;
        Warning? errorsTruncated = null;
        if (WantsDetail(request, InfoDetails.Errors))
        {
            (formulaErrors, int total) = ScanFormulaErrors(budgets, workbook);
            if (total > formulaErrors.Count)
            {
                errorsTruncated = EnvelopeParts.ListTruncated(
                    "workbook.formulaErrors", formulaErrors.Count, total, FormulaErrorsCappedHint);
            }
        }

        var summary = new WorkbookSummary
        {
            Name = Path.GetFileName(filePath),
            SheetCount = sheets.Count,
            Sheets = sheets,
            HasVba = workbook.HasMacro,
            DefinedNameCount = DefinedNames.Of(workbook).Count(),
            StructureProtected = Sheets.StructureProtected(workbook),
            StructurePasswordProtected = Sheets.StructureProtected(workbook) && workbook.IsWorkbookProtectedWithPassword,
            Author = Normalize(workbook.BuiltInDocumentProperties.Author),
            Title = Normalize(workbook.BuiltInDocumentProperties.Title),
            DefinedNames = WantsDetail(request, InfoDetails.Names) ? BuildDefinedNames(workbook) : null,
            FormulaErrors = formulaErrors,
            Fonts = WantsDetail(request, InfoDetails.Fonts) ? FontOps.UsedFonts(workbook) : null,
            Tables = WantsDetail(request, InfoDetails.Tables) ? BuildTables(workbook) : null,
            Charts = WantsDetail(request, InfoDetails.Charts) ? BuildCharts(workbook) : null,
            Pivots = WantsDetail(request, InfoDetails.Pivots) ? BuildPivots(workbook) : null,
            Validations = WantsDetail(request, InfoDetails.Validation) ? BuildValidations(workbook) : null,
        };

        return (summary, errorsTruncated);
    }

    private static bool WantsDetail(InfoRequest request, string detail) =>
        request.Details is { } details && details.Contains(detail);

    private static SheetInfo BuildSheetInfo(Worksheet sheet, int index, InfoRequest request)
    {
        RangeRef? usedRange = Sheets.UsedRange(sheet);

        return new SheetInfo
        {
            Name = sheet.Name,
            Position = index,
            UsedRange = usedRange is { } range ? A1.FormatRange(range) : null,
            RowCount = usedRange?.RowCount ?? 0,
            ColumnCount = usedRange?.ColumnCount ?? 0,
            Hidden = !sheet.IsVisible,
            Protected = sheet.IsProtected,
            PasswordProtected = sheet.IsProtected && sheet.Protection.IsProtectedWithPassword,
            ChartCount = sheet.Charts.Count,
            PivotTableCount = sheet.PivotTables.Count,
            // A used range implies data; its (0,0) origin makes End the old max row/column.
            Preview = request.IncludePreview && usedRange is { } previewRange
                ? BuildPreview(sheet, previewRange, request.PreviewRows)
                : null,
        };
    }

    private static IReadOnlyList<IReadOnlyList<string?>> BuildPreview(
        Worksheet sheet, RangeRef range, int previewRows)
    {
        int rowCount = Math.Min(previewRows, range.End.Row + 1);
        int columnCount = Math.Min(range.End.Column + 1, MaxPreviewColumns);

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

    private static IReadOnlyList<DefinedNameInfo> BuildDefinedNames(Workbook workbook)
    {
        var names = new List<DefinedNameInfo>();
        foreach (Name name in DefinedNames.Of(workbook))
        {
            // Once a name's target sheet is deleted the SDK returns a null
            // RefersTo (26.9.0; the SDK is not nullable-annotated so the compiler
            // misses it). RefersTo is a `required` contract field, so a null would
            // be dropped by the null-ignoring serializer and vanish from the
            // payload — a strict agent parser then sees a required key missing.
            // Excel surfaces #REF! for exactly this broken state; emit that so the
            // name still reports, honestly flagged as broken.
            names.Add(new DefinedNameInfo { Name = name.Text, RefersTo = name.RefersTo ?? "#REF!" });
        }

        return names;
    }

    /// <summary>How to read formula errors beyond a capped list.</summary>
    internal const string FormulaErrorsCappedHint =
        "Sheets scanned after the cap are not represented; read a sheet with "
            + "'cells query range --sheet <name>' and look for cells of type error.";

    /// <summary>Counts every stored error under the work budget and returns the first bounded sample.</summary>
    internal static (IReadOnlyList<CellError> Errors, int Total) ScanFormulaErrors(
        ResourceBudgetLedger budgets, Workbook workbook)
    {
        const int maxErrors = 1000;
        var errors = new List<CellError>();
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
                    errors.Add(new CellError
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

    private static IReadOnlyList<TableInfo> BuildTables(Workbook workbook)
    {
        var tables = new List<TableInfo>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            foreach (var table in sheet.ListObjects)
            {
                tables.Add(new TableInfo
                {
                    Sheet = sheet.Name,
                    Name = table.DisplayName,
                    Range = A1.FormatRange(new RangeRef(
                        new CellRef(table.StartRow, table.StartColumn),
                        new CellRef(table.EndRow, table.EndColumn))),
                });
            }
        }

        return tables;
    }

    private static IReadOnlyList<ChartInfo> BuildCharts(Workbook workbook)
    {
        var charts = new List<ChartInfo>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            for (int index = 0; index < sheet.Charts.Count; index++)
            {
                Chart chart = sheet.Charts[index];
                charts.Add(new ChartInfo
                {
                    Sheet = sheet.Name,
                    Index = index,
                    Name = chart.Name,
                    Type = VocabularyName(chart.Type),
                });
            }
        }

        return charts;
    }

    private static IReadOnlyList<PivotInfo> BuildPivots(Workbook workbook)
    {
        var pivots = new List<PivotInfo>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            foreach (var pivot in sheet.PivotTables)
            {
                pivots.Add(new PivotInfo { Sheet = sheet.Name, Name = pivot.Name, Range = FormatArea(pivot.TableRange1) });
            }
        }

        return pivots;
    }

    // Budgeted like the formula-error scan: a very large validation set is capped.
    private static IReadOnlyList<ValidationInfo> BuildValidations(Workbook workbook)
    {
        const int maxItems = 1000;
        var validations = new List<ValidationInfo>();
        foreach (Worksheet sheet in workbook.Worksheets)
        {
            foreach (var validation in sheet.Validations)
            {
                foreach (var area in validation.Areas)
                {
                    if (validations.Count >= maxItems)
                    {
                        return validations;
                    }

                    validations.Add(new ValidationInfo
                    {
                        Sheet = sheet.Name,
                        Range = FormatArea(area),
                        Type = VocabularyName(validation.Type),
                    });
                }
            }
        }

        return validations;
    }

    /// <summary>
    /// Names an engine chart or validation type in the edit vocabulary. The create_chart and
    /// set_validation values are the engine enum names in lower camel case, so a type the
    /// operations accept reads back as that value and any other type keeps its engine name.
    /// </summary>
    private static string VocabularyName<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static string FormatArea(CellArea area) =>
        A1.FormatRange(new RangeRef(
            new CellRef(area.StartRow, area.StartColumn),
            new CellRef(area.EndRow, area.EndColumn)));

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
