using System.IO.Compression;
using System.Text;
using System.Xml;
using Aspose.Cli.Sdk.Errors;
using Aspose.Cli.Sdk.IO.Budgets;
using Aspose.Cli.Sdk.IO.Files;
using Aspose.Cli.Sdk.IO.Input;

namespace Aspose.Cli.Product.Cells.Engine.Mapping;

/// <summary>
/// Rebuilds an OpenDocument spreadsheet package so that its SVG pictures hold only themselves
/// before the engine reads it. Loading such a package, the engine opens what an SVG picture's
/// <c>&lt;image&gt;</c> elements name, by path, before any hook the CLI can install
/// (CELLS-SVG-EGRESS). The engine always reads the rebuilt package, never the input, and every
/// entry is judged by its content, wherever it sits: an entry whose document is an <c>svg</c>
/// is written as <see cref="SvgReferences"/> keeps it, without its document type, and so is an
/// SVG picture a part stores inline as <c>office:binary-data</c>; any other markup is copied
/// node by node without its document type. Whatever cannot be judged is left out: a compressed
/// SVG, an SVG that does not parse, and markup that does not parse far enough to tell what it
/// is, except the package's own parts, which the engine judges as it does any damaged file.
/// </summary>
internal static class OdsPackages
{
    private const string Phase = "ods-package";
    private const string OfficeNamespace = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";

    // The package's own parts, which stay as they are when they do not parse.
    private static readonly HashSet<string> OwnParts = new(StringComparer.Ordinal)
    {
        "mimetype", "content.xml", "styles.xml", "meta.xml", "settings.xml",
    };

    /// <summary>The rebuilt package and what it left behind.</summary>
    /// <param name="Package">The package the engine reads.</param>
    /// <param name="References">The references removed from SVG pictures.</param>
    /// <param name="Pictures">The pictures and entries left out because they could not be judged.</param>
    internal sealed record Cleaned(byte[] Package, int References, int Pictures);

    /// <summary>What a document is, as far as its first element tells.</summary>
    private enum Kind
    {
        /// <summary>Not markup, or markup whose first element is not svg.</summary>
        Other,

        /// <summary>Markup whose first element is svg.</summary>
        Svg,

        /// <summary>Markup that breaks off before its first element.</summary>
        Undecided,
    }

    /// <summary>The package at <paramref name="path"/>, rebuilt with its SVG pictures kept to themselves.</summary>
    /// <exception cref="InvalidDataException">One of the package's own parts is markup that breaks off.</exception>
    internal static Cleaned KeepSvgInternal(string path, ResourceBudgetLedger budgets)
    {
        var counts = new Counts();
        using FileStream file = InputFiles.OpenRead(path);
        using var package = new ZipArchive(file, ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var rebuilt = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (ZipArchiveEntry entry in package.Entries)
            {
                budgets.Deadline.ThrowIfExpired(Phase);
                if (entry.FullName.EndsWith('/'))
                {
                    continue;
                }

                byte[]? content = Rebuild(entry, budgets, counts);
                if (content is null)
                {
                    counts.Pictures++;
                    continue;
                }

                // The mimetype entry stays first and stored, as OpenDocument requires.
                ZipArchiveEntry copy = rebuilt.CreateEntry(entry.FullName,
                    entry.FullName == "mimetype" ? CompressionLevel.NoCompression : CompressionLevel.Fastest);
                using Stream target = copy.Open();
                target.Write(content);
            }
        }

        budgets.Consume(ResourceBudgetKinds.MemoryBufferBytes, output.Length, "bytes", Phase);
        return new Cleaned(output.ToArray(), counts.References, counts.Pictures);
    }

    private sealed class Counts
    {
        public int References { get; set; }

        public int Pictures { get; set; }
    }

    // What the rebuilt package holds for an entry; null to leave it out.
    private static byte[]? Rebuild(ZipArchiveEntry entry, ResourceBudgetLedger budgets, Counts counts)
    {
        byte[] content = Read(entry, budgets);
        bool own = OwnParts.Contains(entry.FullName) || entry.FullName.StartsWith("META-INF/", StringComparison.Ordinal);
        if (IsCompressed(content))
        {
            // A compressed SVG cannot be kept to itself where the engine would inflate it, and
            // compressed content that does not inflate cannot be judged.
            return Inflate(content, budgets) is { } inflated && Judge(inflated) == Kind.Other ? content : null;
        }

        switch (Judge(content))
        {
            case Kind.Svg:
                return Svg(content, counts);
            case Kind.Undecided:
                return own ? content : null;
        }

        if (!LooksLikeMarkup(content))
        {
            return content;
        }

        try
        {
            return CopyMarkup(content, budgets, counts);
        }
        catch (XmlException) when (!own)
        {
            return null;
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException($"The package part {entry.FullName} breaks off: {exception.Message}", exception);
        }
    }

    // An SVG as SvgReferences keeps it; null when it does not parse.
    private static byte[]? Svg(byte[] content, Counts counts)
    {
        if (SvgReferences.KeepInternal(content) is not { } kept)
        {
            return null;
        }

        counts.References += kept.Removed.Count;
        return kept.Content;
    }

