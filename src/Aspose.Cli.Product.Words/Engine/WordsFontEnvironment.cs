using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Ports;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Fonts;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>
/// Aspose.Words font diagnostics behind the product-neutral font port.
/// SDK font discovery stays in the Words adapter and never crosses into Core.
/// </summary>
internal sealed class WordsFontEnvironment : IFontEnvironment
{
    private readonly ILicenseGate _licenseGate;
    private readonly WordsDocumentLoader _loader;

    /// <summary>Creates a Words font diagnostic adapter.</summary>
    public WordsFontEnvironment(
        ILicenseGate licenseGate,
        ResourceBudgetLedger resourceBudgets)
    {
        _licenseGate = licenseGate ?? throw new ArgumentNullException(nameof(licenseGate));
        _loader = new WordsDocumentLoader(resourceBudgets);
    }

    /// <inheritdoc />
    public FontListResult ListFonts() => new()
    {
        Sources = FontSettings.DefaultInstance.GetFontsSources()
            .Select(ToContract)
            .ToArray(),
    };

    /// <inheritdoc />
    /// <remarks>
    /// Every document opened without its own font settings uses the default
    /// instance, so the directories reach rendering, layout and this check alike.
    /// </remarks>
    public IDisposable UseFonts(FontSearchProfile profile) =>
        FontScope.Enter(profile, static directories =>
        {
            FontSettings settings = FontSettings.DefaultInstance;
            FontSourceBase[] ambient = settings.GetFontsSources();
            settings.SetFontsSources(
            [
                .. ambient,
                .. directories.Select(static directory => new FolderFontSource(directory, scanSubfolders: false)),
            ]);
            return () => settings.SetFontsSources(ambient);
        });

    /// <inheritdoc />
    public FontCheckResult CheckFonts(string filePath, FontCheckRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState state = _licenseGate.EnsureApplied();
        using LoadedDocument loaded = _loader.Open(filePath, request.Password);

        HashSet<string> available = FontSettings.DefaultInstance.GetFontsSources()
            .SelectMany(static source => source.GetAvailableFonts())
            .Select(static font => font.FullFontName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<FontAvailability> fonts = WordsFonts.Used(loaded.Document)
            .Select(name => new FontAvailability
            {
                Name = name,
                Available = available.Contains(name),
            })
            .ToArray();

        return new FontCheckResult
        {
            Source = InfoProjection.Source(filePath, loaded),
            AllAvailable = fonts.All(static font => font.Available),
            Fonts = fonts,
            License = EnvelopeParts.License(state),
        };
    }

    private static FontSource ToContract(FontSourceBase source) => source switch
    {
        FolderFontSource folder => new FontSource { Type = "folder", Location = folder.FolderPath },
        FileFontSource file => new FontSource { Type = "file", Location = file.FilePath },
        MemoryFontSource => new FontSource { Type = "memory" },
        SystemFontSource => new FontSource { Type = "system" },
        _ => new FontSource { Type = "other" },
    };
}
