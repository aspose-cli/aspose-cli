using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Architecture.Tests;

internal sealed class TestProductPreviewAdapter<TPort>(
    IReadOnlyList<ProductPreviewPayloadContract>? contracts = null)
    : IProductPreviewAdapter<TPort>
    where TPort : class
{
    public string DefaultView => "document";

    public IReadOnlyList<ProductPreviewView> ViewDefinitions =>
        [new("document", "Document")];

    public IReadOnlyList<ProductPreviewPayloadContract> PayloadContracts =>
        contracts ?? [];

    public PreviewRenderer CreateRenderer(
        TPort port,
        string filePath,
        ProductPreviewRequest request) => context =>
    {
        context.Artifacts.WriteText("index.html", "<html></html>");
        return new PreviewRenderOutcome("index.html", string.Empty, 0);
    };

    public ProductPreviewPresentation CreatePresentation(
        string? presentationEffect) =>
        new(string.Empty, string.Empty);

    public void ValidateRequest(
        ProductPreviewRequest request,
        string? presentationEffect)
    {
    }

    public void ValidatePayload(ProductPreviewPayload payload)
    {
    }
}

internal sealed class TestProductViewAdapter<TPort>
    : IProductViewAdapter<TPort>
    where TPort : class
{
    public IReadOnlyList<ProductView> Views =>
        [new("document", "Document", ViewPartKinds.Image)];

    public string ReviewView => "document";

    public string LiveView => "document";

    public bool VisualInspectionRequired => true;

    public ViewManifest Render(
        TPort port,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) => new()
        {
            View = request.View,
            SourceFormat = "test",
            SourceSizeBytes = 0,
            TotalParts = 0,
            Parts = [],
        };

    public ProductReviewAssessment Assess(
        TPort port,
        string filePath,
        ViewRenderRequest request,
        ViewManifest rendered) => new();
}
