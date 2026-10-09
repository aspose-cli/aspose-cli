using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Cells;

/// <summary>
/// Worksheet image and interactive workbook views, and the bounded
/// workbook-structure findings of their review.
/// </summary>
internal sealed class CellsViewAdapter : IProductViewAdapter<CellsSession>
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
        CellsSession session,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        CellsView.Render(session, filePath, request, artifacts);

    public ProductReviewAssessment Assess(
        CellsSession session,
        string filePath,
        ViewRenderRequest request,
        ViewManifest rendered)
    {
        WorkbookInfoResult info = CellsInfo.Run(session, new InfoRequest
        {
            Input = filePath,
            Details = [InfoDetails.Errors],
            Password = request.Password,
        });
        CellsReviewLayout layout = CellsView.Layout(session, filePath, request.Password);
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
            findings.Add(SheetFinding(CellsReviewChecks.SheetHidden, sheet.Name,
                $"Hidden worksheet '{sheet.Name}' is excluded from visual evidence."));
        }
        foreach (SheetInfo sheet in visible.Where(sheet =>
                     sheet.UsedRange is null
                     && !layoutBySheet[sheet.Name].HasVisualObjects))
        {
            findings.Add(SheetFinding(CellsReviewChecks.SheetEmpty, sheet.Name,
                $"Visible worksheet '{sheet.Name}' is empty; its PNG is a blank placeholder."));
        }
        foreach (CellsReviewSheetLayout sheet in layout.Sheets.Where(sheet =>
                     visible.Any(visibleSheet =>
                         string.Equals(visibleSheet.Name, sheet.Name, StringComparison.Ordinal))))
        {
            AddLayoutFindings(findings, sheet);
        }
        foreach (CellError error in info.Workbook.FormulaErrors ?? [])
        {
            findings.Add(SheetFinding(CellsReviewChecks.FormulaError, error.Sheet,
                $"Formula evaluates to {error.Error}.",
                location: $"{error.Sheet}!{error.Cell}"));
        }
        foreach (CellsReviewSheetLayout sheet in layout.Sheets.Where(static sheet => sheet.IsEvaluationWarning))
        {
            findings.Add(SheetFinding(CellsReviewChecks.EvaluationSheet, sheet.Name,
                $"Worksheet '{sheet.Name}' is the evaluation warning sheet an Aspose.Cells save without a license added; the file carries evaluation marks.",
                hint: "Tell the user. A licensed re-save keeps the marks: rebuild the deliverable from the original unmarked inputs with a license."));
        }
        foreach (CellsReviewSheetLayout sheet in layout.Sheets)
        {
            if (sheet.EvaluationNoticeRow is int row)
            {
                findings.Add(SheetFinding(CellsReviewChecks.EvaluationNotice, sheet.Name,
                    $"Row {row + 1} of '{sheet.Name}' is the evaluation notice an Aspose.Cells CSV or TSV export without a license wrote after the data; it is not data.",
                    location: $"{sheet.Name}!A{row + 1}",
                    hint: "Tell the user. Delete the row before anything reads the file as data, or export it again with a license."));
            }
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
            findings.Add(SheetFinding(CellsReviewChecks.CellsClipped, sheet.Name,
                $"{sheet.ClippedCells.Count} cell value(s) are wider than their columns; sample: {string.Join(", ", sheet.ClippedCells.Samples)}. "
                    + "Widen the columns (resize_columns without a width auto-fits) or wrap the text."
                    + (sheet.ClippedCells.SamplesHaveEastAsianText
                        ? " East Asian text in a font without East Asian glyphs is measured unreliably, so check those values in the sheet image; if one is cut off after auto-fit, give its column an explicit width."
                        : string.Empty)));
        }
        if (sheet.OverflowingCells.Count > 0)
        {
            // Page layout can cut such text at a column edge (known issue CELLS-OVERFLOW-EDGE,
            // KNOWN-ISSUES.md), which no public API measures, so the finding asks for a look.
            findings.Add(SheetFinding(CellsReviewChecks.TextOverflows, sheet.Name,
                $"{sheet.OverflowingCells.Count} text value(s) spill over empty cells to their right and end close to a column edge; sample: {string.Join(", ", sheet.OverflowingCells.Samples)}. "
                    + "A sheet image or PDF page can cut the last character of such text there; check the end of each in the image.",
                hint: "If a character is cut, widen the column the text starts in by one or two characters, or wrap the text."));
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
            findings.Add(SheetFinding(CellsReviewChecks.UsedRangeSparse, sheet.Name,
                $"Only {density:0.##}% of the {sheet.UsedAreaCells} cells in the used area contain data; inspect for stray far-away content or excessive whitespace."));
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
                findings.Add(SheetFinding(check, sheet.Name,
                    $"{set.Count} {description}; sample: {string.Join(", ", set.Samples.Select(name))}."));
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
            findings.Add(SheetFinding(CellsReviewChecks.PrintAreaInvalid, sheet.Name,
                $"The saved print area '{sheet.PrintArea}' could not be interpreted as bounded A1 ranges."));
        }
        else if (sheet.PrintAreaExcludesContent)
        {
            findings.Add(SheetFinding(CellsReviewChecks.PrintAreaExcludesContent, sheet.Name,
                $"The print area '{sheet.PrintArea}' does not contain all populated cells ({sheet.ContentRange})."));
        }
        if (sheet.PrintAreaExcessive)
        {
            findings.Add(SheetFinding(CellsReviewChecks.PrintAreaExcessive, sheet.Name,
                $"The print area '{sheet.PrintArea}' is more than 20 times the populated content bounds ({sheet.ContentRange})."));
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
            findings.Add(SheetFinding(CellsReviewChecks.ChartHidden, sheet.Name,
                "The chart object is hidden and will not provide visible evidence.", location));
        }
        if (chart.WidthPixels < 120 || chart.HeightPixels < 80)
        {
            findings.Add(SheetFinding(CellsReviewChecks.ChartTooSmall, sheet.Name,
                $"The chart is only {chart.WidthPixels} x {chart.HeightPixels} pixels and may be unreadable.", location));
        }
        if (chart.SeriesCount == 0)
        {
            findings.Add(SheetFinding(CellsReviewChecks.ChartWithoutSeries, sheet.Name,
                "The chart has no data series.", location));
        }
        if (chart.AnchoredInHiddenCells)
        {
            findings.Add(SheetFinding(CellsReviewChecks.ChartAnchoredInHiddenCells, sheet.Name,
                "A chart anchor touches hidden rows or columns; inspect whether the object remains visible after reopening.", location));
        }
        if (chart.ExcludedByPrintArea)
        {
            findings.Add(SheetFinding(CellsReviewChecks.PrintAreaExcludesChart, sheet.Name,
                $"The print area '{sheet.PrintArea}' does not intersect this chart.", location));
        }
        if (chart.PrintedPages > 1 && !chart.Hidden)
        {
            findings.Add(SheetFinding(CellsReviewChecks.ChartSplitAcrossPages, sheet.Name,
                $"The chart reaches {chart.PrintedPages} printed pages, so printing and PDF export split it.", location,
                $"Fit the sheet on fewer pages with set_page_setup {CellsDiagnostics.ChartSplitRemedy}, then review again."));
        }
    }

    /// <summary>A finding on one worksheet, shown with that sheet's image; it is located at the sheet unless <paramref name="location"/> says otherwise.</summary>
    private static ReviewFinding SheetFinding(ReviewCheck check, string sheet, string message, string? location = null, string hint = Hint) =>
        check.Finding(message, location ?? sheet, hint, part: sheet);

    private static ReviewCoverageMetric Metric(string name, long value, string unit) => new()
    {
        Name = name,
        Value = value,
        Unit = unit,
    };
}
