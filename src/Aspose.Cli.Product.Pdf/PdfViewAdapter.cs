using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Pdf;

/// <summary>Page views.</summary>
internal sealed class PdfViewAdapter : IProductViewAdapter<PdfSession>
{
    public IReadOnlyList<ProductView> Views { get; } =
        [new(PdfViews.Pages, "Pages", ViewPartKinds.Image)];

    public string LiveView => PdfViews.Pages;

    public ViewManifest Render(
        PdfSession session,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) =>
        PdfPagesView.Render(session, filePath, request, artifacts);
}
