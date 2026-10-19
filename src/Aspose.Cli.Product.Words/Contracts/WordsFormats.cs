
using Aspose.Cli.Sdk.Formats;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Deny-by-default Words format registry.</summary>
public static class WordsFormats
{
    /// <summary>Canonical immutable format declarations owned by this product.</summary>
    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
        FileFormatRecognition.AttachTo(
    [
        FormatDescriptor.Declare("doc", FormatUse.Input | FormatUse.Convert, 0, 0, null, true, ".doc") with { Protectable = true },
        FormatDescriptor.Declare("dot", FormatUse.Input | FormatUse.Convert, 1, 1, null, true, ".dot") with { Protectable = true },
        FormatDescriptor.Declare("docx", FormatUse.Input | FormatUse.Convert, 2, 2, null, true, ".docx") with { Protectable = true, MistakenFor = ["word"] },
        FormatDescriptor.Declare("docm", FormatUse.Input | FormatUse.Convert, 3, 3, null, true, ".docm") with { Protectable = true },
        FormatDescriptor.Declare("dotx", FormatUse.Input | FormatUse.Convert, 4, 4, null, true, ".dotx") with { Protectable = true },
        FormatDescriptor.Declare("dotm", FormatUse.Input | FormatUse.Convert, 5, 5, null, true, ".dotm") with { Protectable = true },
        // Flat OPC is plain XML: the engine encrypts it only by writing an encrypted package, which
        // is no longer the .xml document the caller named, so it carries no password.
        FormatDescriptor.Declare("flatopc", FormatUse.Input | FormatUse.Convert, 6, 6, null, true, ".xml") with { SharedExtensions = [".xml"] },
        FormatDescriptor.Declare("rtf", FormatUse.Input | FormatUse.Convert, 7, 7, null, true, ".rtf"),
        FormatDescriptor.Declare("wordml", FormatUse.Input | FormatUse.Convert, 8, 8, null, true, ".xml") with { SharedExtensions = [".xml"] },
        FormatDescriptor.Declare("html", FormatUse.Input | FormatUse.Convert, 9, 15, null, false, ".html", ".htm"),
        FormatDescriptor.Declare("mhtml", FormatUse.Input | FormatUse.Convert, 10, 17, null, false, ".mhtml"),
        FormatDescriptor.Declare("odt", FormatUse.Input | FormatUse.Convert, 11, 21, null, true, ".odt") with { Protectable = true },
        FormatDescriptor.Declare("ott", FormatUse.Input | FormatUse.Convert, 12, 22, null, true, ".ott") with { Protectable = true },
        FormatDescriptor.Declare("txt", FormatUse.Input | FormatUse.Convert, 13, 23, null, true, ".txt"),
        FormatDescriptor.Declare("md", FormatUse.Input | FormatUse.Convert, 14, 24, null, true, ".md", ".markdown"),
        FormatDescriptor.Declare("pdf", FormatUse.Input | FormatUse.Convert, 15, 9, null, false, ".pdf"),
        FormatDescriptor.Declare("epub", FormatUse.Input | FormatUse.Convert, 16, 18, null, true, ".epub"),
        FormatDescriptor.Declare("mobi", FormatUse.Input | FormatUse.Convert, 17, 19, null, true, ".mobi"),
        FormatDescriptor.Declare("azw3", FormatUse.Input | FormatUse.Convert, 18, 20, null, true, ".azw3"),
        FormatDescriptor.Declare("chm", FormatUse.Input, 19, null, null, true, ".chm"),
        FormatDescriptor.Declare("xps", FormatUse.Convert, null, 10, null, false, ".xps"),
        FormatDescriptor.Declare("openxps", FormatUse.Convert, null, 11, null, false, ".oxps"),
        FormatDescriptor.Declare("ps", FormatUse.Convert, null, 12, null, false, ".ps"),
        FormatDescriptor.Declare("pcl", FormatUse.Convert, null, 13, null, false, ".pcl"),
        FormatDescriptor.Declare("html-fixed", FormatUse.Convert, null, 16, null, false, ".html", ".htm"),
        FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png") with { Raster = true },
        FormatDescriptor.Declare("jpeg", FormatUse.Render, null, null, 1, false, ".jpg", ".jpeg") with { Raster = true },
        FormatDescriptor.Declare("svg", FormatUse.Render, null, null, 2, false, ".svg"),
    ], WordsFormatRecognition.Rules);

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
