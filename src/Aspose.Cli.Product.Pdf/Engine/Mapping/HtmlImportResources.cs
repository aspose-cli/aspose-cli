using Aspose.Cli.Sdk.Contracts;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// Adapts the document resource policy to the Aspose.PDF HTML importer: verified files beneath
/// the HTML directory are supplied and every other reference is omitted. The importer requests
/// a network resource before it calls this loader (see <see cref="PdfNetworkReferenceGuard"/>),
/// so a network address that reaches the loader may already have been requested. The import
/// then fails and says so, instead of reporting the resource as blocked.
/// </summary>
internal sealed class HtmlImportResources(string htmlPath, ResourceBudgetLedger budgets) : IDisposable
{
    private readonly LocalDocumentResourceLoader _local = new(htmlPath, budgets);
    private readonly object _gate = new();
    private string? _firstNetworkReference;
    private int _networkReferences;

    /// <summary>The native directory form of the verified origin, with its trailing separator.</summary>
    internal string BaseDirectory => new Uri(_local.BaseUri).LocalPath;

    internal LoadOptions.ResourceLoadingResult Load(string resourceUri)
    {
        if (IsNetwork(resourceUri))
        {
            lock (_gate)
            {
                _firstNetworkReference ??= resourceUri;
                _networkReferences++;
            }
        }
        else if (_local.TryRead(resourceUri, out byte[] data))
        {
            return new LoadOptions.ResourceLoadingResult(data);
        }

        // Cancelling the custom loader would enable the SDK default loader.
        return new LoadOptions.ResourceLoadingResult([]) { LoadingCancelled = false };
    }

    /// <summary>Rethrows a captured budget or deadline failure, or refuses an import that reached the network.</summary>
    internal void ThrowIfFailed()
    {
        _local.ThrowIfFailed();
        lock (_gate)
        {
            if (_networkReferences > 0)
            {
                throw new CliException(
                    ErrorCodes.FeatureUnsupported,
                    $"The PDF importer resolved {_networkReferences} network resource(s), starting with '{_firstNetworkReference}', and may already have requested them; no PDF was written.",
                    hint: "Remove every network address from the HTML, or save the resources beside it and reference them by relative path.");
            }
        }
    }

    /// <summary>The completeness warning for omitted local resources, after any failure.</summary>
    internal Warning? Warning
    {
        get
        {
            ThrowIfFailed();
            return _local.Warning;
        }
    }

    public void Dispose() => _local.Dispose();

    private static bool IsNetwork(string reference) =>
        Uri.TryCreate(reference, UriKind.Absolute, out Uri? uri)
        && (uri.IsUnc || !(uri.IsFile || string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase)));
}
