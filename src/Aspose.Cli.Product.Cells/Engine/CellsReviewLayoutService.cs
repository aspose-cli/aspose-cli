using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Licensing;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Projects the sheet, dimension, print-area and chart layout facts the review adapter checks.</summary>
internal sealed class CellsReviewLayoutService
{
    private readonly ILicenseState _license;
    private readonly CellsWorkbookLoader _loader;

    internal CellsReviewLayoutService(
        ILicenseState license,
        CellsWorkbookLoader loader)
    {
        ArgumentNullException.ThrowIfNull(license);
        ArgumentNullException.ThrowIfNull(loader);
        _license = license;
        _loader = loader;
    }

    internal CellsReviewLayout Inspect(
        string filePath,
        Secret? password)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        _ = _license.License;
        using LoadedWorkbook loaded = _loader.Open(filePath, password);
        return ReviewLayoutProjection.Inspect(loaded.Workbook) with { Warnings = loaded.Warnings() };
    }
}
