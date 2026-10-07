using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Product.Slides.Ports;

/// <summary>SDK-neutral port of the Slides product.</summary>
public interface ISlidesEngine
{
    /// <summary>Returns presentation structure, stable slide ids and optional detail inventories.</summary>
    PresentationInfoResult GetInfo(string filePath, PresentationInfoRequest request);

    /// <summary>Returns one bounded slide-content window.</summary>
    PresentationReadResult Read(string filePath, PresentationReadRequest request);

    /// <summary>Converts a presentation or selected slides to a supported format.</summary>
    SlidesConvertResult Convert(string filePath, PresentationConvertRequest request);

    /// <summary>Renders selected slides to deterministic image artifacts.</summary>
    SlidesRenderResult Render(string filePath, PresentationRenderRequest request);

    /// <summary>Creates a blank, template-based or Markdown-authored presentation.</summary>
    SlidesCreateResult Create(NewPresentationRequest request);

    /// <summary>Extracts bounded media, notes or text artifacts.</summary>
    SlidesExtractResult Extract(string filePath, PresentationExtractRequest request);

    /// <summary>Applies one validated operation batch.</summary>
    SlidesEditResult ApplyOps(string filePath, SlidesOpsBatch batch, PresentationEditRequest request);

    /// <summary>Searches shape and speaker-notes text.</summary>
    SlidesSearchResult Search(string filePath, PresentationSearchRequest request);

    /// <summary>Renders the parts of one product view, opening the presentation once.</summary>
    ViewManifest RenderView(
        string filePath,
        ViewRenderRequest request,
        IViewArtifactSink artifacts);
}
