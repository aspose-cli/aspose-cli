using System.Net;
using System.Text;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Cells;

/// <summary>
/// Produces a portable visual-review gallery with one PNG per reported visible
/// worksheet and bounded workbook-structure findings.
/// </summary>
internal sealed class CellsReviewAdapter : IProductReviewAdapter<IWorkbookEngine>
{
    private const string SheetsView = "sheets";
    private const string EntryFileName = "index.html";

    // A valid one-pixel white PNG for a genuinely empty visible worksheet.
    private static readonly byte[] BlankPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl2VQAAAABJRU5ErkJggg==");

    public string DefaultView => SheetsView;

    public IReadOnlyList<string> Views { get; } = [SheetsView];

    public bool VisualInspectionRequired => true;

    public ProductReviewRenderer CreateRenderer(
        IWorkbookEngine port,
        string filePath,
        ProductReviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(port);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(request);
        if (!string.Equals(request.View, SheetsView, StringComparison.Ordinal))
        {
            throw CliErrors.OptionInvalid(
                "--view",
                $"review view '{request.View}' is not supported by cells",
                $"Use {SheetsView}.");
        }

        return directory => Render(port, filePath, request, directory);
    }

    private static ProductReviewRenderOutcome Render(
        IWorkbookEngine port,
        string filePath,
        ProductReviewRequest request,
        string directory)
    {
        WorkbookInfoResult info = port.GetInfo(filePath, new InfoRequest
        {
            Details = [InfoDetails.Errors],
            Password = request.Password,
        });
        if (port is not IWorkbookReviewPort reviewPort)
        {
            throw new InvalidOperationException(
                "The activated Cells engine does not provide review layout facts.");
        }
        WorkbookReviewLayout layout = reviewPort.InspectReviewLayout(
            filePath,
            request.Password);
        IReadOnlyDictionary<string, WorksheetReviewLayout> layoutBySheet =
            layout.Sheets.ToDictionary(static sheet => sheet.Name, StringComparer.Ordinal);
        SheetInfo[] visible = info.Workbook.Sheets
            .Where(static sheet => !sheet.Hidden)
            .ToArray();
        SheetInfo[] reported = visible.Take(request.MaxItems).ToArray();
        var rendered = new List<(SheetInfo Sheet, string FileName)>();
        var warnings = new List<Warning>(info.Warnings ?? []);
        warnings.AddRange(layout.Warnings ?? []);

        foreach (SheetInfo sheet in reported)
        {
            string fileName = $"sheet-{sheet.Index + 1:D4}.png";
            string output = Path.Combine(directory, fileName);
            WorksheetReviewLayout sheetLayout = layoutBySheet[sheet.Name];
            if (sheet.UsedRange is null && !sheetLayout.HasVisualObjects)
            {
                File.WriteAllBytes(output, BlankPng);
            }
            else
            {
                RenderResult render = port.Render(filePath, new RenderRequest
                {
                    TargetFormatId = "png",
                    OutputPath = output,
                    Overwrite = true,
                    SheetName = sheet.Name,
                    Password = request.Password,
                });
                warnings.AddRange(render.Warnings ?? []);
            }
            rendered.Add((sheet, fileName));
        }

        File.WriteAllText(
            Path.Combine(directory, EntryFileName),
            Gallery(info.Workbook.Name, rendered),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        List<ReviewFinding> findings = Findings(
            info,
            layout,
            visible,
            reported.Length);
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
        ReviewCoverageMetric[] coverage =
        [
            Metric("sheets", info.Workbook.SheetCount, "sheets"),
            Metric("visibleSheets", visible.Length, "sheets"),
            Metric("renderedSheets", reported.Length, "sheets"),
            Metric("usedCells", usedCells, "cells"),
            Metric("populatedCells", populatedCells, "cells"),
            Metric("layoutDimensionIssues", layoutIssues, "dimensions"),
            Metric("charts", layout.Sheets.Sum(static sheet => sheet.Charts.Count), "charts"),
            Metric("sheetsWithPrintArea", layout.Sheets.Count(static sheet => sheet.PrintArea is not null), "sheets"),
            Metric("formulaErrors", info.Workbook.FormulaErrors?.Count ?? 0, "cells"),
        ];

        return new ProductReviewRenderOutcome(
            EntryFileName,
            info.Source.Format,
            info.Source.SizeBytes)
        {
            VisualInspectionRequired = true,
            Findings = findings,
            Warnings = warnings.DistinctBy(static warning => warning.Code).ToArray(),
            Coverage = coverage,
            ExpectedItems = visible.Length,
            RenderedItems = reported.Length,
            Complete = findings.All(static finding => finding.Severity != "error")
                && !warnings.Any(static warning => warning.AffectsCompleteness),
        };
    }

    private static List<ReviewFinding> Findings(
        WorkbookInfoResult info,
        WorkbookReviewLayout layout,
        IReadOnlyList<SheetInfo> visible,
        int renderedCount)
    {
        var findings = new List<ReviewFinding>();
        IReadOnlyDictionary<string, WorksheetReviewLayout> layoutBySheet =
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
        foreach (WorksheetReviewLayout sheet in layout.Sheets.Where(sheet =>
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
        WorksheetReviewLayout sheet)
    {
        AddSparseRangeFinding(findings, sheet);
        AddDimensionFindings(findings, sheet);
        AddPrintAreaFindings(findings, sheet);
        AddChartFindings(findings, sheet);
    }

    private static void AddSparseRangeFinding(
        ICollection<ReviewFinding> findings,
        WorksheetReviewLayout sheet)
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
        WorksheetReviewLayout sheet)
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
        WorksheetReviewLayout sheet)
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
        WorksheetReviewLayout sheet)
    {
        foreach (ChartReviewLayout chart in sheet.Charts)
        {
            AddChartFindingSet(findings, sheet, chart);
        }
    }

    private static void AddChartFindingSet(
        ICollection<ReviewFinding> findings,
        WorksheetReviewLayout sheet,
        ChartReviewLayout chart)
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
        WorksheetReviewLayout sheet,
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

    private static string Gallery(
        string workbookName,
        IReadOnlyList<(SheetInfo Sheet, string FileName)> rendered)
    {
        var html = new StringBuilder()
            .Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append("<title>Cells review</title><style>")
            .Append("body{margin:0;padding:20px;font:14px system-ui;background:#eef2f0;color:#17231d}")
            .Append("h1{margin:0 0 18px;font-size:20px}.sheet{margin:0 0 24px;padding:14px;background:#fff;border:1px solid #ccd8d1;border-radius:8px}")
            .Append("h2{margin:0 0 10px;font-size:15px}.sheet img{display:block;max-width:100%;height:auto;border:1px solid #dfe6e2;background:#fff}")
            .Append("</style></head><body><h1>")
            .Append(WebUtility.HtmlEncode(workbookName))
            .Append("</h1>");
        foreach ((SheetInfo sheet, string fileName) in rendered)
        {
            string name = WebUtility.HtmlEncode(sheet.Name);
            html.Append("<section class=\"sheet\"><h2>")
                .Append(name)
                .Append("</h2><img loading=\"lazy\" alt=\"")
                .Append(name)
                .Append(" worksheet\" src=\"")
                .Append(fileName)
                .Append("\"></section>");
        }
        return html.Append("</body></html>\n").ToString();
    }
}
