using Aspose.Cli.Sdk.IO;
using Aspose.Slides;

namespace Aspose.Cli.Product.Slides.Engine.Mapping;

/// <summary>
/// Applies the document resource policy to every external resource Aspose.Slides resolves:
/// linked pictures, linked media and external chart workbooks. A verified local resource
/// beside the document is supplied; every other reference, including any network address,
/// is skipped and counted. The SDK resolves links lazily while rendering or saving and
/// swallows callback failures, so callers check <see cref="ThrowIfFailed"/> before they
/// publish output.
/// </summary>
internal sealed class SlidesResourcePolicy : IResourceLoadingCallback, IDisposable
{
    private readonly LocalDocumentResourceLoader? _local;
    private int _denied;

    private SlidesResourcePolicy(LocalDocumentResourceLoader? local) => _local = local;

    /// <summary>Supplies verified resources beneath the document's own directory.</summary>
    internal static SlidesResourcePolicy Beside(string documentPath, ResourceBudgetLedger budgets) =>
        new(new LocalDocumentResourceLoader(documentPath, budgets));

    /// <summary>Denies every external resource, for a presentation without a source directory.</summary>
    internal static SlidesResourcePolicy DenyAll() => new(local: null);

    public ResourceLoadingAction ResourceLoading(IResourceLoadingArgs args)
    {
        if (_local is null)
        {
            Interlocked.Increment(ref _denied);
            return ResourceLoadingAction.Skip;
        }

        if (_local.TryRead(args.OriginalUri, out byte[] data))
        {
            args.SetData(data);
            return ResourceLoadingAction.UserProvided;
        }

        return ResourceLoadingAction.Skip;
    }

    /// <summary>Rethrows a budget, deadline or cancellation failure the SDK swallowed.</summary>
    internal void ThrowIfFailed() => _local?.ThrowIfFailed();

    /// <summary>The completeness warning for the resources skipped so far, or null.</summary>
    internal Warning? Warning => _local?.Warning ?? LocalDocumentResourceLoader.OmissionWarning(Volatile.Read(ref _denied));

    public void Dispose() => _local?.Dispose();
}
