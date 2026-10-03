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
    private const string Hint =
        "Adjust only the affected worksheet layout, save, and run review again in a new directory.";

    public IReadOnlyList<ProductView> Views { get; } =
    [
        new(CellsViews.Sheets, "Sheets", ViewPartKinds.Image),
        new(CellsViews.Workbook, "Workbook", ViewPartKinds.Html),
    ];

    public string ReviewView => CellsViews.Sheets;

    public string LiveView => CellsViews.Workbook;

    public bool VisualInspectionRequired => true;

    public IReadOnlyList<ReviewCheck> Checks => CellsReviewChecks.All;

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
            sheet.HiddenPopulatedColumns.Count
            + sheet.NarrowPopulatedColumns.Count
            + sheet.WidePopulatedColumns.Count
            + sheet.HiddenPopulatedRows.Count
            + sheet.ShortPopulatedRows.Count
            + sheet.TallPopulatedRows.Count);
        return new ProductReviewAssessment
        {
            Findings = findings,
            Warnings = warnings.DistinctBy(static warning => (warning.Code, warning.Location)).ToArray(),
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
            // Complete says whether every check ran; an error finding is a result, which the
            // review weighs after any --code filter.
            Complete = !warnings.Any(static warning => warning.AffectsCompleteness),
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
            findings.Add(CellsReviewChecks.SheetHidden.Finding(
                $"Hidden worksheet '{sheet.Name}' is excluded from visual evidence.",
                sheet.Name,
                Hint));
        }
        foreach (SheetInfo sheet in visible.Where(sheet =>
                     sheet.UsedRange is null
                     && !layoutBySheet[sheet.Name].HasVisualObjects))
        {
            findings.Add(CellsReviewChecks.SheetEmpty.Finding(
                $"Visible worksheet '{sheet.Name}' is empty; its PNG is a blank placeholder.",
                sheet.Name,
                Hint));
        }
        foreach (CellsReviewSheetLayout sheet in layout.Sheets.Where(sheet =>
                     visible.Any(visibleSheet =>
                         string.Equals(visibleSheet.Name, sheet.Name, StringComparison.Ordinal))))
        {
            AddLayoutFindings(findings, sheet);
        }
        foreach (CellError error in info.Workbook.FormulaErrors ?? [])
        {
            findings.Add(CellsReviewChecks.FormulaError.Finding(
                $"Formula evaluates to {error.Error}.",
                $"{error.Sheet}!{error.Cell}",
                Hint));
        }
        if (info.Workbook.HasVba)
        {
            findings.Add(CellsReviewChecks.VbaPresent.Finding(
                "Workbook contains VBA; static PNG evidence does not exercise macros.",
                hint: Hint));
        }
        if (renderedCount < visible.Count)
        {
            findings.Add(CellsReviewChecks.ReviewTruncated.Finding(
                $"Rendered {renderedCount} of {visible.Count} visible worksheets because of the artifact limit.",
                hint: Hint));
        }
        return findings;
    }

    private static void AddLayoutFindings(
        ICollection<ReviewFinding> findings,
        CellsReviewSheetLayout sheet)
    {
        AddSparseRangeFinding(findings, sheet);
        AddDimensionFindings(findings, sheet);
        if (sheet.ClippedCells.Count > 0)
        {
            findings.Add(CellsReviewChecks.CellsClipped.Finding(
                $"{sheet.ClippedCells.Count} cell value(s) are wider than their columns; sample: {string.Join(", ", sheet.ClippedCells.Samples)}. "
                    + "Widen the columns (resize_columns without a width auto-fits) or wrap the text."
                    + (sheet.ClippedCells.SamplesHaveEastAsianText
                        ? " East Asian text in a font without East Asian glyphs is measured unreliably, so check those values in the sheet image; if one is cut off after auto-fit, give its column an explicit width."
                        : string.Empty),
                sheet.Name,
                Hint));
        }
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
            findings.Add(CellsReviewChecks.UsedRangeSparse.Finding(
                $"Only {density:0.##}% of the {sheet.UsedAreaCells} cells in the used area contain data; inspect for stray far-away content or excessive whitespace.",
                sheet.Name,
                Hint));
        }
    }

    private static void AddDimensionFindings(
        ICollection<ReviewFinding> findings,
        CellsReviewSheetLayout sheet)
    {
        (ReviewCheck Check, CellsReviewDimensionSet Set, string Description, Func<int, string> Name)[] conditions =
        [
            (CellsReviewChecks.PopulatedColumnsHidden, sheet.HiddenPopulatedColumns,
                "populated column(s) are hidden", A1.ColumnName),
            (CellsReviewChecks.PopulatedColumnsNarrow, sheet.NarrowPopulatedColumns,
                "populated column(s) are narrower than 3 character units", A1.ColumnName),
            (CellsReviewChecks.PopulatedColumnsWide, sheet.WidePopulatedColumns,
                "populated column(s) are wider than 80 character units", A1.ColumnName),
            (CellsReviewChecks.PopulatedRowsHidden, sheet.HiddenPopulatedRows,
                "populated row(s) are hidden", RowName),
            (CellsReviewChecks.PopulatedRowsShort, sheet.ShortPopulatedRows,
                "populated row(s) are shorter than 8 points", RowName),
            (CellsReviewChecks.PopulatedRowsTall, sheet.TallPopulatedRows,
                "populated row(s) are taller than 120 points", RowName),
        ];
        foreach ((ReviewCheck check, CellsReviewDimensionSet set, string description, Func<int, string> name) in conditions)
        {
            if (set.Count > 0)
            {
                findings.Add(check.Finding(
                    $"{set.Count} {description}; sample: {string.Join(", ", set.Samples.Select(name))}.",
                    sheet.Name,
                    Hint));
            }
        }
    }

    private static string RowName(int row) =>
        (row + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static void AddPrintAreaFindings(
        ICollection<ReviewFinding> findings,
        CellsReviewSheetLayout sheet)
    {
        if (sheet.PrintAreaInvalid)
        {
            findings.Add(CellsReviewChecks.PrintAreaInvalid.Finding(
                $"The saved print area '{sheet.PrintArea}' could not be interpreted as bounded A1 ranges.",
                sheet.Name,
                Hint));
        }
        else if (sheet.PrintAreaExcludesContent)
        {
            findings.Add(CellsReviewChecks.PrintAreaExcludesContent.Finding(
                $"The print area '{sheet.PrintArea}' does not contain all populated cells ({sheet.ContentRange}).",
                sheet.Name,
                Hint));
        }
        if (sheet.PrintAreaExcessive)
        {
            findings.Add(CellsReviewChecks.PrintAreaExcessive.Finding(
                $"The print area '{sheet.PrintArea}' is more than 20 times the populated content bounds ({sheet.ContentRange}).",
                sheet.Name,
                Hint));
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
            findings.Add(CellsReviewChecks.ChartHidden.Finding(
                "The chart object is hidden and will not provide visible evidence.",
                location,
                Hint));
        }
        if (chart.WidthPixels < 120 || chart.HeightPixels < 80)
        {
            findings.Add(CellsReviewChecks.ChartTooSmall.Finding(
                $"The chart is only {chart.WidthPixels} x {chart.HeightPixels} pixels and may be unreadable.",
                location,
                Hint));
        }
        if (chart.SeriesCount == 0)
        {
            findings.Add(CellsReviewChecks.ChartWithoutSeries.Finding(
                "The chart has no data series.",
                location,
                Hint));
        }
        if (chart.AnchoredInHiddenCells)
        {
            findings.Add(CellsReviewChecks.ChartAnchoredInHiddenCells.Finding(
                "A chart anchor touches hidden rows or columns; inspect whether the object remains visible after reopening.",
                location,
                Hint));
        }
        if (chart.ExcludedByPrintArea)
        {
            findings.Add(CellsReviewChecks.PrintAreaExcludesChart.Finding(
                $"The print area '{sheet.PrintArea}' does not intersect this chart.",
                location,
                Hint));
        }
    }

    private static ReviewCoverageMetric Metric(string name, long value, string unit) => new()
    {
        Name = name,
        Value = value,
        Unit = unit,
    };
}
