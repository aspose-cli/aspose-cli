using Aspose.Cli.Product.Words.Contracts;
using Aspose.Cli.Sdk.Preview;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Words.Ports;

/// <summary>SDK-neutral port of the Words product.</summary>
public interface IDocumentEngine
{
    /// <summary>Returns structural document information.</summary>
    DocumentInfoResult GetInfo(string filePath, DocumentInfoRequest request);

    /// <summary>Returns one budgeted canonical block window.</summary>
    DocumentReadResult Read(string filePath, DocumentReadRequest request);

    /// <summary>Converts a document to a supported output format.</summary>
    WordsConvertResult Convert(string filePath, WordsConvertRequest request);

    /// <summary>Renders selected pages to image files.</summary>
    WordsRenderResult Render(string filePath, WordsRenderRequest request);

    /// <summary>Exports one paginated raster HTML snapshot for the local preview host.</summary>
    PreviewRenderOutcome RenderPreview(
        string filePath,
        WordsPreviewRequest request,
        IPreviewArtifactSink artifacts);

    /// <summary>Renders the parts of one product view, opening the document once.</summary>
    ViewManifest RenderView(
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts);

    /// <summary>Creates a new document.</summary>
    WordsCreateResult CreateDocument(NewDocumentRequest request);

    /// <summary>Applies one validated atomic operation batch.</summary>
    WordsEditResult ApplyOps(string filePath, WordsOpsBatch batch, WordsEditRequest request);

    /// <summary>Compares two revision-free documents.</summary>
    WordsCompareResult Compare(string leftPath, string rightPath, WordsCompareRequest request);

    /// <summary>Searches bounded document scopes.</summary>
    WordsSearchResult Search(string filePath, WordsSearchRequest request);

    /// <summary>Splits a document into a transactional output set.</summary>
    WordsSplitResult Split(string filePath, WordsSplitRequest request);

    /// <summary>Extracts a bounded set of document assets.</summary>
    WordsExtractResult Extract(string filePath, WordsExtractRequest request);
}
