using System.Text.RegularExpressions;
using Aspose.Cli.Product.Words.Engine.Mapping;
using Aspose.Cli.Sdk.IO;
using Aspose.Cli.Sdk.Licensing;
using Aspose.Cli.Sdk.Rendering;
using Aspose.Cli.Sdk.Results;
using Aspose.Words;
using Aspose.Words.Fonts;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>
/// Aspose.Words font diagnostics behind the product-neutral <see cref="IFontEnvironment"/>.
/// Aspose.Words font discovery stays in this adapter.
/// </summary>
internal sealed partial class WordsFontEnvironment : IFontEnvironment
{
    private readonly ILicenseState _license;
    private readonly WordsDocumentLoader _loader;

    /// <summary>Creates a Words font diagnostic adapter.</summary>
    public WordsFontEnvironment(
        ILicenseState license,
        ResourceBudgetLedger resourceBudgets)
    {
        _license = license ?? throw new ArgumentNullException(nameof(license));
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

        LicenseState state = _license.License;
        var substitutions = new FontSubstitutions();
        using LoadedDocument loaded = _loader.Open(filePath, request.Password, substitutions);

        // Only layout resolves fonts, and it resolves them by every name a font file carries:
        // a document's "宋体" is the installed SimSun, whose font sources list only "SimSun".
        // So a font is available unless laying the document out substituted it.
        loaded.Document.WarningCallback = substitutions;
        loaded.Document.UpdatePageLayout();

        IReadOnlyList<FontAvailability> fonts = WordsFonts.Used(loaded.Document)
            .Select(name =>
            {
                string? substitute = substitutions.For(name);
                return new FontAvailability
                {
                    Name = name,
                    Available = substitute is null,
                    SubstitutedBy = substitute,
                };
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

    /// <summary>
    /// The substitutions Aspose.Words reports while it lays the document out. The SDK reports
    /// them only as <see cref="WarningType.FontSubstitution"/> text of the form "Font 'A' has
    /// not been found. Using 'B' font instead. Reason: ..."; a description in another form
    /// names no substitute, and a font the layout never draws reports none.
    /// </summary>
    private sealed partial class FontSubstitutions : IWarningCallback
    {
        private readonly Dictionary<string, string> _substitutes = new(StringComparer.OrdinalIgnoreCase);

        public string? For(string font) => _substitutes.GetValueOrDefault(font);

        public void Warning(WarningInfo info)
        {
            ArgumentNullException.ThrowIfNull(info);
            if (info.WarningType == WarningType.FontSubstitution && Description().Match(info.Description) is { Success: true } match)
            {
                _substitutes.TryAdd(match.Groups["font"].Value, match.Groups["substitute"].Value);
            }
        }

        [GeneratedRegex(@"^Font '(?<font>.+?)' has not been found\. Using '(?<substitute>.+?)' font instead\.", RegexOptions.CultureInvariant)]
        private static partial Regex Description();
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
