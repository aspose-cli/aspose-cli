using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Pdf;
using Aspose.Pdf.Text;

namespace Aspose.Cli.Product.Pdf.Engine;

/// <summary>Aspose.PDF font diagnostics behind the product-neutral <see cref="IFontEnvironment"/>.</summary>
internal sealed class PdfFontEnvironment : IFontEnvironment
{
    private readonly ILicenseState _license;
    private readonly PdfDocumentLoader _loader;

    public PdfFontEnvironment(
        ILicenseState license,
        ResourceBudgetLedger resourceBudgets)
    {
        _license = license ?? throw new ArgumentNullException(nameof(license));
        _loader = new PdfDocumentLoader(resourceBudgets);
    }

    /// <inheritdoc />
    public FontListResult ListFonts() => new()
    {
        Sources = FontRepository.Sources.Select(ToContract).ToArray(),
    };

    /// <inheritdoc />
    /// <remarks>Aspose.PDF resolves fonts only through its global repository.</remarks>
    public IDisposable UseFonts(FontSearchProfile profile) =>
        FontScope.Enter(profile, static directories =>
        {
            Aspose.Pdf.Text.FontSource[] ambient = FontRepository.Sources.ToArray();
            foreach (string directory in directories)
            {
                FontRepository.Sources.Add(new FolderFontSource(directory));
            }
            return () =>
            {
                FontRepository.Sources.Clear();
                foreach (Aspose.Pdf.Text.FontSource source in ambient)
                {
                    FontRepository.Sources.Add(source);
                }
            };
        });

    /// <inheritdoc />
    public FontCheckResult CheckFonts(string filePath, FontCheckRequest request)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        ArgumentNullException.ThrowIfNull(request);

        LicenseState state = _license.License;
        using LoadedPdf loaded = _loader.Open(filePath, request.Password);
        IReadOnlyList<FontAvailability> fonts = UsedFonts(loaded.Document)
            .Select(static font => new FontAvailability
            {
                Name = font.Name,
                Available = font.Available,
            })
            .ToArray();
        return new FontCheckResult
        {
            Source = PdfInfoProjection.Source(filePath),
            AllAvailable = fonts.All(static font => font.Available),
            Fonts = fonts,
            License = EnvelopeParts.License(state),
        };
    }

    private static IReadOnlyList<UsedFont> UsedFonts(Document document)
    {
        var fonts = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (Font font in PdfFontResources.Enumerate(document))
        {
            string name = font.DecodedFontName ?? font.FontName ?? "unknown";
            fonts[name] = fonts.GetValueOrDefault(name) || font.IsEmbedded || font.IsAccessible;
        }

        return fonts
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new UsedFont(pair.Key, pair.Value))
            .ToArray();
    }

    private static Aspose.Cli.Sdk.Contracts.FontSource ToContract(Aspose.Pdf.Text.FontSource source) => source switch
    {
        FolderFontSource folder => new Aspose.Cli.Sdk.Contracts.FontSource { Type = "folder", Location = folder.FolderPath },
        SystemFontSource => new Aspose.Cli.Sdk.Contracts.FontSource { Type = "system" },
        _ => new Aspose.Cli.Sdk.Contracts.FontSource { Type = "other" },
    };

    private sealed record UsedFont(string Name, bool Available);
}
