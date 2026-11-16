using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Slides;

/// <summary>Slide views.</summary>
internal sealed class SlidesViewAdapter : IProductViewAdapter<SlidesSession>
{
    public IReadOnlyList<ProductView> Views { get; } =
        [new(SlidesViews.Slides, "Slides", ViewPartKinds.Image)];

    public string LiveView => SlidesViews.Slides;

    public ViewManifest Render(
        SlidesSession session,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        SlidesView.Render(session, filePath, request, artifacts);
}
