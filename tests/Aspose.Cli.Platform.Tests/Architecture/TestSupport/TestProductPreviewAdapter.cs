using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Preview;

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

internal sealed class TestProductReviewAdapter<TPort>
    : IProductReviewAdapter<TPort>
    where TPort : class
{
    public string DefaultView => "document";

    public IReadOnlyList<string> Views => [DefaultView];

    public bool VisualInspectionRequired => true;

    public ProductReviewRenderer CreateRenderer(
        TPort port,
        string filePath,
        ProductReviewRequest request) =>
        _ => new ProductReviewRenderOutcome("index.html", "test", 0)
        {
            ExpectedItems = 1,
            RenderedItems = 1,
            Complete = true,
        };
}
