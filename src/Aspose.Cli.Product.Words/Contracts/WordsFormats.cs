
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Deny-by-default Words format registry.</summary>
public static class WordsFormats
{
    /// <summary>
    /// Canonical immutable format declarations owned by this product. Convert lists the Word
    /// formats and the fixed-page outputs first, then the web pages, the e-books and the
    /// OpenDocument and text formats: <see cref="FormatDescriptor.ConvertOrder"/> ranks those
    /// groups, each in declaration order.
    /// </summary>
    private static readonly IReadOnlyList<string> EditableIds = ["doc", "dot", "docx", "docm", "dotx", "dotm", "flatopc", "rtf", "wordml", "odt", "ott"];

    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
    [
        FormatDescriptor.Declare("doc", FormatUse.Input | FormatUse.Convert, true, ".doc") with { Protectable = true, Recognition = WordsFormatRecognition.CompoundDocument },
        FormatDescriptor.Declare("dot", FormatUse.Input | FormatUse.Convert, true, ".dot") with { Protectable = true, Recognition = WordsFormatRecognition.CompoundDocument },
        FormatDescriptor.Declare("docx", FormatUse.Input | FormatUse.Convert, true, ".docx") with { Protectable = true, Recognition = WordsFormatRecognition.OpenXmlDocument },
        FormatDescriptor.Declare("docm", FormatUse.Input | FormatUse.Convert, true, ".docm") with { Protectable = true, Recognition = WordsFormatRecognition.OpenXmlDocument },
        FormatDescriptor.Declare("dotx", FormatUse.Input | FormatUse.Convert, true, ".dotx") with { Protectable = true, Recognition = WordsFormatRecognition.OpenXmlDocument },
        FormatDescriptor.Declare("dotm", FormatUse.Input | FormatUse.Convert, true, ".dotm") with { Protectable = true, Recognition = WordsFormatRecognition.OpenXmlDocument },
        // Flat OPC is plain XML: the engine encrypts it only by writing an encrypted package, which
        // is no longer the .xml document the caller named, so it carries no password.
        FormatDescriptor.Declare("flatopc", FormatUse.Input | FormatUse.Convert, true, ".xml") with
        {
            SharedExtensions = [".xml"],
            Recognition = FileFormatRecognition.Match(
                FileProbePattern.All(
                    FileProbePattern.TextStartsIgnoringBomAndWhitespace("<"),
                    FileProbePattern.TextContains("schemas.microsoft.com/office/2006/xmlPackage"),
                    FileProbePattern.Any(
                        FileProbePattern.TextContains("progid=\"Word.Document\""),
                        FileProbePattern.TextContains("pkg:name=\"/word/"))),
                "flat OPC word-processing package"),
        },
        FormatDescriptor.Declare("rtf", FormatUse.Input | FormatUse.Convert, true, ".rtf") with
        {
            Recognition = FileFormatRecognition.Match(FileProbePattern.TextStartsIgnoringBomAndWhitespace(@"{\rtf"), "RTF header"),
        },
        // Word 2003 XML.
        FormatDescriptor.Declare("wordml", FormatUse.Input | FormatUse.Convert, true, ".xml") with
        {
            SharedExtensions = [".xml"],
            Recognition = FileFormatRecognition.Match(
                FileProbePattern.All(
                    FileProbePattern.TextStartsIgnoringBomAndWhitespace("<"),
                    FileProbePattern.TextContains("schemas.microsoft.com/office/word/2003/wordml")),
                "Word 2003 XML namespace"),
        },
        FormatDescriptor.Declare("html", FormatUse.Input | FormatUse.Convert, false, ".html", ".htm") with { ConvertOrder = 1 },
        FormatDescriptor.Declare("mhtml", FormatUse.Input | FormatUse.Convert, false, ".mhtml") with { ConvertOrder = 2 },
        FormatDescriptor.Declare("odt", FormatUse.Input | FormatUse.Convert, true, ".odt") with { Protectable = true, ConvertOrder = 3, Recognition = WordsFormatRecognition.OpenDocumentText },
        FormatDescriptor.Declare("ott", FormatUse.Input | FormatUse.Convert, true, ".ott") with { Protectable = true, ConvertOrder = 3, Recognition = WordsFormatRecognition.OpenDocumentText },
        FormatDescriptor.Declare("txt", FormatUse.Input | FormatUse.Convert, true, ".txt") with { ConvertOrder = 3, Recognition = WordsFormatRecognition.PlainText },
        FormatDescriptor.Declare("md", FormatUse.Input | FormatUse.Convert, true, ".md", ".markdown") with { ConvertOrder = 3, Recognition = WordsFormatRecognition.PlainText },
        FormatDescriptor.Declare("pdf", FormatUse.Input | FormatUse.Convert, false, ".pdf"),
        FormatDescriptor.Declare("epub", FormatUse.Input | FormatUse.Convert, true, ".epub") with
        {
            ConvertOrder = 2,
            Recognition = FileFormatRecognition.Match(
                FileProbePattern.ZipContainsAny("application/epub+zip", "META-INF/container.xml"),
                "EPUB container marker"),
        },
        FormatDescriptor.Declare("mobi", FormatUse.Input | FormatUse.Convert, true, ".mobi") with { ConvertOrder = 2, Recognition = WordsFormatRecognition.Mobipocket },
        FormatDescriptor.Declare("azw3", FormatUse.Input | FormatUse.Convert, true, ".azw3") with { ConvertOrder = 2, Recognition = WordsFormatRecognition.Mobipocket },
        FormatDescriptor.Declare("chm", FormatUse.Input, true, ".chm") with
        {
            Recognition = FileFormatRecognition.Match(FileProbePattern.AsciiBytesAt(0, "ITSF"), "CHM signature"),
        },
        FormatDescriptor.Declare("xps", FormatUse.Convert, false, ".xps"),
        FormatDescriptor.Declare("openxps", FormatUse.Convert, false, ".oxps"),
        FormatDescriptor.Declare("ps", FormatUse.Convert, false, ".ps"),
        FormatDescriptor.Declare("pcl", FormatUse.Convert, false, ".pcl"),
        FormatDescriptor.Declare("html-fixed", FormatUse.Convert, false, ".html", ".htm") with { ConvertOrder = 1 },
        FormatDescriptor.Declare("png", FormatUse.Render, false, ".png") with { Raster = true },
        FormatDescriptor.Declare("jpeg", FormatUse.Render, false, ".jpg", ".jpeg") with { Raster = true },
        FormatDescriptor.Declare("svg", FormatUse.Render, false, ".svg"),
    ];

