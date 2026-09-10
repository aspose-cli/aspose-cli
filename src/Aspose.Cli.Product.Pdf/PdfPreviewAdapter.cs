using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Preview;

namespace Aspose.Cli.Product.Pdf;

/// <summary>PDF-specific preview semantics and browser presentation.</summary>
internal sealed class PdfPreviewAdapter
    : ProductPreviewAdapterBase<IPdfEngine>
{
    public const string PagesView = "pages";

    public PdfPreviewAdapter()
        : base(
            typeof(PdfPreviewAdapter).Assembly,
            "PDF",
            PagesView,
            [new(PagesView, "Pages")],
            "Omit --fx. PDF preview provides page-aware change feedback.")
    {
    }

    public override PreviewRenderer CreateRenderer(
        IPdfEngine port,
        string filePath,
        ProductPreviewRequest request)
    {
        ValidateProductRequest(request);
        return context => port.RenderPreview(
            filePath,
            new PdfPreviewRequest { Password = request.Password },
            context.Artifacts);
    }
}
