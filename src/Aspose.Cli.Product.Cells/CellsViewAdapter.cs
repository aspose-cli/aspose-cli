using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Cells;

/// <summary>
/// Worksheet image and interactive workbook views, and the bounded
/// workbook-structure findings of their review.
/// </summary>
internal sealed class CellsViewAdapter : IProductViewAdapter<ICellsEngine>
{
    public IReadOnlyList<ProductView> Views { get; } =
    [
        new(CellsViews.Sheets, "Sheets", ViewPartKinds.Image),
        new(CellsViews.Workbook, "Workbook", ViewPartKinds.Html),
    ];

    public string ReviewView => CellsViews.Sheets;

    public string LiveView => CellsViews.Workbook;

    public bool VisualInspectionRequired => true;

    public ViewManifest Render(
        ICellsEngine port,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        port.RenderView(filePath, request, artifacts);

    public ProductReviewAssessment Assess(
        ICellsEngine port,
        string filePath,
        ViewRenderRequest request,
        ViewManifest rendered)
    {
        WorkbookInfoResult info = port.GetInfo(filePath, new InfoRequest
        {
            Details = [InfoDetails.Errors],
            Password = request.Password,
        });
        if (port is not ICellsReviewLayoutPort reviewPort)
        {
            throw new InvalidOperationException(
                "The activated Cells engine does not provide review layout facts.");
        }
        CellsReviewLayout layout = reviewPort.Inspect(
            filePath,
            request.Password);
        SheetInfo[] visible = info.Workbook.Sheets
            .Where(static sheet => !sheet.Hidden)
            .ToArray();
        var warnings = new List<Warning>(info.Warnings ?? []);
        warnings.AddRange(layout.Warnings ?? []);

        List<ReviewFinding> findings = Findings(
            info,
            layout,
            visible,
            rendered.Parts.Count);
        long usedCells = info.Workbook.Sheets.Sum(static sheet =>
            checked((long)sheet.RowCount * sheet.ColumnCount));
        long populatedCells = layout.Sheets.Sum(static sheet => sheet.PopulatedCells);
        int layoutIssues = layout.Sheets.Sum(static sheet =>
            sheet.HiddenPopulatedColumns
            + sheet.NarrowPopulatedColumns
            + sheet.WidePopulatedColumns
            + sheet.HiddenPopulatedRows
            + sheet.ShortPopulatedRows
            + sheet.TallPopulatedRows);
        return new ProductReviewAssessment
        {
            Findings = findings,
            Warnings = warnings.DistinctBy(static warning => warning.Code).ToArray(),
            Coverage =
            [
                Metric("sheets", info.Workbook.SheetCount, "sheets"),
                Metric("visibleSheets", visible.Length, "sheets"),
                Metric("renderedSheets", rendered.Parts.Count, "sheets"),
                Metric("usedCells", usedCells, "cells"),
                Metric("populatedCells", populatedCells, "cells"),
                Metric("layoutDimensionIssues", layoutIssues, "dimensions"),
                Metric("charts", layout.Sheets.Sum(static sheet => sheet.Charts.Count), "charts"),
                Metric("sheetsWithPrintArea", layout.Sheets.Count(static sheet => sheet.PrintArea is not null), "sheets"),
                Metric("formulaErrors", info.Workbook.FormulaErrors?.Count ?? 0, "cells"),
            ],
            Complete = findings.All(static finding => finding.Severity != "error")
                && !warnings.Any(static warning => warning.AffectsCompleteness),
        };
    }

    private static List<ReviewFinding> Findings(
        WorkbookInfoResult info,
        CellsReviewLayout layout,
        IReadOnlyList<SheetInfo> visible,
        int renderedCount)
    {
        var findings = new List<ReviewFinding>();
        IReadOnlyDictionary<string, CellsReviewSheetLayout> layoutBySheet =
            layout.Sheets.ToDictionary(static sheet => sheet.Name, StringComparer.Ordinal);
        foreach (SheetInfo sheet in info.Workbook.Sheets.Where(static sheet => sheet.Hidden))
        {
            findings.Add(Finding(
                "CELLS_HIDDEN_SHEET",
                "info",
                $"Hidden worksheet '{sheet.Name}' is excluded from visual evidence.",
                sheet.Name));
        }
        foreach (SheetInfo sheet in visible.Where(sheet =>
                     sheet.UsedRange is null
                     && !layoutBySheet[sheet.Name].HasVisualObjects))
        {
            findings.Add(Finding(
                "CELLS_EMPTY_SHEET",
                "info",
                $"Visible worksheet '{sheet.Name}' is empty; its PNG is a blank placeholder.",
                sheet.Name));
        }
        foreach (CellsReviewSheetLayout sheet in layout.Sheets.Where(sheet =>
                     visible.Any(visibleSheet =>
                         string.Equals(visibleSheet.Name, sheet.Name, StringComparison.Ordinal))))
        {
            AddLayoutFindings(findings, sheet);
        }
        foreach (CellError error in info.Workbook.FormulaErrors ?? [])
        {
            findings.Add(Finding(
                "CELLS_FORMULA_ERROR",
                "error",
                $"Formula evaluates to {error.Error}.",
                $"{error.Sheet}!{error.Cell}"));
        }
        if (info.Workbook.HasVba)
        {
            findings.Add(Finding(
                "CELLS_VBA_PRESENT",
                "warning",
                "Workbook contains VBA; static PNG evidence does not exercise macros.",
                null));
        }
        if (renderedCount < visible.Count)
        {
            findings.Add(Finding(
                "CELLS_REVIEW_TRUNCATED",
                "warning",
                $"Rendered {renderedCount} of {visible.Count} visible worksheets because of the artifact limit.",
                null));
        }
        return findings;
    }

    private static void AddLayoutFindings(
        ICollection<ReviewFinding> findings,
        CellsReviewSheetLayout sheet)
    {
        AddSparseRangeFinding(findings, sheet);
        AddDimensionFindings(findings, sheet);
        AddPrintAreaFindings(findings, sheet);
        AddChartFindings(findings, sheet);
    }

    private static void AddSparseRangeFinding(
        ICollection<ReviewFinding> findings,
        CellsReviewSheetLayout sheet)
    {
        if (sheet.UsedAreaCells >= 100
            && checked(sheet.PopulatedCells * 100) < sheet.UsedAreaCells)
        {
            double density = sheet.PopulatedCells * 100d / sheet.UsedAreaCells;
            findings.Add(Finding(
                "CELLS_SPARSE_USED_RANGE",
                "warning",
                $"Only {density:0.##}% of the {sheet.UsedAreaCells} cells in the used area contain data; inspect for stray far-away content or excessive whitespace.",
                sheet.Name));
        }
    }

    private static void AddDimensionFindings(
        ICollection<ReviewFinding> findings,
        CellsReviewSheetLayout sheet)
    {
        AddDimensionFinding(
            findings,
            sheet,
            "hidden-column",
            sheet.HiddenPopulatedColumns,
            "CELLS_HIDDEN_POPULATED_COLUMNS",
            "populated column(s) are hidden");
        AddDimensionFinding(
            findings,
            sheet,
            "narrow-column",
            sheet.NarrowPopulatedColumns,
            "CELLS_NARROW_POPULATED_COLUMNS",
            "populated column(s) are narrower than 3 character units");
        AddDimensionFinding(
            findings,
            sheet,
            "wide-column",
            sheet.WidePopulatedColumns,
            "CELLS_WIDE_POPULATED_COLUMNS",
            "populated column(s) are wider than 80 character units");
        AddDimensionFinding(
            findings,
            sheet,
            "hidden-row",
            sheet.HiddenPopulatedRows,
            "CELLS_HIDDEN_POPULATED_ROWS",
            "populated row(s) are hidden");
        AddDimensionFinding(
            findings,
            sheet,
            "short-row",
            sheet.ShortPopulatedRows,
            "CELLS_SHORT_POPULATED_ROWS",
            "populated row(s) are shorter than 8 points");
        AddDimensionFinding(
            findings,
            sheet,
            "tall-row",
            sheet.TallPopulatedRows,
            "CELLS_TALL_POPULATED_ROWS",
            "populated row(s) are taller than 120 points");
    }

    private static void AddPrintAreaFindings(
        ICollection<ReviewFinding> findings,
        CellsReviewSheetLayout sheet)
    {
        if (sheet.PrintAreaInvalid)
        {
            findings.Add(Finding(
                "CELLS_PRINT_AREA_INVALID",
                "warning",
                $"The saved print area '{sheet.PrintArea}' could not be interpreted as bounded A1 ranges.",
                sheet.Name));
        }
        else if (sheet.PrintAreaExcludesContent)
        {
            findings.Add(Finding(
                "CELLS_PRINT_AREA_EXCLUDES_CONTENT",
                "warning",
                $"The print area '{sheet.PrintArea}' does not contain all populated cells ({sheet.ContentRange}).",
                sheet.Name));
        }
        if (sheet.PrintAreaExcessive)
        {
            findings.Add(Finding(
                "CELLS_PRINT_AREA_EXCESSIVE",
                "warning",
                $"The print area '{sheet.PrintArea}' is more than 20 times the populated content bounds ({sheet.ContentRange}).",
                sheet.Name));
        }
    }

    private static void AddChartFindings(
        ICollection<ReviewFinding> findings,
        CellsReviewSheetLayout sheet)
    {
        foreach (CellsReviewChartLayout chart in sheet.Charts)
        {
            AddChartFindingSet(findings, sheet, chart);
        }
    }

    private static void AddChartFindingSet(
        ICollection<ReviewFinding> findings,
        CellsReviewSheetLayout sheet,
        CellsReviewChartLayout chart)
    {
        string location = $"{sheet.Name} chart '{chart.Name}'";
        if (chart.Hidden)
        {
            findings.Add(Finding(
                "CELLS_CHART_HIDDEN",
                "warning",
                "The chart object is hidden and will not provide visible evidence.",
                location));
        }
        if (chart.WidthPixels < 120 || chart.HeightPixels < 80)
        {
            findings.Add(Finding(
                "CELLS_CHART_TOO_SMALL",
                "warning",
                $"The chart is only {chart.WidthPixels} x {chart.HeightPixels} pixels and may be unreadable.",
                location));
        }
        if (chart.SeriesCount == 0)
        {
            findings.Add(Finding(
                "CELLS_CHART_WITHOUT_SERIES",
                "warning",
                "The chart has no data series.",
                location));
        }
        if (chart.AnchoredInHiddenCells)
        {
            findings.Add(Finding(
                "CELLS_CHART_ANCHORED_IN_HIDDEN_CELLS",
                "warning",
                "A chart anchor touches hidden rows or columns; inspect whether the object remains visible after reopening.",
                location));
        }
        if (chart.ExcludedByPrintArea)
        {
            findings.Add(Finding(
                "CELLS_PRINT_AREA_EXCLUDES_CHART",
                "warning",
                $"The print area '{sheet.PrintArea}' does not intersect this chart.",
                location));
        }
    }

    private static void AddDimensionFinding(
        ICollection<ReviewFinding> findings,
        CellsReviewSheetLayout sheet,
        string kind,
        int count,
        string code,
        string description)
    {
        if (count == 0)
        {
            return;
        }
        string samples = string.Join(
            ", ",
            sheet.DimensionIssues
                .Where(issue => string.Equals(issue.Kind, kind, StringComparison.Ordinal))
                .Take(5)
                .Select(issue => kind.EndsWith("column", StringComparison.Ordinal)
                    ? A1.ColumnName(issue.Index)
                    : (issue.Index + 1).ToString(
                        System.Globalization.CultureInfo.InvariantCulture)));
        findings.Add(Finding(
            code,
            "warning",
            $"{count} {description}; sample: {samples}.",
            sheet.Name));
    }

    private static ReviewFinding Finding(
        string code,
        string severity,
        string message,
        string? location) => new()
    {
        Code = code,
        Severity = severity,
        Message = message,
        Location = location,
        Hint = "Adjust only the affected worksheet layout, save, and run review again in a new directory.",
    };

    private static ReviewCoverageMetric Metric(string name, long value, string unit) => new()
    {
        Name = name,
        Value = value,
        Unit = unit,
    };
}
