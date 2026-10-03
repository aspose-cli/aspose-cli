using Aspose.Cli.Sdk.Extensibility;

namespace Aspose.Cli.Product.Words.Contracts;

/// <summary>Deny-by-default Words format registry.</summary>
public static class WordsFormats
{
    /// <summary>Canonical immutable format declarations owned by this product.</summary>
    internal static readonly IReadOnlyList<FormatDescriptor> Definitions =
        FileFormatRecognition.AttachTo(
    [
        FormatDescriptor.Declare("doc", FormatUse.Input | FormatUse.Convert, 0, 0, null, true, ".doc"),
        FormatDescriptor.Declare("dot", FormatUse.Input | FormatUse.Convert, 1, 1, null, true, ".dot"),
        FormatDescriptor.Declare("docx", FormatUse.Input | FormatUse.Convert, 2, 2, null, true, ".docx"),
        FormatDescriptor.Declare("docm", FormatUse.Input | FormatUse.Convert, 3, 3, null, true, ".docm"),
        FormatDescriptor.Declare("dotx", FormatUse.Input | FormatUse.Convert, 4, 4, null, true, ".dotx"),
        FormatDescriptor.Declare("dotm", FormatUse.Input | FormatUse.Convert, 5, 5, null, true, ".dotm"),
        FormatDescriptor.Declare("flatopc", FormatUse.Input | FormatUse.Convert, 6, 6, null, false, ".xml"),
        FormatDescriptor.Declare("rtf", FormatUse.Input | FormatUse.Convert, 7, 7, null, true, ".rtf"),
        FormatDescriptor.Declare("wordml", FormatUse.Input | FormatUse.Convert, 8, 8, null, false, ".xml"),
        FormatDescriptor.Declare("html", FormatUse.Input | FormatUse.Convert, 9, 15, null, false, ".html", ".htm"),
        FormatDescriptor.Declare("mhtml", FormatUse.Input | FormatUse.Convert, 10, 17, null, false, ".mhtml"),
        FormatDescriptor.Declare("odt", FormatUse.Input | FormatUse.Convert, 11, 21, null, true, ".odt"),
        FormatDescriptor.Declare("ott", FormatUse.Input | FormatUse.Convert, 12, 22, null, true, ".ott"),
        FormatDescriptor.Declare("txt", FormatUse.Input | FormatUse.Convert, 13, 23, null, true, ".txt"),
        FormatDescriptor.Declare("md", FormatUse.Input | FormatUse.Convert, 14, 24, null, true, ".md"),
        FormatDescriptor.Declare("pdf", FormatUse.Input | FormatUse.Convert, 15, 9, null, false, ".pdf"),
        FormatDescriptor.Declare("epub", FormatUse.Input | FormatUse.Convert, 16, 18, null, true, ".epub"),
        FormatDescriptor.Declare("mobi", FormatUse.Input | FormatUse.Convert, 17, 19, null, true, ".mobi"),
        FormatDescriptor.Declare("azw3", FormatUse.Input | FormatUse.Convert, 18, 20, null, true, ".azw3"),
        FormatDescriptor.Declare("chm", FormatUse.Input, 19, null, null, true, ".chm"),
        FormatDescriptor.Declare("xps", FormatUse.Convert, null, 10, null, false, ".xps"),
        FormatDescriptor.Declare("openxps", FormatUse.Convert, null, 11, null, false, ".oxps"),
        FormatDescriptor.Declare("ps", FormatUse.Convert, null, 12, null, false, ".ps"),
        FormatDescriptor.Declare("pcl", FormatUse.Convert, null, 13, null, false, ".pcl"),
        FormatDescriptor.Declare("html-fixed", FormatUse.Convert, null, 16, null, false, ".html"),
        FormatDescriptor.Declare("png", FormatUse.Render, null, null, 0, false, ".png"),
        FormatDescriptor.Declare("jpeg", FormatUse.Render, null, null, 1, false, ".jpg", ".jpeg")
            with { Aliases = ["jpg"] },
        FormatDescriptor.Declare("svg", FormatUse.Render, null, null, 2, false, ".svg"),
    ], WordsFormatRecognition.Rules);

    public static IReadOnlyList<string> LoadIds { get; } =
        Definitions.IdsFor(FormatUse.Input);

    public static IReadOnlyList<string> ConvertIds { get; } =
        Definitions.IdsFor(FormatUse.Convert);

    public static IReadOnlyList<string> RenderIds { get; } =
        Definitions.IdsFor(FormatUse.Render);
    public static IReadOnlyList<string> FixedPageConvertIds { get; } = ["pdf", "xps", "openxps", "ps", "pcl"];
    public static IReadOnlyList<string> EncryptIds { get; } =
        ["doc", "dot", "docx", "docm", "dotx", "dotm", "flatopc", "odt", "ott"];

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

    public static bool IsLoad(string id) => LoadIds.Contains(id, StringComparer.Ordinal);

    /// <summary>
    /// The save format an output path selects. A source format that owns the extension is kept,
    /// so an in-place edit of a WordML <c>.xml</c> file stays WordML; otherwise the first
    /// convertible format declaring the extension wins (<c>.xml</c> is Flat OPC, <c>.html</c> is HTML).
    /// </summary>
    public static string ForOutput(string path, string? sourceFormatId = null)
    {
        string extension = Path.GetExtension(path);
        IReadOnlyList<FormatDescriptor> candidates = Definitions.WithExtension(FormatUse.Convert, extension);
        return candidates.FirstOrDefault(format => string.Equals(format.Id, sourceFormatId, StringComparison.Ordinal))?.Id
            ?? candidates.FirstOrDefault()?.Id
            ?? throw Sdk.Errors.CliErrors.FormatUnsupported(extension.TrimStart('.').ToLowerInvariant(), ConvertIds);
    }
}
