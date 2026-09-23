using System.Text;
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

        IReadOnlyList<FontAvailability> fonts = UsedFonts(loaded.Document)
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

    /// <summary>
    /// The fonts the document's own text will be drawn with. Word chooses a run's
    /// font per character script, so a run names an East Asian and a complex-script
    /// font whether or not it contains any such character. Reporting those anyway
    /// would flag the theme fonts that sit on every ordinary Latin document.
    /// </summary>
    private static IEnumerable<string> UsedFonts(Document document)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (Run run in document.GetChildNodes(NodeType.Run, true).Cast<Run>())
        {
            bool eastAsian = false;
            bool complexScript = false;
            bool latin = false;
            foreach (Rune rune in run.Text.EnumerateRunes())
            {
                if (IsEastAsian(rune)) { eastAsian = true; }
                else if (IsComplexScript(rune)) { complexScript = true; }
                else { latin = true; }
            }

            if (eastAsian) { Add(names, run.Font.NameFarEast); }
            if (complexScript) { Add(names, run.Font.NameBi); }
            if (latin)
            {
                Add(names, run.Font.Name);
                Add(names, run.Font.NameOther);
            }
        }

        return names.OrderBy(static name => name, StringComparer.Ordinal);
    }

    private static bool IsEastAsian(Rune rune) => rune.Value is
        >= 0x1100 and <= 0x11FF        // Hangul Jamo
        or >= 0x2E80 and <= 0x9FFF     // CJK radicals, kana, Hangul compatibility, ideographs
        or >= 0xAC00 and <= 0xD7AF     // Hangul syllables
        or >= 0xF900 and <= 0xFAFF     // CJK compatibility ideographs
        or >= 0xFF00 and <= 0xFFEF     // Halfwidth and fullwidth forms
        or >= 0x20000 and <= 0x3FFFF;  // CJK ideograph extensions

    private static bool IsComplexScript(Rune rune) => rune.Value is
        >= 0x0590 and <= 0x0E7F        // Hebrew, Arabic, Indic and Thai scripts
        or >= 0xFB1D and <= 0xFDFF     // Hebrew and Arabic presentation forms
        or >= 0xFE70 and <= 0xFEFF;    // Arabic presentation forms-B

    private static void Add(ISet<string> names, string? name)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            names.Add(name);
        }
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
