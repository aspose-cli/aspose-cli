using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Aspose.Cli.Sdk.IO.Input;

/// <summary>
/// Keeps an SVG to what it holds itself, for a document engine that reads what an SVG names past
/// any resource hook the CLI can install (see KNOWN-ISSUES.md). The SVG is parsed as XML with any
/// document type skipped, and only references inside it survive: a fragment (<c>#id</c>) and a base64
/// <c>data:</c> URI whose bytes begin as a PNG, JPEG or GIF file, whatever media type it declares.
/// Every other reference, in an attribute, in CSS or in a processing instruction, is removed, as
/// are <c>xml:base</c> and SMIL animation elements, whose values can set a reference. An element
/// that exists to show what a removed reference named, anything but a link (<c>a</c>), goes with
/// it, since an engine may fail on an image without one.
/// </summary>
public static class SvgReferences
{
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromSeconds(10);

    // A CSS url() or src() function with its quoted or unquoted argument.
    private static readonly Regex CssReference = new(
        """(?:url|src)\(\s*(?:"(?<value>[^"]*)"|'(?<value>[^']*)'|(?<value>[^)]*?))\s*\)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, PatternTimeout);

    private static readonly Regex CssImport = new(
        @"@import\b[^;]*;?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, PatternTimeout);

    // Attributes whose value is itself a reference.
    private static readonly HashSet<string> ReferenceAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "href", "src", "srcset", "data",
    };

    private static readonly XName XmlBase = XNamespace.Xml + "base";

    // The raster formats a data: URI may hold, by the bytes their files begin with.
    private static readonly byte[][] RasterSignatures =
    [
        [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A],
        [0xFF, 0xD8, 0xFF],
        "GIF87a"u8.ToArray(),
        "GIF89a"u8.ToArray(),
    ];

    /// <summary>An SVG that names nothing outside itself, and what was removed from it.</summary>
    /// <param name="Content">The SVG; the input's own bytes when nothing was removed.</param>
    /// <param name="Removed">Each reference or element removed, in document order.</param>
    public sealed record Cleaned(byte[] Content, IReadOnlyList<string> Removed);

    /// <summary>
    /// The SVG with every reference outside itself removed and without its document type, or null
    /// when it does not parse as XML once its document type is skipped.
    /// </summary>
    /// <param name="svg">The SVG's bytes.</param>
    public static Cleaned? KeepInternal(byte[] svg)
    {
        ArgumentNullException.ThrowIfNull(svg);
        // A document type, as editors export it, is skipped whole: no entity it declares is
        // defined, so content that uses one does not parse, and the output carries none.
        bool declared = false;
        XDocument? document = Load(svg, DtdProcessing.Prohibit);
        if (document is null)
        {
            document = Load(svg, DtdProcessing.Ignore);
            declared = document is not null;
        }

        if (document is null)
        {
            return null;
        }

        document.DocumentType?.Remove();
        var removed = new List<string>();
        // A processing instruction such as xml-stylesheet names a style sheet to fetch.
        foreach (XProcessingInstruction instruction in document.DescendantNodes().OfType<XProcessingInstruction>().ToList())
        {
            removed.Add($"<?{instruction.Target} {instruction.Data}?>");
            instruction.Remove();
        }

        foreach (XElement element in document.Descendants().ToList())
        {
            if (element.Document is null)
            {
                // Removed with an ancestor.
                continue;
            }
            if (element != document.Root && IsAnimation(element))
            {
                removed.Add($"<{element.Name.LocalName}>");
                element.Remove();
                continue;
            }

            CleanAttributes(document, element, removed);
            if (element.Document is not null && element.Name.LocalName.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                foreach (XText text in element.Nodes().OfType<XText>().ToList())
                {
                    string? css = CleanCss(text.Value, removed);
                    if (css is null)
                    {
                        text.Remove();
                    }
                    else if (!ReferenceEquals(css, text.Value))
                    {
                        text.Value = css;
                    }
                }
            }
        }

        if (removed.Count == 0 && !declared)
        {
            return new Cleaned(svg, []);
        }

        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
        {
            document.Save(writer);
        }
        return new Cleaned(output.ToArray(), removed);
    }

    // The SVG parsed with no resolver, or null when it does not parse.
    private static XDocument? Load(byte[] svg, DtdProcessing dtd)
    {
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = dtd, XmlResolver = null };
            using var reader = XmlReader.Create(new MemoryStream(svg, writable: false), settings);
            return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    // SMIL: set and the animate elements write attribute values, a reference among them.
    private static bool IsAnimation(XElement element) =>
        element.Name.LocalName.Equals("set", StringComparison.OrdinalIgnoreCase)
        || element.Name.LocalName.StartsWith("animate", StringComparison.OrdinalIgnoreCase);

    private static void CleanAttributes(XDocument document, XElement element, List<string> removed)
    {
        foreach (XAttribute attribute in element.Attributes().ToList())
        {
            if (attribute.IsNamespaceDeclaration)
            {
                continue;
            }

            if (attribute.Name == XmlBase)
            {
                removed.Add($"xml:base={attribute.Value}");
                attribute.Remove();
                continue;
            }

            if (ReferenceAttributes.Contains(attribute.Name.LocalName))
            {
                if (!IsInternal(attribute.Value))
                {
                    removed.Add(attribute.Value);
                    attribute.Remove();
                    // A link keeps its content; any other element shows what it named.
                    if (!element.Name.LocalName.Equals("a", StringComparison.OrdinalIgnoreCase) && element != document.Root)
                    {
                        element.Remove();
                        return;
                    }
                }
                continue;
            }

            string? css = CleanCss(attribute.Value, removed);
            if (css is null)
            {
                attribute.Remove();
            }
            else if (!ReferenceEquals(css, attribute.Value))
            {
                attribute.Value = css;
            }
        }
    }

    /// <summary>
    /// The CSS with every reference outside the SVG replaced by <c>none</c> and every import
    /// removed; the same string when it has neither, and null when it carries an escape or an
    /// <c>image-set()</c>, which could spell a reference this check does not see.
    /// </summary>
    private static string? CleanCss(string css, List<string> removed)
    {
        if (css.Contains('\\', StringComparison.Ordinal) || css.Contains("image-set(", StringComparison.OrdinalIgnoreCase))
        {
            removed.Add(css);
            return null;
        }

        int before = removed.Count;
        string cleaned = CssReference.Replace(css, match =>
        {
            if (IsInternal(match.Groups["value"].Value))
            {
                return match.Value;
            }
            removed.Add(match.Groups["value"].Value);
            return "none";
        });
        cleaned = CssImport.Replace(cleaned, match =>
        {
            removed.Add(match.Value);
            return string.Empty;
        });
        return removed.Count == before ? css : cleaned;
    }

    /// <summary>A fragment of this document, or a base64 data: URI that holds a PNG, JPEG or GIF file.</summary>
    private static bool IsInternal(string reference)
    {
        string value = reference.Trim();
        if (value.StartsWith('#'))
        {
            return true;
        }
        if (!value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int comma = value.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0 || !value[..comma].TrimEnd().EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // The first 12 base64 characters are the first 9 bytes, enough for every signature.
        var start = new StringBuilder(12);
        for (int index = comma + 1; index < value.Length && start.Length < 12; index++)
        {
            if (!char.IsWhiteSpace(value[index]))
            {
                start.Append(value[index]);
            }
        }
        Span<byte> head = stackalloc byte[9];
        if (start.Length < 12 || !Convert.TryFromBase64String(start.ToString(), head, out int written))
        {
            return false;
        }
        ReadOnlySpan<byte> bytes = head[..written];
        foreach (byte[] signature in RasterSignatures)
        {
            if (bytes.StartsWith(signature))
            {
                return true;
            }
        }
        return false;
    }
}
