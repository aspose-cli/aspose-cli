using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Cli.Sdk.Errors;

namespace Aspose.Cli.Sdk.IO;

/// <summary>
/// Refuses markup that names a network address before a document engine reads it. It is for
/// engine paths that request network resources before, or without, any resource-loading hook
/// the CLI can install (see KNOWN-ISSUES.md); a path with a working hook uses
/// <see cref="LocalDocumentResourceLoader"/> instead. The check is deliberately conservative:
/// any network address refuses the input, including a hyperlink or an address in plain text,
/// because a narrower HTML, CSS or SVG parser could disagree with the engine about what it will
/// fetch. Only XML namespace names and document type identifiers, which engines never request,
/// are exempt.
/// </summary>
public static class NetworkReferenceGuard
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

    // Namespace names and document type identifiers are names, not resources. A document type
    // with an internal subset is still scanned.
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

    /// <summary>Throws <c>FEATURE_UNSUPPORTED</c> when markup such as HTML, Markdown or SVG names a network address.</summary>
    public static void EnsureNone(byte[] content, string kind, string path)
    {
        ArgumentNullException.ThrowIfNull(content);
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
                    $"The {kind} names a network address ('{excerpt}'), which the document engine would request before the CLI's resource policy applies: {path}.",
                    hint: $"Remove every network address from the {kind}, including hyperlinks and addresses in text, or save the resources beside it and reference them by relative path.");
            }
        }
    }

    /// <summary>
    /// Refuses an SVG image that names a network address, and a compressed SVG image, which
    /// cannot be checked. Raster images are identified from their first bytes and pass.
    /// </summary>
    public static void EnsureNoneInImage(Stream content, string path)
    {
        ArgumentNullException.ThrowIfNull(content);
        var head = new byte[1024];
        int length = content.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
        if (length >= 2 && head[0] == 0x1F && head[1] == 0x8B)
        {
            throw new CliException(
                ErrorCodes.FeatureUnsupported,
                $"The image is compressed (SVGZ) and cannot be checked for network addresses: {path}.",
                hint: "Decompress it to an .svg file, or supply a raster image.");
        }

        if (!IsMarkup(head.AsSpan(0, length)))
        {
            return;
        }

        using var markup = new MemoryStream();
        markup.Write(head, 0, length);
        content.CopyTo(markup);
        EnsureNone(markup.ToArray(), "SVG image", path);
    }

    /// <summary>Reads an image input in full and refuses it as <see cref="EnsureNoneInImage"/> does.</summary>
    public static byte[] ReadImage(InputResourceScope inputs, string path)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        byte[] image;
        using (var buffer = new MemoryStream())
        {
            inputs.OpenFile(path).CopyTo(buffer);
            image = buffer.ToArray();
        }

        EnsureNoneInImage(new MemoryStream(image, writable: false), path);
        return image;
    }

    // Markup starts with '<' after an optional byte order mark, whitespace and, for unmarked
    // UTF-16, NUL bytes. No raster image format starts that way.
    private static bool IsMarkup(ReadOnlySpan<byte> head)
    {
        foreach (byte value in head)
        {
            if (value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0x00 or 0xEF or 0xBB or 0xBF or 0xFE or 0xFF)
            {
                continue;
            }

            return value == (byte)'<';
        }

        return false;
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
