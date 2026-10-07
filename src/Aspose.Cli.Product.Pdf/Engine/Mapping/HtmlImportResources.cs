using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// Adapts the document resource policy to the Aspose.PDF HTML importer: verified files beneath
/// the HTML directory are supplied and every other local reference, shares included, is
/// omitted. The importer requests a network resource before it calls this loader (known issue
/// PDF-HTML-EGRESS in KNOWN-ISSUES.md). By default the HTML was refused before the import if it
/// named one, so an address that still reaches the loader fails the import, and a supplied
/// stylesheet or SVG that names one is refused before the importer can parse it. With network
/// resources allowed, the importer keeps what it fetched and the result discloses every address.
/// </summary>
internal sealed class HtmlImportResources(string htmlPath, ResourceBudgetLedger budgets, bool allowNetwork) : IDisposable
{
    private const int ListedAddresses = 20;
    private readonly LocalDocumentResourceLoader _local = new(htmlPath, budgets);
    private readonly object _gate = new();
    private readonly List<string> _network = [];
    private CliException? _refused;

    /// <summary>The native directory form of the verified origin, with its trailing separator.</summary>
    internal string BaseDirectory => new Uri(_local.BaseUri).LocalPath;

    internal LoadOptions.ResourceLoadingResult Load(string resourceUri)
    {
        if (IsNetwork(resourceUri))
        {
            lock (_gate)
            {
                if (!_network.Contains(resourceUri, StringComparer.Ordinal))
                {
                    _network.Add(resourceUri);
                }
            }

            // Cancelling this loader keeps what the importer already fetched.
            return new LoadOptions.ResourceLoadingResult([]) { LoadingCancelled = allowNetwork };
        }

        if (_local.TryRead(resourceUri, out byte[] data) && Checked(data, resourceUri))
        {
            return new LoadOptions.ResourceLoadingResult(data);
        }

        // Cancelling the custom loader would enable the SDK default loader.
        return new LoadOptions.ResourceLoadingResult([]) { LoadingCancelled = false };
    }

    /// <summary>Rethrows a captured budget, deadline or resource failure, or refuses an import that reached the network by default.</summary>
    internal void ThrowIfFailed()
    {
        _local.ThrowIfFailed();
        lock (_gate)
        {
            if (_refused is not null)
            {
                throw _refused;
            }

            if (!allowNetwork && _network.Count > 0)
            {
                throw new CliException(
                    ErrorCodes.FeatureUnsupported,
                    $"The PDF importer resolved {_network.Count} network resource(s), starting with '{_network[0]}', and may already have requested them; no PDF was written.",
                    hint: "Remove every network address from the HTML, or save the resources beside it and reference them by relative path.");
            }
        }
    }

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
                    warnings.Add(new Warning
                    {
                        Code = PdfDiagnostics.NetworkResourcesRequested,
                        Message = $"The HTML importer requested {_network.Count} network resource(s): {listed}{more}.",
                        Hint = "The output includes content fetched from these addresses. Review it, and never pass --allow-network-resources for HTML you do not trust.",
                    });
                }
            }

            return warnings;
        }
    }

    public void Dispose() => _local.Dispose();

    // By default a supplied stylesheet, SVG or HTML file must not name a network address or
    // run script: the importer would request what it names before this loader is consulted.
    private bool Checked(byte[] data, string resourceUri)
    {
        if (allowNetwork || NetworkReferenceGuard.IsSelfContained(data))
        {
            return true;
        }

        try
        {
            NetworkReferenceGuard.EnsureNone(data, "resource", resourceUri);
            return true;
        }
        catch (CliException refused)
        {
            lock (_gate)
            {
                _refused ??= refused;
            }

            return false;
        }
    }

    // Any absolute address but a local file, a share or inline data. Shares resolve as file
    // references, which the importer passes to this loader before reading them.
    private static bool IsNetwork(string reference) =>
        Uri.TryCreate(reference, UriKind.Absolute, out Uri? uri)
        && !uri.IsFile && !string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase);
}
