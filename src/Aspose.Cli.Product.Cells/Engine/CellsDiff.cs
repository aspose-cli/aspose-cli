using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary><c>cells compare</c>: the stored values, and formulas in scope, that differ between two workbooks.</summary>
internal static class CellsDiff
{
    public static DiffResult Run(CellsSession session, DiffRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.Left);
        ArgumentException.ThrowIfNullOrEmpty(request.Right);

        LicenseState licenseState = session.Outputs.License;
        using LoadedWorkbook loaded = session.Loader.Open(request.Left, request.LeftPassword);
        using LoadedWorkbook other = session.Loader.Open(request.Right, request.RightPassword);
        Workbook left = loaded.Workbook;
        Workbook right = other.Workbook;

        DiffComparer.Result diff = DiffComparer.Compare(
            session.Budgets, left, right, includeFormulas: request.Scope == DiffScope.Formulas, request.MaxDiffs);

        return new DiffResult
        {
            Left = BuildSource(request.Left, loaded),
            Right = BuildSource(request.Right, other),
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
}
