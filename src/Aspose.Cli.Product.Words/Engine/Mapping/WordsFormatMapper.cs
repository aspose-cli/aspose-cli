using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>The one table that maps each product format id to how Aspose.Words loads and saves it.</summary>
internal static class WordsFormatMapper
{
    // One row per format id: the engine format that loads it and the one that saves it, if any.
    private static readonly (string Id, LoadFormat? Load, SaveFormat? Save)[] Table =
    [
        ("doc", LoadFormat.Doc, SaveFormat.Doc),
        ("dot", LoadFormat.Dot, SaveFormat.Dot),
        ("docx", LoadFormat.Docx, SaveFormat.Docx),
        ("docm", LoadFormat.Docm, SaveFormat.Docm),
        ("dotx", LoadFormat.Dotx, SaveFormat.Dotx),
        ("dotm", LoadFormat.Dotm, SaveFormat.Dotm),
        ("flatopc", LoadFormat.FlatOpc, SaveFormat.FlatOpc),
        ("rtf", LoadFormat.Rtf, SaveFormat.Rtf),
        ("wordml", LoadFormat.WordML, SaveFormat.WordML),
        ("html", LoadFormat.Html, SaveFormat.Html),
        ("mhtml", LoadFormat.Mhtml, SaveFormat.Mhtml),
        ("odt", LoadFormat.Odt, SaveFormat.Odt),
        ("ott", LoadFormat.Ott, SaveFormat.Ott),
        ("txt", LoadFormat.Text, SaveFormat.Text),
        ("md", LoadFormat.Markdown, SaveFormat.Markdown),
        ("pdf", LoadFormat.Pdf, SaveFormat.Pdf),
        ("epub", LoadFormat.Epub, SaveFormat.Epub),
        ("mobi", LoadFormat.Mobi, SaveFormat.Mobi),
        ("azw3", LoadFormat.Azw3, SaveFormat.Azw3),
        ("chm", LoadFormat.Chm, null),
        ("xps", null, SaveFormat.Xps),
        ("openxps", null, SaveFormat.OpenXps),
        ("ps", null, SaveFormat.Ps),
        ("pcl", null, SaveFormat.Pcl),
        ("html-fixed", null, SaveFormat.HtmlFixed),
        ("png", null, SaveFormat.Png),
        ("jpeg", null, SaveFormat.Jpeg),
        ("svg", null, SaveFormat.Svg),
    ];

    private static readonly IReadOnlyDictionary<LoadFormat, string> LoadIds =
        Table.Where(static row => row.Load is not null).ToDictionary(static row => row.Load!.Value, static row => row.Id);

    private static readonly IReadOnlyDictionary<string, SaveFormat> SaveFormats =
        Table.Where(static row => row.Save is not null)
            .ToDictionary(static row => row.Id, static row => row.Save!.Value, StringComparer.Ordinal);

    /// <summary>The format id of a detected load format, or <c>unknown</c>.</summary>
    public static string ToId(LoadFormat format) =>
        LoadIds.TryGetValue(format, out string? id) ? id : "unknown";

    /// <summary>Whether the engine loads documents of a format id.</summary>
    public static bool Loads(string id) => LoadIds.Values.Contains(id, StringComparer.Ordinal);

    /// <summary>Whether the engine saves documents in a format id.</summary>
    public static bool Saves(string id) => SaveFormats.ContainsKey(id);

    /// <summary>The engine format that saves a format id.</summary>
    public static SaveFormat ToSaveFormat(string id) =>
        SaveFormats.TryGetValue(id, out SaveFormat format)
            ? format
            : throw new InvalidOperationException($"'{id}' has no document save format.");
}
