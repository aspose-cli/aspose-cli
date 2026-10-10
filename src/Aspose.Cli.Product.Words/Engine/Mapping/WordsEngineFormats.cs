using Aspose.Words;

namespace Aspose.Cli.Product.Words.Engine.Mapping;

/// <summary>
/// The engine side of each public format id, in one table: the format Aspose.Words loads it as
/// and the one it saves it in. A product test keeps it in step with <c>WordsFormats</c>, so a
/// declared format the engine cannot read or write fails the build, not the user.
/// </summary>
internal static class WordsEngineFormats
{
    /// <summary>Every format id the engine maps, with what it maps to.</summary>
    internal static IReadOnlyList<WordsEngineFormat> All { get; } =
    [
        new("doc") { Load = LoadFormat.Doc, Save = SaveFormat.Doc },
        new("dot") { Load = LoadFormat.Dot, Save = SaveFormat.Dot },
        new("docx") { Load = LoadFormat.Docx, Save = SaveFormat.Docx },
        new("docm") { Load = LoadFormat.Docm, Save = SaveFormat.Docm },
        new("dotx") { Load = LoadFormat.Dotx, Save = SaveFormat.Dotx },
        new("dotm") { Load = LoadFormat.Dotm, Save = SaveFormat.Dotm },
        new("flatopc") { Load = LoadFormat.FlatOpc, Save = SaveFormat.FlatOpc },
        new("rtf") { Load = LoadFormat.Rtf, Save = SaveFormat.Rtf },
        new("wordml") { Load = LoadFormat.WordML, Save = SaveFormat.WordML },
        new("html") { Load = LoadFormat.Html, Save = SaveFormat.Html },
        new("mhtml") { Load = LoadFormat.Mhtml, Save = SaveFormat.Mhtml },
        new("odt") { Load = LoadFormat.Odt, Save = SaveFormat.Odt },
        new("ott") { Load = LoadFormat.Ott, Save = SaveFormat.Ott },
        new("txt") { Load = LoadFormat.Text, Save = SaveFormat.Text },
        new("md") { Load = LoadFormat.Markdown, Save = SaveFormat.Markdown },
        new("pdf") { Load = LoadFormat.Pdf, Save = SaveFormat.Pdf },
        new("epub") { Load = LoadFormat.Epub, Save = SaveFormat.Epub },
        new("mobi") { Load = LoadFormat.Mobi, Save = SaveFormat.Mobi },
        new("azw3") { Load = LoadFormat.Azw3, Save = SaveFormat.Azw3 },
        new("chm") { Load = LoadFormat.Chm },
        new("xps") { Save = SaveFormat.Xps },
        new("openxps") { Save = SaveFormat.OpenXps },
        new("ps") { Save = SaveFormat.Ps },
        new("pcl") { Save = SaveFormat.Pcl },
        new("html-fixed") { Save = SaveFormat.HtmlFixed },
        new("png") { Save = SaveFormat.Png },
        new("jpeg") { Save = SaveFormat.Jpeg },
        new("svg") { Save = SaveFormat.Svg },
    ];

    /// <summary>The format id of a detected load format, or <c>unknown</c>.</summary>
    public static string IdOf(LoadFormat format) =>
        All.FirstOrDefault(entry => entry.Load == format)?.Id ?? "unknown";

    /// <summary>The engine format that saves a format id.</summary>
    public static SaveFormat Save(string formatId) =>
        All.FirstOrDefault(entry => string.Equals(entry.Id, formatId, StringComparison.Ordinal))?.Save
            ?? throw new InvalidOperationException($"'{formatId}' has no document save format.");
}

/// <summary>What one public format id is to the engine.</summary>
/// <param name="Id">The public format id.</param>
internal sealed record WordsEngineFormat(string Id)
{
    /// <summary>The engine format that loads documents of this id; null when it is never read.</summary>
    public LoadFormat? Load { get; init; }

    /// <summary>The engine format that saves documents in this id; null when it is never written.</summary>
    public SaveFormat? Save { get; init; }
}
