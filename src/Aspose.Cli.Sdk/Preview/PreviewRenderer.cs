namespace Aspose.Cli.Sdk.Preview;

/// <summary>
/// What a <see cref="PreviewRenderer"/> reports after a successful render.
/// </summary>
/// <param name="EntryFileName">
/// File name of the document entry point the renderer wrote into the version
/// directory (for example, <c>book.html</c>).
/// </param>
/// <param name="SourceFormatId">Detected format id of the watched source file.</param>
/// <param name="SourceSizeBytes">Size of the watched source file at render time, in bytes.</param>
public sealed record PreviewRenderOutcome(
    string EntryFileName,
    string SourceFormatId,
    long SourceSizeBytes,
    IReadOnlyList<Contracts.Warning>? Warnings = null);

/// <summary>
/// Immutable input for one product preview render. The shared runtime owns
/// the publication directory, exposes it only through a bounded sink, and
/// transports optional product state without interpreting its payload.
/// </summary>
/// <param name="Artifacts">Bounded destination for the immutable snapshot.</param>
/// <param name="State">Validated product-owned view state, or null for the default view.</param>
public sealed record PreviewRenderContext(
    IPreviewArtifactSink Artifacts,
    ProductPreviewPayload? State = null);

/// <summary>
/// Renders the watched source file through the context's bounded artifact sink
/// and reports the entry file name plus source information. This delegate is
/// the seam that keeps preview infrastructure product-neutral: product
/// adapters supply the concrete rendering, and the preview session only sees
/// bounded artifacts going in and an outcome coming out. Implementations throw on
/// failure; engine adapters throw already-translated
/// <see cref="Errors.CliException"/> instances, which the session turns into
/// a <c>status</c> event without tearing the preview down.
/// </summary>
/// <param name="context">Bounded artifacts plus validated product-owned state.</param>
public delegate PreviewRenderOutcome PreviewRenderer(PreviewRenderContext context);

/// <summary>
/// Receives preview-view artifacts without exposing the Host-owned publication
/// directory. Implementations enforce file-count, per-file, and aggregate
/// byte limits while each artifact is written. A state HTML document is served
/// from the immutable publication root, so it references an artifact named
/// <c>images/chart.png</c> as <c>asset/images/chart.png</c>. CSS is served from
/// beneath that <c>asset/</c> route and uses ordinary document-relative URLs:
/// <c>../images/chart.png</c> from <c>styles/main.css</c>, for example. Paths
/// must use URI-escaped, forward-slash segments matching sink-relative paths.
/// Root-relative <c>/asset/</c> URLs are forbidden because that route belongs
/// to the mutable default document snapshot.
/// </summary>
public interface IPreviewArtifactSink : Views.IViewArtifactSink;
