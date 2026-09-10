using Aspose.Cli.Product.Slides.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Product.Slides;

/// <summary>Slides-specific preview semantics and browser presentation.</summary>
internal sealed class SlidesPreviewAdapter
    : ProductPreviewAdapterBase<IPresentationEngine>
{
    public const string SlidesView = "slides";

    public SlidesPreviewAdapter()
        : base(
            typeof(SlidesPreviewAdapter).Assembly,
            "Slides",
            SlidesView,
            [new(SlidesView, "Slides")],
            "Omit --fx. Slides preview provides slide-aware change feedback.")
    {
    }

    public override PreviewRenderer CreateRenderer(
        IPresentationEngine port,
        string filePath,
        ProductPreviewRequest request)
    {
        ValidateProductRequest(request);
        return context => port.RenderPreview(
            filePath,
            new PresentationPreviewRequest
            {
                Password = request.Password,
            },
            context.Artifacts);
    }
}
