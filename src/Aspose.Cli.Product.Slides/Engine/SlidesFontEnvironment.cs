using Aspose.Cli.Sdk.Rendering;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>Aspose.Slides font sources behind the product-neutral <see cref="IFontEnvironment"/>.</summary>
internal sealed class SlidesFontEnvironment : IFontEnvironment
{
    /// <inheritdoc />
    public EngineFonts ListFonts() =>
        new EngineFonts
        {
            Sources = FontsLoader.GetFontFolders()
                .Select(static folder => new FontSource { Kind = "folder", Location = folder })
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
}
