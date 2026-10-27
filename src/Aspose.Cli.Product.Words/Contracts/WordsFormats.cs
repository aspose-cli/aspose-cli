
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
        FormatDescriptor.Declare("doc", FormatUse.Input | FormatUse.Convert, true, ".doc") with { Protectable = true, Recognition = WordsFormatRecognition.CompoundDocument },
        FormatDescriptor.Declare("dot", FormatUse.Input | FormatUse.Convert, true, ".dot") with { Protectable = true, Recognition = WordsFormatRecognition.CompoundDocument },
        FormatDescriptor.Declare("docx", FormatUse.Input | FormatUse.Convert, true, ".docx") with { Protectable = true, MistakenFor = ["word"], Recognition = WordsFormatRecognition.OpenXmlDocument },
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

    public static IReadOnlyList<string> LoadIds { get; } =
        Definitions.IdsFor(FormatUse.Input);

    /// <summary>
    /// What each declared format keeps and how the commands treat it, one row per format id in
    /// declaration order. A product test keeps the rows in step with <see cref="Definitions"/>.
    /// </summary>
    internal static IReadOnlyList<WordsFormatTraits> Traits { get; } =
    [
        new("doc") { Word = true, KeepsRevisions = true, KeepsMacros = true, Editable = true },
        new("dot") { Word = true, KeepsRevisions = true, KeepsMacros = true, Editable = true },
        new("docx") { Word = true, KeepsRevisions = true, Editable = true },
        new("docm") { Word = true, KeepsRevisions = true, KeepsMacros = true, Editable = true },
        new("dotx") { Word = true, KeepsRevisions = true, Editable = true },
        new("dotm") { Word = true, KeepsRevisions = true, KeepsMacros = true, Editable = true },
        new("flatopc") { Word = true, KeepsRevisions = true, Editable = true },
        new("rtf") { KeepsRevisions = true, Editable = true },
        new("wordml") { Word = true, KeepsRevisions = true, KeepsMacros = true, Editable = true },
        new("html"),
        new("mhtml"),
        new("odt") { KeepsRevisions = true, Editable = true },
        new("ott") { KeepsRevisions = true, Editable = true },
        new("txt"),
        new("md"),
        new("pdf") { FixedPage = true },
        new("epub"),
        new("mobi"),
        new("azw3"),
        new("chm"),
        new("xps") { FixedPage = true },
        new("openxps") { FixedPage = true },
        new("ps") { FixedPage = true },
        new("pcl") { FixedPage = true },
        new("html-fixed") { FixedPage = true },
        new("png"),
        new("jpeg"),
        new("svg"),
    ];

    private static readonly Dictionary<string, WordsFormatTraits> TraitsById =
        Traits.ToDictionary(static traits => traits.Id, StringComparer.Ordinal);

    /// <summary>The fixed-page convert formats, which can hold a selection of the pages.</summary>
    public static IReadOnlyList<string> FixedPageFormats { get; } = [.. Traits.Where(static traits => traits.FixedPage).Select(static traits => traits.Id)];

    /// <summary>Microsoft Word formats, which keep fields, revisions and protection.</summary>
    public static IReadOnlyList<string> WordIds { get; } = [.. Traits.Where(static traits => traits.Word).Select(static traits => traits.Id)];

    /// <summary>
    /// Formats that store tracked changes as revisions a reviewer can accept or reject: the
    /// Word formats, RTF and OpenDocument text. Other outputs show or drop the changes.
    /// </summary>
    public static IReadOnlyList<string> RevisionIds { get; } = [.. Traits.Where(static traits => traits.KeepsRevisions).Select(static traits => traits.Id)];

    /// <summary>Formats that keep a document's macros (its VBA project).</summary>
    public static IReadOnlyList<string> MacroIds { get; } = [.. Traits.Where(static traits => traits.KeepsMacros).Select(static traits => traits.Id)];

    /// <summary>Whether a format is a Microsoft Word format (see <see cref="WordIds"/>).</summary>
    internal static bool IsWord(string id) => Of(id)?.Word == true;

    /// <summary>Whether a format keeps tracked changes as revisions (see <see cref="RevisionIds"/>).</summary>
    internal static bool KeepsRevisions(string id) => Of(id)?.KeepsRevisions == true;

    /// <summary>Whether a format keeps a document's macros (see <see cref="MacroIds"/>).</summary>
    internal static bool KeepsMacros(string id) => Of(id)?.KeepsMacros == true;

    public static bool IsLoad(string id) => LoadIds.Contains(id, StringComparer.Ordinal);

    /// <summary>The formats compare writes its redline in: every convert format.</summary>
    internal static IReadOnlyList<FormatDescriptor> Writable { get; } =
        [.. Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Convert)).OrderBy(static format => format.ConvertOrder)];

    /// <summary>
    /// The formats create and edit write: those that reopen as the document they saved, with its
    /// styles, sections, headers, fields and tracked changes, so a later edit works on the same
    /// document: the Word formats, RTF and OpenDocument text (<see cref="WordsFormatTraits.Editable"/>).
    /// Every other convert format is a fixed-page, web, text or e-book rendition that loses part of
    /// the document; convert writes it from one of these.
    /// </summary>
    internal static IReadOnlyList<FormatDescriptor> Editable { get; } =
        [.. Writable.Where(static format => Of(format.Id)?.Editable == true)];

    /// <summary>The format split writes every part in: docx.</summary>
    internal static IReadOnlyList<FormatDescriptor> SplitParts { get; } = [.. Definitions.Where(static format => format.Id == "docx")];

    /// <summary>A CSV merge data file, whose header row names the columns.</summary>
    internal static FormatDescriptor CsvMergeData { get; } = new("csv", FormatUse.Input, ".csv");

    /// <summary>
    /// The files mail_merge and repeat_table_row read rows from: CSV, or else JSON, an array of
    /// flat objects.
    /// </summary>
    internal static IReadOnlyList<FormatDescriptor> MergeData { get; } = [CsvMergeData, new("json", FormatUse.Input, ".json")];

    /// <summary>The traits of a format id, or null when the product declares no such format.</summary>
    private static WordsFormatTraits? Of(string id) => TraitsById.GetValueOrDefault(id);
}
