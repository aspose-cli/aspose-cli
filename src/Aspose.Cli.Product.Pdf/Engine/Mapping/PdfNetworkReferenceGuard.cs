using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// Refuses HTML or Markdown that names a network address before Aspose.PDF reads it.
/// Aspose.PDF.Drawing 26.8 requests http(s) resources before it consults
/// <c>HtmlLoadOptions.CustomLoaderOfExternalResources</c>, and its Markdown importer has no
/// resource hook at all (KNOWN-ISSUES.md, gate PDF-HTML-EGRESS), so no load option can stop
/// the request once the importer starts. The check is deliberately conservative: any network
/// address refuses the input, including a hyperlink or an address in plain text, because a
/// narrower HTML or CSS parser could disagree with the importer about what it will fetch. Only
/// XML namespace names and document type identifiers, which the importer never requests, are
/// exempt.
/// </summary>
internal static class PdfNetworkReferenceGuard
{
    // A scheme followed by two slashes or backslashes, except a local file:/// URL; a WHATWG
    // special network scheme, which parses as a network URL even without slashes; or a
    // scheme-relative or UNC reference.
    private static readonly Regex NetworkReference = new(
        """
        (?<![a-z0-9+.\-])(?:(?!file:[/\\]{3}(?![/\\]))[a-z][a-z0-9+.\-]+:[/\\]{2}|(?:https?|wss?|ftp):)
        |(?<![a-z0-9+.\-:/\\])[/\\]{2}(?=[^/\\\s])
        """,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace,
        TimeSpan.FromSeconds(10));

    // Namespace names and document type identifiers are names, not resources; the importer
    // requests neither. A document type with an internal subset is still scanned.
    private static readonly Regex Identifiers = new(
        """
        <!DOCTYPE[^>\[]*>
        |(?<![a-z0-9_.:\-])xmlns(?::[a-z0-9_.\-]+)?\s*=\s*(?:"[^"]*"|'[^']*')
        """,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace,
        TimeSpan.FromSeconds(10));

    private static readonly Regex CssEscape = new(
        @"\\(?:([0-9a-f]{1,6})\s?|(.))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline,
        TimeSpan.FromSeconds(10));

    /// <summary>Throws <c>FEATURE_UNSUPPORTED</c> when <paramref name="content"/> names a network address.</summary>
    internal static void Ensure(byte[] content, string kind, string path)
    {
        string text = Normalize(content);
        string decoded = WebUtility.HtmlDecode(text);
        foreach (string candidate in new[] { text, decoded, UnescapeCss(decoded) })
        {
            string scanned = Identifiers.Replace(candidate, " ");
            Match match = NetworkReference.Match(scanned);
            if (match.Success)
            {
                string excerpt = scanned.Substring(match.Index, Math.Min(80, scanned.Length - match.Index));
                throw new CliException(
                    ErrorCodes.FeatureUnsupported,
                    $"The {kind} input names a network address ('{excerpt}'), and the PDF importer requests network resources before the CLI can refuse them: {path}.",
                    hint: $"Remove every network address from the {kind}, including hyperlinks and addresses in text, or save the resources beside it and reference them by relative path.");
            }
        }
    }

    // Byte order marks select UTF-8, UTF-16 or UTF-32; otherwise Latin-1 keeps every ASCII
    // token of an ASCII-compatible encoding intact. Removing NUL exposes the ASCII tokens of
    // unmarked UTF-16, and URL parsers ignore tabs and line breaks inside an address.
    private static string Normalize(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content, writable: false), Encoding.Latin1,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd().Replace("\0", "", StringComparison.Ordinal)
            .Replace("\t", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal);
    }

    private static string UnescapeCss(string text) =>
        CssEscape.Replace(text, static match => match.Groups[1].Success
            ? int.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code)
              && code is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF)
                ? char.ConvertFromUtf32(code)
                : "\uFFFD"
            : match.Groups[2].Value);
}
