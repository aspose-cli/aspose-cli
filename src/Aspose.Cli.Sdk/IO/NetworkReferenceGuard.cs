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
/// fetch. Script is refused too, because an engine that runs it requests addresses the script
/// computes, and compressed content, which cannot be read. Only XML namespace names and
/// document type identifiers, which engines never request, are exempt.
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

    // A script element, including a prefixed SVG one; an event-handler attribute; or a script URL.
    private static readonly Regex Script = new(
        """
        <\s*(?:[a-z][a-z0-9_.\-]*:)?script(?![a-z0-9_.\-])
        |<[a-z][^<>]*?[\s/"']on[a-z]+\s*=
        |(?<![a-z0-9+.\-])(?:java|vb)script:
        """,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace,
        TimeSpan.FromSeconds(10));

    private static readonly Regex CssEscape = new(
        @"\\(?:([0-9a-f]{1,6})\s?|(.))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline,
        TimeSpan.FromSeconds(10));

    /// <summary>
    /// Throws <c>FEATURE_UNSUPPORTED</c> when markup such as HTML, Markdown, CSS or SVG names a
    /// network address, contains script, or is compressed.
    /// </summary>
    /// <param name="content">The markup's bytes.</param>
    /// <param name="kind">What the markup is, for the message.</param>
    /// <param name="path">The markup's file, for the message.</param>
    /// <param name="optIn">The caller's option that lets the engine fetch trusted input's
    /// addresses, named in the hint; null when the caller has none.</param>
    public static void EnsureNone(byte[] content, string kind, string path, string? optIn = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (IsCompressed(content))
        {
            throw CliErrors.ResourceRefused(
                $"The {kind} is compressed and cannot be checked for network addresses: {path}.",
                $"Decompress the {kind}.");
        }

        string text = Decode(content);
        string decoded = WebUtility.HtmlDecode(text);
        foreach (string markup in new[] { text, decoded, UnescapeCss(decoded) })
        {
            // URL parsers ignore tabs and line breaks inside an address; markup separates
            // attributes with them.
            string candidate = RemoveLineBreaks(markup);
            foreach (string scripted in new[] { markup, candidate })
            {
                Match script = Script.Match(scripted);
                if (script.Success)
                {
                    throw CliErrors.ResourceRefused(
                        $"The {kind} contains script ('{Excerpt(scripted, script.Index)}'), which the document engine runs and which can request network addresses the CLI cannot check: {path}.",
                        $"Remove script elements, event-handler attributes and script URLs from the {kind}.");
                }
            }

            string scanned = Identifiers.Replace(candidate, " ");
            Match match = NetworkReference.Match(scanned);
            if (match.Success)
            {
                throw CliErrors.ResourceRefused(
                    $"The {kind} names a network address ('{Address(scanned, match.Index)}'), which the document engine would request before the CLI's resource policy applies: {path}.",
                    $"Remove every network address from the {kind}, including hyperlinks and addresses in text, or save the resources beside it and reference them by relative path."
                        + (optIn is null ? string.Empty : $" If you trust the {kind}, {optIn} lets the engine fetch its addresses and lists every one."));
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
        if (IsCompressed(head.AsSpan(0, length)))
        {
            throw CliErrors.ResourceRefused(
                $"The image is compressed (SVGZ) and cannot be checked for network addresses: {path}.",
                "Decompress it to an .svg file, or supply a raster image.");
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

    /// <summary>
    /// Whether content is a raster image or a font, identified from its first bytes: formats
    /// that name no other resource, so an engine reading them requests nothing further.
    /// </summary>
    public static bool IsSelfContained(ReadOnlySpan<byte> head) =>
        head.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A])
        || head.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])
        || head.StartsWith("GIF87a"u8)
        || head.StartsWith("GIF89a"u8)
        || (head.StartsWith("BM"u8) && head.Length >= 10 && head[6..10].IndexOfAnyExcept((byte)0) < 0)
        || head.StartsWith((ReadOnlySpan<byte>)[(byte)'I', (byte)'I', 0x2A, 0x00])
        || head.StartsWith((ReadOnlySpan<byte>)[(byte)'M', (byte)'M', 0x00, 0x2A])
        || (head.StartsWith("RIFF"u8) && head.Length >= 12 && head[8..12].SequenceEqual("WEBP"u8))
        || head.StartsWith((ReadOnlySpan<byte>)[0x00, 0x00, 0x01, 0x00])
        || head.StartsWith((ReadOnlySpan<byte>)[0x00, 0x01, 0x00, 0x00])
        || head.StartsWith("OTTO"u8)
        || head.StartsWith("true"u8)
        || head.StartsWith("ttcf"u8)
        || head.StartsWith("wOFF"u8)
        || head.StartsWith("wOF2"u8);

    private static bool IsCompressed(ReadOnlySpan<byte> head) =>
        head.Length >= 2 && head[0] == 0x1F && head[1] == 0x8B;

    private static string Excerpt(string text, int index) =>
        text.Substring(index, Math.Min(80, text.Length - index));

    // The address alone: it ends where markup, text or a character outside ASCII begins. The
    // markup is decoded as Latin-1, so anything beyond ASCII would be shown garbled.
    private static string Address(string text, int index)
    {
        int end = index;
        while (end < text.Length && end - index < 200
            && text[end] is > ' ' and < '\u007F' and not ('"' or '\'' or '<' or '>' or '(' or ')' or '`'))
        {
            end++;
        }

        return text[index..end];
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
    // unmarked UTF-16.
    private static string Decode(byte[] content)
    {
        using var reader = new StreamReader(new MemoryStream(content, writable: false), Encoding.Latin1,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd().Replace("\0", "", StringComparison.Ordinal);
    }

    private static string RemoveLineBreaks(string text) =>
        text.Replace("\t", "", StringComparison.Ordinal)
            .Replace("\r", "", StringComparison.Ordinal)
            .Replace("\n", "", StringComparison.Ordinal);

    private static string UnescapeCss(string text) =>
        CssEscape.Replace(text, static match => match.Groups[1].Success
            ? int.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code)
              && code is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF)
                ? char.ConvertFromUtf32(code)
                : "\uFFFD"
            : match.Groups[2].Value);
}