    // What content is, read no further than its first element and never through its document type.
    private static Kind Judge(byte[] content)
    {
        if (!LooksLikeMarkup(content))
        {
            return Kind.Other;
        }

        try
        {
            using XmlReader reader = Reader(content);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    return reader.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase) ? Kind.Svg : Kind.Other;
                }
            }
        }
        catch (XmlException)
        {
        }

        return Kind.Undecided;
    }

    private static XmlReader Reader(byte[] content) => XmlReader.Create(new MemoryStream(content, writable: false), new XmlReaderSettings
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
    });

    /// <summary>
    /// The markup copied node by node, without a document type, with each SVG picture stored
    /// inline as <c>office:binary-data</c> kept to itself, and one that cannot be judged left out.
    /// </summary>
    private static byte[] CopyMarkup(byte[] content, ResourceBudgetLedger budgets, Counts counts)
    {
        using var output = new MemoryStream();
        using (XmlReader reader = Reader(content))
        using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false) }))
        {
            reader.Read();
            while (!reader.EOF)
            {
                budgets.Deadline.ThrowIfExpired(Phase);
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "binary-data" && reader.NamespaceURI == OfficeNamespace)
                {
                    string prefix = reader.Prefix;
                    string data = reader.ReadElementContentAsString();
                    if (BinaryData(data, budgets, counts) is { } kept)
                    {
                        writer.WriteStartElement(prefix, "binary-data", OfficeNamespace);
                        writer.WriteString(kept);
                        writer.WriteEndElement();
                    }

                    continue;
                }

                Copy(reader, writer);
                reader.Read();
            }
        }

        return output.ToArray();
    }

    // One node; an element's content follows node by node.
    private static void Copy(XmlReader reader, XmlWriter writer)
    {
        switch (reader.NodeType)
        {
            case XmlNodeType.Element:
                writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                writer.WriteAttributes(reader, defattr: false);
                if (reader.IsEmptyElement)
                {
                    writer.WriteEndElement();
                }
                break;
            case XmlNodeType.EndElement:
                writer.WriteFullEndElement();
                break;
            case XmlNodeType.Text:
                writer.WriteString(reader.Value);
                break;
            case XmlNodeType.Whitespace:
            case XmlNodeType.SignificantWhitespace:
                writer.WriteWhitespace(reader.Value);
                break;
            case XmlNodeType.CDATA:
                writer.WriteCData(reader.Value);
                break;
            case XmlNodeType.Comment:
                writer.WriteComment(reader.Value);
                break;
            case XmlNodeType.ProcessingInstruction:
                writer.WriteProcessingInstruction(reader.Name, reader.Value);
                break;
            // The XML declaration is the writer's own; a document type is never copied.
        }
    }

    // An inline picture's base64 data as the engine may read it; null to leave it out.
    private static string? BinaryData(string data, ResourceBudgetLedger budgets, Counts counts)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(data);
        }
        catch (FormatException)
        {
            counts.Pictures++;
            return null;
        }

        budgets.Consume(ResourceBudgetKinds.MemoryBufferBytes, bytes.Length, "bytes", Phase);
        Kind kind = !IsCompressed(bytes) ? Judge(bytes)
            : Inflate(bytes, budgets) is { } inflated && Judge(inflated) == Kind.Other ? Kind.Other : Kind.Undecided;
        if (kind == Kind.Other)
        {
            return data;
        }

        if (kind == Kind.Svg && Svg(bytes, counts) is { } kept)
        {
            return Convert.ToBase64String(kept);
        }

        counts.Pictures++;
        return null;
    }

    private static bool IsCompressed(byte[] content) => content.Length >= 2 && content[0] == 0x1F && content[1] == 0x8B;

    /// <summary>
    /// Whether content is markup, or may be: a byte order mark, UTF-16 without one, or a
    /// <c>&lt;</c> first after whitespace.
    /// </summary>
    private static bool LooksLikeMarkup(byte[] content)
    {
        ReadOnlySpan<byte> head = content.AsSpan(0, Math.Min(content.Length, 512));
        if (head.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) || head.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE])
            || head.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            return true;
        }

        if (head.Length >= 2 && (head[0] == 0 || head[1] == 0))
        {
            int first = head.IndexOfAnyExcept((byte)0);
            return first >= 0 && head[first] == (byte)'<';
        }

        foreach (byte value in head)
        {
            if (value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }

            return value == (byte)'<';
        }

        return false;
    }

    // A compressed entry inflated within the memory budget; null when it is not intact.
    private static byte[]? Inflate(byte[] content, ResourceBudgetLedger budgets)
    {
        try
        {
            using var inflated = new GZipStream(new MemoryStream(content, writable: false), CompressionMode.Decompress);
            return Bounded(inflated, budgets);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return null;
        }
    }

    private static byte[] Read(ZipArchiveEntry entry, ResourceBudgetLedger budgets)
    {
        using Stream content = entry.Open();
        return Bounded(content, budgets);
    }

    // A stream in full, within the memory budget however large it says it is.
    private static byte[] Bounded(Stream content, ResourceBudgetLedger budgets)
    {
        long limit = budgets.Remaining(ResourceBudgetKinds.MemoryBufferBytes);
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        int read;
        while ((read = content.Read(chunk)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                budgets.Consume(ResourceBudgetKinds.MemoryBufferBytes, buffer.Length + read, "bytes", Phase);
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
