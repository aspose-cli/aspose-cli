using Aspose.Cells;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Font-environment queries against the engine's global <see cref="FontConfigs"/>.
/// Isolated here so the Aspose font API stays out of the engine's main flow and
/// the anti-corruption layer owns the translation to contract types.
/// </summary>
internal static class FontOps
{
    /// <summary>The engine's font environment: default fallback plus the scanned sources.</summary>
    public static EngineFonts BuildFontList() => new()
    {
        DefaultFont = FontConfigs.DefaultFontName,
        Sources = FontConfigs.GetFontSources().Select(ToSource).ToArray(),
    };

    /// <summary>
    /// Adds the profile's directories to the global font configuration for one
    /// scope. The global configuration, not <see cref="LoadOptions.FontConfigs"/>,
    /// is the documented way here: individual font configs of one workbook leak
    /// into every workbook the process loads next, including the availability
    /// answers of <see cref="FontConfigs.IsFontAvailable"/>.
    /// </summary>
    public static IDisposable Use(FontSearchProfile profile) =>
        FontScope.Enter(profile, static directories =>
        {
            FontSourceBase[] ambient = FontConfigs.GetFontSources();
            FontConfigs.SetFontSources(
            [
                .. ambient,
                .. directories.Select(static directory => new FolderFontSource(directory, false)),
            ]);
            return () => FontConfigs.SetFontSources(ambient);
        });

    private static FontSource ToSource(FontSourceBase source) => source switch
    {
        FolderFontSource folder => new FontSource { Kind = "folder", Location = folder.FolderPath },
        FileFontSource file => new FontSource { Kind = "file", Location = file.FilePath },
        _ => new FontSource { Kind = "memory" },
    };
}
