using Aspose.Cells;
using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using static Aspose.Cli.Product.Cells.Engine.CellsEngineSupport;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Aspose.Cells font diagnostics behind the product-neutral font port.</summary>
internal sealed class CellsFontEnvironment : IFontEnvironment
{
    private readonly ILicenseState _license;
    private readonly CellsWorkbookLoader _loader;

    public CellsFontEnvironment(
        ILicenseState license,
        ResourceBudgetLedger resourceBudgets)
    {
        ArgumentNullException.ThrowIfNull(license);
        ArgumentNullException.ThrowIfNull(resourceBudgets);
        _license = license;
        _loader = new CellsWorkbookLoader(resourceBudgets);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Font configuration is global to the engine and license-independent, so
    /// this neither opens a workbook nor touches the license gate.
    /// </remarks>
    public FontListResult ListFonts() => FontOps.BuildFontList();

    /// <inheritdoc />
    public IDisposable UseFonts(FontSearchProfile profile) => FontOps.Use(profile);

    /// <inheritdoc />
    public FontCheckResult CheckFonts(string filePath, FontCheckRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState licenseState = _license.License;
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
