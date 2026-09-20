using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>The one view a product in these tests declares and renders.</summary>
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
