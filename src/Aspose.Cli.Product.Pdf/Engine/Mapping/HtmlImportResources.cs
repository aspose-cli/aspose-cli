using System.Text.Json.Nodes;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Input;
using Aspose.Pdf;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// The resources of an HTML page the engine imports, through its
/// <see cref="HtmlLoadOptions.CustomLoaderOfExternalResources"/>: ordinary files beneath the
/// page's folder are supplied, inline data: addresses get the bytes they hold, and every other
/// reference gets nothing (SECURITY.md); it keeps the references it supplied nothing for. The
/// importer requests some http(s) resources itself as well, which the process's network gate
/// blocks and lists (KNOWN-ISSUES PDF-HTML-EGRESS).
/// </summary>
internal sealed class HtmlImportResources(string htmlPath, ResourceBudgetLedger budgets) : IDisposable
{
    private readonly LocalDocumentResourceLoader _local = new(htmlPath, budgets);
    private readonly List<string> _network = [];

    /// <summary>The native directory form of the page's folder, with its trailing separator.</summary>
    internal string BaseDirectory => new Uri(_local.BaseUri).LocalPath;

    internal LoadOptions.ResourceLoadingResult Load(string resourceUri) =>
        // Inline data, such as a font the engine's own HTML export embeds, is in the page itself:
        // its bytes are supplied as the address states them, with no file and no network read.
        IsInline(resourceUri) ? new LoadOptions.ResourceLoadingResult(Inline(resourceUri))
        // A network address is the gate's, which blocked the request and lists it. Cancelling the
        // loader would let the engine's own loader read the resource, so nothing is supplied instead.
        : IsNetwork(resourceUri) ? Network(resourceUri)
        : _local.TryRead(resourceUri, out byte[] data)
            ? new LoadOptions.ResourceLoadingResult(data)
            : new LoadOptions.ResourceLoadingResult([]) { LoadingCancelled = false };

    private LoadOptions.ResourceLoadingResult Network(string resourceUri)
    {
        lock (_network)
        {
            _network.Add(resourceUri);
        }

        return new LoadOptions.ResourceLoadingResult([]) { LoadingCancelled = false };
    }

    /// <summary>
    /// The references the loader supplied no bytes for, each with why, once each: network
    /// addresses, and local references the local loader left out. Null when there are none.
    /// </summary>
    internal JsonObject? NotSupplied()
    {
        string[] network;
        lock (_network)
        {
            network = [.. _network];
        }

        OmittedResource[] all =
        [
            .. network.Select(static reference => new OmittedResource(reference, "a network address")),
            .. _local.Omitted,
        ];
        OmittedResource[] distinct = [.. all.DistinctBy(static item => item.Reference, StringComparer.Ordinal)];
        return distinct.Length == 0
            ? null
            : new JsonObject
            {
                ["notSupplied"] = new JsonArray([.. distinct.Select(static item => (JsonNode)new JsonObject
                {
                    ["reference"] = item.Reference,
                    ["reason"] = item.Reason,
                })]),
            };
    }

    private static bool IsInline(string reference) =>
        reference.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && reference.Contains(',', StringComparison.Ordinal);

    /// <summary>The bytes a <c>data:</c> address holds: base64 when its header says so, else percent-encoded text.</summary>
    private static byte[] Inline(string reference)
    {
        int comma = reference.IndexOf(',', StringComparison.Ordinal);
        string header = reference[..comma];
        string payload = reference[(comma + 1)..];
        if (!header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
        }

        try
        {
            return Convert.FromBase64String(Uri.UnescapeDataString(payload));
        }
        catch (FormatException)
        {
            return [];
        }
    }

    // Any absolute address but a local file, a share or inline data.
    private static bool IsNetwork(string reference) =>
        Uri.TryCreate(reference, UriKind.Absolute, out Uri? uri)
        && !uri.IsFile && !string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase);

    /// <summary>Rethrows a captured budget, cancellation or resource failure.</summary>
    internal void ThrowIfFailed() => _local.ThrowIfFailed();

    /// <summary>The warning that names the resources left out, after any failure.</summary>
    internal IReadOnlyList<Warning> Warnings
    {
        get
        {
            ThrowIfFailed();
            return _local.Warning is { } omitted ? [omitted] : [];
        }
    }

    public void Dispose() => _local.Dispose();
}