    /// <summary>The formats edit writes, and read, search, edit and render open: the engine reads and saves the whole document in each.</summary>
    internal static IReadOnlyList<FormatDescriptor> Editable { get; } =
        [.. Definitions.Where(static format => EditableIds.Contains(format.Id, StringComparer.Ordinal))];

    /// <summary>The formats convert writes, in the order its help lists them.</summary>
    internal static IReadOnlyList<FormatDescriptor> Writable { get; } =
        [.. Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Convert)).OrderBy(static format => format.ConvertOrder)];

    /// <summary>The formats whose output has pages, which --pages selects.</summary>
    internal static IReadOnlyList<string> FixedPageFormats { get; } = ["pdf", "xps", "openxps", "ps", "pcl", "html-fixed"];

    /// <summary>Whether edit writes <paramref name="id"/>.</summary>
    internal static bool IsEditable(string id) => EditableIds.Contains(id, StringComparer.Ordinal);

    /// <summary>Whether <paramref name="id"/> is plain text or Markdown, which convert reads with the options the caller stated.</summary>
    internal static bool IsText(string id) => IsPlainText(id) || IsMarkdown(id);

    /// <summary>Whether <paramref name="id"/> is plain text, which TxtLoadOptions read.</summary>
    internal static bool IsPlainText(string id) => id == "txt";

    /// <summary>Whether <paramref name="id"/> is Markdown, which MarkdownLoadOptions read and MarkdownSaveOptions write.</summary>
    internal static bool IsMarkdown(string id) => id == "md";
}
