using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using Aspose.Cli.Sdk.Text;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Owns workbook comparison and budgeted cell search.</summary>
internal sealed class CellsInspectionService
{
    private readonly ILicenseGate _licenseGate;
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly CellsWorkbookLoader _loader;

    internal CellsInspectionService(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        CellsWorkbookLoader loader)
    {
        ArgumentNullException.ThrowIfNull(licenseGate);
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentNullException.ThrowIfNull(loader);
        _licenseGate = licenseGate;
        _resourceBudgets = resourceBudgets;
        _loader = loader;
    }

    internal DiffResult Diff(string leftPath, string rightPath, DiffRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(leftPath);
        ArgumentException.ThrowIfNullOrEmpty(rightPath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        using LoadedWorkbook loaded = _loader.Open(leftPath, request.LeftPassword);
        using LoadedWorkbook other = _loader.Open(rightPath, request.RightPassword);
        Workbook left = loaded.Workbook;
        Workbook right = other.Workbook;

        DiffComparer.Result diff = DiffComparer.Compare(
            _resourceBudgets, left, right, includeFormulas: request.Scope == DiffScope.Formulas, request.MaxDiffs);

        return new DiffResult
        {
            Left = BuildSource(leftPath, left),
            Right = BuildSource(rightPath, right),
            Identical = diff.Identical,
            Summary = diff.Summary,
            Sheets = diff.Sheets.Count > 0 ? diff.Sheets : null,
            License = EnvelopeParts.License(licenseState),
            Warnings = loaded.Warnings([other.Resources.CoverageWarning, CellsTruncated(diff), .. diff.Warnings]),
        };
    }

    // The summary counts every differing cell; the listed cells stop at --max-diffs.
    private static Warning? CellsTruncated(DiffComparer.Result diff) =>
        diff.Truncated
            ? EnvelopeParts.ListTruncated(
                "sheets[].cells",
                diff.Sheets.Sum(static sheet => sheet.Cells?.Count ?? 0),
                diff.Summary.CellsDiffering,
                "Raise --max-diffs to list more differing cells; summary.cellsDiffering counts them all.")
            : null;

    internal SearchResult Search(string filePath, SearchRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        using LoadedWorkbook loaded = _loader.Open(filePath, request.Password);
        Workbook workbook = loaded.Workbook;
        SourceInfo source = BuildSource(filePath, workbook);

        int? sheetIndex = request.SheetName is { } name ? Sheets.Resolve(workbook, name).Index : null;
        SearchHits<SearchHit> hits = SearchMatcher.Find(_resourceBudgets, workbook, request, sheetIndex);

        return new SearchResult
        {
            Source = source,
            Pattern = request.Query.Text.Pattern,
            Hits = hits.Hits,
            // The window's next is left unset: the search command spells the follow-up.
            Window = hits.Window(),
            License = EnvelopeParts.License(licenseState),
            Warnings = loaded.Warnings(),
        };
    }
}
