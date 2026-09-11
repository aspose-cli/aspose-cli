using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Owns read-only workbook queries, comparisons, and font inspection.</summary>
internal sealed class CellsQueryService
{
    private readonly ILicenseGate _licenseGate;
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly WorkbookLoadService _loader;

    internal CellsQueryService(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets,
        WorkbookLoadService loader)
    {
        ArgumentNullException.ThrowIfNull(licenseGate);
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        ArgumentNullException.ThrowIfNull(loader);
        _licenseGate = licenseGate;
        _resourceBudgets = resourceBudgets;
        _loader = loader;
    }

    /// <inheritdoc />
    internal WorkbookInfoResult GetInfo(string filePath, InfoRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        using LoadedWorkbook loaded = _loader.Open(filePath, request.Password);
        Workbook workbook = loaded.Workbook;

        (WorkbookSummary summary, Warning? errorsTruncated) =
            InfoProjection.Summarize(workbook, filePath, request);

        return new WorkbookInfoResult
        {
            Source = BuildSource(filePath, workbook),
            Workbook = summary,
            License = EnvelopeParts.License(licenseState),
            // Info is read-only, so no evaluation watermark — but an honest count
            // that outran its capped list is a condition the caller must see.
            Warnings = loaded.Warnings(errorsTruncated),
        };
    }

    internal WorkbookReviewLayout InspectReviewLayout(
        string filePath,
        string? password)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _ = _licenseGate.EnsureApplied();
        using LoadedWorkbook loaded = _loader.Open(filePath, password);
        Workbook workbook = loaded.Workbook;
        return ReviewLayoutProjection.Inspect(workbook) with { Warnings = loaded.Warnings() };
    }

    /// <inheritdoc />
    internal WorkbookReadResult Read(string filePath, ReadRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        using LoadedWorkbook loaded = _loader.Open(filePath, request.Password);
        Workbook workbook = loaded.Workbook;
        (SheetProjection sheet, IReadOnlyDictionary<string, StyleData>? styles) =
            ReadProjection.Project(_resourceBudgets, workbook, request);

        return new WorkbookReadResult
        {
            Source = BuildSource(filePath, workbook),
            Scope = request.Scope.ToContractName(),
            Sheet = sheet,
            Styles = styles,
            // Next is left unset: the CLI assembles the follow-up command from
            // this projection (its own spelling), keeping the engine free of any
            // CLI syntax. See Commands/Cells/NextReadCommand.
            License = EnvelopeParts.License(licenseState),
            Warnings = loaded.Warnings(),
        };
    }

    /// <inheritdoc />
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
            Truncated = diff.Truncated,
            License = EnvelopeParts.License(licenseState),
            Warnings = loaded.Warnings(other.Resources.CoverageWarning),
        };
    }

    /// <inheritdoc />
    internal SearchResult Search(string filePath, SearchRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        using LoadedWorkbook loaded = _loader.Open(filePath, request.Password);
        Workbook workbook = loaded.Workbook;
        SourceInfo source = BuildSource(filePath, workbook);

        if (request.SheetName is { } name && workbook.Worksheets[name] is null)
        {
            throw CellsErrors.SheetNotFound(name, Sheets.Names(workbook));
        }

        (IReadOnlyList<SearchHit> hits, bool truncated) = SearchMatcher.Find(workbook, request);

        return new SearchResult
        {
            Source = source,
            Pattern = request.Pattern,
            Hits = hits,
            Truncated = truncated,
            Hint = truncated
                ? $"showing the first {request.MaxHits} hits; narrow with --sheet, tighten the pattern, or raise --max-hits"
                : null,
            License = EnvelopeParts.License(licenseState),
            Warnings = loaded.Warnings(),
        };
    }

    /// <inheritdoc />
    // Font configuration is global to the engine and license-independent, so
    // this neither opens a workbook nor touches the license gate.
    internal FontListResult ListFonts() => FontOps.BuildFontList();

    /// <inheritdoc />
    internal FontCheckResult CheckFonts(string filePath, FontCheckRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        using LoadedWorkbook loaded = _loader.Open(filePath, request.Password);
        Workbook workbook = loaded.Workbook;

        IReadOnlyList<FontAvailability> fonts = FontOps.CheckAvailability(FontOps.UsedFonts(workbook));
        return new FontCheckResult
        {
            Source = BuildSource(filePath, workbook),
            AllAvailable = fonts.All(static font => font.Available),
            Fonts = fonts,
            License = EnvelopeParts.License(licenseState),
            Warnings = loaded.Warnings(),
        };
    }


}
