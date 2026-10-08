using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Projects the sheet, dimension, print-area and chart layout facts the review adapter checks.</summary>
internal sealed class CellsReviewLayoutService
{
    private readonly ILicenseGate _licenseGate;
    private readonly CellsWorkbookLoader _loader;

    internal CellsReviewLayoutService(
        ILicenseGate licenseGate,
        CellsWorkbookLoader loader)
    {
        ArgumentNullException.ThrowIfNull(licenseGate);
        ArgumentNullException.ThrowIfNull(loader);
        _licenseGate = licenseGate;
        _loader = loader;
    }

    internal CellsReviewLayout Inspect(
        string filePath,
        Secret? password)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _ = _licenseGate.EnsureApplied();
        using LoadedWorkbook loaded = _loader.Open(filePath, password);
        return ReviewLayoutProjection.Inspect(loaded.Workbook) with { Warnings = loaded.Warnings() };
    }
}
