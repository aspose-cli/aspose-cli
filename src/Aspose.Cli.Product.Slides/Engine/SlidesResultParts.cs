using Aspose.Cli.Sdk.IO.Output;

namespace Aspose.Cli.Product.Slides.Engine;

/// <summary>The parts every handler's result takes from the loaded presentation.</summary>
internal static class SlidesResultParts
{
    /// <summary>The external resources the document policy left out, or null.</summary>
    internal static IReadOnlyList<Warning>? InputWarnings(LoadedPresentation loaded) =>
        loaded.Resources.Warning is { } omitted ? [omitted] : null;

    internal static InputInfo Source(string path, LoadedPresentation loaded) =>
        FileObjects.Input(path, loaded.FormatId, loaded.Encrypted);
}
