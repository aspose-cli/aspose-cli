using Aspose.Cells;
using Aspose.Cli.Sdk.Contracts;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Font-environment queries against the engine's global <see cref="FontConfigs"/>.
/// Isolated here so the Aspose font API stays out of the engine's main flow and
/// the anti-corruption layer owns the translation to contract types.
/// </summary>
internal static class FontOps
{
    /// <summary>The engine's font environment: default fallback plus the scanned sources.</summary>
    public static FontListResult BuildFontList() => new()
    {
        DefaultFont = FontConfigs.DefaultFontName,
        Sources = FontConfigs.GetFontSources().Select(ToSource).ToArray(),
    };

    /// <summary>The distinct font names a workbook uses, sorted — the input to
    /// both <c>cells inspect --detail fonts</c> and the <c>fonts check</c> diagnostic.</summary>
    public static IReadOnlyList<string> UsedFonts(Workbook workbook) =>
        workbook.GetFonts()
            .Select(font => font.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    /// <summary>
    /// Availability of each used font, with the substitute a render would pick
    /// for any that is missing — the actionable half of the P-5 diagnostic.
    /// </summary>
    public static IReadOnlyList<FontAvailability> CheckAvailability(IEnumerable<string> usedFonts)
    {
        var result = new List<FontAvailability>();
        foreach (string name in usedFonts)
        {
            bool available = FontConfigs.IsFontAvailable(name);
            result.Add(new FontAvailability
            {
                Name = name,
                Available = available,
                SubstitutedBy = available ? null : PredictSubstitute(name),
            });
        }

        return result;
    }

    private static FontSource ToSource(FontSourceBase source) => source switch
    {
        FolderFontSource folder => new FontSource { Type = "folder", Location = folder.FolderPath },
        FileFontSource file => new FontSource { Type = "file", Location = file.FilePath },
        _ => new FontSource { Type = "memory" },
    };

    // A configured substitute wins; otherwise fall back to the engine default
    // when one is configured. Null means "unavailable, engine picks a fallback"
    // — honest when no substitution is queryable, rather than inventing one.
    private static string? PredictSubstitute(string name)
    {
        string[] substitutes = FontConfigs.GetFontSubstitutes(name);
        return substitutes is { Length: > 0 } ? substitutes[0] : FontConfigs.DefaultFontName;
    }
}
