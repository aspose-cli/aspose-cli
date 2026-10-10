using Aspose.Cli.Sdk.IO;

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
        FormatDescriptor.Declare("docx", FormatUse.Input | FormatUse.Convert, 2, 2, null, true, ".docx") with { Protectable = true },
        FormatDescriptor.Declare("docm", FormatUse.Input | FormatUse.Convert, 3, 3, null, true, ".docm") with { Protectable = true },
        FormatDescriptor.Declare("dotx", FormatUse.Input | FormatUse.Convert, 4, 4, null, true, ".dotx") with { Protectable = true },
        FormatDescriptor.Declare("dotm", FormatUse.Input | FormatUse.Convert, 5, 5, null, true, ".dotm") with { Protectable = true },
        // Flat OPC is plain XML: the engine encrypts it only by writing an encrypted package, which
        // is no longer the .xml document the caller named, so it carries no password.
        FormatDescriptor.Declare("flatopc", FormatUse.Input | FormatUse.Convert, 6, 6, null, false, ".xml"),
        FormatDescriptor.Declare("rtf", FormatUse.Input | FormatUse.Convert, 7, 7, null, true, ".rtf"),
        FormatDescriptor.Declare("wordml", FormatUse.Input | FormatUse.Convert, 8, 8, null, false, ".xml"),
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
        FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png"),
        FormatDescriptor.Declare("jpeg", FormatUse.Render, null, null, 1, false, ".jpg", ".jpeg")
            with { Aliases = ["jpg"] },
        FormatDescriptor.Declare("svg", FormatUse.Render, null, null, 2, false, ".svg"),
    ], WordsFormatRecognition.Rules);

    public static IReadOnlyList<string> LoadIds { get; } =
        Definitions.IdsFor(FormatUse.Input);

    public static IReadOnlyList<string> FixedPageFormats { get; } = ["pdf", "xps", "openxps", "ps", "pcl"];

    /// <summary>Microsoft Word formats, which keep fields, revisions and protection.</summary>
    public static IReadOnlyList<string> WordIds { get; } =
        ["doc", "dot", "docx", "docm", "dotx", "dotm", "flatopc", "wordml"];

    /// <summary>
    /// Formats that store tracked changes as revisions a reviewer can accept or reject: the
    /// Word formats, RTF and OpenDocument text. Other outputs show or drop the changes.
    /// </summary>
    public static IReadOnlyList<string> RevisionIds { get; } =
        [.. WordIds, "rtf", "odt", "ott"];

    /// <summary>Formats that keep a document's macros (its VBA project).</summary>
    public static IReadOnlyList<string> MacroIds { get; } = ["doc", "dot", "docm", "dotm", "wordml"];

    /// <summary>Whether a format is a Microsoft Word format (see <see cref="WordIds"/>).</summary>
    internal static bool IsWord(string id) => WordIds.Contains(id, StringComparer.Ordinal);

    /// <summary>Whether a format keeps tracked changes as revisions (see <see cref="RevisionIds"/>).</summary>
    internal static bool KeepsRevisions(string id) => RevisionIds.Contains(id, StringComparer.Ordinal);

    /// <summary>Whether a format keeps a document's macros (see <see cref="MacroIds"/>).</summary>
    internal static bool KeepsMacros(string id) => MacroIds.Contains(id, StringComparer.Ordinal);

    public static bool IsLoad(string id) => LoadIds.Contains(id, StringComparer.Ordinal);

    /// <summary>The formats create, edit and compare write: every convert format.</summary>
    internal static IReadOnlyList<FormatDescriptor> Writable { get; } =
        [.. Definitions.Where(static format => format.Uses.HasFlag(FormatUse.Convert)).OrderBy(static format => format.ConvertOrder)];
}
