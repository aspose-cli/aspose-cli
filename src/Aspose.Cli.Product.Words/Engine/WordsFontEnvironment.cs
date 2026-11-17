using Aspose.Cli.Sdk.Rendering;
using Aspose.Words.Fonts;

namespace Aspose.Cli.Product.Words.Engine;

/// <summary>
/// Aspose.Words font diagnostics behind the product-neutral <see cref="IFontEnvironment"/>.
/// Aspose.Words font discovery stays in this adapter.
/// </summary>
internal sealed partial class WordsFontEnvironment : IFontEnvironment
{
    /// <inheritdoc />
    public EngineFonts ListFonts() => new()
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

    private static FontSource ToContract(FontSourceBase source) => source switch
    {
        FolderFontSource folder => new FontSource { Kind = "folder", Location = folder.FolderPath },
        FileFontSource file => new FontSource { Kind = "file", Location = file.FilePath },
        MemoryFontSource => new FontSource { Kind = "memory" },
        SystemFontSource => new FontSource { Kind = "system" },
        _ => new FontSource { Kind = "other" },
    };
}
