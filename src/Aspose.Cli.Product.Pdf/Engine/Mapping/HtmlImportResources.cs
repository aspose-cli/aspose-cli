using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// Adapts the document resource policy to the Aspose.PDF HTML importer: verified files beneath
/// the HTML directory are supplied and every other local reference, shares included, is
/// omitted. The importer requests a network resource before it calls this loader (known issue
/// PDF-HTML-EGRESS in KNOWN-ISSUES.md), so the process's <see cref="NetworkGate"/>, not this
/// loader, decides whether that request leaves the process. By default the gate blocked it and
/// the loader supplies nothing; with network resources allowed, the importer keeps what it
/// fetched and the result discloses every address.
/// </summary>
internal sealed class HtmlImportResources(string htmlPath, ResourceBudgetLedger budgets, bool allowNetwork) : IDisposable
{
    private const int ListedAddresses = 20;
    private readonly LocalDocumentResourceLoader _local = new(htmlPath, budgets);
    private readonly object _gate = new();
    private readonly List<string> _network = [];

    /// <summary>The native directory form of the verified origin, with its trailing separator.</summary>
    internal string BaseDirectory => new Uri(_local.BaseUri).LocalPath;

    internal LoadOptions.ResourceLoadingResult Load(string resourceUri)
    {
        if (IsNetwork(resourceUri))
        {
            if (!allowNetwork)
            {
                // The gate blocked the request and lists it; supply nothing in its place.
                return new LoadOptions.ResourceLoadingResult([]) { LoadingCancelled = false };
            }

            lock (_gate)
            {
                if (!_network.Contains(resourceUri, StringComparer.Ordinal))
                {
                    _network.Add(resourceUri);
                }
            }

            // Cancelling this loader keeps what the importer already fetched.
            return new LoadOptions.ResourceLoadingResult([]) { LoadingCancelled = true };
        }

        if (_local.TryRead(resourceUri, out byte[] data))
        {
            return new LoadOptions.ResourceLoadingResult(data);
        }

        // Cancelling the custom loader would enable the SDK default loader.
        return new LoadOptions.ResourceLoadingResult([]) { LoadingCancelled = false };
    }

    /// <summary>Rethrows a captured budget, deadline or resource failure.</summary>
    internal void ThrowIfFailed() => _local.ThrowIfFailed();

    /// <summary>The omitted-resource and network-request warnings, after any failure.</summary>
    internal IReadOnlyList<Warning> Warnings
    {
        get
        {
            ThrowIfFailed();
            var warnings = new List<Warning>();
            if (_local.Warning is { } omitted)
            {
                warnings.Add(omitted);
            }

            lock (_gate)
            {
                if (_network.Count > 0)
                {
                    string listed = string.Join(", ", _network.Take(ListedAddresses));
                    string more = _network.Count > ListedAddresses ? $", and {_network.Count - ListedAddresses} more" : "";
                    warnings.Add(new Warning(PdfDiagnostics.NetworkResourcesRequested, $"The HTML importer requested {_network.Count} network resource(s): {listed}{more}.")
                    {
                        Hint = "The output includes content fetched from these addresses. Review it, and never pass --allow-network-resources for HTML you do not trust.",
                    });
                }
            }

            return warnings;
        }
    }

    public void Dispose() => _local.Dispose();

    // Any absolute address but a local file, a share or inline data. Shares resolve as file
    // references, which the importer passes to this loader before reading them.
    private static bool IsNetwork(string reference) =>
        Uri.TryCreate(reference, UriKind.Absolute, out Uri? uri)
        && !uri.IsFile && !string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase);
}
