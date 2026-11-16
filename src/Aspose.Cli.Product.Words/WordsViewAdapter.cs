using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Words;

/// <summary>Product-owned page views.</summary>
internal sealed class WordsViewAdapter : IProductViewAdapter<WordsSession>
{
    public IReadOnlyList<ProductView> Views { get; } =
        [new(WordsViews.Pages, "Pages", ViewPartKinds.Image)];

    public string LiveView => WordsViews.Pages;

    public ViewManifest Render(
        WordsSession session,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        WordsRender.View(session, filePath, request, artifacts);
}
