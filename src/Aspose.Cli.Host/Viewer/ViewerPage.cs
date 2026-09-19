using System.Net;
using System.Text;
using System.Text.Json;
using Aspose.Cli.Sdk.Views;

namespace Aspose.Cli.Host.Viewer;

/// <summary>
/// Composes pages of the shared document viewer: the kit, one product
/// presenter and the document the kit starts from.
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
        string fallbackHtml)
    {
        var page = new StringBuilder()
            .Append("<!doctype html>\n<html lang=\"en\"><head><meta charset=\"utf-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n")
            .Append("<meta name=\"color-scheme\" content=\"light dark\">\n")
            .Append("<title>").Append(WebUtility.HtmlEncode(title)).Append("</title>\n")
            .Append(Inline("style", KitStylesheet.Value));
        if (presentation.Stylesheet is { } stylesheet)
        {
            page.Append(Inline("style", stylesheet));
        }
        return page
            .Append("</head><body>\n<noscript>").Append(fallbackHtml).Append("</noscript>\n")
            // JSON can hold "<" only inside strings, where its escape keeps
            // "</script>" and "<!--" from ending the block early.
            .Append("<script type=\"application/json\" id=\"aspose-viewer-data\">")
            .Append(documentJson.Replace("<", "\\u003c", StringComparison.Ordinal))
            .Append("</script>\n")
            .Append(Inline("script", KitScript.Value))
            .Append(Inline("script", presentation.Script))
            .Append("<script>AsposeViewer.start({ base: \"")
            .Append(JsonEncodedText.Encode(partBase))
            .Append("\" });</script>\n</body></html>\n")
            .ToString();
    }

    private static string Inline(string tag, string content)
    {
        if (content.Contains("</" + tag, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"An inlined viewer asset must not contain '</{tag}'.");
        }
        return $"<{tag}>\n{content}\n</{tag}>\n";
    }

    private static string Read(string name)
    {
        using Stream stream = typeof(ViewerPage).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing from the build.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
