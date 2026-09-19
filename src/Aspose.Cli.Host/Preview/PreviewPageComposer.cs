using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace Aspose.Cli.Host.Preview;

/// <summary>
/// The session-fixed inputs of the page shell that
/// <see cref="PreviewPageComposer"/> weaves around every served page: the
/// metadata published to the live client plus whether the shell stylesheet is
/// served. One instance describes a whole session; only the revision varies
/// between composed pages.
/// </summary>
/// <param name="View">
/// The view identifier published in the page metadata (e.g. <c>html</c>).
/// A plain token, embedded in JSON without escaping.
/// </param>
/// <param name="DocumentName">
/// File name of the watched document, published in the page metadata; null
/// publishes <c>"file":null</c>.
/// </param>
/// <param name="EvalMode">
/// Whether the engine runs under an evaluation license; published in the
/// page metadata so clients can surface the watermark state.
/// </param>
/// <param name="StylesheetAvailable">
/// Whether <c>/live/shell.css</c> is being served, and therefore whether
/// composed pages link it.
/// </param>
/// <param name="ScriptNonce">
/// Per-session CSP nonce for the Host-owned inline metadata bootstrap.
/// </param>
/// <param name="DocumentPath">
/// Same-origin route from which the live client refreshes the document.
/// </param>
internal sealed record PreviewPageShell(
    string View,
    string? DocumentName,
    bool EvalMode,
    bool StylesheetAvailable,
    string ScriptNonce,
    string DocumentPath);

/// <summary>
/// Composes the HTML pages the preview server serves: it injects the
/// live-client bootstrap into rendered documents: the shell stylesheet link
/// (when one is served), one inline script publishing the page metadata as
/// <c>window.__asposePreview</c> (whose revision lets the client detect it is
/// stale against the <c>hello</c> event) and one reference to
/// <c>/live/client.js</c>.
/// </summary>
internal static class PreviewPageComposer
{
    /// <summary>
    /// Injects the live-client bootstrap before the last closing body tag,
    /// matched case-insensitively; a document without one gets the bootstrap
    /// appended at the end. The rendered HTML is engine output and is never
    /// parsed here; plain string surgery keeps every other byte intact.
    /// </summary>
    /// <param name="html">The rendered document.</param>
    /// <param name="revision">The snapshot revision the document belongs to.</param>
    /// <param name="shell">The session-fixed page-shell inputs.</param>
    public static string InjectLiveClient(string html, int revision, PreviewPageShell shell)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(shell);

        html = RemoveEmbeddedContentSecurityPolicy(html);
        string bootstrap = ComposeBootstrap(revision, shell);
        int index = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return index < 0 ? html + bootstrap : html.Insert(index, bootstrap);
    }

    /// <summary>
    /// Composes the bootstrap fragment injected into every served page, in a
    /// fixed order: the stylesheet link (when one is served), the metadata
    /// script and the client-script reference. The metadata is hand-built
    /// JSON with a fixed key order. Every string is JavaScript-encoded so a
    /// file name or mounted route containing markup, quotes, or non-ASCII
    /// characters can never break out of the script element.
    /// </summary>
    private static string ComposeBootstrap(int revision, PreviewPageShell shell)
    {
        var bootstrap = new StringBuilder();
        if (shell.StylesheetAvailable)
        {
            bootstrap.Append("<link rel=\"stylesheet\" href=\"/live/shell.css\">");
        }

        bootstrap.Append("<script nonce=\"")
            .Append(HtmlEncoder.Default.Encode(shell.ScriptNonce))
            .Append("\">window.__asposePreview={\"revision\":");
        bootstrap.Append(revision.ToString(CultureInfo.InvariantCulture));
        bootstrap.Append(",\"view\":\"").Append(shell.View).Append('"');
        bootstrap.Append(",\"file\":");
        if (shell.DocumentName is { } documentName)
        {
            bootstrap.Append('"').Append(JavaScriptEncoder.Default.Encode(documentName)).Append('"');
        }
        else
        {
            bootstrap.Append("null");
        }

        bootstrap.Append(",\"eval\":").Append(shell.EvalMode ? "true" : "false");
        bootstrap.Append(",\"documentPath\":\"")
            .Append(JavaScriptEncoder.Default.Encode(shell.DocumentPath))
            .Append('"');
        bootstrap.Append("};</script><script nonce=\"")
            .Append(HtmlEncoder.Default.Encode(shell.ScriptNonce))
            .Append("\" src=\"/live/client.js\" defer></script>");
        return bootstrap.ToString();
    }

    private static string RemoveEmbeddedContentSecurityPolicy(string html) =>
        Regex.Replace(
            html,
            "<meta\\s+[^>]*http-equiv\\s*=\\s*(?:\\\"Content-Security-Policy\\\"|'Content-Security-Policy'|Content-Security-Policy)[^>]*>",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
}
