using Aspose.Cli.Product.Pdf.Contracts;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Pdf.Ports;

/// <summary>SDK-neutral port of the PDF product.</summary>
public interface IPdfEngine
{
    /// <summary>Returns structural, security and metadata information.</summary>
    PdfInfoResult GetInfo(string filePath, PdfInfoRequest request);

    /// <summary>Returns one budgeted page-text window.</summary>
    PdfReadResult Read(string filePath, PdfReadRequest request);

    /// <summary>Converts selected PDF pages to a supported output format.</summary>
    PdfConvertResult Convert(string filePath, PdfConvertRequest request);

    /// <summary>Renders selected PDF pages to visual artifacts.</summary>
    PdfRenderResult Render(string filePath, PdfRenderRequest request);

    /// <summary>Creates one PDF from images, HTML, text or Markdown.</summary>
    PdfWriteResult Create(NewPdfRequest request);

    /// <summary>Merges PDF inputs into one output document.</summary>
    PdfWriteResult Merge(PdfMergeRequest request);

    /// <summary>Splits a PDF transactionally by ranges, fixed groups or bookmarks.</summary>
    PdfSplitResult Split(string filePath, PdfSplitRequest request);

    /// <summary>Extracts bounded assets or text from selected PDF pages.</summary>
    PdfExtractResult Extract(string filePath, PdfExtractRequest request);

    /// <summary>Applies a validated atomic PDF operation batch.</summary>
    PdfEditResult ApplyOps(string filePath, PdfOpsBatch batch, PdfEditRequest request);

    /// <summary>Reads AcroForm or XFA field metadata.</summary>
    PdfFormResult ReadForm(string filePath, PdfFormReadRequest request);

    /// <summary>Fills AcroForm values atomically.</summary>
    PdfEditResult FillForm(string filePath, PdfFormFillRequest request);

    /// <summary>Exports PDF form data.</summary>
    PdfFormExportResult ExportForm(string filePath, PdfFormExportRequest request);

    /// <summary>Searches text and returns page rectangles.</summary>
    PdfSearchResult Search(string filePath, PdfSearchRequest request);

    /// <summary>Validates a PDF against one PDF/A profile.</summary>
    PdfValidateResult Validate(string filePath, PdfValidateRequest request);

    /// <summary>Renders the parts of one product view, opening the document once.</summary>
    ViewManifest RenderView(
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts);

    /// <summary>Applies one PKCS#7 signature and verifies the saved field.</summary>
    PdfSignResult Sign(string filePath, PdfSignRequest request);
}
