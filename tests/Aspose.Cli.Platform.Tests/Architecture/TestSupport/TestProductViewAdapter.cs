using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Architecture.Tests;

/// <summary>The one view a product in these tests declares and renders.</summary>
internal sealed class TestProductViewAdapter<TSession>
    : IProductViewAdapter<TSession>
    where TSession : class
{
    public IReadOnlyList<ProductView> Views =>
        [new("document", "Document", ViewPartKinds.Image)];

    public string LiveView => "document";

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
}
