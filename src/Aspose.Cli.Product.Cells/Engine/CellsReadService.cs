using Aspose.Cells;
using Aspose.Cli.Product.Cells.Contracts;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Owns the workbook info summary and windowed sheet reads.</summary>
internal sealed class CellsReadService
{
    private readonly ILicenseGate _licenseGate;
    private readonly ResourceBudgetLedger _resourceBudgets;
    private readonly CellsWorkbookLoader _loader;

    internal CellsReadService(
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

    internal WorkbookInfoResult GetInfo(string filePath, InfoRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        using LoadedWorkbook loaded = _loader.Open(filePath, request.Password);
        Workbook workbook = loaded.Workbook;

        (WorkbookSummary summary, Warning? errorsTruncated) =
            InfoProjection.Summarize(_resourceBudgets, workbook, filePath, request);

        return new WorkbookInfoResult
        {
            Source = BuildSource(filePath, workbook),
            Workbook = summary,
            License = EnvelopeParts.License(licenseState),
            // Info is read-only, so no evaluation watermark — but an honest count
            // that outran its capped list is a condition the caller must see.
            Warnings = loaded.Warnings([errorsTruncated, .. TextTableLayout.Warnings(loaded, _resourceBudgets)]),
        };
    }

    internal WorkbookReadResult Read(string filePath, ReadRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = _licenseGate.EnsureApplied();
        using LoadedWorkbook loaded = _loader.Open(filePath, request.Password);
        Workbook workbook = loaded.Workbook;
        (SheetProjection sheet, IReadOnlyDictionary<string, StyleData>? styles, ResultWindow window) =
            ReadProjection.Project(_resourceBudgets, workbook, request);

        return new WorkbookReadResult
        {
            Source = BuildSource(filePath, workbook),
            Scope = request.Scope.ToContractName(),
            Sheet = sheet,
            Styles = styles,
            // The window's next is left unset: the read command spells the follow-up
            // command, keeping the engine free of CLI syntax.
            Window = window,
            License = EnvelopeParts.License(licenseState),
            Warnings = loaded.Warnings(),
        };
    }
}
