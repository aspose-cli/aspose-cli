using Aspose.Cli.Product.Slides.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Ports;
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
        SlidesFontCatalog.EnsureInitialized();
    }

    /// <inheritdoc />
    public FontListResult ListFonts() =>
        SlidesErrorTranslator.Execute("fonts list", () => new FontListResult
        {
            Sources = FontsLoader.GetFontFolders()
                .Select(static folder => new FontSource { Type = "folder", Location = folder })
                .ToArray(),
        });

    /// <inheritdoc />
    public FontCheckResult CheckFonts(string filePath, FontCheckRequest request) =>
        SlidesErrorTranslator.Execute("fonts check", () => CheckFontsCore(filePath, request));

    private FontCheckResult CheckFontsCore(string filePath, FontCheckRequest request)
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
