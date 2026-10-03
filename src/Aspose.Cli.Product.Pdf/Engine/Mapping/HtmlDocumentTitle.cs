using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Aspose.Cli.Product.Pdf.Engine.Mapping;

/// <summary>
/// Reads the document title an HTML file states in its <c>title</c> element, which the
/// engine's HTML importer ignores (PDF-IMPORT-INFO-PLACEHOLDER).
/// </summary>
internal static partial class HtmlDocumentTitle
{
    /// <summary>The decoded, whitespace-collapsed title, or null when there is none or its charset is unknown.</summary>
    internal static string? Read(byte[] html)
    {
        if (Decode(html) is not { } text)
        {
            return null;
        }

        Match title = TitleElement().Match(text);
        if (!title.Success)
        {
            return null;
        }

        string value = Whitespace().Replace(WebUtility.HtmlDecode(title.Groups["text"].Value), " ").Trim();
        return value.Length == 0 ? null : value;
    }

    private static string? Decode(byte[] html)
    {
        // A byte order mark decides the encoding, as it does for a browser.
        if (html is [0xEF, 0xBB, 0xBF, ..] or [0xFF, 0xFE, ..] or [0xFE, 0xFF, ..])
        {
            using var reader = new StreamReader(new MemoryStream(html), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        string head = Encoding.Latin1.GetString(html, 0, Math.Min(html.Length, 4096));
        Match declared = Charset().Match(head);
        string name = declared.Success ? declared.Groups["name"].Value : "utf-8";
        Encoding? encoding = name.Equals("utf-8", StringComparison.OrdinalIgnoreCase) || name.Equals("utf8", StringComparison.OrdinalIgnoreCase)
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            : CodePagesEncodingProvider.Instance.GetEncoding(name) ?? Known(name);
        return encoding?.GetString(html);
    }

    private static Encoding? Known(string name)
    {
        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"<title\b[^>]*>(?<text>.*?)</title\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex TitleElement();

    [GeneratedRegex(@"<meta\b[^>]*charset\s*=\s*[""']?(?<name>[A-Za-z0-9_.:-]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Charset();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
