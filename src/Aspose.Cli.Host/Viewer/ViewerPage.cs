using System.Net;
using System.Text;
using System.Text.Json;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Host.Viewer;

/// <summary>
/// Composes pages of the shared document viewer: the kit, one product
/// presenter and the document the kit starts from. A static page carries its
/// document inline and loads parts from disk; a live page carries the state
/// of an open document and follows it over its event stream.
/// </summary>
internal static class ViewerPage
{
    private static readonly Lazy<string> KitScript = new(() => Read("viewer/kit.js"));
    private static readonly Lazy<string> KitStylesheet = new(() => Read("viewer/kit.css"));

    /// <summary>
    /// A self-contained page for a static bundle that works when opened from
    /// disk: every asset is inlined, the document travels as a JSON block and
    /// parts load from <paramref name="partBase"/> relative to the page.
    /// <paramref name="fallbackHtml"/> is what the page shows without scripts.
    /// </summary>
    public static string Static(
        string title,
        ViewPresentation presentation,
        string documentJson,
        string partBase,
        string fallbackHtml) =>
        Compose(
            title,
            presentation,
            documentJson,
            $"AsposeViewer.start({{ base: \"{JsonEncodedText.Encode(partBase)}\" }});",
            fallbackHtml,
            nonce: null);

    /// <summary>
    /// The page of one open document. It carries the state the viewer starts
    /// from and finds everything else under its own address: the revision
    /// files it serves and the event stream it follows.
    /// </summary>
    public static string Live(
        string fileName,
        ViewPresentation presentation,
        string documentJson,
        string scriptNonce)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptNonce);
        return Compose(
            fileName,
            presentation,
            $"{{\"live\":{documentJson}}}",
            "AsposeViewer.start({ live: true });",
            "<p>The live viewer needs JavaScript.</p>",
            scriptNonce);
    }

    private static string Compose(
        string title,
        ViewPresentation presentation,
        string documentJson,
        string bootstrap,
        string fallbackHtml,
        string? nonce)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        var page = new StringBuilder()
            .Append("<!doctype html>\n<html lang=\"en\"><head><meta charset=\"utf-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n")
            .Append("<meta name=\"color-scheme\" content=\"light dark\">\n")
            .Append("<title>").Append(WebUtility.HtmlEncode(title)).Append("</title>\n")
            .Append(Inline("style", KitStylesheet.Value, nonce: null));
        if (presentation.Stylesheet is { } stylesheet)
        {
            page.Append(Inline("style", stylesheet, nonce: null));
        }
        return page
            .Append("</head><body>\n<noscript>").Append(fallbackHtml).Append("</noscript>\n")
            // JSON can hold "<" only inside strings, where its escape keeps
            // "</script>" and "<!--" from ending the block early.
            .Append("<script type=\"application/json\" id=\"aspose-viewer-data\">")
            .Append(documentJson.Replace("<", "\\u003c", StringComparison.Ordinal))
            .Append("</script>\n")
            .Append(Inline("script", KitScript.Value, nonce))
            .Append(Inline("script", presentation.Script, nonce))
            .Append(Inline("script", bootstrap, nonce))
            .Append("</body></html>\n")
            .ToString();
    }

    private static string Inline(string tag, string content, string? nonce)
    {
        if (content.Contains("</" + tag, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"An inlined viewer asset must not contain '</{tag}'.");
        }
        string attributes = nonce is null ? string.Empty : $" nonce=\"{nonce}\"";
        return $"<{tag}{attributes}>\n{content}\n</{tag}>\n";
    }

    private static string Read(string name)
    {
        using Stream stream = typeof(ViewerPage).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing from the build.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
