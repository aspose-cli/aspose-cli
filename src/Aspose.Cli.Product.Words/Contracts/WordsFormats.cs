
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
    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
    [
        FormatDescriptor.Declare("doc", FormatUse.Input | FormatUse.Convert, ".doc") with { Protectable = true },
        FormatDescriptor.Declare("dot", FormatUse.Input | FormatUse.Convert, ".dot") with { Protectable = true },
        FormatDescriptor.Declare("docx", FormatUse.Input | FormatUse.Convert, ".docx") with { Protectable = true },
        FormatDescriptor.Declare("docm", FormatUse.Input | FormatUse.Convert, ".docm") with { Protectable = true },
        FormatDescriptor.Declare("dotx", FormatUse.Input | FormatUse.Convert, ".dotx") with { Protectable = true },
        FormatDescriptor.Declare("dotm", FormatUse.Input | FormatUse.Convert, ".dotm") with { Protectable = true },
        // Flat OPC is plain XML: the engine encrypts it only by writing an encrypted package, which
        // is no longer the .xml document the caller named, so it carries no password.
        FormatDescriptor.Declare("flatopc", FormatUse.Input | FormatUse.Convert, ".xml"),
        FormatDescriptor.Declare("rtf", FormatUse.Input | FormatUse.Convert, ".rtf"),
        // Word 2003 XML.
        FormatDescriptor.Declare("wordml", FormatUse.Input | FormatUse.Convert, ".xml"),
        FormatDescriptor.Declare("html", FormatUse.ConvertInput | FormatUse.Convert, ".html", ".htm") with { ConvertOrder = 1 },
        FormatDescriptor.Declare("mhtml", FormatUse.ConvertInput | FormatUse.Convert, ".mhtml") with { ConvertOrder = 2 },
        FormatDescriptor.Declare("odt", FormatUse.Input | FormatUse.Convert, ".odt") with { Protectable = true, ConvertOrder = 3 },
        FormatDescriptor.Declare("ott", FormatUse.Input | FormatUse.Convert, ".ott") with { Protectable = true, ConvertOrder = 3 },
        FormatDescriptor.Declare("txt", FormatUse.ConvertInput | FormatUse.Convert, ".txt") with { ConvertOrder = 3 },
        FormatDescriptor.Declare("md", FormatUse.ConvertInput | FormatUse.Convert, ".md", ".markdown") with { ConvertOrder = 3 },
        FormatDescriptor.Declare("pdf", FormatUse.ConvertInput | FormatUse.Convert, ".pdf"),
        FormatDescriptor.Declare("epub", FormatUse.ConvertInput | FormatUse.Convert, ".epub") with { ConvertOrder = 2 },
        FormatDescriptor.Declare("mobi", FormatUse.ConvertInput | FormatUse.Convert, ".mobi") with { ConvertOrder = 2 },
        FormatDescriptor.Declare("azw3", FormatUse.ConvertInput | FormatUse.Convert, ".azw3") with { ConvertOrder = 2 },
        FormatDescriptor.Declare("chm", FormatUse.ConvertInput, ".chm"),
        FormatDescriptor.Declare("xps", FormatUse.Convert, ".xps"),
        FormatDescriptor.Declare("openxps", FormatUse.Convert, ".oxps"),
        FormatDescriptor.Declare("ps", FormatUse.Convert, ".ps"),
        FormatDescriptor.Declare("pcl", FormatUse.Convert, ".pcl"),
        FormatDescriptor.Declare("html-fixed", FormatUse.Convert, ".html", ".htm") with { ConvertOrder = 1 },
        FormatDescriptor.Declare("png", FormatUse.Render, ".png") with { Raster = true },
        FormatDescriptor.Declare("jpeg", FormatUse.Render, ".jpg", ".jpeg") with { Raster = true },
        FormatDescriptor.Declare("svg", FormatUse.Render, ".svg"),
    ];

    /// <summary>
    /// The formats edit writes, and read, search, edit and render open, which every command that
    /// opens a document reads: the engine reads and saves the whole document in each.
    /// </summary>
    internal static IReadOnlyList<FormatDescriptor> Editable { get; } =
        [.. Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Input))];

    /// <summary>The formats convert writes, in the order its help lists them.</summary>
    internal static IReadOnlyList<FormatDescriptor> Writable { get; } =
        [.. Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Convert)).OrderBy(static format => format.ConvertOrder)];

    /// <summary>The formats whose output has pages, which --pages selects.</summary>
    internal static IReadOnlyList<string> FixedPageFormats { get; } = ["pdf", "xps", "openxps", "ps", "pcl", "html-fixed"];

    /// <summary>Whether edit writes <paramref name="id"/>.</summary>
    internal static bool IsEditable(string id) => Editable.Any(format => format.Id == id);

    /// <summary>Whether <paramref name="id"/> is plain text or Markdown, which convert reads with the options the caller stated.</summary>
    internal static bool IsText(string id) => IsPlainText(id) || IsMarkdown(id);

    /// <summary>Whether <paramref name="id"/> is plain text, which TxtLoadOptions read.</summary>
    internal static bool IsPlainText(string id) => id == "txt";

    /// <summary>Whether <paramref name="id"/> is Markdown, which MarkdownLoadOptions read and MarkdownSaveOptions write.</summary>
    internal static bool IsMarkdown(string id) => id == "md";
}
