using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>The one view a product in these tests declares and renders, with the given review checks and findings.</summary>
internal sealed class TestProductViewAdapter<TSession>(
    IReadOnlyList<ReviewCheck>? checks = null,
    IReadOnlyList<ReviewFinding>? findings = null)
    : IProductViewAdapter<TSession>
    where TSession : class
{
    public IReadOnlyList<ProductView> Views =>
        [new("document", "Document", ViewPartKinds.Image)];

    public string ReviewView => "document";

    public string LiveView => "document";

    public bool VisualInspectionRequired => true;

    public IReadOnlyList<ReviewCheck> Checks => checks ?? [];

    public ViewManifest Render(
        TSession session,
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts) => new()
        {
            View = request.View,
            SourceFormat = "test",
            SourceSizeBytes = 0,
            SourceEncrypted = false,
            TotalPartCount = 0,
            Parts = [],
        };

    public ProductReviewAssessment Assess(
        TSession session,
        string filePath,
        ViewRenderRequest request,
        ViewManifest rendered) => new() { Findings = findings };
}
