using Aspose.Cli.Product.Cells.Engine.Mapping;
using Aspose.Cli.Sdk.Rendering;

namespace Aspose.Cli.Product.Cells.Engine;

/// <summary>Aspose.Cells font diagnostics behind the product-neutral <see cref="IFontEnvironment"/>.</summary>
internal sealed class CellsFontEnvironment : IFontEnvironment
{
    /// <inheritdoc />
    /// <remarks>
    /// Font configuration is global to the engine and license-independent, so
    /// this neither opens a workbook nor touches the license gate.
    /// </remarks>
    public EngineFonts ListFonts() => FontOps.BuildFontList();

    /// <inheritdoc />
    public IDisposable UseFonts(FontSearchProfile profile) => FontOps.Use(profile);
}
