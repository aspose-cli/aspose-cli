using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Aspose.Slides font diagnostics behind the product-neutral font port.</summary>
internal sealed class SlidesFontEnvironment : IFontEnvironment
{
    private readonly ILicenseGate _licenseGate;
    private readonly SlidesPresentationLoader _loader;

    public SlidesFontEnvironment(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _loader = new SlidesPresentationLoader(resourceBudgets);
    }

    /// <inheritdoc />
    public FontListResult ListFonts() =>
        new FontListResult
        {
            Sources = FontsLoader.GetFontFolders()
                .Select(static folder => new FontSource { Type = "folder", Location = folder })
                .ToArray(),
        };

    /// <inheritdoc />
    /// <remarks>
    /// External fonts are the documented process-wide Slides sources, and the
    /// application loads no other, so clearing them restores the system fonts.
    /// </remarks>
    public IDisposable UseFonts(FontSearchProfile profile) =>
        FontScope.Enter(profile, static directories =>
        {
            FontsLoader.LoadExternalFonts(directories.ToArray());
            return FontsLoader.ClearCache;
        });

    /// <inheritdoc />
    public FontCheckResult CheckFonts(string filePath, FontCheckRequest request)
    {
        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedPresentation loaded = _loader.Open(filePath, request.Password);
        Dictionary<string, string> substitutions = loaded.Presentation.FontsManager.GetSubstitutions()
            .ToDictionary(static item => item.OriginalFontName, static item => item.SubstitutedFontName, StringComparer.Ordinal);
        FontAvailability[] fonts = loaded.Presentation.FontsManager.GetFonts()
            .Select(static font => font.FontName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(name => new FontAvailability
            {
                Name = name,
                Available = !substitutions.ContainsKey(name),
                SubstitutedBy = substitutions.GetValueOrDefault(name),
            })
            .ToArray();
        return new FontCheckResult
        {
            Source = new SourceInfo
            {
                Path = filePath,
                Format = loaded.FormatId,
                SizeBytes = new FileInfo(filePath).Length,
            },
            AllAvailable = fonts.All(static font => font.Available),
            Fonts = fonts,
            License = EnvelopeParts.License(state),
        };
    }
}
